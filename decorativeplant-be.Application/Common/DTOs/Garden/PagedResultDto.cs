using System.Text.Json.Serialization;

namespace decorativeplant_be.Application.Common.DTOs.Garden;

/// <summary>
/// Paginated result wrapper.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
public class PagedResultDto<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
}
