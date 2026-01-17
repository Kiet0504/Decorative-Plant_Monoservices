using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class StoreAddressConfiguration : IEntityTypeConfiguration<StoreAddress>
{
    public void Configure(EntityTypeBuilder<StoreAddress> builder)
    {
        builder.ToTable("store_address");

        builder.HasKey(sa => sa.Id);
        builder.Property(sa => sa.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(sa => sa.StoreId)
            .IsRequired();

        builder.Property(sa => sa.Label)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(sa => sa.RecipientName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(sa => sa.Phone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(sa => sa.FullAddressText)
            .IsRequired();

        builder.Property(sa => sa.City)
            .HasMaxLength(100);

        builder.Property(sa => sa.Coordinates)
            .HasColumnType("jsonb");

        builder.Property(sa => sa.IsDefaultPickup)
            .HasDefaultValue(false);

        builder.Property(sa => sa.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(sa => sa.Store)
            .WithMany(s => s.StoreAddresses)
            .HasForeignKey(sa => sa.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(sa => sa.StoreId);
    }
}
