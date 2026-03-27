using FluentValidation;
using decorativeplant_be.Application.Features.Garden.Queries;

namespace decorativeplant_be.Application.Features.Garden.Validators;

public class GetGrowthGalleryQueryValidator : AbstractValidator<GetGrowthGalleryQuery>
{
    public GetGrowthGalleryQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PlantId).NotEmpty();
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
    }
}

