using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class StoreWalletConfiguration : IEntityTypeConfiguration<StoreWallet>
{
    public void Configure(EntityTypeBuilder<StoreWallet> builder)
    {
        builder.ToTable("store_wallet");

        builder.HasKey(sw => sw.Id);
        builder.Property(sw => sw.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(sw => sw.StoreId)
            .IsRequired();

        builder.Property(sw => sw.Balance)
            .IsRequired()
            .HasPrecision(12, 2)
            .HasDefaultValue(0);

        builder.Property(sw => sw.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(sw => sw.Store)
            .WithMany(s => s.StoreWallets)
            .HasForeignKey(sw => sw.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(sw => sw.StoreId)
            .IsUnique();
    }
}
