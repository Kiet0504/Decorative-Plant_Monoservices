namespace decorativeplant_be.Domain.Entities;

public class WishlistItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ListingId { get; set; }
    public DateTime CreatedAt { get; set; }

    public UserAccount User { get; set; } = null!;
    public ProductListing Listing { get; set; } = null!;
}

