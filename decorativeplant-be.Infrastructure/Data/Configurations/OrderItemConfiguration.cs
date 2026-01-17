using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_item");

        builder.HasKey(oi => oi.Id);
        builder.Property(oi => oi.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(oi => oi.OrderId)
            .IsRequired();

        builder.Property(oi => oi.ListingId)
            .IsRequired();

        builder.Property(oi => oi.StockId)
            .IsRequired();

        builder.Property(oi => oi.Quantity)
            .IsRequired();

        builder.Property(oi => oi.UnitPrice)
            .IsRequired()
            .HasPrecision(12, 2);

        builder.Property(oi => oi.TitleSnapshot)
            .IsRequired()
            .HasMaxLength(255);

        // Relationships
        builder.HasOne(oi => oi.OrderHeader)
            .WithMany(oh => oh.OrderItems)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(oi => oi.Listing)
            .WithMany(l => l.OrderItems)
            .HasForeignKey(oi => oi.ListingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(oi => oi.BatchStock)
            .WithMany(bs => bs.OrderItems)
            .HasForeignKey(oi => oi.StockId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(oi => oi.OrderId);
    }
}
