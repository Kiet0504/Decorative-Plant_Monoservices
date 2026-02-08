using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Promotion. JSONB: config. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class Promotion
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public Guid? BranchId { get; set; }
    public JsonDocument? Config { get; set; }

    public Branch? Branch { get; set; }
}
