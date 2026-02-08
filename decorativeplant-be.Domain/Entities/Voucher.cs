using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Voucher. JSONB: info, rules. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class Voucher
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public Guid? BranchId { get; set; }
    public JsonDocument? Info { get; set; }
    public JsonDocument? Rules { get; set; }
    public bool IsActive { get; set; }

    public Branch? Branch { get; set; }
}
