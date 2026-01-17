using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class ShippingConfiguration : IEntityTypeConfiguration<Shipping>
{
    public void Configure(EntityTypeBuilder<Shipping> builder)
    {
        builder.ToTable("shipping");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.OrderId)
            .IsRequired();

        builder.Property(s => s.PickupAddressId)
            .IsRequired();

        builder.Property(s => s.DeliveryAddressId)
            .IsRequired();

        builder.Property(s => s.Carrier)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.TrackingCode)
            .HasMaxLength(50);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.ShippingFee)
            .IsRequired()
            .HasPrecision(12, 2);

        builder.Property(s => s.EventsJson)
            .HasColumnType("jsonb");

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(s => s.OrderHeader)
            .WithOne(oh => oh.Shipping)
            .HasForeignKey<Shipping>(s => s.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.PickupAddressSnapshot)
            .WithOne(pas => pas.Shipping)
            .HasForeignKey<Shipping>(s => s.PickupAddressId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ShippingAddressSnapshot)
            .WithOne(sas => sas.Shipping)
            .HasForeignKey<Shipping>(s => s.DeliveryAddressId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.OrderId)
            .IsUnique();
    }
}
