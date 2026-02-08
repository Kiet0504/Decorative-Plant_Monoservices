using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class BatchStockConfiguration : IEntityTypeConfiguration<BatchStock>
{
    public void Configure(EntityTypeBuilder<BatchStock> builder)
    {
        builder.ToTable("batch_stock");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(b => b.Quantities).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(b => b.HealthStatus).HasMaxLength(50);
        builder.Property(b => b.LastCountInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(b => b.Batch).WithMany(p => p.BatchStocks).HasForeignKey(b => b.BatchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(b => b.Location).WithMany(l => l.BatchStocks).HasForeignKey(b => b.LocationId).OnDelete(DeleteBehavior.SetNull);
    }
}
