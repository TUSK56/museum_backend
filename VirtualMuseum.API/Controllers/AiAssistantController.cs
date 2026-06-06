using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VirtualMuseum.API.DTOs;
using VirtualMuseum.Domain.Entities;
using VirtualMuseum.Infrastructure.Data;

namespace VirtualMuseum.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiAssistantController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiAssistantController> _logger;
    private readonly MuseumDbContext _db;

    public AiAssistantController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AiAssistantController> logger,
        MuseumDbContext db)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _db = db;
    }

    [HttpPost("chat")]
    [ProducesResponseType(typeof(ApiResponse<AiChatResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> Chat([FromBody] AiChatRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));

        var message = (request.Message ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(message) && string.IsNullOrWhiteSpace(request.ImageBase64))
            return BadRequest(new ApiResponse(false, "Message or image is required"));

        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.NewGuid().ToString("N")
            : request.SessionId.Trim();

        var source = NormalizeSource(request.Source);
        var userId = ParseUserId(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var displayMessage = string.IsNullOrWhiteSpace(message)
            ? "[Image attachment]"
            : message;

        var webhookUrl = _configuration["N8n:WebhookUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            var fallbackReply =
                "The AI assistant is not connected yet. Add your n8n Webhook URL to N8n:WebhookUrl in server appsettings, then restart the API.";
            await SaveExchangeAsync(
                userId, userEmail, userName, sessionId, displayMessage, fallbackReply, source, false, cancellationToken);

            return Ok(new ApiResponse<AiChatResponse>(
                true,
                new AiChatResponse(fallbackReply, sessionId, false),
                "n8n webhook is not configured"));
        }

        var outbound = new
        {
            message,
            chatInput = message,
            sessionId,
            session_id = sessionId,
            imageBase64 = request.ImageBase64,
            imageMimeType = request.ImageMimeType ?? "image/jpeg",
            userId = userId?.ToString(),
            userEmail,
            source
        };

        try
        {
            var client = _httpClientFactory.CreateClient("N8n");
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(outbound),
                    Encoding.UTF8,
                    "application/json")
            };
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await client.SendAsync(httpRequest, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("n8n webhook returned {Status}: {Body}", (int)response.StatusCode, raw);
                var hint = BuildN8nErrorHint((int)response.StatusCode, raw);
                await SaveExchangeAsync(
                    userId, userEmail, userName, sessionId, displayMessage, hint, source, false, cancellationToken);

                return Ok(new ApiResponse<AiChatResponse>(
                    true,
                    new AiChatResponse(hint, sessionId, false),
                    "n8n request failed"));
            }

            var reply = ExtractReply(raw);
            var resolvedSessionId = ExtractSessionId(raw) ?? sessionId;
            if (string.IsNullOrWhiteSpace(reply))
            {
                reply =
                    "I received your message but could not parse a reply from the workflow. Check your n8n Respond to Webhook node returns { \"reply\": \"...\" } or { \"output\": \"...\" }.";
            }

            var trimmedReply = reply.Trim();
            await SaveExchangeAsync(
                userId,
                userEmail,
                userName,
                resolvedSessionId,
                displayMessage,
                trimmedReply,
                source,
                true,
                cancellationToken);

            return Ok(new ApiResponse<AiChatResponse>(
                true,
                new AiChatResponse(trimmedReply, resolvedSessionId, true)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call n8n webhook");
            var errorReply =
                "Could not reach the AI workflow. Verify N8n:WebhookUrl and that n8n is online.";
            await SaveExchangeAsync(
                userId, userEmail, userName, sessionId, displayMessage, errorReply, source, false, cancellationToken);

            return Ok(new ApiResponse<AiChatResponse>(
                true,
                new AiChatResponse(errorReply, sessionId, false),
                "n8n call failed"));
        }
    }

    [HttpGet("logs")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<AiChatLogsResponseDto>), 200)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int take = 50,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);
        var searchTerm = (search ?? string.Empty).Trim().ToLowerInvariant();

        var todayUtc = DateTime.UtcNow.Date;

        var totalQueries = await _db.AiChatExchanges.CountAsync(cancellationToken);
        var queriesToday = await _db.AiChatExchanges
            .CountAsync(x => x.CreatedAt >= todayUtc, cancellationToken);
        var successfulReplies = await _db.AiChatExchanges
            .CountAsync(x => x.FromN8n, cancellationToken);

        var topQuestions = await _db.AiChatExchanges
            .AsNoTracking()
            .Where(x => x.UserMessage != "[Image attachment]")
            .GroupBy(x => x.UserMessage.ToLower())
            .Select(g => new { Question = g.OrderByDescending(x => x.CreatedAt).Select(x => x.UserMessage).First(), Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync(cancellationToken);

        var query = _db.AiChatExchanges.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(x =>
                x.UserMessage.ToLower().Contains(searchTerm) ||
                x.AssistantReply.ToLower().Contains(searchTerm) ||
                x.UserEmail.ToLower().Contains(searchTerm) ||
                x.UserDisplayName.ToLower().Contains(searchTerm));
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var entries = rows.Select(x => new AiChatLogEntryDto(
            x.Id,
            BuildUserLabel(x),
            x.UserMessage,
            x.AssistantReply,
            x.Source,
            x.CreatedAt,
            x.FromN8n)).ToList();

        var stats = new AiChatLogStatsDto(
            totalQueries,
            queriesToday,
            totalQueries == 0 ? 0 : (int)Math.Round(successfulReplies * 100.0 / totalQueries),
            topQuestions.Select(q => new AiChatTopQuestionDto(q.Question, q.Count)).ToList());

        return Ok(new ApiResponse<AiChatLogsResponseDto>(
            true,
            new AiChatLogsResponseDto(stats, entries)));
    }

    private async Task SaveExchangeAsync(
        Guid? userId,
        string userEmail,
        string userName,
        string sessionKey,
        string userMessage,
        string assistantReply,
        string source,
        bool fromN8n,
        CancellationToken cancellationToken)
    {
        try
        {
            _db.AiChatExchanges.Add(new AiChatExchange
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserEmail = userEmail,
                UserDisplayName = userName,
                SessionKey = sessionKey,
                UserMessage = userMessage,
                AssistantReply = assistantReply,
                Source = source,
                FromN8n = fromN8n,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist AI chat exchange");
        }
    }

    private static string BuildUserLabel(AiChatExchange exchange)
    {
        if (!string.IsNullOrWhiteSpace(exchange.UserDisplayName))
            return exchange.UserDisplayName;
        if (!string.IsNullOrWhiteSpace(exchange.UserEmail))
            return exchange.UserEmail;
        if (exchange.UserId.HasValue)
            return $"User #{exchange.UserId.Value.ToString("N")[..8]}";
        return "Guest";
    }

    private static Guid? ParseUserId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static string NormalizeSource(string? source)
    {
        var normalized = (source ?? "web").Trim().ToLowerInvariant();
        return normalized is "mobile" or "web" ? normalized : "web";
    }

    private static string BuildN8nErrorHint(int statusCode, string raw)
    {
        if (statusCode == 404 && raw.Contains("not registered", StringComparison.OrdinalIgnoreCase))
        {
            return "n8n webhook is not active. Open your workflow in n8n, turn ON the Active toggle (top-right), then try again.";
        }

        if (statusCode == 404)
            return "n8n webhook URL was not found. Check N8n:WebhookUrl matches the Production URL from your Webhook node.";

        return "The AI service is temporarily unavailable. Please try again shortly.";
    }

    private static string? ExtractSessionId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || (!raw.TrimStart().StartsWith('{') && !raw.TrimStart().StartsWith('[')))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return FindSessionId(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    private static string? FindSessionId(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "sessionId", "session_id" })
            {
                if (element.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    var value = prop.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }

            if (element.TryGetProperty("json", out var jsonProp))
            {
                var nested = FindSessionId(jsonProp);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }
        }

        return null;
    }

    private static string? ExtractReply(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        raw = raw.Trim();
        if (!raw.StartsWith('{') && !raw.StartsWith('['))
            return raw;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return FindReply(doc.RootElement);
        }
        catch
        {
            return raw;
        }
    }

    private static string? FindReply(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nested = FindReply(item);
                    if (!string.IsNullOrWhiteSpace(nested))
                        return nested;
                }
                return null;
            case JsonValueKind.Object:
                foreach (var key in new[] { "reply", "output", "text", "message", "response", "answer" })
                {
                    if (element.TryGetProperty(key, out var prop))
                    {
                        var value = FindReply(prop);
                        if (!string.IsNullOrWhiteSpace(value))
                            return value;
                    }
                }
                if (element.TryGetProperty("json", out var jsonProp))
                {
                    var fromJson = FindReply(jsonProp);
                    if (!string.IsNullOrWhiteSpace(fromJson))
                        return fromJson;
                }
                if (element.TryGetProperty("data", out var dataProp))
                {
                    var fromData = FindReply(dataProp);
                    if (!string.IsNullOrWhiteSpace(fromData))
                        return fromData;
                }
                return null;
            default:
                return null;
        }
    }
}
