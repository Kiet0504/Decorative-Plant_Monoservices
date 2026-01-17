namespace decorativeplant_be.Domain.Entities;

public class UserAccount : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty; // Admin/Seller/Buyer
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public UserProfile? UserProfile { get; set; }
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<Store> OwnedStores { get; set; } = new List<Store>();
    public ICollection<ShoppingCart> ShoppingCarts { get; set; } = new List<ShoppingCart>();
    public ICollection<OrderHeader> Orders { get; set; } = new List<OrderHeader>();
    public ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();
    public ICollection<MyGardenPlant> MyGardenPlants { get; set; } = new List<MyGardenPlant>();
    public ICollection<BatchLog> BatchLogs { get; set; } = new List<BatchLog>();
}
