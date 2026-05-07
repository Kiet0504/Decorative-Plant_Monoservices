namespace decorativeplant_be.Application.Common.DTOs.Commerce;

public class CreateReturnRequestRequest
{
    public Guid OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<ReturnImageDto>? Images { get; set; }
}

public class UpdateReturnStatusRequest
{
    /// <summary>pending|approved|rejected|refunded</summary>
    public string Status { get; set; } = string.Empty;
    public string? ResolutionNote { get; set; }
    public bool CreateGhnReturnOrder { get; set; }
    /// <summary>Required when Status = "refunded". URLs of bill/transfer proof images.</summary>
    public List<string>? EvidenceImageUrls { get; set; }
}

public class ReturnRequestResponse
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public string? OrderCode { get; set; }
    public Guid? UserId { get; set; }
    public string Status { get; set; } = "pending";
    public string? Reason { get; set; }
    public string? Description { get; set; }
    public string? ResolutionNote { get; set; }
    public string? ReturnShippingCode { get; set; }
    public List<ReturnImageDto> Images { get; set; } = new();
    public List<string> RefundEvidenceImageUrls { get; set; } = new();
    public DateTime? CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class ReturnImageDto
{
    public string Url { get; set; } = string.Empty;
    public string? Alt { get; set; }
    public int Sort { get; set; }
}
