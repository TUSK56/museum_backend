using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using Google.Apis.Auth;
using VirtualMuseum.Application.Interfaces;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IOtpRepository _otpRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPendingUserRegistrationRepository _pendingUserRegistrationRepository;
    private readonly IEmailService _emailService;
    private readonly TokenService _tokenService;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IOtpRepository otpRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPendingUserRegistrationRepository pendingUserRegistrationRepository,
        IEmailService emailService,
        TokenService tokenService,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _otpRepository = otpRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _pendingUserRegistrationRepository = pendingUserRegistrationRepository;
        _emailService = emailService;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    public async Task<LoginOutcome> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return new LoginOutcome(null, LoginFailureKind.InvalidCredentials);

        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (user == null)
            return new LoginOutcome(null, LoginFailureKind.InvalidCredentials);
        if (!user.IsActive)
            return new LoginOutcome(null, LoginFailureKind.AccountDisabled);

        try
        {
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash ?? string.Empty))
                return new LoginOutcome(null, LoginFailureKind.InvalidCredentials);
        }
        catch
        {
            return new LoginOutcome(null, LoginFailureKind.InvalidCredentials);
        }

        if (!user.EmailConfirmed)
            return new LoginOutcome(null, LoginFailureKind.EmailNotConfirmed);

        user.LastLogin = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        var roleName = user.Role?.Name ?? "User";
        var accessToken = _tokenService.GenerateAccessToken(user, roleName);
        await _refreshTokenRepository.InvalidateUserTokensAsync(user.Id, cancellationToken);
        var (refreshToken, expiresAt) = _tokenService.GenerateRefreshToken();
        var rt = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };
        await _refreshTokenRepository.AddAsync(rt, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return new LoginOutcome(new AuthResult(accessToken, refreshToken, user.Id, user.Email, user.FullName, roleName), LoginFailureKind.None);
    }

    public async Task<RegisterResult?> RegisterAsync(string fullName, string email, string region, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(region) ||
            string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var existing = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (existing != null)
            return null;

        var smtpEnabled = bool.TryParse(_configuration.GetSection("Smtp")["Enabled"], out var smtpEnabledValue)
            && smtpEnabledValue;
        if (!smtpEnabled)
        {
            var userRole = await _roleRepository.GetByNameAsync("User", cancellationToken)
                           ?? throw new InvalidOperationException("Default 'User' role is missing");
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Email = email,
                Region = region,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                RoleId = userRole.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true
            };
            await _userRepository.AddAsync(newUser, cancellationToken);
            return new RegisterResult(newUser.Id, newUser.Email, newUser.FullName, newUser.Region, false);
        }

        var code = GenerateOtpCode();
        var pending = await _pendingUserRegistrationRepository.GetByEmailAsync(email, cancellationToken);
        if (pending == null)
        {
            pending = new PendingUserRegistration
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Email = email,
                Region = region,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                OtpCode = code,
                OtpExpirationTime = DateTime.UtcNow.AddMinutes(5),
                IsVerified = false,
                CreatedAt = DateTime.UtcNow
            };
            await _pendingUserRegistrationRepository.AddAsync(pending, cancellationToken);
        }
        else
        {
            pending.FullName = fullName;
            pending.Region = region;
            pending.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            pending.OtpCode = code;
            pending.OtpExpirationTime = DateTime.UtcNow.AddMinutes(5);
            pending.IsVerified = false;
            pending.CreatedAt = DateTime.UtcNow;
            await _pendingUserRegistrationRepository.UpdateAsync(pending, cancellationToken);
        }

        var body = $"Your verification code is: {code}{Environment.NewLine}This code will expire in 5 minutes.";
        await _emailService.SendEmailAsync(email, "Email Verification", body, cancellationToken);

        return new RegisterResult(Guid.Empty, email, fullName, region, true);
    }

    public async Task<string?> SendOtpAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalizedEmail = email.Trim();
        var pending = await _pendingUserRegistrationRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (pending != null)
        {
            var pendingCode = GenerateOtpCode();
            pending.OtpCode = pendingCode;
            pending.OtpExpirationTime = DateTime.UtcNow.AddMinutes(5);
            pending.IsVerified = false;
            pending.CreatedAt = DateTime.UtcNow;
            await _pendingUserRegistrationRepository.UpdateAsync(pending, cancellationToken);

            var pendingBody = $"Your verification code is: {pendingCode}{Environment.NewLine}This code will expire in 5 minutes.";
            await _emailService.SendEmailAsync(normalizedEmail, "Email Verification", pendingBody, cancellationToken);
            return pendingCode;
        }

        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user == null) return null;

        var code = GenerateOtpCode();
        var otp = new EmailOtp
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Code = code,
            CreatedAt = DateTime.UtcNow,
            ExpirationTime = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        };

        await _otpRepository.AddAsync(otp, cancellationToken);
        await _otpRepository.SaveChangesAsync(cancellationToken);

        var body = $"Your verification code is: {code}{Environment.NewLine}This code will expire in 5 minutes.";
        await _emailService.SendEmailAsync(normalizedEmail, "Email Verification", body, cancellationToken);
        return code;
    }

    public async Task<bool> VerifyOtpAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim();
        var pending = await _pendingUserRegistrationRepository.GetActiveByEmailAndOtpAsync(normalizedEmail, code, cancellationToken);
        if (pending != null)
        {
            var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
            if (existingUser != null)
                return false;

            var userRole = await _roleRepository.GetByNameAsync("User", cancellationToken)
                           ?? throw new InvalidOperationException("Default 'User' role is missing");

            var newUser = new User
            {
                Id = Guid.NewGuid(),
                FullName = pending.FullName,
                Email = pending.Email,
                Region = pending.Region,
                PasswordHash = pending.PasswordHash,
                RoleId = userRole.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true
            };
            await _userRepository.AddAsync(newUser, cancellationToken);

            pending.IsVerified = true;
            await _pendingUserRegistrationRepository.DeleteAsync(pending, cancellationToken);
            return true;
        }

        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user == null)
            return false;

        var otp = await _otpRepository.GetActiveOtpAsync(user.Id, code, cancellationToken);
        if (otp == null)
            return false;

        otp.IsUsed = true;
        await _otpRepository.SaveChangesAsync(cancellationToken);

        user.EmailConfirmed = true;
        await _userRepository.UpdateAsync(user, cancellationToken);
        return true;
    }

    public async Task<AuthResult?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var stored = await _refreshTokenRepository.GetByTokenAsync(refreshToken, cancellationToken);
        if (stored == null)
            return null;

        var user = stored.User;
        if (!user.IsActive || !user.EmailConfirmed)
            return null;

        var roleName = user.Role?.Name ?? "User";
        var accessToken = _tokenService.GenerateAccessToken(user, roleName);

        stored.Revoked = true;
        await _refreshTokenRepository.InvalidateUserTokensAsync(user.Id, cancellationToken);
        var (newRefreshToken, expiresAt) = _tokenService.GenerateRefreshToken();
        var rt = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = newRefreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };
        await _refreshTokenRepository.AddAsync(rt, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return new AuthResult(accessToken, newRefreshToken, user.Id, user.Email, user.FullName, roleName);
    }

    public Task<AuthResult?> GoogleLoginAsync(string idToken, CancellationToken cancellationToken = default)
    {
        return GoogleLoginInternalAsync(idToken, cancellationToken);
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (user == null || !user.IsActive)
            return;

        var code = GenerateOtpCode();
        var otp = new EmailOtp
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Code = code,
            CreatedAt = DateTime.UtcNow,
            ExpirationTime = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        };

        await _otpRepository.AddAsync(otp, cancellationToken);
        await _otpRepository.SaveChangesAsync(cancellationToken);

        var body = $"Your password reset code is: {code}{Environment.NewLine}This code will expire in 5 minutes.{Environment.NewLine}If you did not request this, you can ignore this email.";
        await _emailService.SendEmailAsync(user.Email, "Password reset", body, cancellationToken);
    }

    public async Task<bool> ResetPasswordWithOtpAsync(string email, string code, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(newPassword))
            return false;
        if (newPassword.Length < 6)
            return false;

        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (user == null || !user.IsActive)
            return false;

        var otp = await _otpRepository.GetActiveOtpAsync(user.Id, code, cancellationToken);
        if (otp == null)
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        otp.IsUsed = true;
        await _userRepository.UpdateAsync(user, cancellationToken);
        return true;
    }

    private static string GenerateOtpCode()
    {
        // cryptographically secure 6-digit OTP
        var n = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return n.ToString("D6");
    }

    private async Task<AuthResult?> GoogleLoginInternalAsync(string idToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return null;

        var clientId = _configuration["Google:ClientId"] ?? _configuration["GoogleAuth:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Google:ClientId is not configured in appsettings.json");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            });
        }
        catch
        {
            return null;
        }

        var email = payload.Email;
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (user == null)
        {
            var userRole = await _roleRepository.GetByNameAsync("User", cancellationToken)
                           ?? throw new InvalidOperationException("Default 'User' role is missing");

            user = new User
            {
                Id = Guid.NewGuid(),
                FullName = payload.Name ?? payload.GivenName ?? "Google User",
                Email = email,
                Region = "Google",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                RoleId = userRole.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true,
                LastLogin = DateTime.UtcNow
            };
            await _userRepository.AddAsync(user, cancellationToken);
        }
        else
        {
            if (!user.IsActive)
                return null;
            user.EmailConfirmed = true;
            user.LastLogin = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user, cancellationToken);
        }

        var roleName = user.Role?.Name ?? "User";
        var accessToken = _tokenService.GenerateAccessToken(user, roleName);
        await _refreshTokenRepository.InvalidateUserTokensAsync(user.Id, cancellationToken);
        var (refreshToken, expiresAt) = _tokenService.GenerateRefreshToken();
        var rt = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };
        await _refreshTokenRepository.AddAsync(rt, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return new AuthResult(accessToken, refreshToken, user.Id, user.Email, user.FullName, roleName, payload.Picture);
    }
}
