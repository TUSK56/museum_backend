namespace VirtualMuseum.Domain.Entities;

public class CommunityComment
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public CommunityPost Post { get; set; } = null!;
    public User User { get; set; } = null!;
}
