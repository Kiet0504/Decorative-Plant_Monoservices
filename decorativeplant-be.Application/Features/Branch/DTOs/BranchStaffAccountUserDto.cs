namespace decorativeplant_be.Application.Features.Branch.DTOs;

/// <summary>User summary returned after branch-scoped staff account create/update (camelCase via JSON options).</summary>
public sealed class BranchStaffAccountUserDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    public string Role { get; init; } = "";
    public string Status { get; init; } = "";
    public string Phone { get; init; } = "";
    public Guid? BranchId { get; init; }
    public string? BranchName { get; init; }
}
