using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class BatchStockConfiguration : IEntityTypeConfiguration<BatchStock>
{
    public void Configure(EntityTypeBuilder<BatchStock> builder)
    {
        builder.ToTable("batch_stock");

        builder.HasKey(bs => bs.Id);
        builder.Property(bs => bs.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(bs => bs.BatchId)
            .IsRequired();

        builder.Property(bs => bs.LocationId)
            .IsRequired();

        builder.Property(bs => bs.Quantity)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(bs => bs.ReservedQuantity)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(bs => bs.Unit)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(bs => bs.PotSize)
            .HasMaxLength(50);

        builder.Property(bs => bs.CurrentHealthStatus)
            .HasMaxLength(50);

        builder.Property(bs => bs.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(bs => bs.PlantBatch)
            .WithMany(pb => pb.BatchStocks)
            .HasForeignKey(bs => bs.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bs => bs.InventoryLocation)
            .WithMany(il => il.BatchStocks)
            .HasForeignKey(bs => bs.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(bs => bs.BatchId);
        builder.HasIndex(bs => bs.LocationId);
    }
}
