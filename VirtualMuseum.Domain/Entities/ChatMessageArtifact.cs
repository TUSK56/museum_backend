namespace VirtualMuseum.Domain.Entities;

public class ChatMessageArtifact
{
    public Guid MessageId { get; set; }
    public Guid ArtifactId { get; set; }

    public ChatMessage Message { get; set; } = null!;
    public Artifact Artifact { get; set; } = null!;
}
