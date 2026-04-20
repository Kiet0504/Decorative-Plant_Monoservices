namespace decorativeplant_be.Application.Common.DTOs.Commerce;

public class CreateProductReviewRequest
{
    public Guid ListingId { get; set; }
    public Guid? OrderId { get; set; }
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string? Comment { get; set; }
    public List<ReviewImageDto>? Images { get; set; }
}

public class UpdateReviewStatusRequest
{
    public string Status { get; set; } = string.Empty; // published|pending|hidden
}

public class ProductReviewResponse
{
    public Guid Id { get; set; }
    public Guid? ListingId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrderId { get; set; }
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string? Comment { get; set; }
    public bool IsVerified { get; set; }
    public int HelpfulCount { get; set; }
    public string Status { get; set; } = "pending";
    public List<ReviewImageDto> Images { get; set; } = new();
    public DateTime? CreatedAt { get; set; }
    public string? UserName { get; set; }
    public string? ProductName { get; set; }
}

public class ReviewImageDto
{
    public string Url { get; set; } = string.Empty;
    public string? Alt { get; set; }
    public int Sort { get; set; }
}
