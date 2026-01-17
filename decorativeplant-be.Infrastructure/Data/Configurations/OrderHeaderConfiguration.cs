using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class OrderHeaderConfiguration : IEntityTypeConfiguration<OrderHeader>
{
    public void Configure(EntityTypeBuilder<OrderHeader> builder)
    {
        builder.ToTable("order_header");

        builder.HasKey(oh => oh.Id);
        builder.Property(oh => oh.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(oh => oh.OrderCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(oh => oh.OrderCode)
            .IsUnique();

        builder.Property(oh => oh.UserId)
            .IsRequired();

        builder.Property(oh => oh.StoreId)
            .IsRequired();

        builder.Property(oh => oh.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(oh => oh.PaymentStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(oh => oh.TotalAmount)
            .IsRequired()
            .HasPrecision(12, 2);

        builder.Property(oh => oh.ShippingFee)
            .IsRequired()
            .HasPrecision(12, 2);

        builder.Property(oh => oh.DiscountAmount)
            .IsRequired()
            .HasPrecision(12, 2)
            .HasDefaultValue(0);

        builder.Property(oh => oh.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(oh => oh.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(oh => oh.UserAccount)
            .WithMany(u => u.Orders)
            .HasForeignKey(oh => oh.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(oh => oh.Store)
            .WithMany(s => s.Orders)
            .HasForeignKey(oh => oh.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(oh => oh.UserId);
        builder.HasIndex(oh => oh.StoreId);
    }
}
