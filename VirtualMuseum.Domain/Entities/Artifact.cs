namespace VirtualMuseum.Domain.Entities;

public class Artifact
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public Guid? EraId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? MaterialId { get; set; }
    public Guid? DiscoveryLocationId { get; set; }
    public Guid? ModelFileId { get; set; }
    public Guid? ThumbnailFileId { get; set; }
    public decimal? Height { get; set; }
    public decimal? Width { get; set; }
    public decimal? Depth { get; set; }
    public decimal? Weight { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public Era? Era { get; set; }
    public Category? Category { get; set; }
    public Material? Material { get; set; }
    public DiscoveryLocation? DiscoveryLocation { get; set; }
    public MuseumFile? ModelFile { get; set; }
    public MuseumFile? ThumbnailFile { get; set; }
    public User? Creator { get; set; }
    public ICollection<ArtifactTranslation> Translations { get; set; } = new List<ArtifactTranslation>();
    public ICollection<ArtifactMedia> Media { get; set; } = new List<ArtifactMedia>();
    public ICollection<ArtifactTag> ArtifactTags { get; set; } = new List<ArtifactTag>();
    public ICollection<ArtifactEmbedding> Embeddings { get; set; } = new List<ArtifactEmbedding>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<ArtifactView> ArtifactViews { get; set; } = new List<ArtifactView>();
}
