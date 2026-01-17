using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class IotDeviceConfiguration : IEntityTypeConfiguration<IotDevice>
{
    public void Configure(EntityTypeBuilder<IotDevice> builder)
    {
        builder.ToTable("iot_device");

        builder.HasKey(id => id.Id);
        builder.Property(id => id.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(id => id.StoreId)
            .IsRequired();

        builder.Property(id => id.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(id => id.MacAddress)
            .HasMaxLength(50);

        builder.Property(id => id.FirmwareVer)
            .HasMaxLength(20);

        builder.Property(id => id.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(id => id.ComponentsJson)
            .HasColumnType("jsonb");

        builder.Property(id => id.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(id => id.Store)
            .WithMany(s => s.IotDevices)
            .HasForeignKey(id => id.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(id => id.Location)
            .WithMany(il => il.IotDevices)
            .HasForeignKey(id => id.LocationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(id => id.Stock)
            .WithMany()
            .HasForeignKey(id => id.StockId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(id => id.StoreId);
    }
}
