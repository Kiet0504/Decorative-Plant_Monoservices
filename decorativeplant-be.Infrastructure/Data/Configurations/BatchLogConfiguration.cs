using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class BatchLogConfiguration : IEntityTypeConfiguration<BatchLog>
{
    public void Configure(EntityTypeBuilder<BatchLog> builder)
    {
        builder.ToTable("batch_log");

        builder.HasKey(bl => bl.Id);
        builder.Property(bl => bl.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(bl => bl.BatchId)
            .IsRequired();

        builder.Property(bl => bl.ActionType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(bl => bl.PerformedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(bl => bl.PerformerId)
            .IsRequired();

        // Relationships
        builder.HasOne(bl => bl.PlantBatch)
            .WithMany(pb => pb.BatchLogs)
            .HasForeignKey(bl => bl.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bl => bl.Performer)
            .WithMany(u => u.BatchLogs)
            .HasForeignKey(bl => bl.PerformerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(bl => bl.BatchId);
    }
}
