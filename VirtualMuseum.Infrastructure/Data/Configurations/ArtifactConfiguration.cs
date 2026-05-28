using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data.Configurations;

public class ArtifactConfiguration : IEntityTypeConfiguration<Artifact>
{
    public void Configure(EntityTypeBuilder<Artifact> builder)
    {
        builder.ToTable("Artifacts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Slug).IsRequired().HasMaxLength(300);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.HasIndex(a => a.Slug).IsUnique();

        builder.HasOne(a => a.Era)
            .WithMany(e => e.Artifacts)
            .HasForeignKey(a => a.EraId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Category)
            .WithMany(c => c.Artifacts)
            .HasForeignKey(a => a.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Material)
            .WithMany(m => m.Artifacts)
            .HasForeignKey(a => a.MaterialId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.DiscoveryLocation)
            .WithMany(d => d.Artifacts)
            .HasForeignKey(a => a.DiscoveryLocationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.ModelFile)
            .WithMany()
            .HasForeignKey(a => a.ModelFileId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(a => a.ThumbnailFile)
            .WithMany()
            .HasForeignKey(a => a.ThumbnailFileId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(a => a.Creator)
            .WithMany(u => u.CreatedArtifacts)
            .HasForeignKey(a => a.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
