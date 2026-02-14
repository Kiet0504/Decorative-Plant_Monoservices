namespace decorativeplant_be.Application.Common.DTOs.Garden;

/// <summary>
/// Paginated result wrapper.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}
