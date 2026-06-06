using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VirtualMuseum.Application.Interfaces;

namespace VirtualMuseum.Infrastructure.Services;

public class CloudinaryService : ICloudinaryService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CloudinaryService> _logger;

    public CloudinaryService(
        IConfiguration configuration,
        ILogger<CloudinaryService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            var storage = ResolveConfig("Upload:Storage", "EGY_UPLOAD_STORAGE");
            if (!string.Equals(storage, "cloudinary", StringComparison.OrdinalIgnoreCase))
                return false;

            return !string.IsNullOrWhiteSpace(CloudName)
                   && !string.IsNullOrWhiteSpace(ApiKey)
                   && !string.IsNullOrWhiteSpace(ApiSecret);
        }
    }

    private string? CloudName => ResolveConfig("Cloudinary:CloudName", "CLOUDINARY_CLOUD_NAME");
    private string? ApiKey => ResolveConfig("Cloudinary:ApiKey", "CLOUDINARY_API_KEY");
    private string? ApiSecret => ResolveConfig("Cloudinary:ApiSecret", "CLOUDINARY_API_SECRET");
    private string Folder => ResolveConfig("Cloudinary:Folder", "CLOUDINARY_FOLDER") ?? "egy";

    public async Task<string?> UploadImageAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Cloudinary upload skipped: storage is not configured.");
            return null;
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signaturePayload = $"folder={Folder}&timestamp={timestamp}{ApiSecret}";
        var signature = ComputeSha1Hex(signaturePayload);

        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType);
        form.Add(fileContent, "file", string.IsNullOrWhiteSpace(fileName) ? "upload.jpg" : fileName);
        form.Add(new StringContent(ApiKey!), "api_key");
        form.Add(new StringContent(timestamp), "timestamp");
        form.Add(new StringContent(signature), "signature");
        form.Add(new StringContent(Folder), "folder");

        var url = $"https://api.cloudinary.com/v1_1/{CloudName}/image/upload";

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        using var response = await client.PostAsync(url, form, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Cloudinary upload failed ({Status}): {Body}", (int)response.StatusCode, raw);
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("secure_url", out var secureUrl))
                return secureUrl.GetString();
            if (doc.RootElement.TryGetProperty("url", out var plainUrl))
                return plainUrl.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Cloudinary response.");
        }

        return null;
    }

    private string? ResolveConfig(string primaryKey, string envStyleKey)
    {
        var value = _configuration[primaryKey];
        if (!string.IsNullOrWhiteSpace(value))
            return value.Trim();

        value = _configuration[envStyleKey];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ComputeSha1Hex(string input)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
