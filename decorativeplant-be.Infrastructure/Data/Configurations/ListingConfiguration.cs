using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("listing");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(l => l.StoreId)
            .IsRequired();

        builder.Property(l => l.StockId)
            .IsRequired();

        builder.Property(l => l.Title)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(l => l.Price)
            .IsRequired()
            .HasPrecision(12, 2);

        builder.Property(l => l.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("VND");

        builder.Property(l => l.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(l => l.PhotosJson)
            .HasColumnType("jsonb");

        builder.Property(l => l.MinOrderQty)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(l => l.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(l => l.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(l => l.Store)
            .WithMany(s => s.Listings)
            .HasForeignKey(l => l.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.BatchStock)
            .WithMany(bs => bs.Listings)
            .HasForeignKey(l => l.StockId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.StoreId);
        builder.HasIndex(l => l.StockId);
    }
}
