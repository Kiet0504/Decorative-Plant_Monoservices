namespace decorativeplant_be.Application.Common.DTOs.Commerce;

public class CreateShippingRequest
{
    public Guid OrderId { get; set; }
    public string Carrier { get; set; } = "ghn"; // ghn|ghtk|viettel_post
    public string Method { get; set; } = "standard"; // standard|express
    public string Fee { get; set; } = "0";
}

public class UpdateShippingStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Description { get; set; }
}

public class ShippingResponse
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public string? TrackingCode { get; set; }
    public string? Carrier { get; set; }
    public string? Method { get; set; }
    public string? Fee { get; set; }
    public string? Status { get; set; }
    public string? EstimatedDate { get; set; }
    public string? ActualDate { get; set; }
    public List<ShippingEventDto> Events { get; set; } = new();
}

public class ShippingEventDto
{
    public string? Status { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? EventTime { get; set; }
}

// ── ShippingZone DTOs ──
public class CreateShippingZoneRequest
{
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Cities { get; set; } = new();
    public List<string> Districts { get; set; } = new();
    public string BaseFee { get; set; } = "0";
    public string? FeePerKm { get; set; }
    public string? FreeThreshold { get; set; }
    public int MinDays { get; set; } = 1;
    public int MaxDays { get; set; } = 3;
}

public class UpdateShippingZoneRequest
{
    public string? Name { get; set; }
    public List<string>? Cities { get; set; }
    public List<string>? Districts { get; set; }
    public string? BaseFee { get; set; }
    public string? FeePerKm { get; set; }
    public string? FreeThreshold { get; set; }
    public int? MinDays { get; set; }
    public int? MaxDays { get; set; }
}

public class ShippingZoneResponse
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public string? Name { get; set; }
    public List<string> Cities { get; set; } = new();
    public List<string> Districts { get; set; } = new();
    public string BaseFee { get; set; } = "0";
    public string? FeePerKm { get; set; }
    public string? FreeThreshold { get; set; }
    public int MinDays { get; set; }
    public int MaxDays { get; set; }
}
