using decorativeplant_be.Application.Features.RoomScan.Commands;
using FluentValidation;

namespace decorativeplant_be.Application.Features.RoomScan.Validators;

public sealed class RoomScanCommandValidator : AbstractValidator<RoomScanCommand>
{
    private const int MaxBase64Length = 18_000_000;

    public RoomScanCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Request.ImageBase64)
            .NotEmpty()
            .MaximumLength(MaxBase64Length)
            .WithMessage("Image is too large. Use a smaller photo.");
    }
}
