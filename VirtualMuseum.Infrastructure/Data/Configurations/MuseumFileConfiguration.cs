using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data.Configurations;

public class MuseumFileConfiguration : IEntityTypeConfiguration<MuseumFile>
{
    public void Configure(EntityTypeBuilder<MuseumFile> builder)
    {
        builder.ToTable("Files");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.FileName).IsRequired().HasMaxLength(500);
        builder.Property(f => f.FileType).IsRequired().HasMaxLength(50);
        builder.Property(f => f.Url).IsRequired();
        builder.Property(f => f.StorageProvider).IsRequired().HasMaxLength(50);
        builder.Property(f => f.CreatedAt).IsRequired();
    }
}
