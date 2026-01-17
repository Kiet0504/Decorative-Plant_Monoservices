using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class CareLogConfiguration : IEntityTypeConfiguration<CareLog>
{
    public void Configure(EntityTypeBuilder<CareLog> builder)
    {
        builder.ToTable("care_log");

        builder.HasKey(cl => cl.Id);
        builder.Property(cl => cl.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(cl => cl.GardenPlantId)
            .IsRequired();

        builder.Property(cl => cl.Action)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(cl => cl.ImagesJson)
            .HasColumnType("jsonb");

        builder.Property(cl => cl.PerformedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(cl => cl.MyGardenPlant)
            .WithMany(mgp => mgp.CareLogs)
            .HasForeignKey(cl => cl.GardenPlantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(cl => cl.GardenPlantId);
    }
}
