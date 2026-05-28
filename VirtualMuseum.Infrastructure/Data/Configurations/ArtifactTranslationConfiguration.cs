using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data.Configurations;

public class ArtifactTranslationConfiguration : IEntityTypeConfiguration<ArtifactTranslation>
{
    public void Configure(EntityTypeBuilder<ArtifactTranslation> builder)
    {
        builder.ToTable("ArtifactTranslations");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.LanguageCode).IsRequired().HasMaxLength(10);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(500);

        builder.HasOne(t => t.Artifact)
            .WithMany(a => a.Translations)
            .HasForeignKey(t => t.ArtifactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
