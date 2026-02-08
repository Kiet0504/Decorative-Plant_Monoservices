using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class SensorReadingConfiguration : IEntityTypeConfiguration<SensorReading>
{
    public void Configure(EntityTypeBuilder<SensorReading> builder)
    {
        builder.ToTable("sensor_reading");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.DeviceId).IsRequired();
        builder.Property(s => s.ComponentKey).HasMaxLength(50);
        builder.Property(s => s.Value).HasPrecision(10, 2);
        builder.HasOne(s => s.Device).WithMany(d => d.SensorReadings).HasForeignKey(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}
