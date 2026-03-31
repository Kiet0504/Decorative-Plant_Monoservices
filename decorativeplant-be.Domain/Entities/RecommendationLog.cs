using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class RecommendationLog
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Strategy { get; set; } = "rule";

    public JsonDocument? RequestJson { get; set; }

    public JsonDocument? ResponseJson { get; set; }

    public JsonDocument? SeedSignalsJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

