using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class SellerSubscriptionConfiguration : IEntityTypeConfiguration<SellerSubscription>
{
    public void Configure(EntityTypeBuilder<SellerSubscription> builder)
    {
        builder.ToTable("seller_subscription");

        builder.HasKey(ss => ss.Id);
        builder.Property(ss => ss.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(ss => ss.StoreId)
            .IsRequired();

        builder.Property(ss => ss.PackageId)
            .IsRequired();

        builder.Property(ss => ss.StartAt)
            .IsRequired();

        builder.Property(ss => ss.EndAt)
            .IsRequired();

        builder.Property(ss => ss.Status)
            .IsRequired()
            .HasMaxLength(20);

        // Relationships
        builder.HasOne(ss => ss.Store)
            .WithMany(s => s.Subscriptions)
            .HasForeignKey(ss => ss.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ss => ss.SellerPackage)
            .WithMany(sp => sp.Subscriptions)
            .HasForeignKey(ss => ss.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ss => ss.PaymentTransaction)
            .WithMany(pt => pt.SellerSubscriptions)
            .HasForeignKey(ss => ss.PaymentTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(ss => ss.StoreId);
    }
}
