using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace decorativeplant_be.Infrastructure.Auth;

public sealed class GoogleCalendarService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public GoogleCalendarService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<GoogleCalendarSyncResult> UpsertEventsAsync(
        string accessToken,
        IReadOnlyList<GoogleCalendarEventInput> events,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var created = 0;
        var skipped = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var ev in events)
        {
            var payload = new
            {
                id = ev.EventId,
                summary = ev.Summary,
                description = ev.Description,
                start = new { dateTime = ev.StartUtc.ToString("O"), timeZone = "UTC" },
                end = new { dateTime = ev.EndUtc.ToString("O"), timeZone = "UTC" }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/calendar/v3/calendars/primary/events")
            {
                Content = JsonContent.Create(payload)
            };

            using var resp = await client.SendAsync(req, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                created++;
                continue;
            }

            // Duplicate id in calendar: treat as skipped (already synced previously).
            if (resp.StatusCode == HttpStatusCode.Conflict)
            {
                skipped++;
                continue;
            }

            failed++;
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            errors.Add($"[{(int)resp.StatusCode}] {Truncate(body, 200)}");
        }

        return new GoogleCalendarSyncResult(created, skipped, failed, errors);
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var text = value.Trim();
        return text.Length <= max ? text : text.Substring(0, max) + "...";
    }
}

public sealed record GoogleCalendarEventInput(
    string EventId,
    string Summary,
    DateTime StartUtc,
    DateTime EndUtc,
    string? Description);

public sealed record GoogleCalendarSyncResult(
    int Created,
    int Skipped,
    int Failed,
    IReadOnlyList<string> Errors);
