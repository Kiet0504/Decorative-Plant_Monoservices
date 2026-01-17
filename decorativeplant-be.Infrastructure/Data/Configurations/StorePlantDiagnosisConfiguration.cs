using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class StorePlantDiagnosisConfiguration : IEntityTypeConfiguration<StorePlantDiagnosis>
{
    public void Configure(EntityTypeBuilder<StorePlantDiagnosis> builder)
    {
        builder.ToTable("store_plant_diagnosis");

        builder.HasKey(spd => spd.Id);
        builder.Property(spd => spd.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(spd => spd.StockId)
            .IsRequired();

        builder.Property(spd => spd.AiResultJson)
            .HasColumnType("jsonb");

        builder.Property(spd => spd.IsResolved)
            .HasDefaultValue(false);

        builder.Property(spd => spd.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(spd => spd.BatchStock)
            .WithMany(bs => bs.StorePlantDiagnoses)
            .HasForeignKey(spd => spd.StockId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(spd => spd.StockId);
    }
}
