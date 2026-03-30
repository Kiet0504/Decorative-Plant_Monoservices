using FluentValidation;
using decorativeplant_be.Application.Features.Recommendations.Queries;

namespace decorativeplant_be.Application.Features.Recommendations.Validators;

public class GetProductRecommendationsQueryValidator : AbstractValidator<GetProductRecommendationsQuery>
{
    public GetProductRecommendationsQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Limit).InclusiveBetween(1, 10);
    }
}

