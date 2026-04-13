using FluentValidation;
using decorativeplant_be.Application.Features.Garden.Commands;

namespace decorativeplant_be.Application.Features.Garden.Validators;

public static class CareScheduleValidators
{
    public static readonly string[] ValidTypes = { "water", "fertilize", "prune", "repot", "inspect" };
    // Allow tighter watering cadences driven by AI/user profile.
    public static readonly string[] ValidFrequencies = { "daily", "every_2_3_days", "weekly", "biweekly", "monthly", "rarely" };
    public static readonly string[] ValidTimesOfDay = { "morning", "afternoon", "evening" };
}

public sealed class CreateCareScheduleCommandValidator : AbstractValidator<CreateCareScheduleCommand>
{
    public CreateCareScheduleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PlantId).NotEmpty();
        RuleFor(x => x.TaskInfo).NotNull();
        RuleFor(x => x.TaskInfo.Type)
            .NotEmpty()
            .Must(t => CareScheduleValidators.ValidTypes.Contains(t.Trim().ToLowerInvariant()))
            .WithMessage($"Task type must be one of: {string.Join(", ", CareScheduleValidators.ValidTypes)}");
        RuleFor(x => x.TaskInfo.Frequency)
            .NotEmpty()
            .Must(f => CareScheduleValidators.ValidFrequencies.Contains(f.Trim().ToLowerInvariant()))
            .WithMessage($"Frequency must be one of: {string.Join(", ", CareScheduleValidators.ValidFrequencies)}");
        RuleFor(x => x.TaskInfo.TimeOfDay)
            .Must(t => string.IsNullOrWhiteSpace(t) || CareScheduleValidators.ValidTimesOfDay.Contains(t.Trim().ToLowerInvariant()))
            .WithMessage($"TimeOfDay must be one of: {string.Join(", ", CareScheduleValidators.ValidTimesOfDay)}")
            .When(x => x.TaskInfo != null);
        RuleFor(x => x.TaskInfo.NextDue)
            .NotNull()
            .WithMessage("NextDue is required.");
    }
}

public sealed class BulkCreateCareSchedulesCommandValidator : AbstractValidator<BulkCreateCareSchedulesCommand>
{
    public BulkCreateCareSchedulesCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PlantId).NotEmpty();
        RuleFor(x => x.Tasks)
            .NotNull()
            .Must(x => x != null && x.Count > 0)
            .WithMessage("Tasks must not be empty.");
        RuleForEach(x => x.Tasks).ChildRules(t =>
        {
            t.RuleFor(x => x.Type)
                .NotEmpty()
                .Must(v => CareScheduleValidators.ValidTypes.Contains(v.Trim().ToLowerInvariant()))
                .WithMessage($"Task type must be one of: {string.Join(", ", CareScheduleValidators.ValidTypes)}");
            t.RuleFor(x => x.Frequency)
                .NotEmpty()
                .Must(v => CareScheduleValidators.ValidFrequencies.Contains(v.Trim().ToLowerInvariant()))
                .WithMessage($"Frequency must be one of: {string.Join(", ", CareScheduleValidators.ValidFrequencies)}");
            t.RuleFor(x => x.TimeOfDay)
                .Must(v => string.IsNullOrWhiteSpace(v) || CareScheduleValidators.ValidTimesOfDay.Contains(v.Trim().ToLowerInvariant()))
                .WithMessage($"TimeOfDay must be one of: {string.Join(", ", CareScheduleValidators.ValidTimesOfDay)}");
            t.RuleFor(x => x.NextDue)
                .NotNull()
                .WithMessage("NextDue is required.");
        });
    }
}

public sealed class UpdateCareScheduleCommandValidator : AbstractValidator<UpdateCareScheduleCommand>
{
    public UpdateCareScheduleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ScheduleId).NotEmpty();

        When(x => x.TaskInfo != null, () =>
        {
            RuleFor(x => x.TaskInfo!.Type)
                .NotEmpty()
                .Must(v => CareScheduleValidators.ValidTypes.Contains(v.Trim().ToLowerInvariant()))
                .WithMessage($"Task type must be one of: {string.Join(", ", CareScheduleValidators.ValidTypes)}");
            RuleFor(x => x.TaskInfo!.Frequency)
                .NotEmpty()
                .Must(v => CareScheduleValidators.ValidFrequencies.Contains(v.Trim().ToLowerInvariant()))
                .WithMessage($"Frequency must be one of: {string.Join(", ", CareScheduleValidators.ValidFrequencies)}");
            RuleFor(x => x.TaskInfo!.TimeOfDay)
                .Must(v => string.IsNullOrWhiteSpace(v) || CareScheduleValidators.ValidTimesOfDay.Contains(v.Trim().ToLowerInvariant()))
                .WithMessage($"TimeOfDay must be one of: {string.Join(", ", CareScheduleValidators.ValidTimesOfDay)}");
            RuleFor(x => x.TaskInfo!.NextDue)
                .NotNull()
                .WithMessage("NextDue is required.");
        });
    }
}

