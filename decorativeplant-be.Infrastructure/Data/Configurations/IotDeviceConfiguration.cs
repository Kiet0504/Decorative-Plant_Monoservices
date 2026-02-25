using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class IotDeviceConfiguration : IEntityTypeConfiguration<IotDevice>
{
    public void Configure(EntityTypeBuilder<IotDevice> builder)
    {
        builder.ToTable("iot_device");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(i => i.SecretKey).IsRequired().HasMaxLength(255);
        builder.Property(i => i.DeviceInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(i => i.Status).HasMaxLength(50);
        builder.Property(i => i.ActivityLog).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(i => i.Components).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(i => i.Branch).WithMany(b => b.IotDevices).HasForeignKey(i => i.BranchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(i => i.Location).WithMany(l => l.IotDevices).HasForeignKey(i => i.LocationId).OnDelete(DeleteBehavior.SetNull);
    }
}
