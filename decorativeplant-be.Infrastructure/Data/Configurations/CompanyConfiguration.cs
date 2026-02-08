using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("company");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.Name).IsRequired().HasMaxLength(255);
        builder.Property(c => c.TaxCode).HasMaxLength(50);
        builder.Property(c => c.Email).HasMaxLength(255);
        builder.Property(c => c.Phone).HasMaxLength(20);
        builder.Property(c => c.Info).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
    }
}
