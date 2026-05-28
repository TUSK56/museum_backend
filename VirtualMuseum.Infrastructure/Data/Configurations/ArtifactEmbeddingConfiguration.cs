using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data.Configurations;

public class ArtifactEmbeddingConfiguration : IEntityTypeConfiguration<ArtifactEmbedding>
{
    public void Configure(EntityTypeBuilder<ArtifactEmbedding> builder)
    {
        builder.ToTable("ArtifactEmbeddings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EmbeddingVector).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasOne(e => e.Artifact)
            .WithMany(a => a.Embeddings)
            .HasForeignKey(e => e.ArtifactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
