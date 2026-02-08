using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Return request. JSONB: info, images. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class ReturnRequest
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? UserId { get; set; }
    public string? Status { get; set; }
    public JsonDocument? Info { get; set; }
    public JsonDocument? Images { get; set; }
    public DateTime? CreatedAt { get; set; }

    public OrderHeader? Order { get; set; }
    public UserAccount? User { get; set; }
}
