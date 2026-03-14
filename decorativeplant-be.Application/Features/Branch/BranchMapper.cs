// decorativeplant-be.Application/Features/Branch/BranchMapper.cs

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
        CreatedAt = b.CreatedAt,

        // ContactInfo jsonb
        ContactPhone = b.ContactInfo?.RootElement.TryGetProperty("phone", out var phone) == true
            ? phone.GetString() : null,
        ContactEmail = b.ContactInfo?.RootElement.TryGetProperty("email", out var email) == true
            ? email.GetString() : null,
        FullAddress = b.ContactInfo?.RootElement.TryGetProperty("full_address", out var address) == true
            ? address.GetString() : null,
        City = b.ContactInfo?.RootElement.TryGetProperty("city", out var city) == true
            ? city.GetString() : null,
        Lat = b.ContactInfo?.RootElement.TryGetProperty("lat", out var lat) == true && lat.TryGetDouble(out var latVal)
            ? latVal : null,
        Long = b.ContactInfo?.RootElement.TryGetProperty("long", out var lng) == true && lng.TryGetDouble(out var lngVal)
            ? lngVal : null,

        // OperatingHours (raw JsonDocument)
        OperatingHours = b.OperatingHours,

        // Settings jsonb
        SupportsOnlineOrder = b.Settings?.RootElement.TryGetProperty("supports_online_order", out var online) == true
            && online.GetBoolean(),
        SupportsPickup = b.Settings?.RootElement.TryGetProperty("supports_pickup", out var pickup) == true
            && pickup.GetBoolean(),
        SupportsShipping = b.Settings?.RootElement.TryGetProperty("supports_shipping", out var shipping) == true
            && shipping.GetBoolean()
    };

    public static StaffAssignmentDto ToDto(
        this StaffAssignment sa,
        string staffEmail,
        string branchName) => new()
    {
        Id = sa.Id,
        StaffId = sa.StaffId,
        StaffEmail = staffEmail,
        BranchId = sa.BranchId,
        BranchName = branchName,
        Position = sa.Position,
        IsPrimary = sa.IsPrimary,
        AssignedAt = sa.AssignedAt,

        // Permissions jsonb
        CanManageInventory = sa.Permissions?.RootElement.TryGetProperty("can_manage_inventory", out var inv) == true
            && inv.GetBoolean(),
        CanManageOrders = sa.Permissions?.RootElement.TryGetProperty("can_manage_orders", out var orders) == true
            && orders.GetBoolean(),
        CanManageStaff = sa.Permissions?.RootElement.TryGetProperty("can_manage_staff", out var staff) == true
            && staff.GetBoolean(),
        CanViewOtherBranches = sa.Permissions?.RootElement.TryGetProperty("can_view_other_branches", out var branches) == true
            && branches.GetBoolean()
    };
}
