using decorativeplant_be.Application.Common.DTOs.RoomScan;
using MediatR;

namespace decorativeplant_be.Application.Features.RoomScan.Commands;

public sealed class RoomScanCommand : IRequest<RoomScanResultDto>
{
    public Guid UserId { get; set; }
    public RoomScanRequestDto Request { get; set; } = new();
}
