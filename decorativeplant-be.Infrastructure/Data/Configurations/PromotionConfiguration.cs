using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("promotion");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.Name).HasMaxLength(255);
        builder.Property(p => p.Config).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(p => p.Branch).WithMany(b => b.Promotions).HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.SetNull);
    }
}
