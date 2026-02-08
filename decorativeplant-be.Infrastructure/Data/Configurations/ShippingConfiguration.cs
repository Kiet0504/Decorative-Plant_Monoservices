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
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.TrackingCode).HasMaxLength(100);
        builder.Property(s => s.CarrierInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(s => s.Status).HasMaxLength(50);
        builder.Property(s => s.DeliveryDetails).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(s => s.Events).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(s => s.Order).WithMany(o => o.Shippings).HasForeignKey(s => s.OrderId).OnDelete(DeleteBehavior.SetNull);
    }
}
