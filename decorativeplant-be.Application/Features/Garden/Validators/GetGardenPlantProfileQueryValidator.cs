using FluentValidation;
using decorativeplant_be.Application.Features.Garden.Queries;

namespace decorativeplant_be.Application.Features.Garden.Validators;

public class GetGardenPlantProfileQueryValidator : AbstractValidator<GetGardenPlantProfileQuery>
{
    public GetGardenPlantProfileQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PlantId).NotEmpty();
        RuleFor(x => x.RecentLogsLimit).InclusiveBetween(1, 50);
    }
}

