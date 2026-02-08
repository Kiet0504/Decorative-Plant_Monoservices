using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("voucher");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(v => v.Code).HasMaxLength(50);
        builder.Property(v => v.Info).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(v => v.Rules).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(v => v.Branch).WithMany(b => b.Vouchers).HasForeignKey(v => v.BranchId).OnDelete(DeleteBehavior.SetNull);
    }
}
