namespace decorativeplant_be.Application.Common.DTOs.Commerce;

public class CreateVoucherRequest
{
    public string Code { get; set; } = string.Empty;
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ValidFrom { get; set; }
    public string? ValidTo { get; set; }
    // Rules
    public string Type { get; set; } = "percentage"; // percentage|fixed_amount|free_shipping
    public string Value { get; set; } = "0";
    public string? MinOrder { get; set; }
    public int? UsageLimits { get; set; }
    public List<Guid>? ApplicableProducts { get; set; }
}

public class UpdateVoucherRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ValidFrom { get; set; }
    public string? ValidTo { get; set; }
    public string? Type { get; set; }
    public string? Value { get; set; }
    public string? MinOrder { get; set; }
    public int? UsageLimits { get; set; }
    public List<Guid>? ApplicableProducts { get; set; }
    public bool? IsActive { get; set; }
}

public class VoucherResponse
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public Guid? BranchId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ValidFrom { get; set; }
    public string? ValidTo { get; set; }
    public string? Type { get; set; }
    public string? Value { get; set; }
    public string? MinOrder { get; set; }
    public int? UsageLimits { get; set; }
    public bool IsActive { get; set; }
}

public class ValidateVoucherResponse
{
    public bool IsValid { get; set; }
    public string? Message { get; set; }
    public VoucherResponse? Voucher { get; set; }
}
