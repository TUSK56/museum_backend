using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data.Configurations;

public class ArtifactMediaConfiguration : IEntityTypeConfiguration<ArtifactMedia>
{
    public void Configure(EntityTypeBuilder<ArtifactMedia> builder)
    {
        builder.ToTable("ArtifactMedia");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.MediaType).IsRequired().HasMaxLength(50);
        builder.Property(m => m.CreatedAt).IsRequired();

        builder.HasOne(m => m.Artifact)
            .WithMany(a => a.Media)
            .HasForeignKey(m => m.ArtifactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
