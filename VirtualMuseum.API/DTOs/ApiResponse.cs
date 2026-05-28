namespace VirtualMuseum.API.DTOs;

public record ApiResponse<T>(bool Success, T? Data, string? Message = null, string? Details = null);

public record ApiResponse(bool Success, string? Message = null, string? Details = null);
