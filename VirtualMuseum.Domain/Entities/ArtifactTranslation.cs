namespace VirtualMuseum.Domain.Entities;

public class ArtifactTranslation
{
    public Guid Id { get; set; }
    public Guid ArtifactId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? HistoricalStory { get; set; }

    public Artifact Artifact { get; set; } = null!;
}
