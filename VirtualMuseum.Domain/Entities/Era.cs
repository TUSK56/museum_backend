namespace VirtualMuseum.Domain.Entities;

public class Era
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }

    public ICollection<Artifact> Artifacts { get; set; } = new List<Artifact>();
}
