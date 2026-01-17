using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("wallet_transaction");

        builder.HasKey(wt => wt.Id);
        builder.Property(wt => wt.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(wt => wt.WalletId)
            .IsRequired();

        builder.Property(wt => wt.Amount)
            .IsRequired()
            .HasPrecision(12, 2);

        builder.Property(wt => wt.Type)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(wt => wt.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(wt => wt.StoreWallet)
            .WithMany(sw => sw.WalletTransactions)
            .HasForeignKey(wt => wt.WalletId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(wt => wt.RefOrder)
            .WithMany(o => o.WalletTransactions)
            .HasForeignKey(wt => wt.RefOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(wt => wt.WalletId);
        builder.HasIndex(wt => wt.RefOrderId);
    }
}
