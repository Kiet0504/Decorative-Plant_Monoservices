using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Net.payOS;
using Net.payOS.Types;
using decorativeplant_be.Application.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
            returnUrl: returnUrl,
            expiredAt: (int)DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds()
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

    /// <summary>
    /// Verify webhook signature using raw JSON body.
    /// This avoids any DTO mapping issues with null vs empty string.
    /// PayOS algorithm: sort data object keys alphabetically, build "key=value&key=value" string,
    /// HMAC-SHA256 hash it, compare with provided signature.
    /// </summary>
    public bool VerifyWebhookSignature(string rawJsonBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJsonBody);
            var root = doc.RootElement;

            // Get the signature from root
            if (!root.TryGetProperty("signature", out var sigElement))
            {
                _logger.LogWarning("Webhook body missing 'signature' field");
                return false;
            }
            var providedSignature = sigElement.GetString() ?? "";

            // Get the data object from root
            if (!root.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning("Webhook body missing 'data' object");
                return false;
            }

            // Build sorted key=value pairs from data
            // IMPORTANT: PayOS SDK includes null values as empty strings, NOT skip them
            var sortedData = new SortedDictionary<string, string>();
            foreach (var prop in dataElement.EnumerateObject())
            {
                string value;
                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.Null:
                        value = ""; // PayOS treats null as empty string in signature
                        break;
                    case JsonValueKind.String:
                        value = prop.Value.GetString() ?? "";
                        break;
                    case JsonValueKind.Number:
                        // Use raw text to preserve exact number format
                        value = prop.Value.GetRawText();
                        break;
                    case JsonValueKind.True:
                        value = "true";
                        break;
                    case JsonValueKind.False:
                        value = "false";
                        break;
                    default:
                        value = prop.Value.GetRawText();
                        break;
                }
                sortedData[prop.Name] = value;
            }

            var dataString = string.Join("&", sortedData.Select(kvp => $"{kvp.Key}={kvp.Value}"));

            _logger.LogInformation("PayOS webhook signature data string: {DataString}", dataString);

            // Compute HMAC-SHA256
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_checksumKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataString));
            var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();

            var isValid = computedSignature == providedSignature.ToLower();

            if (!isValid)
            {
                _logger.LogWarning(
                    "PayOS webhook signature mismatch. Computed: {Computed}, Provided: {Provided}",
                    computedSignature, providedSignature);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify PayOS webhook signature");
            return false;
        }
    }
}
