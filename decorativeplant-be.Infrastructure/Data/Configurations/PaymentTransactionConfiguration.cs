using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transaction");

        builder.HasKey(pt => pt.Id);
        builder.Property(pt => pt.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(pt => pt.OrderId)
            .IsRequired();

        builder.Property(pt => pt.Amount)
            .IsRequired()
            .HasPrecision(12, 2);

        builder.Property(pt => pt.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(pt => pt.Type)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(pt => pt.TransactionRef)
            .HasMaxLength(100);

        builder.Property(pt => pt.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(pt => pt.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(pt => pt.OrderHeader)
            .WithMany(oh => oh.PaymentTransactions)
            .HasForeignKey(pt => pt.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pt => pt.OrderId);
    }
}
