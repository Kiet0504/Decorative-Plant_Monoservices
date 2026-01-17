using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class CareScheduleConfiguration : IEntityTypeConfiguration<CareSchedule>
{
    public void Configure(EntityTypeBuilder<CareSchedule> builder)
    {
        builder.ToTable("care_schedule");

        builder.HasKey(cs => cs.Id);
        builder.Property(cs => cs.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(cs => cs.GardenPlantId)
            .IsRequired();

        builder.Property(cs => cs.TaskType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(cs => cs.Frequency)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(cs => cs.IsActive)
            .HasDefaultValue(true);

        // Relationships
        builder.HasOne(cs => cs.MyGardenPlant)
            .WithMany(mgp => mgp.CareSchedules)
            .HasForeignKey(cs => cs.GardenPlantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(cs => cs.GardenPlantId);
    }
}
