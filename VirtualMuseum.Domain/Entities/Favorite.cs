namespace VirtualMuseum.Domain.Entities;

public class Favorite
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ArtifactId { get; set; }

    public User User { get; set; } = null!;
    public Artifact Artifact { get; set; } = null!;
}
