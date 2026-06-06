namespace VirtualMuseum.Domain.Entities;

public class CommunityReaction
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public string ReactionType { get; set; } = "like";
    public DateTime CreatedAt { get; set; }

    public CommunityPost Post { get; set; } = null!;
    public User User { get; set; } = null!;
}
