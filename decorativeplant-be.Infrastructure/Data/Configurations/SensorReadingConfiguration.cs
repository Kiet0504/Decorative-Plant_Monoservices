using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class SensorReadingConfiguration : IEntityTypeConfiguration<SensorReading>
{
    public void Configure(EntityTypeBuilder<SensorReading> builder)
    {
        builder.ToTable("sensor_reading");

        builder.HasKey(sr => sr.Id);
        builder.Property(sr => sr.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(sr => sr.DeviceId)
            .IsRequired();

        builder.Property(sr => sr.ComponentKey)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(sr => sr.Value)
            .IsRequired();

        builder.Property(sr => sr.Timestamp)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(sr => sr.IotDevice)
            .WithMany(id => id.SensorReadings)
            .HasForeignKey(sr => sr.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(sr => sr.DeviceId);
        builder.HasIndex(sr => sr.Timestamp);
    }
}
