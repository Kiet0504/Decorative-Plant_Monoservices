using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class Promotion : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    
    public JsonNode? Config { get; set; }
}
