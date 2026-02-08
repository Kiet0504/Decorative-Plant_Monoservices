using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class PlantBatchConfiguration : IEntityTypeConfiguration<PlantBatch>
{
    public void Configure(EntityTypeBuilder<PlantBatch> builder)
    {
        builder.ToTable("plant_batch");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.BatchCode).HasMaxLength(50);
        builder.Property(p => p.SourceInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(p => p.Specs).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(p => p.Branch).WithMany(b => b.PlantBatches).HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(p => p.Taxonomy).WithMany(t => t.PlantBatches).HasForeignKey(p => p.TaxonomyId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(p => p.Supplier).WithMany(s => s.PlantBatches).HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(p => p.ParentBatch).WithMany(p => p.ChildBatches).HasForeignKey(p => p.ParentBatchId).OnDelete(DeleteBehavior.SetNull);
    }
}
