using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualMuseum.API.DTOs;

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

    public AiAssistantController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AiAssistantController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
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

        var webhookUrl = _configuration["N8n:WebhookUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return Ok(new ApiResponse<AiChatResponse>(
                true,
                new AiChatResponse(
                    "The AI assistant is not connected yet. Add your n8n Webhook URL to N8n:WebhookUrl in server appsettings, then restart the API.",
                    false),
                "n8n webhook is not configured"));
        }

        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.NewGuid().ToString("N")
            : request.SessionId.Trim();

        var outbound = new
        {
            message,
            chatInput = message,
            sessionId,
            imageBase64 = request.ImageBase64,
            imageMimeType = request.ImageMimeType ?? "image/jpeg",
            userId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            userEmail = User.FindFirstValue(ClaimTypes.Email)
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
                return Ok(new ApiResponse<AiChatResponse>(
                    true,
                    new AiChatResponse(hint, false),
                    "n8n request failed"));
            }

            var reply = ExtractReply(raw);
            if (string.IsNullOrWhiteSpace(reply))
                reply = "I received your message but could not parse a reply from the workflow. Check your n8n Respond to Webhook node returns { \"reply\": \"...\" }.";

            return Ok(new ApiResponse<AiChatResponse>(true, new AiChatResponse(reply.Trim(), true)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call n8n webhook");
            return Ok(new ApiResponse<AiChatResponse>(
                true,
                new AiChatResponse("Could not reach the AI workflow. Verify N8n:WebhookUrl and that n8n is online.", false),
                "n8n call failed"));
        }
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
