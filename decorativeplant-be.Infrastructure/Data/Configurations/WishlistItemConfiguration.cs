using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("wishlist_item");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(w => w.UserId).IsRequired();
        builder.Property(w => w.ListingId).IsRequired();

        builder.Property(w => w.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(w => w.User)
            .WithMany(u => u.WishlistItems)
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.Listing)
            .WithMany(l => l.WishlistItems)
            .HasForeignKey(w => w.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => new { w.UserId, w.ListingId }).IsUnique();
        builder.HasIndex(w => w.UserId);
        builder.HasIndex(w => w.ListingId);
    }
}

