using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
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

            if (string.IsNullOrWhiteSpace(CloudName))
                return false;

            // Signed upload path
            if (!string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret))
                return true;

            // Unsigned upload path (requires preset in Cloudinary dashboard)
            return !string.IsNullOrWhiteSpace(UploadPreset);
        }
    }

    private string? CloudName => ResolveConfig("Cloudinary:CloudName", "CLOUDINARY_CLOUD_NAME");
    private string? ApiKey => ResolveConfig("Cloudinary:ApiKey", "CLOUDINARY_API_KEY");
    private string? ApiSecret => ResolveConfig("Cloudinary:ApiSecret", "CLOUDINARY_API_SECRET");
    private string Folder => ResolveConfig("Cloudinary:Folder", "CLOUDINARY_FOLDER") ?? "egy";
    private string? UploadPreset =>
        ResolveConfig("Cloudinary:UploadPreset", "CLOUDINARY_UPLOAD_PRESET");

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

        if (stream.CanSeek)
            stream.Position = 0;

        var safeName = string.IsNullOrWhiteSpace(fileName) ? "upload.jpg" : fileName;

        try
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(safeName, stream),
                Folder = Folder,
                Overwrite = false,
                UniqueFilename = true,
            };

            ImageUploadResult result;

            if (!string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret))
            {
                var account = new Account(CloudName, ApiKey, ApiSecret);
                var cloudinary = new Cloudinary(account);
                result = await cloudinary.UploadAsync(uploadParams, cancellationToken);
            }
            else
            {
                uploadParams.UploadPreset = UploadPreset;
                var cloudinary = new Cloudinary(CloudName);
                result = await cloudinary.UploadAsync(uploadParams, cancellationToken);
            }

            if (result.Error != null)
            {
                _logger.LogWarning(
                    "Cloudinary upload failed: {Message}",
                    result.Error.Message);
                return null;
            }

            return string.IsNullOrWhiteSpace(result.SecureUrl?.ToString())
                ? result.Url?.ToString()
                : result.SecureUrl.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cloudinary upload threw an exception.");
            return null;
        }
    }

    private string? ResolveConfig(string primaryKey, string envStyleKey)
    {
        var value = _configuration[primaryKey];
        if (!string.IsNullOrWhiteSpace(value))
            return value.Trim();

        value = _configuration[envStyleKey];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
