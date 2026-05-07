using System.Globalization;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.Revenue.Queries;

/// <summary>
/// Reads the COD override that staff may have applied via GHN's update-COD flow.
/// When present, it represents the cash the shipper actually collected — so
/// revenue reports should reflect that number, not the original Financials.Total.
/// </summary>
internal static class RevenueAdjustments
{
    public static decimal? TryReadCodOverride(JsonDocument? notes)
    {
        if (notes == null) return null;
        if (!notes.RootElement.TryGetProperty("cod_override", out var co)) return null;
        return co.ValueKind switch
        {
            JsonValueKind.Number when co.TryGetDecimal(out var n) => n,
            JsonValueKind.String when decimal.TryParse(co.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s) => s,
            _ => null,
        };
    }

    public static decimal ReadFinancialDecimal(JsonDocument? financials, string key)
    {
        if (financials == null) return 0;
        if (!financials.RootElement.TryGetProperty(key, out var p)) return 0;
        return decimal.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    /// <summary>
    /// Multiplier to apply to a per-item revenue contribution when the order has a
    /// COD override. Returns 1 (no adjustment) when no override or original total is 0.
    /// </summary>
    public static decimal CodScaleFactor(JsonDocument? notes, JsonDocument? financials)
    {
        var ov = TryReadCodOverride(notes);
        if (ov == null) return 1m;
        var total = ReadFinancialDecimal(financials, "total");
        if (total <= 0) return 1m;
        return ov.Value / total;
    }
}
