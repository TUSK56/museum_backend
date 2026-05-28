using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VirtualMuseum.API.DTOs;
using VirtualMuseum.Domain.Entities;
using VirtualMuseum.Infrastructure.Data;

namespace VirtualMuseum.API.Controllers;

[ApiController]
[Route("api/app-status")]
public class AppStatusController : ControllerBase
{
    private const string MaintenanceEnabledKey = "app.maintenance.enabled";
    private const string MaintenanceMessageKey = "app.maintenance.message";

    private readonly MuseumDbContext _db;

    public AppStatusController(MuseumDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AppStatusResponse>), 200)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var status = await ReadStatusAsync(cancellationToken);
        return Ok(new ApiResponse<AppStatusResponse>(true, status));
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<AppStatusResponse>), 200)]
    public async Task<IActionResult> Set([FromBody] SetAppStatusRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));

        await UpsertAsync(MaintenanceEnabledKey, request.MaintenanceEnabled ? "true" : "false", cancellationToken);
        await UpsertAsync(
            MaintenanceMessageKey,
            string.IsNullOrWhiteSpace(request.Message) ? "The application is temporarily unavailable." : request.Message.Trim(),
            cancellationToken);

        var status = await ReadStatusAsync(cancellationToken);
        return Ok(new ApiResponse<AppStatusResponse>(true, status, "App status updated"));
    }

    private async Task<AppStatusResponse> ReadStatusAsync(CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings
            .AsNoTracking()
            .Where(x => x.Key == MaintenanceEnabledKey || x.Key == MaintenanceMessageKey)
            .ToListAsync(cancellationToken);

        var enabledRaw = settings.FirstOrDefault(x => x.Key == MaintenanceEnabledKey)?.Value;
        var message = settings.FirstOrDefault(x => x.Key == MaintenanceMessageKey)?.Value;
        var enabled = bool.TryParse(enabledRaw, out var parsed) && parsed;
        return new AppStatusResponse(enabled, string.IsNullOrWhiteSpace(message) ? "The application is temporarily unavailable." : message);
    }

    private async Task UpsertAsync(string key, string value, CancellationToken cancellationToken)
    {
        var existing = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (existing == null)
        {
            _db.AppSettings.Add(new AppSetting
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
