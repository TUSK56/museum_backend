using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data.Configurations;

public class AiChatExchangeConfiguration : IEntityTypeConfiguration<AiChatExchange>
{
    public void Configure(EntityTypeBuilder<AiChatExchange> builder)
    {
        builder.ToTable("AiChatExchanges");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserEmail).HasMaxLength(256);
        builder.Property(x => x.UserDisplayName).HasMaxLength(256);
        builder.Property(x => x.SessionKey).IsRequired().HasMaxLength(128);
        builder.Property(x => x.UserMessage).IsRequired();
        builder.Property(x => x.AssistantReply).IsRequired();
        builder.Property(x => x.Source).IsRequired().HasMaxLength(16);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.UserId);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
