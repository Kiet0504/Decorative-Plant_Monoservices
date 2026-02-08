using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// System-wide config key-value. Primary key: Key (string). value is JSONB.
/// </summary>
public class SystemConfig
{
    public string Key { get; set; } = string.Empty;
    public JsonDocument? Value { get; set; }
    public string? Description { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
