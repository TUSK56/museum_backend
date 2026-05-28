using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data.Configurations;

public class DiscoveryLocationConfiguration : IEntityTypeConfiguration<DiscoveryLocation>
{
    public void Configure(EntityTypeBuilder<DiscoveryLocation> builder)
    {
        builder.ToTable("DiscoveryLocations");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).IsRequired().HasMaxLength(300);
    }
}
