using System.ComponentModel.DataAnnotations;

namespace VirtualMuseum.API.DTOs;

public sealed class AiChatRequest
{
    [MaxLength(8000)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? SessionId { get; set; }

    [MaxLength(16)]
    public string? Source { get; set; }

    /// <summary>Base64 image data without the data-URL prefix.</summary>
    [MaxLength(6_000_000)]
    public string? ImageBase64 { get; set; }

    [MaxLength(64)]
    public string? ImageMimeType { get; set; }
}

public sealed record AiChatResponse(string Reply, string SessionId, bool FromN8n = true);
