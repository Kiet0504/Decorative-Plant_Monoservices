using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class PlantBatchConfiguration : IEntityTypeConfiguration<PlantBatch>
{
    public void Configure(EntityTypeBuilder<PlantBatch> builder)
    {
        builder.ToTable("plant_batch");

        builder.HasKey(pb => pb.Id);
        builder.Property(pb => pb.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(pb => pb.StoreId)
            .IsRequired();

        builder.Property(pb => pb.TaxonomyId)
            .IsRequired();

        builder.Property(pb => pb.BatchCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(pb => pb.SourceType)
            .HasMaxLength(50);

        builder.Property(pb => pb.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(pb => pb.Store)
            .WithMany(s => s.PlantBatches)
            .HasForeignKey(pb => pb.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pb => pb.PlantTaxonomy)
            .WithMany(pt => pt.PlantBatches)
            .HasForeignKey(pb => pb.TaxonomyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pb => pb.ParentBatch)
            .WithMany(pb => pb.ChildBatches)
            .HasForeignKey(pb => pb.ParentBatchId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(pb => pb.StoreId);
    }
}
