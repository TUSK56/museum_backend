using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data.Configurations;

public class ChatMessageArtifactConfiguration : IEntityTypeConfiguration<ChatMessageArtifact>
{
    public void Configure(EntityTypeBuilder<ChatMessageArtifact> builder)
    {
        builder.ToTable("ChatMessageArtifacts");
        builder.HasKey(cma => new { cma.MessageId, cma.ArtifactId });

        builder.HasOne(cma => cma.Message)
            .WithMany(m => m.Artifacts)
            .HasForeignKey(cma => cma.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cma => cma.Artifact)
            .WithMany()
            .HasForeignKey(cma => cma.ArtifactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
