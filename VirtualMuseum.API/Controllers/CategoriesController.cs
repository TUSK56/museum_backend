using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualMuseum.API.DTOs;
using VirtualMuseum.Application.Services;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly CategoryService _categoryService;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(CategoryService categoryService, ILogger<CategoriesController> logger)
    {
        _categoryService = categoryService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<Category>>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var categories = await _categoryService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<Category>>(true, categories));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<Category>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var category = await _categoryService.GetByIdAsync(id, cancellationToken);
        if (category == null)
            return NotFound(new ApiResponse(false, "Category not found"));
        return Ok(new ApiResponse<Category>(true, category));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<Category>), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] Category? category, CancellationToken cancellationToken)
    {
        if (category == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));
        var created = await _categoryService.CreateAsync(category, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new ApiResponse<Category>(true, created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<Category>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] Category? category, CancellationToken cancellationToken)
    {
        if (category == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));
        var existing = await _categoryService.GetByIdAsync(id, cancellationToken);
        if (existing == null)
            return NotFound(new ApiResponse(false, "Category not found"));
        existing.Name = category.Name;
        await _categoryService.UpdateAsync(existing, cancellationToken);
        return Ok(new ApiResponse<Category>(true, existing));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var category = await _categoryService.GetByIdAsync(id, cancellationToken);
        if (category == null)
            return NotFound(new ApiResponse(false, "Category not found"));
        await _categoryService.DeleteAsync(category, cancellationToken);
        return NoContent();
    }
}
