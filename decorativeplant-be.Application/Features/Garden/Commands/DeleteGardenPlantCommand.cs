using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Commands;

/// <summary>
/// Command to delete (archive or permanent) a garden plant.
/// </summary>
public class DeleteGardenPlantCommand : IRequest<Unit>
{
    public Guid UserId { get; set; }

    public Guid Id { get; set; }

    /// <summary>If true, hard delete. If false, soft delete (archive).</summary>
    public bool Permanent { get; set; }
}
