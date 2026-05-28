using System.ComponentModel.DataAnnotations;

namespace VirtualMuseum.API.DTOs;

public record CreateBookingRequest(
    [Required] string TicketNumber,
    [Required] string LocationId,
    [Required] string LocationName,
    [Required] string VisitorName,
    [Required] [EmailAddress] string VisitorEmail,
    string? VisitorPhone,
    DateTime VisitDate,
    [Range(1, 100)] int Guests,
    [Range(0, 1_000_000)] decimal TotalPaid);

public record BookingResponse(
    Guid Id,
    Guid UserId,
    string TicketNumber,
    string LocationId,
    string LocationName,
    string VisitorName,
    string VisitorEmail,
    string? VisitorPhone,
    DateTime VisitDate,
    int Guests,
    decimal TotalPaid,
    string Status,
    DateTime CreatedAt);
