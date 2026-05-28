using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualMuseum.API.DTOs;
using VirtualMuseum.Application.Services;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ErasController : ControllerBase
{
    private readonly EraService _eraService;
    private readonly ILogger<ErasController> _logger;

    public ErasController(EraService eraService, ILogger<ErasController> logger)
    {
        _eraService = eraService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<Era>>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var eras = await _eraService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<Era>>(true, eras));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<Era>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var era = await _eraService.GetByIdAsync(id, cancellationToken);
        if (era == null)
            return NotFound(new ApiResponse(false, "Era not found"));
        return Ok(new ApiResponse<Era>(true, era));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<Era>), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] Era? era, CancellationToken cancellationToken)
    {
        if (era == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));
        var created = await _eraService.CreateAsync(era, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new ApiResponse<Era>(true, created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<Era>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] Era? era, CancellationToken cancellationToken)
    {
        if (era == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));
        var existing = await _eraService.GetByIdAsync(id, cancellationToken);
        if (existing == null)
            return NotFound(new ApiResponse(false, "Era not found"));
        existing.Name = era.Name;
        existing.StartYear = era.StartYear;
        existing.EndYear = era.EndYear;
        await _eraService.UpdateAsync(existing, cancellationToken);
        return Ok(new ApiResponse<Era>(true, existing));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var era = await _eraService.GetByIdAsync(id, cancellationToken);
        if (era == null)
            return NotFound(new ApiResponse(false, "Era not found"));
        await _eraService.DeleteAsync(era, cancellationToken);
        return NoContent();
    }
}
