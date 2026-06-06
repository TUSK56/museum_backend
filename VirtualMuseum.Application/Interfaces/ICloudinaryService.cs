namespace VirtualMuseum.Application.Interfaces;

public interface ICloudinaryService
{
    bool IsConfigured { get; }
    Task<string?> UploadImageAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default);
}
