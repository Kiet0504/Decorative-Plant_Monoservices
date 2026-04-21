namespace decorativeplant_be.Application.Common.DTOs.Commerce;

public class CreatePaymentRequest
{
    public List<Guid> OrderIds { get; set; } = new();
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
}

public class PaymentResponse
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public string? TransactionCode { get; set; }
    public string? Provider { get; set; }
    public string? Method { get; set; }
    public string? Type { get; set; }
    public string? Amount { get; set; }
    public string? Status { get; set; }
    public string? ExternalId { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? QrCode { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? CollectedAt { get; set; }
    public string? CollectedBy { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public string? PickedUpBy { get; set; }
}

/// <summary>
/// PayOS webhook data
/// </summary>
public class PayOSWebhookRequest
{
    public string? Code { get; set; }
    public string? Desc { get; set; }
    public bool? Success { get; set; }
    public PayOSWebhookData? Data { get; set; }
    public string? Signature { get; set; }
}

public class PayOSWebhookData
{
    public long? OrderCode { get; set; }
    public int? Amount { get; set; }
    public string? Description { get; set; }
    public string? AccountNumber { get; set; }
    public string? Reference { get; set; }
    public string? TransactionDateTime { get; set; }
    public string? Currency { get; set; }
    public string? PaymentLinkId { get; set; }
    public string? Code { get; set; }
    public string? Desc { get; set; }
    public string? CounterAccountBankId { get; set; }
    public string? CounterAccountBankName { get; set; }
    public string? CounterAccountName { get; set; }
    public string? CounterAccountNumber { get; set; }
    public string? VirtualAccountName { get; set; }
    public string? VirtualAccountNumber { get; set; }
}
