using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data.Configurations;

public class CommunityPostConfiguration : IEntityTypeConfiguration<CommunityPost>
{
    public void Configure(EntityTypeBuilder<CommunityPost> builder)
    {
        builder.ToTable("CommunityPosts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Content).IsRequired();
        builder.Property(p => p.ImageUrl).HasMaxLength(2048);
        builder.Property(p => p.Location).IsRequired().HasMaxLength(256);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.HasIndex(p => p.CreatedAt);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
