using System.ComponentModel.DataAnnotations;

namespace VirtualMuseum.API.DTOs;

public sealed class ArtifactUpsertDto
{
    [Required]
    public string Slug { get; set; } = string.Empty;

    public Guid? EraId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? MaterialId { get; set; }
    public Guid? DiscoveryLocationId { get; set; }
    public string? DiscoverySite { get; set; }
    public Guid? ModelFileId { get; set; }
    public Guid? ThumbnailFileId { get; set; }
    public decimal? Height { get; set; }
    public decimal? Width { get; set; }
    public decimal? Depth { get; set; }
    public decimal? Weight { get; set; }
    public Guid? CreatedBy { get; set; }
    /// <summary>Short label (pharaoh / ruler) stored in translation HistoricalStory.</summary>
    public string? AssociatedKing { get; set; }
    /// <summary>Long narrative stored in translation Description.</summary>
    public string? HistoricalContext { get; set; }
}
