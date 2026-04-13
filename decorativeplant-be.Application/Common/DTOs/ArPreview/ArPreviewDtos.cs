using System.Text.Json;

namespace decorativeplant_be.Application.Common.DTOs.ArPreview;

public class CreateArPreviewSessionRequest
{
    public JsonDocument Scan { get; set; } = JsonDocument.Parse("{}");

    /// <summary>
    /// Optional: which product user is previewing right now.
    /// </summary>
    public Guid? ProductListingId { get; set; }

    /// <summary>
    /// Optional: if client already selected a plane/anchor to place the model.
    /// </summary>
    public JsonDocument? Placement { get; set; }
}

public class ArPreviewSessionResponse
{
    public Guid SessionId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public JsonDocument Scan { get; set; } = JsonDocument.Parse("{}");
    public string ViewerToken { get; set; } = string.Empty;
}

