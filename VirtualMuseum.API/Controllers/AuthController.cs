using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using VirtualMuseum.API.DTOs;
using VirtualMuseum.Application.Interfaces;

namespace VirtualMuseum.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<AuthController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public AuthController(
        IAuthService authService,
        IUserRepository userRepository,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _userRepository = userRepository;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Login with email and password (for both users and admins).
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> Login([FromBody] LoginRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            _logger.LogWarning("Login attempted with null request");
            return BadRequest(new ApiResponse(false, "Invalid request body"));
        }

        var outcome = await _authService.LoginAsync(request.Email ?? string.Empty, request.Password ?? string.Empty, cancellationToken);

        if (outcome.Failure == LoginFailureKind.EmailNotConfirmed)
        {
            _logger.LogWarning("Login blocked - email not verified: {Email}", request.Email);
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiResponse(false, "Email not verified. Call POST /api/auth/send-otp then POST /api/auth/verify-otp, then try again."));
        }

        if (outcome.Failure == LoginFailureKind.AccountDisabled)
        {
            _logger.LogWarning("Login blocked - account inactive: {Email}", request.Email);
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse(false, "This account is disabled."));
        }

        if (outcome.Result == null)
        {
            _logger.LogWarning("Login failed for email: {Email}", request.Email);
            return Unauthorized(new ApiResponse(false, "Invalid email or password"));
        }

        return Ok(new ApiResponse<LoginResponse>(true, new LoginResponse(
            outcome.Result.AccessToken, outcome.Result.RefreshToken, outcome.Result.UserId, outcome.Result.Email, outcome.Result.FullName, outcome.Result.Role)));
    }

    /// <summary>
    /// Register a new user account and send OTP. User is created only after OTP verification.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<RegisterResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            _logger.LogWarning("Register attempted with null request");
            return BadRequest(new ApiResponse(false, "Invalid request body"));
        }

        var result = await _authService.RegisterAsync(
            request.FullName ?? string.Empty,
            request.Email ?? string.Empty,
            request.Region ?? string.Empty,
            request.Password ?? string.Empty,
            cancellationToken);

        if (result == null)
        {
            _logger.LogWarning("Register failed - email already exists: {Email}", request.Email);
            return BadRequest(new ApiResponse(false, "Email already registered"));
        }

        var message = result.RequiresVerification
            ? "Registration OTP sent. Verify OTP to activate account."
            : "Registration completed. You can login now.";
        return Ok(new ApiResponse<RegisterResponse>(true, new RegisterResponse(
            result.UserId, result.Email, result.FullName, result.Region, result.RequiresVerification), message));
    }

    /// <summary>
    /// Send email OTP for verification (5 minutes expiry).
    /// </summary>
    [HttpPost("send-otp")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));

        var smtpEnabled = bool.TryParse(_configuration.GetSection("Smtp")["Enabled"], out var enabled) && enabled;
        if (!smtpEnabled && !_environment.IsDevelopment())
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ApiResponse(false, "Email delivery is not configured. Please enable SMTP on the server to receive OTP emails."));
        }

        var code = await _authService.SendOtpAsync(request.Email, cancellationToken);
        if (code == null)
            return BadRequest(new ApiResponse(false, "Invalid email"));

        return Ok(new ApiResponse(true, "OTP has been sent"));
    }

    /// <summary>
    /// Verify email OTP and mark user EmailConfirmed.
    /// </summary>
    [HttpPost("verify-otp")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));

        var ok = await _authService.VerifyOtpAsync(request.Email, request.Code, cancellationToken);
        if (!ok)
            return BadRequest(new ApiResponse(false, "Invalid or expired OTP"));

        return Ok(new ApiResponse(true, "Email verified successfully"));
    }

    /// <summary>
    /// Request a password reset code (email must be registered). Response is generic for security.
    /// </summary>
    [HttpPost("forgot-password/request")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> ForgotPasswordRequest([FromBody] ForgotPasswordRequestDto? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));

        var smtpEnabled = bool.TryParse(_configuration.GetSection("Smtp")["Enabled"], out var enabled) && enabled;
        if (!smtpEnabled && !_environment.IsDevelopment())
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ApiResponse(false, "Email delivery is not configured. Please enable SMTP on the server to receive OTP emails."));
        }

        await _authService.RequestPasswordResetAsync(request.Email, cancellationToken);

        return Ok(new ApiResponse(true, "If an account exists for this email, a reset code has been sent."));
    }

    /// <summary>
    /// Reset password using the code from forgot-password/request.
    /// </summary>
    [HttpPost("forgot-password/reset")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> ForgotPasswordReset([FromBody] ForgotPasswordResetRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));
        if (request.NewPassword != request.ConfirmPassword)
            return BadRequest(new ApiResponse(false, "Passwords do not match"));

        var ok = await _authService.ResetPasswordWithOtpAsync(request.Email, request.OtpCode, request.NewPassword, cancellationToken);
        if (!ok)
            return BadRequest(new ApiResponse(false, "Invalid or expired code, or password does not meet requirements"));

        return Ok(new ApiResponse(true, "Password has been reset. You can sign in with your new password."));
    }

    /// <summary>
    /// Exchange a refresh token for a new access + refresh token pair.
    /// </summary>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));

        var result = await _authService.RefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (result == null)
            return BadRequest(new ApiResponse(false, "Invalid or expired refresh token"));

        return Ok(new ApiResponse<LoginResponse>(true, new LoginResponse(
            result.AccessToken, result.RefreshToken, result.UserId, result.Email, result.FullName, result.Role)));
    }

    /// <summary>
    /// Login using Google ID token.
    /// </summary>
    [HttpPost("google-login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));

        var result = await _authService.GoogleLoginAsync(request.IdToken, cancellationToken);
        if (result == null)
            return Unauthorized(new ApiResponse(false, "Invalid Google token"));

        return Ok(new ApiResponse<LoginResponse>(true, new LoginResponse(
            result.AccessToken, result.RefreshToken, result.UserId, result.Email, result.FullName, result.Role)));
    }

    /// <summary>
    /// Login or register using Google ID token.
    /// </summary>
    [HttpPost("google")]
    [ProducesResponseType(typeof(ApiResponse<GoogleAuthResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> Google([FromBody] GoogleLoginRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));

        var result = await _authService.GoogleLoginAsync(request.IdToken, cancellationToken);
        if (result == null)
            return Unauthorized(new ApiResponse(false, "Invalid Google token"));

        return Ok(new ApiResponse<GoogleAuthResponse>(true, new GoogleAuthResponse(
            result.AccessToken,
            new GoogleAuthUserDto(result.UserId, result.FullName, result.Email, result.Picture))));
    }

    /// <summary>
    /// Verify current JWT token and return logged-in user info.
    /// </summary>
    [Authorize]
    [HttpGet("verify")]
    [ProducesResponseType(typeof(ApiResponse<VerifyTokenResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    public async Task<IActionResult> Verify(CancellationToken cancellationToken)
    {
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId))
            return Unauthorized(new ApiResponse(false, "Invalid token"));

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null || !user.IsActive)
            return Unauthorized(new ApiResponse(false, "Invalid token"));

        var role = user.Role?.Name ?? User.FindFirstValue(ClaimTypes.Role) ?? "User";
        return Ok(new ApiResponse<VerifyTokenResponse>(true, new VerifyTokenResponse(user.Id, user.FullName, user.Email, role)));
    }
}
