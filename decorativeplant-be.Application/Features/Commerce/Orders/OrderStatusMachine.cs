using System.Text.Json;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.Orders;

/// <summary>
/// Shopee-style order state machine. Enforces forward-only transitions and
/// appends an audit trail to <see cref="OrderHeader.Notes"/>.status_history.
/// </summary>
public static class OrderStatusMachine
{
    public const string Pending    = "pending";
    public const string Confirmed  = "confirmed";
    public const string Processing = "processing";
    public const string Shipping   = "shipping";
    public const string Shipped    = "shipped";   // legacy alias of shipping
    public const string Delivered  = "delivered";
    public const string Completed  = "completed";
    public const string Cancelled  = "cancelled";
    public const string Returned   = "returned";
    public const string Expired    = "expired";

    /// <summary>
    /// Canonical forward progression index. -1 = outside main flow.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> Step = new Dictionary<string, int>
    {
        [Pending]    = 0,
        [Confirmed]  = 1,
        [Processing] = 1,
        [Shipping]   = 2,
        [Shipped]    = 2,
        [Delivered]  = 3,
        [Completed]  = 4,
        [Cancelled]  = -1,
        [Returned]   = -1,
        [Expired]    = -1,
    };

    private static readonly HashSet<string> Terminal = new(StringComparer.OrdinalIgnoreCase)
    {
        Completed, Cancelled, Returned, Expired
    };

    public static bool IsTerminal(string? status) =>
        status != null && Terminal.Contains(status);

    /// <summary>
    /// Returns true if <paramref name="from"/> can transition to <paramref name="to"/>.
    /// Rules:
    ///   - No transition out of a terminal state.
    ///   - Same state is a no-op (allowed).
    ///   - Any active state may jump to cancelled/expired/returned (terminal side-exits).
    ///   - Otherwise must move strictly forward in Step ordering.
    ///   - Cancellation only allowed before shipping (step &lt; 2).
    /// </summary>
    public static bool CanTransition(string? from, string to)
    {
        var src = (from ?? Pending).ToLowerInvariant();
        var dst = to.ToLowerInvariant();

        if (IsTerminal(src)) return false;
        if (src == dst) return true;

        if (dst == Cancelled)
        {
            // Shopee: cancel only before the seller hands to carrier.
            return Step.TryGetValue(src, out var s) && s < 2;
        }
        if (dst == Expired)  return src == Pending;
        if (dst == Returned) return src is Delivered or Completed;

        if (!Step.TryGetValue(src, out var srcStep) || srcStep < 0) return false;
        if (!Step.TryGetValue(dst, out var dstStep) || dstStep < 0) return false;

        return dstStep > srcStep;
    }

    public static void EnsureCanTransition(string? from, string to)
    {
        if (!CanTransition(from, to))
            throw new BadRequestException(
                $"Illegal status transition: '{from ?? "null"}' → '{to}'.");
    }

    /// <summary>
    /// GHN/PayOS are external sources of truth. They may legally push state
    /// backward (e.g. shipping → returned) that an internal actor could not.
    /// Applies without forward-only validation, but still refuses overriding
    /// a terminal state except returned→completed which is disallowed anyway.
    /// </summary>
    public static void ApplyFromExternalSource(
        OrderHeader order,
        string toStatus,
        string source,
        string? reason = null)
    {
        var to = toStatus.ToLowerInvariant();
        var from = order.Status;
        if (IsTerminal(from) && !string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            return; // do not resurrect a closed order

        order.Status = to;
        if (to == Confirmed && order.ConfirmedAt == null)
            order.ConfirmedAt = DateTime.UtcNow;
        AppendHistory(order, from, to, changedBy: null, reason, source);
    }

    /// <summary>
    /// Applies a validated status change and appends an audit entry to
    /// <c>notes.status_history[]</c>. Call this instead of setting Status directly.
    /// </summary>
    public static void Apply(
        OrderHeader order,
        string toStatus,
        Guid? changedBy,
        string? reason = null,
        string? source = null)
    {
        var from = order.Status;
        var to = toStatus.ToLowerInvariant();
        EnsureCanTransition(from, to);

        order.Status = to;
        if (to == Confirmed && order.ConfirmedAt == null)
            order.ConfirmedAt = DateTime.UtcNow;

        AppendHistory(order, from, to, changedBy, reason, source);
    }

    /// <summary>
    /// Appends a history entry without validating the transition. Use only for
    /// bootstrapping (order creation) where there is no prior state to check.
    /// </summary>
    public static void AppendHistory(
        OrderHeader order,
        string? from,
        string to,
        Guid? changedBy,
        string? reason,
        string? source)
    {
        var notes = ReadNotes(order);
        var history = notes.TryGetValue("status_history", out var existing) && existing is List<object?> list
            ? list
            : new List<object?>();

        history.Add(new
        {
            from,
            to,
            at = DateTime.UtcNow.ToString("o"),
            by = changedBy?.ToString(),
            reason,
            source,
        });
        notes["status_history"] = history;
        order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes));
    }

    /// <summary>
    /// Reads the Notes JSON into a mutable dictionary. history arrays are
    /// hydrated into List&lt;object?&gt; so they can be appended to.
    /// </summary>
    private static Dictionary<string, object?> ReadNotes(OrderHeader order)
    {
        var result = new Dictionary<string, object?>();
        if (order.Notes == null) return result;

        foreach (var p in order.Notes.RootElement.EnumerateObject())
        {
            if (p.Name == "status_history" && p.Value.ValueKind == JsonValueKind.Array)
            {
                var items = new List<object?>();
                foreach (var el in p.Value.EnumerateArray())
                    items.Add(JsonSerializer.Deserialize<object?>(el.GetRawText()));
                result[p.Name] = items;
            }
            else
            {
                result[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Number => p.Value.TryGetInt64(out var l) ? l : p.Value.GetDouble(),
                    JsonValueKind.True   => true,
                    JsonValueKind.False  => false,
                    JsonValueKind.Null   => null,
                    _                    => JsonSerializer.Deserialize<object?>(p.Value.GetRawText()),
                };
            }
        }
        return result;
    }
}
