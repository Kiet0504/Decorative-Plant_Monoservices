namespace decorativeplant_be.Application.Features.Branch.Commands;

/// <summary>Request body for POST /api/branches/{branchId}/staff-accounts (create, transfer, or update by email).</summary>
public sealed class UpsertBranchStaffAccountBody
{
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string Role { get; set; } = "";
    /// <summary>Optional. Random password is generated for new accounts when omitted.</summary>
    public string? Password { get; set; }
}
