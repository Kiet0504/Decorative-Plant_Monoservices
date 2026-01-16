namespace decorativeplant_be.Domain.Entities;

public class Store : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public UserAccount OwnerUser { get; set; } = null!;
    
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
