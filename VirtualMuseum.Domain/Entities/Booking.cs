namespace VirtualMuseum.Domain.Entities;

public class Booking
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string VisitorName { get; set; } = string.Empty;
    public string VisitorEmail { get; set; } = string.Empty;
    public string? VisitorPhone { get; set; }
    public DateTime VisitDate { get; set; }
    public int Guests { get; set; }
    public decimal TotalPaid { get; set; }
    public string Status { get; set; } = "Confirmed";
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
