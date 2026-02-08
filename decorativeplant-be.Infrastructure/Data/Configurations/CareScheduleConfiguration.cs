using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class CareScheduleConfiguration : IEntityTypeConfiguration<CareSchedule>
{
    public void Configure(EntityTypeBuilder<CareSchedule> builder)
    {
        builder.ToTable("care_schedule");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.TaskInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(c => c.GardenPlant).WithMany(g => g.CareSchedules).HasForeignKey(c => c.GardenPlantId).OnDelete(DeleteBehavior.Cascade);
    }
}
