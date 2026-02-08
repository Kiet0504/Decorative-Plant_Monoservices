using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transaction");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.TransactionCode).HasMaxLength(100);
        builder.Property(p => p.Details).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(p => p.Order).WithMany(o => o.PaymentTransactions).HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
