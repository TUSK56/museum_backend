using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualMuseum.API.DTOs;
using VirtualMuseum.Application.Services;

namespace VirtualMuseum.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly UserService _userService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(UserService userService, ILogger<ProfileController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<MyProfileResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new ApiResponse(false, "Unauthorized"));

        var user = await _userService.GetByIdAsync(userId.Value, cancellationToken);
        if (user == null)
            return Unauthorized(new ApiResponse(false, "Unauthorized"));

        return Ok(new ApiResponse<MyProfileResponse>(true,
            new MyProfileResponse(user.Id, user.Email, user.FullName, user.Region)));
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<MyProfileResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMyProfileRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));

        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new ApiResponse(false, "Unauthorized"));

        var user = await _userService.GetByIdAsync(userId.Value, cancellationToken);
        if (user == null)
            return Unauthorized(new ApiResponse(false, "Unauthorized"));

        user.FullName = request.FullName.Trim();
        user.Region = request.Region.Trim();

        await _userService.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User profile updated for {UserId}", user.Id);

        return Ok(new ApiResponse<MyProfileResponse>(true,
            new MyProfileResponse(user.Id, user.Email, user.FullName, user.Region),
            "Profile updated"));
    }

    private Guid? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}

