namespace VirtualMuseum.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public bool EmailConfirmed { get; set; }
    public DateTime? LastLogin { get; set; }

    public Role Role { get; set; } = null!;
    public ICollection<Artifact> CreatedArtifacts { get; set; } = new List<Artifact>();
    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<ArtifactView> ArtifactViews { get; set; } = new List<ArtifactView>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<EmailOtp> EmailOtps { get; set; } = new List<EmailOtp>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
