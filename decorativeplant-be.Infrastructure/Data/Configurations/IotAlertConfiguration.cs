using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class IotAlertConfiguration : IEntityTypeConfiguration<IotAlert>
{
    public void Configure(EntityTypeBuilder<IotAlert> builder)
    {
        builder.ToTable("iot_alert");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.AlertInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(a => a.ResolutionInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        
        builder.HasOne(a => a.Device)
            .WithMany(d => d.IotAlerts)
            .HasForeignKey(a => a.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
