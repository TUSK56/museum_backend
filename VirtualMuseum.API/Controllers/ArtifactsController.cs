using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VirtualMuseum.API.DTOs;
using VirtualMuseum.Application.Services;
using VirtualMuseum.Domain.Entities;
using VirtualMuseum.Infrastructure.Data;

namespace VirtualMuseum.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArtifactsController : ControllerBase
{
    private readonly ArtifactService _artifactService;
    private readonly MuseumDbContext _db;
    private readonly ILogger<ArtifactsController> _logger;

    public ArtifactsController(
        ArtifactService artifactService,
        MuseumDbContext db,
        ILogger<ArtifactsController> logger)
    {
        _artifactService = artifactService;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ArtifactResponseDto>>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? categoryId,
        [FromQuery] bool? only3d,
        [FromQuery] int? skip,
        [FromQuery] int? take,
        CancellationToken cancellationToken)
    {
        // IMPORTANT: This endpoint must stay fast on shared hosting.
        // We return a lightweight list and only the EN translation (not all translations).
        var safeSkip = Math.Max(skip ?? 0, 0);
        var safeTake = Math.Clamp(take ?? 500, 1, 2000);

        var query = _db.Artifacts
            .AsNoTracking()
            .Include(a => a.Era)
            .Include(a => a.Category)
            .Include(a => a.Material)
            .Include(a => a.DiscoveryLocation)
            .Include(a => a.ModelFile)
            .Include(a => a.ThumbnailFile)
            .Select(a => new
            {
                Artifact = a,
                EnTranslation = a.Translations
                    .Where(t => t.LanguageCode == "en")
                    .Select(t => new ArtifactTranslationDto(
                        t.Id,
                        t.LanguageCode,
                        t.Name,
                        t.Description,
                        t.HistoricalStory))
                    .FirstOrDefault()
            });

        if (categoryId.HasValue)
            query = query.Where(x => x.Artifact.CategoryId == categoryId);
        if (only3d == true)
            query = query.Where(x => x.Artifact.ModelFileId != null);

        var response = await query
            .OrderByDescending(x => x.Artifact.CreatedAt)
            .Skip(safeSkip)
            .Take(safeTake)
            .Select(x => new ArtifactResponseDto(
                x.Artifact.Id,
                x.Artifact.Slug,
                x.Artifact.EraId,
                x.Artifact.CategoryId,
                x.Artifact.MaterialId,
                x.Artifact.DiscoveryLocationId,
                x.Artifact.ModelFileId,
                x.Artifact.ThumbnailFileId,
                x.Artifact.Height,
                x.Artifact.Width,
                x.Artifact.Depth,
                x.Artifact.Weight,
                x.Artifact.CreatedBy,
                x.Artifact.CreatedAt,
                x.Artifact.Era == null ? null : new NamedRefDto(x.Artifact.Era.Id, x.Artifact.Era.Name),
                x.Artifact.Category == null ? null : new NamedRefDto(x.Artifact.Category.Id, x.Artifact.Category.Name),
                x.Artifact.Material == null ? null : new NamedRefDto(x.Artifact.Material.Id, x.Artifact.Material.Name),
                x.Artifact.DiscoveryLocation == null ? null : new DiscoveryLocationDto(
                    x.Artifact.DiscoveryLocation.Id,
                    x.Artifact.DiscoveryLocation.Name,
                    x.Artifact.DiscoveryLocation.Latitude,
                    x.Artifact.DiscoveryLocation.Longitude),
                x.Artifact.ModelFile == null ? null : new FileDto(
                    x.Artifact.ModelFile.Id,
                    x.Artifact.ModelFile.FileName,
                    x.Artifact.ModelFile.FileType,
                    x.Artifact.ModelFile.Url,
                    x.Artifact.ModelFile.StorageProvider),
                x.Artifact.ThumbnailFile == null ? null : new FileDto(
                    x.Artifact.ThumbnailFile.Id,
                    x.Artifact.ThumbnailFile.FileName,
                    x.Artifact.ThumbnailFile.FileType,
                    x.Artifact.ThumbnailFile.Url,
                    x.Artifact.ThumbnailFile.StorageProvider),
                x.EnTranslation == null ? new List<ArtifactTranslationDto>() : new List<ArtifactTranslationDto> { x.EnTranslation }
            ))
            .ToListAsync(cancellationToken);

        return Ok(new ApiResponse<IEnumerable<ArtifactResponseDto>>(true, response));
    }

    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ArtifactResponseDto>>), 200)]
    public async Task<IActionResult> GetAllAdmin(CancellationToken cancellationToken)
    {
        // Full list for maintenance scripts; avoid using this from the UI.
        var artifacts = await _db.Artifacts
            .AsNoTracking()
            .Include(a => a.Era)
            .Include(a => a.Category)
            .Include(a => a.Material)
            .Include(a => a.DiscoveryLocation)
            .Include(a => a.ModelFile)
            .Include(a => a.ThumbnailFile)
            .Include(a => a.Translations)
            .ToListAsync(cancellationToken);

        var response = artifacts.Select(MapArtifact).ToList();
        return Ok(new ApiResponse<IEnumerable<ArtifactResponseDto>>(true, response));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ArtifactResponseDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var artifact = await _artifactService.GetByIdAsync(id, cancellationToken);
        if (artifact == null)
            return NotFound(new ApiResponse(false, "Artifact not found"));
        return Ok(new ApiResponse<ArtifactResponseDto>(true, MapArtifact(artifact)));
    }

    [HttpGet("top-viewed-3d")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TopViewed3DArtifactDto>>), 200)]
    public async Task<IActionResult> GetTopViewed3D(CancellationToken cancellationToken)
    {
        var viewCounts = await _db.ArtifactViews
            .AsNoTracking()
            .GroupBy(v => v.ArtifactId)
            .Select(g => new { ArtifactId = g.Key, Views = g.Count() })
            .ToListAsync(cancellationToken);

        var countMap = viewCounts.ToDictionary(x => x.ArtifactId, x => x.Views);

        var artifacts = await _db.Artifacts
            .AsNoTracking()
            .Where(a => a.ModelFileId != null)
            .Select(a => new { a.Id, a.Slug })
            .ToListAsync(cancellationToken);

        var result = artifacts
            .Select(a => new TopViewed3DArtifactDto(
                a.Id,
                a.Slug,
                countMap.TryGetValue(a.Id, out var views) ? views : 0))
            .OrderByDescending(a => a.Views)
            .ThenBy(a => a.Slug)
            .Take(5)
            .ToList();

        return Ok(new ApiResponse<IEnumerable<TopViewed3DArtifactDto>>(true, result));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<Artifact>), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] ArtifactUpsertDto? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));
        if (string.IsNullOrWhiteSpace(request.Slug))
            return BadRequest(new ApiResponse(false, "Slug is required"));
        var discoveryLocationId = await ResolveDiscoveryLocationIdAsync(request.DiscoveryLocationId, request.DiscoverySite, cancellationToken);
        var artifact = new Artifact
        {
            Slug = request.Slug.Trim(),
            EraId = request.EraId,
            CategoryId = request.CategoryId,
            MaterialId = request.MaterialId,
            DiscoveryLocationId = discoveryLocationId,
            ModelFileId = request.ModelFileId,
            ThumbnailFileId = request.ThumbnailFileId,
            Height = request.Height,
            Width = request.Width,
            Depth = request.Depth,
            Weight = request.Weight,
            CreatedBy = request.CreatedBy,
        };
        AttachTranslationFields(artifact, request.AssociatedKing, request.HistoricalContext);
        var created = await _artifactService.CreateAsync(artifact, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new ApiResponse<ArtifactResponseDto>(true, MapArtifact(created)));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<Artifact>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ArtifactUpsertDto? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));
        if (string.IsNullOrWhiteSpace(request.Slug))
            return BadRequest(new ApiResponse(false, "Slug is required"));
        var existing = await _db.Artifacts
            .Include(a => a.Translations)
            .Include(a => a.Era)
            .Include(a => a.Category)
            .Include(a => a.Material)
            .Include(a => a.DiscoveryLocation)
            .Include(a => a.ModelFile)
            .Include(a => a.ThumbnailFile)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (existing == null)
            return NotFound(new ApiResponse(false, "Artifact not found"));
        existing.Slug = request.Slug.Trim();
        existing.EraId = request.EraId;
        existing.CategoryId = request.CategoryId;
        existing.MaterialId = request.MaterialId;
        existing.DiscoveryLocationId = await ResolveDiscoveryLocationIdAsync(request.DiscoveryLocationId, request.DiscoverySite, cancellationToken);
        existing.ModelFileId = request.ModelFileId;
        existing.ThumbnailFileId = request.ThumbnailFileId;
        existing.Height = request.Height;
        existing.Width = request.Width;
        existing.Depth = request.Depth;
        existing.Weight = request.Weight;
        existing.CreatedBy = request.CreatedBy;
        AttachTranslationFields(existing, request.AssociatedKing, request.HistoricalContext);
        await _artifactService.UpdateAsync(existing, cancellationToken);
        return Ok(new ApiResponse<ArtifactResponseDto>(true, MapArtifact(existing)));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var artifact = await _artifactService.GetByIdAsync(id, cancellationToken);
        if (artifact == null)
            return NotFound(new ApiResponse(false, "Artifact not found"));
        await _artifactService.DeleteAsync(artifact, cancellationToken);
        return NoContent();
    }

    private static ArtifactResponseDto MapArtifact(Artifact artifact) =>
        new(
            artifact.Id,
            artifact.Slug,
            artifact.EraId,
            artifact.CategoryId,
            artifact.MaterialId,
            artifact.DiscoveryLocationId,
            artifact.ModelFileId,
            artifact.ThumbnailFileId,
            artifact.Height,
            artifact.Width,
            artifact.Depth,
            artifact.Weight,
            artifact.CreatedBy,
            artifact.CreatedAt,
            artifact.Era is null ? null : new NamedRefDto(artifact.Era.Id, artifact.Era.Name),
            artifact.Category is null ? null : new NamedRefDto(artifact.Category.Id, artifact.Category.Name),
            artifact.Material is null ? null : new NamedRefDto(artifact.Material.Id, artifact.Material.Name),
            artifact.DiscoveryLocation is null ? null : new DiscoveryLocationDto(
                artifact.DiscoveryLocation.Id,
                artifact.DiscoveryLocation.Name,
                artifact.DiscoveryLocation.Latitude,
                artifact.DiscoveryLocation.Longitude),
            artifact.ModelFile is null ? null : new FileDto(
                artifact.ModelFile.Id,
                artifact.ModelFile.FileName,
                artifact.ModelFile.FileType,
                artifact.ModelFile.Url,
                artifact.ModelFile.StorageProvider),
            artifact.ThumbnailFile is null ? null : new FileDto(
                artifact.ThumbnailFile.Id,
                artifact.ThumbnailFile.FileName,
                artifact.ThumbnailFile.FileType,
                artifact.ThumbnailFile.Url,
                artifact.ThumbnailFile.StorageProvider),
            artifact.Translations.Select(t => new ArtifactTranslationDto(
                t.Id,
                t.LanguageCode,
                t.Name,
                t.Description,
                t.HistoricalStory)).ToList()
        );

    public sealed record NamedRefDto(Guid Id, string Name);

    public sealed record FileDto(Guid Id, string FileName, string FileType, string Url, string StorageProvider);

    public sealed record DiscoveryLocationDto(
        Guid Id,
        string Name,
        decimal? Latitude,
        decimal? Longitude);

    public sealed record ArtifactTranslationDto(
        Guid Id,
        string LanguageCode,
        string Name,
        string? Description,
        string? HistoricalStory);

    public sealed record ArtifactResponseDto(
        Guid Id,
        string Slug,
        Guid? EraId,
        Guid? CategoryId,
        Guid? MaterialId,
        Guid? DiscoveryLocationId,
        Guid? ModelFileId,
        Guid? ThumbnailFileId,
        decimal? Height,
        decimal? Width,
        decimal? Depth,
        decimal? Weight,
        Guid? CreatedBy,
        DateTime CreatedAt,
        NamedRefDto? Era,
        NamedRefDto? Category,
        NamedRefDto? Material,
        DiscoveryLocationDto? DiscoveryLocation,
        FileDto? ModelFile,
        FileDto? ThumbnailFile,
        List<ArtifactTranslationDto> Translations);

    public sealed record TopViewed3DArtifactDto(
        Guid Id,
        string Slug,
        int Views);

    private async Task<Guid?> ResolveDiscoveryLocationIdAsync(Guid? discoveryLocationId, string? discoverySite, CancellationToken cancellationToken)
    {
        if (discoveryLocationId.HasValue)
            return discoveryLocationId;
        var siteName = (discoverySite ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(siteName))
            return null;
        var existing = await _db.DiscoveryLocations
            .FirstOrDefaultAsync(
                x => x.Name.ToLower() == siteName.ToLower(),
                cancellationToken);
        if (existing != null)
            return existing.Id;
        var created = new DiscoveryLocation
        {
            Name = siteName
        };
        _db.DiscoveryLocations.Add(created);
        await _db.SaveChangesAsync(cancellationToken);
        return created.Id;
    }

    private static void AttachTranslationFields(Artifact artifact, string? associatedKing, string? historicalContext)
    {
        var kingText = (associatedKing ?? string.Empty).Trim();
        var contextText = (historicalContext ?? string.Empty).Trim();
        var translation = artifact.Translations.FirstOrDefault(t => t.LanguageCode == "en");

        if (translation == null && (string.IsNullOrWhiteSpace(kingText) && string.IsNullOrWhiteSpace(contextText)))
            return;

        if (translation == null)
        {
            artifact.Translations.Add(new ArtifactTranslation
            {
                LanguageCode = "en",
                Name = artifact.Slug,
                HistoricalStory = string.IsNullOrWhiteSpace(kingText) ? null : kingText,
                Description = string.IsNullOrWhiteSpace(contextText) ? null : contextText
            });
            return;
        }

        if (!string.IsNullOrWhiteSpace(kingText))
            translation.HistoricalStory = kingText;
        else if (associatedKing != null)
            translation.HistoricalStory = null;

        if (!string.IsNullOrWhiteSpace(contextText))
            translation.Description = contextText;
        else if (historicalContext != null)
            translation.Description = null;

        if (string.IsNullOrWhiteSpace(translation.Name))
            translation.Name = artifact.Slug;
    }
}
