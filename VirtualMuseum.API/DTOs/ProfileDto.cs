using System.ComponentModel.DataAnnotations;

namespace VirtualMuseum.API.DTOs;

public record MyProfileResponse(Guid UserId, string Email, string FullName, string Region);

public record UpdateMyProfileRequest(
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string FullName,
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string Region);

