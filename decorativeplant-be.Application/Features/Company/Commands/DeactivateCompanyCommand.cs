// decorativeplant-be.Application/Features/Company/Commands/DeactivateCompanyCommand.cs

using MediatR;

namespace decorativeplant_be.Application.Features.Company.Commands;

public record DeactivateCompanyCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}
