namespace VirtualMuseum.Domain.Entities;

public class Review
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ArtifactId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Artifact Artifact { get; set; } = null!;
}
