namespace decorativeplant_be.Application.Common.Interfaces;

/// <summary>
/// GHTK carrier abstraction. Kept separate from <see cref="IShippingService"/> (GHN) because
/// GHTK uses plain-text addresses (no province/district/ward master data) and a different
/// status code set. Docs: https://api.ghtk.vn/docs/submit-order/logistic-overview
/// </summary>
public interface IGhtkShippingService
{
    Task<GhtkFeeResponse> CalculateFeeAsync(GhtkFeeRequest request, CancellationToken ct = default);
    Task<GhtkCreateOrderResponse> CreateOrderAsync(GhtkCreateOrderRequest request, CancellationToken ct = default);
    Task<GhtkTrackingResponse?> TrackOrderAsync(string trackingCode, CancellationToken ct = default);
    Task<bool> CancelOrderAsync(string trackingCode, CancellationToken ct = default);
}

public class GhtkFeeRequest
{
    public string PickProvince { get; set; } = string.Empty;
    public string PickDistrict { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public int Weight { get; set; }      // grams
    public int Value { get; set; }       // VND (insurance)
    public string? DeliverOption { get; set; } // xteam | none
    public string? Transport { get; set; }     // road | fly
}

public class GhtkFeeResponse
{
    public bool Success { get; set; }
    public int Fee { get; set; }
    public int InsuranceFee { get; set; }
    public string? Message { get; set; }
    public bool DeliverRequired { get; set; } // true = xteam only
}

/// <summary>
/// Payload for <c>POST /services/shipment/order</c>. Mirrors the structure documented in the GHTK
/// "Submit Order - Express" page (products + order wrapper).
/// </summary>
public class GhtkCreateOrderRequest
{
    public string ClientOrderId { get; set; } = string.Empty; // our internal order code / unique id
    public string Name { get; set; } = string.Empty;          // recipient
    public string Tel { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string? Ward { get; set; }
    public string? Street { get; set; }
    public string? Hamlet { get; set; } = "Khác";

    // Pickup (defaults to config when blank)
    public string? PickName { get; set; }
    public string? PickTel { get; set; }
    public string? PickAddress { get; set; }
    public string? PickProvince { get; set; }
    public string? PickDistrict { get; set; }
    public string? PickWard { get; set; }
    public string? PickStreet { get; set; }

    public int PickMoney { get; set; } // COD amount, 0 for prepaid
    public int Value { get; set; }     // insurance value
    public string? Note { get; set; }
    public string? Transport { get; set; } // road | fly
    public string? DeliverOption { get; set; } // xteam | none
    public string? Pick_Option { get; set; } = "cod"; // cod | post

    public List<GhtkProductItem> Products { get; set; } = new();
}

public class GhtkProductItem
{
    public string Name { get; set; } = string.Empty;
    public int Weight { get; set; }   // grams (per unit or total — GHTK accepts per-line)
    public int Quantity { get; set; } = 1;
    public string? ProductCode { get; set; }
    public int Price { get; set; }    // per-unit VND
}

public class GhtkCreateOrderResponse
{
    public bool Success { get; set; }
    public string? TrackingId { get; set; }   // GHTK label_id / tracking code
    public string? Label { get; set; }
    public int Fee { get; set; }
    public int InsuranceFee { get; set; }
    public string? EstimatedPickTime { get; set; }
    public string? EstimatedDeliverTime { get; set; }
    public string? Message { get; set; }
}

public class GhtkTrackingResponse
{
    public string? TrackingId { get; set; }
    public string? LabelId { get; set; }
    public int StatusId { get; set; }         // GHTK numeric status
    public string? StatusText { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset? Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
    public DateTimeOffset? PickDate { get; set; }
    public DateTimeOffset? DeliverDate { get; set; }
}
