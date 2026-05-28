using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TicketNumber).IsRequired().HasMaxLength(80);
        builder.Property(x => x.LocationId).IsRequired().HasMaxLength(120);
        builder.Property(x => x.LocationName).IsRequired().HasMaxLength(300);
        builder.Property(x => x.VisitorName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.VisitorEmail).IsRequired().HasMaxLength(256);
        builder.Property(x => x.VisitorPhone).HasMaxLength(64);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(40);
        builder.Property(x => x.TotalPaid).HasPrecision(18, 2);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.VisitDate).IsRequired();
        builder.Property(x => x.Guests).IsRequired();

        builder.HasIndex(x => x.TicketNumber).IsUnique();
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne(x => x.User)
            .WithMany(u => u.Bookings)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
