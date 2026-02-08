using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class PlantDiagnosisConfiguration : IEntityTypeConfiguration<PlantDiagnosis>
{
    public void Configure(EntityTypeBuilder<PlantDiagnosis> builder)
    {
        builder.ToTable("plant_diagnosis");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.UserInput).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(p => p.AiResult).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(p => p.Feedback).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(p => p.GardenPlant).WithMany(g => g.PlantDiagnoses).HasForeignKey(p => p.GardenPlantId).OnDelete(DeleteBehavior.Cascade);
    }
}
