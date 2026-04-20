using decorativeplant_be.Application.Features.Branch.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Branch.Commands;

public record UpsertBranchStaffAccountCommand : IRequest<BranchStaffAccountUserDto>
{
    public string Email { get; init; } = "";
    public string FullName { get; init; } = "";
    public string? Phone { get; init; }
    public string Role { get; init; } = "";
    public string? Password { get; init; }
    public Guid BranchId { get; init; }

    public string CurrentUserRole { get; init; } = string.Empty;
    public Guid? CurrentUserBranchId { get; init; }
}
