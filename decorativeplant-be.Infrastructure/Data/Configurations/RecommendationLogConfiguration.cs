using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class RecommendationLogConfiguration : IEntityTypeConfiguration<RecommendationLog>
{
    public void Configure(EntityTypeBuilder<RecommendationLog> builder)
    {
        builder.ToTable("recommendation_log");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(r => r.Strategy).HasMaxLength(50);
        builder.Property(r => r.RequestJson).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(r => r.ResponseJson).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(r => r.SeedSignalsJson).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
    }
}

