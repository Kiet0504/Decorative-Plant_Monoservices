using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class CultivationLogConfiguration : IEntityTypeConfiguration<CultivationLog>
{
    public void Configure(EntityTypeBuilder<CultivationLog> builder)
    {
        builder.ToTable("cultivation_log");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.ActivityType).HasMaxLength(50);
        builder.Property(c => c.Details).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(c => c.Batch).WithMany(p => p.CultivationLogs).HasForeignKey(c => c.BatchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(c => c.Location).WithMany(l => l.CultivationLogs).HasForeignKey(c => c.LocationId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(c => c.PerformedByUser).WithMany(u => u.CultivationLogs).HasForeignKey(c => c.PerformedBy).OnDelete(DeleteBehavior.SetNull);
    }
}
