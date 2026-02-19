using FluentValidation;
using decorativeplant_be.Application.Features.Commerce.ProductReviews.Commands;

namespace decorativeplant_be.Application.Features.Commerce.ProductReviews.Validators;

public class CreateProductReviewCommandValidator : AbstractValidator<CreateProductReviewCommand>
{
    public CreateProductReviewCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Request.ListingId).NotEmpty();
        
        RuleFor(x => x.Request.Rating)
            .InclusiveBetween(1, 5).WithMessage("Rating must be between 1 and 5.");

        RuleFor(x => x.Request.Title)
            .MaximumLength(100).WithMessage("Title cannot exceed 100 characters.")
            .When(x => x.Request.Title != null);

        RuleFor(x => x.Request.Comment)
            .MaximumLength(1000).WithMessage("Comment cannot exceed 1000 characters.")
            .When(x => x.Request.Comment != null);
    }
}

public class UpdateReviewStatusCommandValidator : AbstractValidator<UpdateReviewStatusCommand>
{
    public UpdateReviewStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.Status)
            .NotEmpty()
            .Must(s => s == "published" || s == "pending" || s == "hidden")
            .WithMessage("Invalid review status.");
    }
}
