using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// AI training feedback. JSONB: source_info, feedback_content. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class AiTrainingFeedback
{
    public Guid Id { get; set; }
    public JsonDocument? SourceInfo { get; set; }
    public JsonDocument? FeedbackContent { get; set; }
    public Guid? UserId { get; set; }
    public DateTime? CreatedAt { get; set; }

    public UserAccount? User { get; set; }
}
