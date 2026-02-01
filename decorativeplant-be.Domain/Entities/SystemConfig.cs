using System.Text.Json.Nodes;
using System.ComponentModel.DataAnnotations;

namespace decorativeplant_be.Domain.Entities;

public class SystemConfig
{
    [Key]
    public string Key { get; set; } = string.Empty;
    public JsonNode? Value { get; set; }
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
