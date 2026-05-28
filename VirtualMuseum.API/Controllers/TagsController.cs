using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualMuseum.API.DTOs;
using VirtualMuseum.Application.Services;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TagsController : ControllerBase
{
    private readonly TagService _tagService;
    private readonly ILogger<TagsController> _logger;

    public TagsController(TagService tagService, ILogger<TagsController> logger)
    {
        _tagService = tagService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<Tag>>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var tags = await _tagService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<Tag>>(true, tags));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<Tag>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tag = await _tagService.GetByIdAsync(id, cancellationToken);
        if (tag == null)
            return NotFound(new ApiResponse(false, "Tag not found"));
        return Ok(new ApiResponse<Tag>(true, tag));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<Tag>), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] Tag? tag, CancellationToken cancellationToken)
    {
        if (tag == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));
        var created = await _tagService.CreateAsync(tag, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new ApiResponse<Tag>(true, created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<Tag>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] Tag? tag, CancellationToken cancellationToken)
    {
        if (tag == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));
        var existing = await _tagService.GetByIdAsync(id, cancellationToken);
        if (existing == null)
            return NotFound(new ApiResponse(false, "Tag not found"));
        existing.Name = tag.Name;
        await _tagService.UpdateAsync(existing, cancellationToken);
        return Ok(new ApiResponse<Tag>(true, existing));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var tag = await _tagService.GetByIdAsync(id, cancellationToken);
        if (tag == null)
            return NotFound(new ApiResponse(false, "Tag not found"));
        await _tagService.DeleteAsync(tag, cancellationToken);
        return NoContent();
    }
}
