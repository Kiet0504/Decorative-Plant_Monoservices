namespace decorativeplant_be.Application.Services;

/// <summary>
/// PayOS payment gateway service interface.
/// </summary>
public interface IPayOSService
{
    /// <summary>
    /// Creates a PayOS payment link for the given order.
    /// </summary>
    Task<PayOSCreatePaymentResult> CreatePaymentLinkAsync(
        long orderCode,
        int amount,
        string description,
        List<PayOSItem> items,
        string returnUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets payment link information by order code.
    /// </summary>
    Task<PayOSPaymentInfo?> GetPaymentInfoAsync(long orderCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a payment link by order code.
    /// </summary>
    Task<bool> CancelPaymentLinkAsync(long orderCode, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the webhook signature from PayOS.
    /// </summary>
    bool VerifyWebhookSignature(string data, string signature);
}

public class PayOSItem
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int Price { get; set; }
}

public class PayOSCreatePaymentResult
{
    public string CheckoutUrl { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public string PaymentLinkId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class PayOSPaymentInfo
{
    public long OrderCode { get; set; }
    public int Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PaymentLinkId { get; set; }
}
