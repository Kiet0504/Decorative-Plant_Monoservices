using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class InventoryLocationConfiguration : IEntityTypeConfiguration<InventoryLocation>
{
    public void Configure(EntityTypeBuilder<InventoryLocation> builder)
    {
        builder.ToTable("inventory_location");

        builder.HasKey(il => il.Id);
        builder.Property(il => il.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(il => il.StoreId)
            .IsRequired();

        builder.Property(il => il.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(il => il.Type)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(il => il.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(il => il.Store)
            .WithMany(s => s.InventoryLocations)
            .HasForeignKey(il => il.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(il => il.Address)
            .WithMany(sa => sa.InventoryLocations)
            .HasForeignKey(il => il.AddressId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(il => il.ParentLocation)
            .WithMany(il => il.ChildLocations)
            .HasForeignKey(il => il.ParentLocationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(il => il.StoreId);
    }
}
