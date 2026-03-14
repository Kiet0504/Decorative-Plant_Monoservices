// decorativeplant-be.Application/Features/Company/Queries/GetAllCompaniesQuery.cs

using decorativeplant_be.Application.Features.Company.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Company.Queries;

public record GetAllCompaniesQuery : IRequest<List<CompanyDto>>;
