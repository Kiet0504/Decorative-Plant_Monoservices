using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branch");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(b => b.CompanyId).IsRequired();
        builder.Property(b => b.Code).IsRequired().HasMaxLength(20);
        builder.HasIndex(b => b.Code).IsUnique();
        builder.Property(b => b.Name).IsRequired().HasMaxLength(255);
        builder.Property(b => b.Slug).IsRequired().HasMaxLength(100);
        builder.HasIndex(b => b.Slug).IsUnique();
        builder.Property(b => b.BranchType).HasMaxLength(50);
        builder.Property(b => b.ContactInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(b => b.OperatingHours).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(b => b.Settings).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(b => b.IsActive).HasDefaultValue(true);
        builder.Property(b => b.CreatedAt).HasDefaultValueSql("now()");
        builder.HasOne(b => b.Company).WithMany(c => c.Branches).HasForeignKey(b => b.CompanyId).OnDelete(DeleteBehavior.Cascade);
    }
}
