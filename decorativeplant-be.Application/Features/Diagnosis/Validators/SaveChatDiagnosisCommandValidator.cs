using decorativeplant_be.Application.Features.Diagnosis.Commands;
using FluentValidation;

namespace decorativeplant_be.Application.Features.Diagnosis.Validators;

public sealed class SaveChatDiagnosisCommandValidator : AbstractValidator<SaveChatDiagnosisCommand>
{
    public SaveChatDiagnosisCommandValidator()
    {
        RuleFor(x => x.GardenPlantId).NotEmpty();
        RuleFor(x => x.AiResult).NotNull();
        RuleFor(x => x.AiResult!.Disease).NotEmpty().MaximumLength(500);
    }
}
