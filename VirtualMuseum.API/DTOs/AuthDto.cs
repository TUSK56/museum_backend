using System.ComponentModel.DataAnnotations;

namespace VirtualMuseum.API.DTOs;

public record LoginRequest(
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    string Email,
    [Required(ErrorMessage = "Password is required")]
    [MinLength(1)]
    string Password);

public record LoginResponse(string AccessToken, string RefreshToken, Guid UserId, string Email, string FullName, string Role);

public record RegisterRequest(
    [Required(ErrorMessage = "Full name is required")]
    [StringLength(200, MinimumLength = 1)]
    string FullName,
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    string Email,
    [Required(ErrorMessage = "Region is required")]
    [StringLength(200, MinimumLength = 1)]
    string Region,
    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    string Password);

public record RegisterResponse(Guid UserId, string Email, string FullName, string Region, bool RequiresVerification);

public record GoogleAuthResponse(string Token, GoogleAuthUserDto User);

public record GoogleAuthUserDto(Guid Id, string Name, string Email, string? Picture);

public record VerifyTokenResponse(Guid Id, string Name, string Email, string Role);

public record ForgotPasswordRequestDto(
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    string Email);

public record ForgotPasswordResetRequest(
    [Required]
    [EmailAddress]
    string Email,
    [Required]
    string OtpCode,
    [Required]
    [MinLength(6)]
    string NewPassword,
    [Required]
    string ConfirmPassword);

public record SendOtpRequest(
    [Required]
    [EmailAddress]
    string Email);

public record VerifyOtpRequest(
    [Required]
    [EmailAddress]
    string Email,
    [Required]
    string Code);

public record RefreshTokenRequest(
    [Required]
    string RefreshToken);

public record GoogleLoginRequest(
    [Required]
    string IdToken);
