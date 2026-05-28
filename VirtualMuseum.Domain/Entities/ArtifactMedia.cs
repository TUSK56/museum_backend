namespace VirtualMuseum.Domain.Entities;

public class ArtifactMedia
{
    public Guid Id { get; set; }
    public Guid ArtifactId { get; set; }
    public Guid FileId { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool IsEmbeddingSource { get; set; }
    public DateTime CreatedAt { get; set; }

    public Artifact Artifact { get; set; } = null!;
}
