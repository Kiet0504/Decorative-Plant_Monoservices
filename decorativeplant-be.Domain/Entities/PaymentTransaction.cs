using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Payment transaction. JSONB: details. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class PaymentTransaction
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public string? TransactionCode { get; set; }
    public JsonDocument? Details { get; set; }
    public DateTime? CreatedAt { get; set; }

    public OrderHeader? Order { get; set; }
}
