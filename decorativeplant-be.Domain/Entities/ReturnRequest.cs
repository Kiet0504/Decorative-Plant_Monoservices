using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class ReturnRequest : BaseEntity
{
    public Guid OrderId { get; set; }
    public OrderHeader Order { get; set; } = null!;
    
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    
    public string Status { get; set; } = string.Empty;
    public JsonNode? Info { get; set; }
    public JsonNode? Images { get; set; }
}
