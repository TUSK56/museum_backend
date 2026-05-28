using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualMuseum.API.DTOs;
using VirtualMuseum.Application.Services;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaterialsController : ControllerBase
{
    private readonly MaterialService _materialService;
    private readonly ILogger<MaterialsController> _logger;

    public MaterialsController(MaterialService materialService, ILogger<MaterialsController> logger)
    {
        _materialService = materialService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<Material>>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var materials = await _materialService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<Material>>(true, materials));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<Material>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var material = await _materialService.GetByIdAsync(id, cancellationToken);
        if (material == null)
            return NotFound(new ApiResponse(false, "Material not found"));
        return Ok(new ApiResponse<Material>(true, material));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<Material>), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] Material? material, CancellationToken cancellationToken)
    {
        if (material == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));
        var created = await _materialService.CreateAsync(material, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new ApiResponse<Material>(true, created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<Material>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] Material? material, CancellationToken cancellationToken)
    {
        if (material == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));
        var existing = await _materialService.GetByIdAsync(id, cancellationToken);
        if (existing == null)
            return NotFound(new ApiResponse(false, "Material not found"));
        existing.Name = material.Name;
        await _materialService.UpdateAsync(existing, cancellationToken);
        return Ok(new ApiResponse<Material>(true, existing));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var material = await _materialService.GetByIdAsync(id, cancellationToken);
        if (material == null)
            return NotFound(new ApiResponse(false, "Material not found"));
        await _materialService.DeleteAsync(material, cancellationToken);
        return NoContent();
    }
}
