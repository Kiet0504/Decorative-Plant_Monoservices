namespace decorativeplant_be.Application.Common.DTOs.Commerce;

public class GhtkFeeRequest
{
    public string PickProvince { get; set; } = string.Empty;
    public string PickDistrict { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Weight { get; set; } // in grams
    public int Value { get; set; } // VND order value
}

public class GhtkFeeResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public GhtkFeeDetail? Fee { get; set; }
}

public class GhtkFeeDetail
{
    public string Name { get; set; } = string.Empty;
    public int Fee { get; set; }
    public int InsuranceFee { get; set; }
    public string DeliveryType { get; set; } = string.Empty;
    public string A { get; set; } = string.Empty;
    public string Dt { get; set; } = string.Empty;
    public int Delivery { get; set; }
}

public class GhtkOrderRequest
{
    public List<GhtkProduct> Products { get; set; } = new();
    public GhtkOrderInfo Order { get; set; } = new();
}

public class GhtkProduct
{
    public string Name { get; set; } = string.Empty;
    public double Weight { get; set; } // in kg
    public int Quantity { get; set; }
}

public class GhtkOrderInfo
{
    public string Id { get; set; } = string.Empty;
    public string PickName { get; set; } = string.Empty;
    public string PickAddress { get; set; } = string.Empty;
    public string PickProvince { get; set; } = string.Empty;
    public string PickDistrict { get; set; } = string.Empty;
    public string PickTel { get; set; } = string.Empty;
    public string Tel { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public int IsFreeship { get; set; } = 1; // 1 = shop pays, 0 = customer pays
    public string PickMoney { get; set; } = "0"; // COD
    public string Note { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Transport { get; set; } = "road";
}

public class GhtkOrderResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public GhtkOrderResponseData? Order { get; set; }
}

public class GhtkOrderResponseData
{
    public string Label { get; set; } = string.Empty;
    public string PartnerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public long Created { get; set; }
    public long Modified { get; set; }
    public string Message { get; set; } = string.Empty;
    public int EstimatedPickTime { get; set; }
    public int EstimatedDeliverTime { get; set; }
}
