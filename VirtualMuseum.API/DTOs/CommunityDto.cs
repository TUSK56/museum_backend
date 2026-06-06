using System.ComponentModel.DataAnnotations;

namespace VirtualMuseum.API.DTOs;

public sealed record CommunityCommentDto(
    Guid Id,
    string User,
    string Avatar,
    string Text,
    DateTime CreatedAt);

public sealed record CommunityPostDto(
    Guid Id,
    string User,
    string Role,
    bool IsVerified,
    string Avatar,
    string Location,
    string Content,
    string? Image,
    int ReactionCount,
    string? UserReaction,
    int CommentsCount,
    IReadOnlyList<CommunityCommentDto> CommentsList,
    DateTime CreatedAt);

public sealed record CommunityFeedResponseDto(
    IReadOnlyList<CommunityPostDto> Posts,
    int ExploringNow,
    int TotalPosts);

public sealed class CreateCommunityPostRequest
{
    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? ImageUrl { get; set; }

    [MaxLength(256)]
    public string? Location { get; set; }
}

public sealed class CreateCommunityCommentRequest
{
    [Required]
    [MaxLength(2000)]
    public string Text { get; set; } = string.Empty;
}

public sealed class SetCommunityReactionRequest
{
    [Required]
    [MaxLength(16)]
    public string ReactionType { get; set; } = "like";
}

public sealed record CommunityUploadResponseDto(string Url, string StorageProvider);

public sealed record CommunityReactionResultDto(
    Guid PostId,
    int ReactionCount,
    string? UserReaction);
