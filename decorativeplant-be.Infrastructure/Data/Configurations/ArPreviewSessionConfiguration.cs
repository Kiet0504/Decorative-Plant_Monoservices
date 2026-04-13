using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class ArPreviewSessionConfiguration : IEntityTypeConfiguration<ArPreviewSession>
{
    public void Configure(EntityTypeBuilder<ArPreviewSession> builder)
    {
        builder.ToTable("ar_preview_session");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId);
        builder.Property(x => x.ScanJson)
            .HasColumnType("jsonb")
            .HasConversion(JsonDocumentConverter.Instance);
        builder.Property(x => x.ScanJson).IsRequired();

        builder.Property(x => x.TokenSalt).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.ExpiresAt);
    }
}

