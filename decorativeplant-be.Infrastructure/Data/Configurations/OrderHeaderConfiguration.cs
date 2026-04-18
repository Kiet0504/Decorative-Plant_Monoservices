using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class OrderHeaderConfiguration : IEntityTypeConfiguration<OrderHeader>
{
    public void Configure(EntityTypeBuilder<OrderHeader> builder)
    {
        builder.ToTable("order_header");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(o => o.OrderCode).HasMaxLength(50);
        builder.Property(o => o.TypeInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(o => o.Financials).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(o => o.Status).HasMaxLength(50);
        builder.Property(o => o.Notes).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(o => o.DeliveryAddress).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(o => o.PickupInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(o => o.User).WithMany(u => u.Orders).HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(o => o.Voucher).WithMany().HasForeignKey(o => o.VoucherId).OnDelete(DeleteBehavior.SetNull);

        // Partial index: AutoCompleteDeliveredOrdersJob scans only orders currently
        // in "delivered" state by DeliveredAt. Filtering at index level keeps the
        // job O(k) in eligible rows, not O(n) in all orders.
        builder.HasIndex(o => o.DeliveredAt)
            .HasFilter("\"Status\" = 'delivered'")
            .HasDatabaseName("IX_order_header_DeliveredAt_delivered");
    }
}
