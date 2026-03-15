// decorativeplant-be.Application/Features/Company/Commands/CreateCompanyCommand.cs

using decorativeplant_be.Application.Features.Company.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Company.Commands;

public record CreateCompanyCommand : IRequest<CompanyDto>
{
    public string Name { get; init; } = string.Empty;
    public string? TaxCode { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? LogoUrl { get; init; }
    public string? Website { get; init; }
    public string? Description { get; init; }
    public string? Address { get; init; }
}
