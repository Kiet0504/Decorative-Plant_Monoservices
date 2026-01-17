using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class ShoppingCartConfiguration : IEntityTypeConfiguration<ShoppingCart>
{
    public void Configure(EntityTypeBuilder<ShoppingCart> builder)
    {
        builder.ToTable("shopping_cart");

        builder.HasKey(sc => sc.Id);
        builder.Property(sc => sc.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(sc => sc.UserId)
            .IsRequired();

        builder.Property(sc => sc.ItemsJson)
            .HasColumnType("jsonb");

        builder.Property(sc => sc.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(sc => sc.UserAccount)
            .WithMany(u => u.ShoppingCarts)
            .HasForeignKey(sc => sc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(sc => sc.UserId)
            .IsUnique();
    }
}
