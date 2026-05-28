using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VirtualMuseum.API.DTOs;
using VirtualMuseum.Domain.Entities;
using VirtualMuseum.Infrastructure.Data;

namespace VirtualMuseum.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly MuseumDbContext _db;

    public BookingsController(MuseumDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<BookingResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));

        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId))
            return Unauthorized(new ApiResponse(false, "Invalid token"));

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        var tokenEmail = User.FindFirstValue(ClaimTypes.Email)?.Trim();

        var visitorEmail = request.VisitorEmail.Trim();
        if (string.IsNullOrWhiteSpace(visitorEmail) ||
            visitorEmail.Equals("unknown@example.com", StringComparison.OrdinalIgnoreCase))
        {
            visitorEmail = tokenEmail ?? user?.Email ?? visitorEmail;
        }

        var visitorName = request.VisitorName.Trim();
        if (string.IsNullOrWhiteSpace(visitorName) ||
            visitorName.Equals("Visitor", StringComparison.OrdinalIgnoreCase))
        {
            visitorName = user?.FullName ?? visitorName;
        }

        var visitDate = request.VisitDate;
        if (visitDate.Kind == DateTimeKind.Unspecified)
            visitDate = DateTime.SpecifyKind(visitDate, DateTimeKind.Utc);
        visitDate = new DateTime(visitDate.Year, visitDate.Month, visitDate.Day, 0, 0, 0, DateTimeKind.Utc);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TicketNumber = request.TicketNumber.Trim(),
            LocationId = request.LocationId.Trim(),
            LocationName = request.LocationName.Trim(),
            VisitorName = visitorName,
            VisitorEmail = visitorEmail,
            VisitorPhone = request.VisitorPhone?.Trim(),
            VisitDate = visitDate,
            Guests = request.Guests,
            TotalPaid = request.TotalPaid,
            Status = "Confirmed",
            CreatedAt = DateTime.UtcNow
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<BookingResponse>(true, ToResponse(booking, user?.Email), "Booking created successfully"));
    }

    [HttpGet("my")]
    [ProducesResponseType(typeof(ApiResponse<List<BookingResponse>>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    public async Task<IActionResult> GetMyBookings(CancellationToken cancellationToken)
    {
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId))
            return Unauthorized(new ApiResponse(false, "Invalid token"));

        var bookings = await _db.Bookings
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var accountEmail = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new ApiResponse<List<BookingResponse>>(
            true,
            bookings.Select(b => ToResponse(b, accountEmail)).ToList()));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<BookingResponse>>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var bookings = await _db.Bookings
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var userIds = bookings.Select(b => b.UserId).Distinct().ToList();
        var userEmails = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        var responses = bookings
            .Select(b =>
            {
                userEmails.TryGetValue(b.UserId, out var accountEmail);
                return ToResponse(b, accountEmail);
            })
            .ToList();

        return Ok(new ApiResponse<List<BookingResponse>>(true, responses));
    }

    private static BookingResponse ToResponse(Booking b, string? accountEmail = null)
    {
        var email = b.VisitorEmail;
        if (string.IsNullOrWhiteSpace(email) ||
            email.Equals("unknown@example.com", StringComparison.OrdinalIgnoreCase))
        {
            email = accountEmail ?? email;
        }

        return new(
            b.Id,
            b.UserId,
            b.TicketNumber,
            b.LocationId,
            b.LocationName,
            b.VisitorName,
            email,
            b.VisitorPhone,
            b.VisitDate,
            b.Guests,
            b.TotalPaid,
            b.Status,
            b.CreatedAt);
    }
}
