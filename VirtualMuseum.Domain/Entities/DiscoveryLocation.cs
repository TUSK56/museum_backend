namespace VirtualMuseum.Domain.Entities;

public class DiscoveryLocation
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public ICollection<Artifact> Artifacts { get; set; } = new List<Artifact>();
}
