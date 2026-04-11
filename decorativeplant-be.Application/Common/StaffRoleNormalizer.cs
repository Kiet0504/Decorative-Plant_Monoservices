namespace decorativeplant_be.Application.Common;

/// <summary>
/// Canonical role strings stored in user_account.role and JWT role claims (snake_case).
/// Accepts legacy camelCase and display names from older clients.
/// </summary>
public static class StaffRoleNormalizer
{
    public static readonly string[] BranchAssignableRoles =
        ["branch_manager", "store_staff", "cultivation_staff", "fulfillment_staff"];

    public static string Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return "customer";

        var r = role.Trim().ToLowerInvariant().Replace('-', '_');

        return r switch
        {
            "super_admin" or "admin" => "admin",
            "user" or "customer" => "customer",
            "moderator" or "branchmanager" or "branch_manager" => "branch_manager",
            "editor" or "staff" or "storestaff" or "store_staff" => "store_staff",
            "cultivationstaff" or "cultivation_staff" => "cultivation_staff",
            "fulfillmentstaff" or "fulfillment_staff" => "fulfillment_staff",
            _ => r
        };
    }

    public static bool IsBranchManager(string? role) => Normalize(role) == "branch_manager";
}
