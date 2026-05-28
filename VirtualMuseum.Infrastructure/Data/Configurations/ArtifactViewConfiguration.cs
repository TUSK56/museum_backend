using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data.Configurations;

public class ArtifactViewConfiguration : IEntityTypeConfiguration<ArtifactView>
{
    public void Configure(EntityTypeBuilder<ArtifactView> builder)
    {
        builder.ToTable("ArtifactViews");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.ViewedAt).IsRequired();

        builder.HasOne(v => v.Artifact)
            .WithMany(a => a.ArtifactViews)
            .HasForeignKey(v => v.ArtifactId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.User)
            .WithMany(u => u.ArtifactViews)
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
