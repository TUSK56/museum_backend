using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VirtualMuseum.API.DTOs;
using VirtualMuseum.Domain.Entities;
using VirtualMuseum.Infrastructure.Data;

namespace VirtualMuseum.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly MuseumDbContext _dbContext;

    public FilesController(MuseumDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult GetAll()
    {
        var files = _dbContext.Files
            .OrderByDescending(f => f.CreatedAt)
            .Take(200)
            .ToList();
        return Ok(new ApiResponse<IEnumerable<MuseumFile>>(true, files));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateFileRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest(new ApiResponse(false, "File url is required."));
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var uploadedBy = Guid.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : Guid.Empty;

        var fileName = string.IsNullOrWhiteSpace(request.FileName)
            ? Path.GetFileName(new Uri(request.Url, UriKind.RelativeOrAbsolute).ToString())
            : request.FileName.Trim();

        var file = new MuseumFile
        {
            Id = Guid.NewGuid(),
            FileName = string.IsNullOrWhiteSpace(fileName) ? $"file-{DateTime.UtcNow.Ticks}" : fileName,
            FileType = string.IsNullOrWhiteSpace(request.FileType) ? "unknown" : request.FileType.Trim(),
            Url = request.Url.Trim(),
            StorageProvider = string.IsNullOrWhiteSpace(request.StorageProvider) ? "external" : request.StorageProvider.Trim(),
            UploadedBy = uploadedBy,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Files.Add(file);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<MuseumFile>(true, file));
    }
}

public sealed record CreateFileRequest(string Url, string? FileName, string? FileType, string? StorageProvider);
