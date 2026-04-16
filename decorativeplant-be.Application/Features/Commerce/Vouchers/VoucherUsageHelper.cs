using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.Interfaces;

namespace decorativeplant_be.Application.Features.Commerce.Vouchers;

/// <summary>
/// Reverses the <c>used_count</c> increment that <see cref="Orders.Handlers.CreateOrderHandler"/>
/// applied when the order claimed a voucher. Call this from any terminal path that invalidates
/// the sale: order cancellation, pending-order expiration, and return-approved.
///
/// Caller is expected to be inside an active transaction — we take a FOR UPDATE row lock
/// before mutating the JSONB counter so parallel rollbacks don't both decrement.
/// </summary>
public static class VoucherUsageHelper
{
    public static async Task RollbackUsageAsync(
        IApplicationDbContext context,
        Guid voucherId,
        CancellationToken ct = default)
    {
        await context.AcquireVoucherLockAsync(voucherId, ct);

        var voucher = await context.Vouchers.FirstOrDefaultAsync(v => v.Id == voucherId, ct);
        if (voucher?.Rules == null) return;

        var rules = new Dictionary<string, object?>();
        foreach (var p in voucher.Rules.RootElement.EnumerateObject())
        {
            rules[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString(),
                JsonValueKind.Number => p.Value.TryGetInt64(out var l) ? l : p.Value.GetDouble(),
                JsonValueKind.True   => true,
                JsonValueKind.False  => false,
                JsonValueKind.Null   => null,
                _                    => JsonSerializer.Deserialize<object?>(p.Value.GetRawText()),
            };
        }

        int used = 0;
        if (rules.TryGetValue("used_count", out var uc) && uc is not null)
        {
            switch (uc)
            {
                case long l:   used = (int)l; break;
                case int i:    used = i; break;
                case double d: used = (int)d; break;
                case string s when int.TryParse(s, out var parsed): used = parsed; break;
            }
        }
        if (used <= 0) return;

        rules["used_count"] = used - 1;
        voucher.Rules = JsonDocument.Parse(JsonSerializer.Serialize(rules));
    }
}
