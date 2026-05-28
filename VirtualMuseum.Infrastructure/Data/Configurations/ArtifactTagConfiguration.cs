using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data.Configurations;

public class ArtifactTagConfiguration : IEntityTypeConfiguration<ArtifactTag>
{
    public void Configure(EntityTypeBuilder<ArtifactTag> builder)
    {
        builder.ToTable("ArtifactTags");
        builder.HasKey(at => new { at.ArtifactId, at.TagId });

        builder.HasOne(at => at.Artifact)
            .WithMany(a => a.ArtifactTags)
            .HasForeignKey(at => at.ArtifactId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(at => at.Tag)
            .WithMany(t => t.ArtifactTags)
            .HasForeignKey(at => at.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
