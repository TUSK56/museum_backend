namespace VirtualMuseum.Domain.Entities;

public class Material
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Artifact> Artifacts { get; set; } = new List<Artifact>();
}
