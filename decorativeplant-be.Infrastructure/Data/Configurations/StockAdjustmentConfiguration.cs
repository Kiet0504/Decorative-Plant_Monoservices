using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ToTable("stock_adjustment");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.Type).HasMaxLength(50);
        builder.Property(s => s.MetaInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(s => s.Stock).WithMany(b => b.StockAdjustments).HasForeignKey(s => s.StockId).OnDelete(DeleteBehavior.SetNull);
    }
}
