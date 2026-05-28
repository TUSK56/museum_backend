using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data.Configurations;

public class EraConfiguration : IEntityTypeConfiguration<Era>
{
    public void Configure(EntityTypeBuilder<Era> builder)
    {
        builder.ToTable("Eras");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
    }
}
