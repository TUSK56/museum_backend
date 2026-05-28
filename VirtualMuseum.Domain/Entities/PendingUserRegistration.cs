namespace VirtualMuseum.Domain.Entities;

public class PendingUserRegistration
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
    public DateTime OtpExpirationTime { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}
