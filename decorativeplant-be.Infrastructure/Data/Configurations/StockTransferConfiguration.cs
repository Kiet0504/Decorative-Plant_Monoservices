using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("stock_transfer");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.TransferCode).HasMaxLength(50);
        builder.Property(s => s.Status).HasMaxLength(50);
        builder.Property(s => s.LogisticsInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(s => s.Batch).WithMany(p => p.StockTransfers).HasForeignKey(s => s.BatchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(s => s.FromBranch).WithMany(b => b.StockTransfersFrom).HasForeignKey(s => s.FromBranchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(s => s.ToBranch).WithMany(b => b.StockTransfersTo).HasForeignKey(s => s.ToBranchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(s => s.FromLocation).WithMany(l => l.TransfersFrom).HasForeignKey(s => s.FromLocationId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(s => s.ToLocation).WithMany(l => l.TransfersTo).HasForeignKey(s => s.ToLocationId).OnDelete(DeleteBehavior.SetNull);
    }
}
