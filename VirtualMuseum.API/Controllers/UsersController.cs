using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualMuseum.API.DTOs;
using VirtualMuseum.Application.Services;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(UserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<User>>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var users = await _userService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<User>>(true, users));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<User>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);
        if (user == null)
            return NotFound(new ApiResponse(false, "User not found"));
        return Ok(new ApiResponse<User>(true, user));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<User>), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));
        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            Region = request.Region,
            RoleId = request.RoleId,
            IsActive = request.IsActive
        };
        var created = await _userService.CreateAsync(user, request.Password, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new ApiResponse<User>(true, created));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<User>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));
        var existing = await _userService.GetByIdAsync(id, cancellationToken);
        if (existing == null)
            return NotFound(new ApiResponse(false, "User not found"));
        existing.FullName = request.FullName;
        existing.Email = request.Email;
        existing.Region = request.Region;
        existing.IsActive = request.IsActive;
        await _userService.UpdateAsync(existing, cancellationToken);
        return Ok(new ApiResponse<User>(true, existing));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);
        if (user == null)
            return NotFound(new ApiResponse(false, "User not found"));
        await _userService.DeleteAsync(user, cancellationToken);
        return NoContent();
    }
}

public record CreateUserRequest(string FullName, string Email, string Region, string Password, Guid RoleId, bool IsActive = true);
public record UpdateUserRequest(string FullName, string Email, string Region, bool IsActive);
