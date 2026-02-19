namespace decorativeplant_be.Application.Common.DTOs.Commerce;

public class CreatePromotionRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid? BranchId { get; set; }
    public string DiscountType { get; set; } = "percentage"; // percentage|fixed_amount
    public string Value { get; set; } = "0";
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool ApplyToAll { get; set; }
    public List<Guid>? TargetCategories { get; set; }
    public string? MinOrder { get; set; }
}

public class UpdatePromotionRequest
{
    public string? Name { get; set; }
    public string? DiscountType { get; set; }
    public string? Value { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool? ApplyToAll { get; set; }
    public List<Guid>? TargetCategories { get; set; }
    public string? MinOrder { get; set; }
}

public class PromotionResponse
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public Guid? BranchId { get; set; }
    public string? DiscountType { get; set; }
    public string? Value { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool ApplyToAll { get; set; }
    public List<Guid>? TargetCategories { get; set; }
    public string? MinOrder { get; set; }
}
