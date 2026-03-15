// decorativeplant-be.Application/Features/Branch/Handlers/DeactivateBranchCommandHandler.cs

using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Branch.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Branch.Handlers;

public class DeactivateBranchCommandHandler : IRequestHandler<DeactivateBranchCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public DeactivateBranchCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(DeactivateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (branch == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Branch), request.Id);
        }

        branch.IsActive = false;
        branch.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
