using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class PickupAddressSnapshotConfiguration : IEntityTypeConfiguration<PickupAddressSnapshot>
{
    public void Configure(EntityTypeBuilder<PickupAddressSnapshot> builder)
    {
        builder.ToTable("pickup_address_snapshot");

        builder.HasKey(pas => pas.Id);
        builder.Property(pas => pas.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(pas => pas.OrderId)
            .IsRequired();

        builder.Property(pas => pas.StoreAddressId)
            .IsRequired();

        builder.Property(pas => pas.FullAddressText)
            .IsRequired();

        builder.Property(pas => pas.ContactName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(pas => pas.ContactPhone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(pas => pas.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(pas => pas.OrderHeader)
            .WithOne(oh => oh.PickupAddressSnapshot)
            .HasForeignKey<PickupAddressSnapshot>(pas => pas.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pas => pas.StoreAddress)
            .WithMany(sa => sa.PickupAddressSnapshots)
            .HasForeignKey(pas => pas.StoreAddressId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
