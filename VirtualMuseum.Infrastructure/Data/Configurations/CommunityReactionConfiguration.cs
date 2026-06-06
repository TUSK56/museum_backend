using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data.Configurations;

public class CommunityReactionConfiguration : IEntityTypeConfiguration<CommunityReaction>
{
    public void Configure(EntityTypeBuilder<CommunityReaction> builder)
    {
        builder.ToTable("CommunityReactions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.ReactionType).IsRequired().HasMaxLength(16);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.HasIndex(r => new { r.PostId, r.UserId }).IsUnique();

        builder.HasOne(r => r.Post)
            .WithMany(p => p.Reactions)
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
