namespace VirtualMuseum.API.DTOs;

public record AppStatusResponse(bool MaintenanceEnabled, string Message);
public record SetAppStatusRequest(bool MaintenanceEnabled, string? Message);
