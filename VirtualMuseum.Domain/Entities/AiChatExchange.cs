namespace VirtualMuseum.Domain.Entities;

/// <summary>
/// One user message paired with the assistant reply (for admin live stream).
/// </summary>
public class AiChatExchange
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string SessionKey { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
    public string AssistantReply { get; set; } = string.Empty;
    public string Source { get; set; } = "web";
    public bool FromN8n { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
}
