using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class CareLogConfiguration : IEntityTypeConfiguration<CareLog>
{
    public void Configure(EntityTypeBuilder<CareLog> builder)
    {
        builder.ToTable("care_log");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.LogInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(c => c.Images).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(c => c.GardenPlant).WithMany(g => g.CareLogs).HasForeignKey(c => c.GardenPlantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.Schedule).WithMany(s => s.CareLogs).HasForeignKey(c => c.ScheduleId).OnDelete(DeleteBehavior.SetNull);
    }
}
