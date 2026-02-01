using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class AiTrainingFeedback : BaseEntity
{
    public JsonNode? SourceInfo { get; set; }
    public JsonNode? FeedbackContent { get; set; }
    
    public Guid? UserId { get; set; }
    public UserAccount? User { get; set; }
}
