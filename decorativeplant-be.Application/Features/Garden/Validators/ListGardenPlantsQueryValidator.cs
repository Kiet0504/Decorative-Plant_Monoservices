using FluentValidation;
using decorativeplant_be.Application.Features.Garden.Queries;

namespace decorativeplant_be.Application.Features.Garden.Validators;

public class ListGardenPlantsQueryValidator : AbstractValidator<ListGardenPlantsQuery>
{
    private static readonly string[] ValidHealth = ["healthy", "needs_attention", "struggling"];

    public ListGardenPlantsQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Page size must be between 1 and 50.");
        RuleFor(x => x.HealthFilter)
            .Must(h => string.IsNullOrEmpty(h) || ValidHealth.Contains(h!))
            .When(x => !string.IsNullOrEmpty(x.HealthFilter));
    }
}
