using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class Voucher : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    
    public JsonNode? Info { get; set; }
    public JsonNode? Rules { get; set; }
    public bool IsActive { get; set; }
}
