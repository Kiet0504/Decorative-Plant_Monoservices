using System.Text.Json;
using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.HealthCheck;

public static class HealthIncidentMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public static HealthIncidentDto ToDto(HealthIncident entity)
    {
        object? treatment = null;
        if (entity.TreatmentInfo != null)
        {
            try { treatment = JsonSerializer.Deserialize<object>(entity.TreatmentInfo.RootElement.GetRawText(), JsonOptions); } catch {}
        }
        
        object? evidence = null;
        string? imageUrl = null;
        if (entity.Images != null)
        {
            try 
            { 
                evidence = JsonSerializer.Deserialize<object>(entity.Images.RootElement.GetRawText(), JsonOptions);
                if (entity.Images.RootElement.TryGetProperty("urls", out var urls) && urls.ValueKind == JsonValueKind.Array && urls.GetArrayLength() > 0)
                {
                    imageUrl = urls[0].GetString();
                }
                else if (entity.Images.RootElement.ValueKind == JsonValueKind.Array && entity.Images.RootElement.GetArrayLength() > 0)
                {
                    imageUrl = entity.Images.RootElement[0].GetString();
                }
            } 
            catch {}
        }

        string status = entity.StatusInfo?.RootElement.TryGetProperty("status", out var s) == true ? s.GetString() ?? "Unknown" : "Unknown";
        DateTime? reportedAt = null;
        if (entity.StatusInfo?.RootElement.TryGetProperty("reported_at", out var ra) == true && ra.TryGetDateTime(out var dt)) reportedAt = dt;
        
        DateTime? resolvedAt = null;
        if (entity.StatusInfo?.RootElement.TryGetProperty("resolved_at", out var res) == true && res.TryGetDateTime(out var dres)) resolvedAt = dres;

        return new HealthIncidentDto
        {
            Id = entity.Id,
            BatchId = entity.BatchId,
            BatchCode = entity.Batch?.BatchCode,
            IncidentType = entity.IncidentType ?? "Unknown",
            Severity = entity.Severity ?? "Unknown",
            Status = status,
            Description = entity.Description,
            TreatmentDetails = treatment,
            EvidenceImages = evidence,
            ImageUrl = imageUrl,
            ReportedAt = reportedAt,
            // ReportedBy and ResolvedBy fields might be in JSON or navigation, checking entity definition...
            // Entity definition doesn't have ReportedBy/ResolvedBy columns. They should be in JSON StatusInfo or audit logs.
            // For now, mapping from JSON if present.
             ReportedBy = entity.StatusInfo?.RootElement.TryGetProperty("reported_by", out var rb) == true && Guid.TryParse(rb.GetString(), out var rbg) ? rbg : null,
             ResolvedBy = entity.StatusInfo?.RootElement.TryGetProperty("resolved_by", out var rsb) == true && Guid.TryParse(rsb.GetString(), out var rsbg) ? rsbg : null,
             BranchId = entity.Batch?.BranchId,
             BranchName = entity.Batch?.Branch?.Name
        };
    }

    public static JsonDocument? BuildJson(object? data)
    {
        if (data == null) return null;
        return JsonSerializer.SerializeToDocument(data, JsonOptions);
    }
}
