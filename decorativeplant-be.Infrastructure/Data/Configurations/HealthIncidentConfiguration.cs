using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class HealthIncidentConfiguration : IEntityTypeConfiguration<HealthIncident>
{
    public void Configure(EntityTypeBuilder<HealthIncident> builder)
    {
        builder.ToTable("health_incident");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(h => h.IncidentType).HasMaxLength(50);
        builder.Property(h => h.Severity).HasMaxLength(50);
        builder.Property(h => h.Details).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(h => h.TreatmentInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(h => h.StatusInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(h => h.Images).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(h => h.Batch).WithMany(p => p.HealthIncidents).HasForeignKey(h => h.BatchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(h => h.Stock).WithMany(b => b.HealthIncidents).HasForeignKey(h => h.StockId).OnDelete(DeleteBehavior.SetNull);
    }
}
