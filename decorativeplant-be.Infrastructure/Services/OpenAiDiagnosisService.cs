using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Services;

public class OpenAiDiagnosisService : IAiDiagnosisService
{
    private readonly AiDiagnosisSettings _settings;
    private readonly ILogger<OpenAiDiagnosisService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public OpenAiDiagnosisService(
        IOptions<AiDiagnosisSettings> settings,
        ILogger<OpenAiDiagnosisService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AiDiagnosisResultDto> AnalyzePlantImageAsync(string imageUrl, string? userDescription, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogWarning("AiDiagnosis ApiKey not configured. Returning mock result.");
            return GetMockResult();
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");

            var systemPrompt = "You are a plant disease expert. Analyze the plant image and respond with a JSON object only (no markdown, no extra text) with this exact structure: {\"disease\": \"name or Healthy\", \"confidence\": 0.0-1.0, \"symptoms\": [\"s1\", \"s2\"], \"recommendations\": [\"r1\", \"r2\"], \"explanation\": \"brief\"}";
            var userText = "User description: " + (userDescription ?? "None") + ". Analyze this plant image for disease.";

            var requestBody = new Dictionary<string, object>
            {
                ["model"] = _settings.Model,
                ["max_tokens"] = _settings.MaxTokens,
                ["messages"] = new object[]
                {
                    new Dictionary<string, object> { ["role"] = "system", ["content"] = systemPrompt },
                    new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object> { ["type"] = "text", ["text"] = userText },
                            new Dictionary<string, object> { ["type"] = "image_url", ["image_url"] = new Dictionary<string, object> { ["url"] = imageUrl } }
                        }
                    }
                },
                ["response_format"] = new Dictionary<string, object> { ["type"] = "json_object" }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>(cancellationToken);
            var text = result?.Choices?.FirstOrDefault()?.Message?.Content;

            return ParseResponse(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI diagnosis failed for image {ImageUrl}. Returning mock result.", imageUrl);
            return GetMockResult();
        }
    }

    private static AiDiagnosisResultDto ParseResponse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return GetMockResult();
        }

        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(text);
            return new AiDiagnosisResultDto
            {
                Disease = json.TryGetProperty("disease", out var d) ? d.GetString() ?? "Unknown" : "Unknown",
                Confidence = json.TryGetProperty("confidence", out var c) && c.TryGetDouble(out var conf) ? conf : 0.5,
                Symptoms = json.TryGetProperty("symptoms", out var s) && s.ValueKind == JsonValueKind.Array
                    ? s.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList()
                    : new List<string>(),
                Recommendations = json.TryGetProperty("recommendations", out var r) && r.ValueKind == JsonValueKind.Array
                    ? r.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList()
                    : new List<string>(),
                Explanation = json.TryGetProperty("explanation", out var e) ? e.GetString() : null
            };
        }
        catch
        {
            return GetMockResult();
        }
    }

    private static AiDiagnosisResultDto GetMockResult()
    {
        return new AiDiagnosisResultDto
        {
            Disease = "Analysis unavailable",
            Confidence = 0,
            Symptoms = new List<string>(),
            Recommendations = new List<string> { "Please ensure AI service is configured and try again." },
            Explanation = "Mock result - AI service not configured or request failed."
        };
    }

    private sealed class OpenAiChatResponse
    {
        public List<OpenAiChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChoice
    {
        public OpenAiMessage? Message { get; set; }
    }

    private sealed class OpenAiMessage
    {
        public string? Content { get; set; }
    }
}
