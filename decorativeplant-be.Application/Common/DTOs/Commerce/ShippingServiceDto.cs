namespace decorativeplant_be.Application.Common.DTOs.Commerce;

// ── Shipping Fee ──

public class ShippingFeeRequest
{
    public int FromDistrictId { get; set; }
    public string FromWardCode { get; set; } = string.Empty;
    public int ToDistrictId { get; set; }
    public string ToWardCode { get; set; } = string.Empty;
    public int Weight { get; set; } // grams
    public int InsuranceValue { get; set; } // VND
    public int ServiceTypeId { get; set; } = 2; // 2 = E-Commerce Delivery
}

public class ShippingFeeResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Total { get; set; }
    public int ServiceFee { get; set; }
    public int InsuranceFee { get; set; }
}

// ── Create Order ──

public class ShippingOrderRequest
{
    public string ToName { get; set; } = string.Empty;
    public string ToPhone { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public string ToWardCode { get; set; } = string.Empty;
    public int ToDistrictId { get; set; }
    public int FromDistrictId { get; set; }
    public string FromWardCode { get; set; } = string.Empty;
    public int Weight { get; set; } // grams
    public int Length { get; set; } = 20; // cm
    public int Width { get; set; } = 20;
    public int Height { get; set; } = 20;
    public int InsuranceValue { get; set; }
    public int ServiceTypeId { get; set; } = 2;
    public int PaymentTypeId { get; set; } = 1; // 1 = seller pays, 2 = buyer pays
    public int CodAmount { get; set; } // VND that shipper must collect on delivery (0 = no COD)
    public int CodFailedAmount { get; set; } // VND shipper keeps on failed COD (typically 0)
    public string RequiredNote { get; set; } = "KHONGCHOXEMHANG";
    public string? Note { get; set; }
    public string? ClientOrderCode { get; set; }
    public List<ShippingOrderItem> Items { get; set; } = new();
}

public class ShippingOrderItem
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int Weight { get; set; } // grams
}

public class ShippingOrderResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? OrderCode { get; set; }
    public string? ExpectedDeliveryTime { get; set; }
    public int TotalFee { get; set; }
}

// ── GHN Location Master Data ──

public record GhnProvince(int ProvinceId, string ProvinceName);
public record GhnDistrict(int DistrictId, string DistrictName, int ProvinceId);
public record GhnWard(string WardCode, string WardName, int DistrictId);
