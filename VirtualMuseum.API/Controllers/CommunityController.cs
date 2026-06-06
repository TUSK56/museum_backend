using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VirtualMuseum.API.DTOs;
using VirtualMuseum.Application.Interfaces;
using VirtualMuseum.Domain.Entities;
using VirtualMuseum.Infrastructure.Data;

namespace VirtualMuseum.API.Controllers;

[ApiController]
[Route("api/community")]
public class CommunityController : ControllerBase
{
    private static readonly HashSet<string> AllowedReactions = new(StringComparer.OrdinalIgnoreCase)
    {
        "like", "love", "wow", "crown"
    };

    private readonly MuseumDbContext _db;
    private readonly ICloudinaryService _cloudinary;
    private readonly ILogger<CommunityController> _logger;

    public CommunityController(
        MuseumDbContext db,
        ICloudinaryService cloudinary,
        ILogger<CommunityController> logger)
    {
        _db = db;
        _cloudinary = cloudinary;
        _logger = logger;
    }

    [HttpGet("posts")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<CommunityFeedResponseDto>), 200)]
    public async Task<IActionResult> GetPosts([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        var currentUserId = GetCurrentUserId();

        var posts = await _db.CommunityPosts
            .AsNoTracking()
            .Include(p => p.User)
            .ThenInclude(u => u.Role)
            .Include(p => p.Comments)
            .ThenInclude(c => c.User)
            .ThenInclude(u => u.Role)
            .Include(p => p.Reactions)
            .OrderByDescending(p => p.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var totalPosts = await _db.CommunityPosts.CountAsync(cancellationToken);
        var since = DateTime.UtcNow.AddHours(-24);
        var exploringNow = await _db.CommunityPosts
            .Where(p => p.CreatedAt >= since)
            .Select(p => p.UserId)
            .Union(_db.CommunityComments.Where(c => c.CreatedAt >= since).Select(c => c.UserId))
            .Union(_db.CommunityReactions.Where(r => r.CreatedAt >= since).Select(r => r.UserId))
            .Distinct()
            .CountAsync(cancellationToken);

        var mapped = posts.Select(p => MapPost(p, currentUserId)).ToList();
        return Ok(new ApiResponse<CommunityFeedResponseDto>(
            true,
            new CommunityFeedResponseDto(mapped, exploringNow, totalPosts)));
    }

    [HttpPost("posts")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CommunityPostDto>), 200)]
    public async Task<IActionResult> CreatePost(
        [FromBody] CreateCommunityPostRequest? request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));

        var content = request.Content.Trim();
        var imageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        if (string.IsNullOrWhiteSpace(content) && imageUrl == null)
            return BadRequest(new ApiResponse(false, "Post content or image is required"));

        if (imageUrl != null && !IsAllowedImageUrl(imageUrl))
            return BadRequest(new ApiResponse(false, "Image must be hosted on Cloudinary."));

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new ApiResponse(false, "Authentication required"));

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
            return Unauthorized(new ApiResponse(false, "User not found"));

        var post = new CommunityPost
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Content = string.IsNullOrWhiteSpace(content) ? "Shared an image from my visit." : content,
            ImageUrl = imageUrl,
            Location = string.IsNullOrWhiteSpace(request.Location) ? "Museum Lobby" : request.Location.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.CommunityPosts.Add(post);
        await _db.SaveChangesAsync(cancellationToken);

        post.User = user;
        post.Comments = new List<CommunityComment>();
        post.Reactions = new List<CommunityReaction>();

        return Ok(new ApiResponse<CommunityPostDto>(true, MapPost(post, userId)));
    }

    [HttpPost("posts/{postId:guid}/comments")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CommunityCommentDto>), 200)]
    public async Task<IActionResult> AddComment(
        Guid postId,
        [FromBody] CreateCommunityCommentRequest? request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));

        var text = request.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return BadRequest(new ApiResponse(false, "Comment text is required"));

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new ApiResponse(false, "Authentication required"));

        var postExists = await _db.CommunityPosts.AnyAsync(p => p.Id == postId, cancellationToken);
        if (!postExists)
            return NotFound(new ApiResponse(false, "Post not found"));

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
            return Unauthorized(new ApiResponse(false, "User not found"));

        var comment = new CommunityComment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            UserId = userId.Value,
            Text = text,
            CreatedAt = DateTime.UtcNow
        };

        _db.CommunityComments.Add(comment);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<CommunityCommentDto>(
            true,
            new CommunityCommentDto(
                comment.Id,
                user.FullName,
                BuildAvatarUrl(user),
                comment.Text,
                comment.CreatedAt)));
    }

    [HttpPut("posts/{postId:guid}/reactions")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CommunityReactionResultDto>), 200)]
    public async Task<IActionResult> SetReaction(
        Guid postId,
        [FromBody] SetCommunityReactionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));

        var reactionType = request.ReactionType.Trim().ToLowerInvariant();
        if (!AllowedReactions.Contains(reactionType))
            return BadRequest(new ApiResponse(false, "Invalid reaction type."));

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new ApiResponse(false, "Authentication required"));

        var post = await _db.CommunityPosts
            .Include(p => p.Reactions)
            .FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        if (post == null)
            return NotFound(new ApiResponse(false, "Post not found"));

        var existing = post.Reactions.FirstOrDefault(r => r.UserId == userId);
        string? userReaction;

        if (existing != null)
        {
            if (string.Equals(existing.ReactionType, reactionType, StringComparison.OrdinalIgnoreCase))
            {
                _db.CommunityReactions.Remove(existing);
                userReaction = null;
            }
            else
            {
                existing.ReactionType = reactionType;
                existing.CreatedAt = DateTime.UtcNow;
                userReaction = reactionType;
            }
        }
        else
        {
            _db.CommunityReactions.Add(new CommunityReaction
            {
                Id = Guid.NewGuid(),
                PostId = postId,
                UserId = userId.Value,
                ReactionType = reactionType,
                CreatedAt = DateTime.UtcNow
            });
            userReaction = reactionType;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var reactionCount = await _db.CommunityReactions.CountAsync(r => r.PostId == postId, cancellationToken);
        return Ok(new ApiResponse<CommunityReactionResultDto>(
            true,
            new CommunityReactionResultDto(postId, reactionCount, userReaction)));
    }

    [HttpPost("upload")]
    [Authorize]
    [RequestSizeLimit(6_000_000)]
    [ProducesResponseType(typeof(ApiResponse<CommunityUploadResponseDto>), 200)]
    public async Task<IActionResult> UploadImage(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ApiResponse(false, "Image file is required."));

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ApiResponse(false, "Only image uploads are allowed."));

        if (file.Length > 5_000_000)
            return BadRequest(new ApiResponse(false, "Image must be under 5 MB."));

        if (!_cloudinary.IsConfigured)
            return BadRequest(new ApiResponse(false, "Cloudinary upload is not configured on the server."));

        await using var stream = file.OpenReadStream();
        var url = await _cloudinary.UploadImageAsync(stream, file.FileName, file.ContentType, cancellationToken);
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new ApiResponse(false, "Failed to upload image to Cloudinary."));

        return Ok(new ApiResponse<CommunityUploadResponseDto>(
            true,
            new CommunityUploadResponseDto(url, "cloudinary")));
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static bool IsAllowedImageUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        return uri.Host.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase)
               || uri.Host.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase);
    }

    private static CommunityPostDto MapPost(CommunityPost post, Guid? currentUserId)
    {
        var user = post.User;
        var roleName = user.Role?.Name ?? "User";
        var userReaction = currentUserId == null
            ? null
            : post.Reactions.FirstOrDefault(r => r.UserId == currentUserId)?.ReactionType;

        var comments = post.Comments
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommunityCommentDto(
                c.Id,
                c.User?.FullName ?? "Member",
                BuildAvatarUrl(c.User),
                c.Text,
                c.CreatedAt))
            .ToList();

        return new CommunityPostDto(
            post.Id,
            user.FullName,
            MapRoleLabel(roleName),
            string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase),
            BuildAvatarUrl(user),
            post.Location,
            post.Content,
            post.ImageUrl,
            post.Reactions.Count,
            userReaction,
            post.Comments.Count,
            comments,
            post.CreatedAt);
    }

    private static string MapRoleLabel(string roleName)
    {
        if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase))
            return "Museum Staff";
        if (string.Equals(roleName, "User", StringComparison.OrdinalIgnoreCase))
            return "Explorer";
        return roleName;
    }

    private static string BuildAvatarUrl(User? user)
    {
        var name = string.IsNullOrWhiteSpace(user?.FullName) ? "Guest" : user.FullName.Trim();
        return $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(name)}&background=D4AF37&color=000&size=128";
    }
}
