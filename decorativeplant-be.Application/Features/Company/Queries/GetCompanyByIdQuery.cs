// decorativeplant-be.Application/Features/Company/Queries/GetCompanyByIdQuery.cs

using decorativeplant_be.Application.Features.Company.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Company.Queries;

public record GetCompanyByIdQuery : IRequest<CompanyDto>
{
    public Guid Id { get; init; }
}
