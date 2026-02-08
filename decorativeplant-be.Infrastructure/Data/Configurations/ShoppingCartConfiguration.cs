using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class ShoppingCartConfiguration : IEntityTypeConfiguration<ShoppingCart>
{
    public void Configure(EntityTypeBuilder<ShoppingCart> builder)
    {
        builder.ToTable("shopping_cart");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.Items).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(s => s.User).WithMany(u => u.ShoppingCarts).HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
