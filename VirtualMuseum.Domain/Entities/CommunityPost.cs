namespace VirtualMuseum.Domain.Entities;

public class CommunityPost
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Location { get; set; } = "Museum Lobby";
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<CommunityComment> Comments { get; set; } = new List<CommunityComment>();
    public ICollection<CommunityReaction> Reactions { get; set; } = new List<CommunityReaction>();
}
