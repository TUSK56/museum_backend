namespace VirtualMuseum.Domain.Entities;

public class ArtifactEmbedding
{
    public Guid Id { get; set; }
    public Guid ArtifactId { get; set; }
    public string EmbeddingVector { get; set; } = string.Empty; // JSON or base64 encoded vector
    public string? ModelVersion { get; set; }
    public DateTime CreatedAt { get; set; }

    public Artifact Artifact { get; set; } = null!;
}
