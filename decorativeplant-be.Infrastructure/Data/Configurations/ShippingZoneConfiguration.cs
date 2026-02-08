using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class ShippingZoneConfiguration : IEntityTypeConfiguration<ShippingZone>
{
    public void Configure(EntityTypeBuilder<ShippingZone> builder)
    {
        builder.ToTable("shipping_zone");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.Name).HasMaxLength(255);
        builder.Property(s => s.Locations).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(s => s.FeeConfig).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(s => s.DeliveryTimeConfig).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(s => s.Branch).WithMany(b => b.ShippingZones).HasForeignKey(s => s.BranchId).OnDelete(DeleteBehavior.SetNull);
    }
}
