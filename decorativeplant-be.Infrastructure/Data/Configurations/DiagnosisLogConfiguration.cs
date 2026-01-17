using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class DiagnosisLogConfiguration : IEntityTypeConfiguration<DiagnosisLog>
{
    public void Configure(EntityTypeBuilder<DiagnosisLog> builder)
    {
        builder.ToTable("diagnosis_log");

        builder.HasKey(dl => dl.Id);
        builder.Property(dl => dl.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(dl => dl.GardenPlantId)
            .IsRequired();

        builder.Property(dl => dl.AiResultJson)
            .HasColumnType("jsonb");

        builder.Property(dl => dl.UserFeedbackJson)
            .HasColumnType("jsonb");

        builder.Property(dl => dl.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(dl => dl.MyGardenPlant)
            .WithMany(mgp => mgp.DiagnosisLogs)
            .HasForeignKey(dl => dl.GardenPlantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(dl => dl.GardenPlantId);
    }
}
