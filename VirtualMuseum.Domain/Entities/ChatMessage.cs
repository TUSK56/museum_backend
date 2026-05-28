namespace VirtualMuseum.Domain.Entities;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string MessageText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ChatSession Session { get; set; } = null!;
    public ICollection<ChatMessageArtifact> Artifacts { get; set; } = new List<ChatMessageArtifact>();
}
