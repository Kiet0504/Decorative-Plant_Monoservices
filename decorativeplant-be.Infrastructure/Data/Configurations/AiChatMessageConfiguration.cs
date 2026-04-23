using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public sealed class AiChatMessageConfiguration : IEntityTypeConfiguration<AiChatMessage>
{
    public void Configure(EntityTypeBuilder<AiChatMessage> builder)
    {
        builder.ToTable("ai_chat_message");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(m => m.ThreadId).IsRequired();
        builder.Property(m => m.Role).IsRequired().HasMaxLength(16);
        builder.Property(m => m.Content).IsRequired();

        builder.Property(m => m.CreatedAt).HasDefaultValueSql("now()");

        builder.Property(m => m.AttachmentUrl).HasMaxLength(2000);
        builder.Property(m => m.AttachmentMimeType).HasMaxLength(64);

        builder.Property(m => m.DiagnosisJson).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(m => m.RecommendationsJson).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(m => m.MetadataJson).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);

        builder.HasIndex(m => new { m.ThreadId, m.CreatedAt });
    }
}

