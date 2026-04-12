using FluentValidation;
using decorativeplant_be.Application.Features.Garden.Queries;

namespace decorativeplant_be.Application.Features.Garden.Validators;

public sealed class GenerateGardenPlantAiCareAdviceQueryValidator : AbstractValidator<GenerateGardenPlantAiCareAdviceQuery>
{
    public GenerateGardenPlantAiCareAdviceQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PlantId).NotEmpty();
    }
}

