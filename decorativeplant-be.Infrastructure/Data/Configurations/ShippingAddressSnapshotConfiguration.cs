using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class ShippingAddressSnapshotConfiguration : IEntityTypeConfiguration<ShippingAddressSnapshot>
{
    public void Configure(EntityTypeBuilder<ShippingAddressSnapshot> builder)
    {
        builder.ToTable("shipping_address_snapshot");

        builder.HasKey(sas => sas.Id);
        builder.Property(sas => sas.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(sas => sas.OrderId)
            .IsRequired();

        builder.Property(sas => sas.RecipientName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(sas => sas.Phone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(sas => sas.FullAddressText)
            .IsRequired();

        builder.Property(sas => sas.City)
            .HasMaxLength(100);

        builder.Property(sas => sas.Coordinates)
            .HasColumnType("jsonb");

        // Relationships
        builder.HasOne(sas => sas.OrderHeader)
            .WithOne(oh => oh.ShippingAddressSnapshot)
            .HasForeignKey<ShippingAddressSnapshot>(sas => sas.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
