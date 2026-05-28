namespace VirtualMuseum.Domain.Entities;

public class ArtifactTag
{
    public Guid ArtifactId { get; set; }
    public Guid TagId { get; set; }

    public Artifact Artifact { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
