using MediatR;

namespace decorativeplant_be.Application.Features.Diagnosis.Commands;

/// <summary>
/// Command to submit user feedback on a diagnosis.
/// </summary>
public class SubmitFeedbackCommand : IRequest<Unit>
{
    public Guid UserId { get; set; }

    public Guid DiagnosisId { get; set; }

    /// <summary>helpful|not_helpful|wrong</summary>
    public string UserFeedback { get; set; } = string.Empty;

    public string? ExpertNotes { get; set; }
}
