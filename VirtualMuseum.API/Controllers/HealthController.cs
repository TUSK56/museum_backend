using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VirtualMuseum.Infrastructure.Data;

namespace VirtualMuseum.API.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly MuseumDbContext _db;
    private readonly IConfiguration _configuration;

    public HealthController(MuseumDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Live() => Ok(new { status = "ok" });

    [HttpGet("db")]
    public async Task<IActionResult> Database(CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken);
            var pending = (await _db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            var applied = (await _db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

            return Ok(new
            {
                connected = canConnect,
                host = DatabaseConnectionResolver.DescribeHost(
                    DatabaseConnectionResolver.Resolve(_configuration)),
                appliedMigrations = applied.Count,
                pendingMigrations = pending.Count,
                pending
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new
            {
                connected = false,
                host = DatabaseConnectionResolver.DescribeHost(
                    DatabaseConnectionResolver.Resolve(_configuration)),
                error = ex.Message
            });
        }
    }
}
