using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public sealed class AiChatThreadConfiguration : IEntityTypeConfiguration<AiChatThread>
{
    public void Configure(EntityTypeBuilder<AiChatThread> builder)
    {
        builder.ToTable("ai_chat_thread");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.UserId).IsRequired();
        builder.HasIndex(t => t.UserId);

        builder.Property(t => t.Title).HasMaxLength(120);

        builder.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(t => t.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Messages)
            .WithOne(m => m.Thread)
            .HasForeignKey(m => m.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

