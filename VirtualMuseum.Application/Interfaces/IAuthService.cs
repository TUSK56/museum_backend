namespace VirtualMuseum.Application.Interfaces;

public interface IAuthService
{
    Task<LoginOutcome> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<RegisterResult?> RegisterAsync(string fullName, string email, string region, string password, CancellationToken cancellationToken = default);
    Task<string?> SendOtpAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> VerifyOtpAsync(string email, string code, CancellationToken cancellationToken = default);
    Task<AuthResult?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<AuthResult?> GoogleLoginAsync(string idToken, CancellationToken cancellationToken = default);

    /// <summary>Sends a password-reset OTP if the email is registered. Does not reveal whether the email exists.</summary>
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> ResetPasswordWithOtpAsync(string email, string code, string newPassword, CancellationToken cancellationToken = default);
}

public record AuthResult(string AccessToken, string RefreshToken, Guid UserId, string Email, string FullName, string Role, string? Picture = null);

public record LoginOutcome(AuthResult? Result, LoginFailureKind Failure);

public enum LoginFailureKind
{
    None,
    InvalidCredentials,
    /// <summary>Password matched but email not verified yet (use send-otp / verify-otp).</summary>
    EmailNotConfirmed,
    AccountDisabled
}

public record RegisterResult(Guid UserId, string Email, string FullName, string Region, bool RequiresVerification);
