using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Net.payOS;
using Net.payOS.Types;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.Infrastructure.Services;

public class PayOSService : IPayOSService
{
    private readonly PayOS _payOS;
    private readonly string _checksumKey;
    private readonly ILogger<PayOSService> _logger;

    public PayOSService(IConfiguration configuration, ILogger<PayOSService> logger)
    {
        _logger = logger;
        var clientId = configuration["PayOS:ClientId"] ?? throw new ArgumentNullException("PayOS:ClientId");
        var apiKey = configuration["PayOS:ApiKey"] ?? throw new ArgumentNullException("PayOS:ApiKey");
        _checksumKey = configuration["PayOS:ChecksumKey"] ?? throw new ArgumentNullException("PayOS:ChecksumKey");

        _payOS = new PayOS(clientId, apiKey, _checksumKey);
    }

    public async Task<PayOSCreatePaymentResult> CreatePaymentLinkAsync(
        long orderCode, int amount, string description,
        List<PayOSItem> items, string returnUrl, string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        var payOSItems = items.Select(i => new ItemData(i.Name, i.Quantity, i.Price)).ToList();

        var paymentData = new PaymentData(
            orderCode: orderCode,
            amount: amount,
            description: description,
            items: payOSItems,
            cancelUrl: cancelUrl,
            returnUrl: returnUrl
        );

        var result = await _payOS.createPaymentLink(paymentData);

        return new PayOSCreatePaymentResult
        {
            CheckoutUrl = result.checkoutUrl,
            QrCode = result.qrCode,
            PaymentLinkId = result.paymentLinkId,
            Status = result.status
        };
    }

    public async Task<PayOSPaymentInfo?> GetPaymentInfoAsync(long orderCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _payOS.getPaymentLinkInformation(orderCode);
            return new PayOSPaymentInfo
            {
                OrderCode = result.orderCode,
                Amount = result.amount,
                Status = result.status,
                PaymentLinkId = result.id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get PayOS payment info for order {OrderCode}", orderCode);
            return null;
        }
    }

    public async Task<bool> CancelPaymentLinkAsync(long orderCode, string? reason = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await _payOS.cancelPaymentLink(orderCode, reason);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel PayOS payment link for order {OrderCode}", orderCode);
            return false;
        }
    }

    public bool VerifyWebhookSignature(string data, string signature)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(_checksumKey));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        var computed = BitConverter.ToString(hash).Replace("-", "").ToLower();
        return computed == signature.ToLower();
    }
}
