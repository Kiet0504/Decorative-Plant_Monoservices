using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class InventoryAdjustmentConfiguration : IEntityTypeConfiguration<InventoryAdjustment>
{
    public void Configure(EntityTypeBuilder<InventoryAdjustment> builder)
    {
        builder.ToTable("inventory_adjustment");

        builder.HasKey(ia => ia.Id);
        builder.Property(ia => ia.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(ia => ia.StockId)
            .IsRequired();

        builder.Property(ia => ia.Reason)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ia => ia.QuantityChange)
            .IsRequired();

        builder.Property(ia => ia.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(ia => ia.BatchStock)
            .WithMany(bs => bs.InventoryAdjustments)
            .HasForeignKey(ia => ia.StockId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ia => ia.StockId);
    }
}
