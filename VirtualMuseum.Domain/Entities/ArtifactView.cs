namespace VirtualMuseum.Domain.Entities;

public class ArtifactView
{
    public Guid Id { get; set; }
    public Guid ArtifactId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime ViewedAt { get; set; }

    public Artifact Artifact { get; set; } = null!;
    public User? User { get; set; }
}
