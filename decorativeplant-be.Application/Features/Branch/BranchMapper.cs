using System.Text.Json;
using decorativeplant_be.Application.Features.Branch.DTOs;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Branch;

public static class BranchMapper
{
    public static BranchDto ToDto(this Domain.Entities.Branch b, string companyName) => new()
    {
        Id = b.Id,
        CompanyId = b.CompanyId,
        CompanyName = companyName,
        Code = b.Code,
        Name = b.Name,
        Slug = b.Slug,
        BranchType = b.BranchType,
        IsActive = b.IsActive,
        IsDeleted = b.IsDeleted,
        CreatedAt = b.CreatedAt,

        ContactPhone = b.ContactInfo?.RootElement.TryGetProperty("phone", out var phone) == true && phone.ValueKind == JsonValueKind.String ? phone.GetString() : null,
        ContactEmail = b.ContactInfo?.RootElement.TryGetProperty("email", out var email) == true && email.ValueKind == JsonValueKind.String ? email.GetString() : null,
        FullAddress = b.ContactInfo?.RootElement.TryGetProperty("full_address", out var address) == true && address.ValueKind == JsonValueKind.String ? address.GetString() : null,
        City = b.ContactInfo?.RootElement.TryGetProperty("city", out var city) == true && city.ValueKind == JsonValueKind.String ? city.GetString() : null,
        Lat = b.Lat,
        Long = b.Long,

        OperatingHours = b.OperatingHours,

        SupportsOnlineOrder = b.Settings?.RootElement.TryGetProperty("supports_online_order", out var online) == true && online.ValueKind == JsonValueKind.True,
        SupportsPickup = b.Settings?.RootElement.TryGetProperty("supports_pickup", out var pickup) == true && pickup.ValueKind == JsonValueKind.True,
        SupportsShipping = b.Settings?.RootElement.TryGetProperty("supports_shipping", out var shipping) == true && shipping.ValueKind == JsonValueKind.True
    };

    public static StaffAssignmentDto ToDto(
        this StaffAssignment sa,
        string staffEmail,
        string branchName,
        string? staffDisplayName = null) => new()
    {
        Id = sa.Id,
        StaffId = sa.StaffId,
        StaffEmail = staffEmail,
        StaffDisplayName = staffDisplayName,
        BranchId = sa.BranchId,
        BranchName = branchName,
        Position = sa.Position,
        IsPrimary = sa.IsPrimary,
        AssignedAt = sa.AssignedAt,

        CanManageInventory = sa.Permissions?.RootElement.TryGetProperty("can_manage_inventory", out var inv) == true && inv.ValueKind == JsonValueKind.True,
        CanManageOrders = sa.Permissions?.RootElement.TryGetProperty("can_manage_orders", out var orders) == true && orders.ValueKind == JsonValueKind.True,
        CanManageStaff = sa.Permissions?.RootElement.TryGetProperty("can_manage_staff", out var staff) == true && staff.ValueKind == JsonValueKind.True,
        CanViewOtherBranches = sa.Permissions?.RootElement.TryGetProperty("can_view_other_branches", out var branches) == true && branches.ValueKind == JsonValueKind.True
    };
}
