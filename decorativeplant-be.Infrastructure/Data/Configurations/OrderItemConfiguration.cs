using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_item");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(o => o.Pricing).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(o => o.Snapshots).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(o => o.Order).WithMany(h => h.OrderItems).HasForeignKey(o => o.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(o => o.Listing).WithMany(l => l.OrderItems).HasForeignKey(o => o.ListingId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(o => o.Stock).WithMany(s => s.OrderItems).HasForeignKey(o => o.StockId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(o => o.Batch).WithMany(b => b.OrderItems).HasForeignKey(o => o.BatchId).OnDelete(DeleteBehavior.SetNull);
    }
}
