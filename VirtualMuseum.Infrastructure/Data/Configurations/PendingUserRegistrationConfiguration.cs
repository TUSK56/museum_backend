using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data.Configurations;

public class PendingUserRegistrationConfiguration : IEntityTypeConfiguration<PendingUserRegistration>
{
    public void Configure(EntityTypeBuilder<PendingUserRegistration> builder)
    {
        builder.ToTable("PendingUserRegistrations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Region).IsRequired().HasMaxLength(200);
        builder.Property(x => x.PasswordHash).IsRequired();
        builder.Property(x => x.OtpCode).IsRequired().HasMaxLength(16);
        builder.Property(x => x.OtpExpirationTime).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.Email).IsUnique();
    }
}
