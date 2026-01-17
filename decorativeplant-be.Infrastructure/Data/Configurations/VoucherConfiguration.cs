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
        builder.Property(v => v.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(v => v.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(v => v.Code);

        builder.Property(v => v.DiscountType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(v => v.DiscountValue)
            .IsRequired()
            .HasPrecision(12, 2);

        builder.Property(v => v.MinOrderValue)
            .IsRequired()
            .HasPrecision(12, 2);

        builder.Property(v => v.ValidFrom)
            .IsRequired();

        builder.Property(v => v.ValidTo)
            .IsRequired();

        // Relationships
        builder.HasOne(v => v.Store)
            .WithMany(s => s.Vouchers)
            .HasForeignKey(v => v.StoreId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(v => v.StoreId);
    }
}
