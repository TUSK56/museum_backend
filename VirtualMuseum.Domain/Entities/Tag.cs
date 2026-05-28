namespace VirtualMuseum.Domain.Entities;

public class Tag
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<ArtifactTag> ArtifactTags { get; set; } = new List<ArtifactTag>();
}
