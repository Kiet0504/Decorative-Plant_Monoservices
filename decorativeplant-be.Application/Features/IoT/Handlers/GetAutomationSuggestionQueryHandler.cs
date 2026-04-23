using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.IoT.DTOs;
using decorativeplant_be.Application.Features.IoT.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.IoT.Handlers;

public class GetAutomationSuggestionQueryHandler : IRequestHandler<GetAutomationSuggestionQuery, List<AutomationSuggestionDto>>
{
    private readonly IRepositoryFactory _repositoryFactory;

    public GetAutomationSuggestionQueryHandler(IRepositoryFactory repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
    }

    public async Task<List<AutomationSuggestionDto>> Handle(GetAutomationSuggestionQuery request, CancellationToken cancellationToken)
    {
        var deviceRepo = _repositoryFactory.CreateRepository<IotDevice>();
        
        // Find device and its location
        var device = await deviceRepo.Query()
            .Include(d => d.Location)
            .FirstOrDefaultAsync(d => d.Id == request.DeviceId, cancellationToken);

        if (device?.LocationId == null) return new List<AutomationSuggestionDto>();

        // Find all unique plant taxonomies at this location
        var batchStockRepo = _repositoryFactory.CreateRepository<BatchStock>();
        var taxonomies = await batchStockRepo.Query()
            .Where(bs => bs.LocationId == device.LocationId)
            .Include(bs => bs.Batch)
                .ThenInclude(b => (b != null) ? b.Taxonomy : null)
            .Select(bs => (bs.Batch != null) ? bs.Batch.Taxonomy : null)
            .Where(t => t != null && t.AutomationMasterData != null)
            .Distinct()
            .ToListAsync(cancellationToken);

        var currentSeason = GetCurrentSeason();
        var results = new List<AutomationSuggestionDto>();

        foreach (var taxonomy in taxonomies)
        {
            var suggestions = ExtractSuggestions(taxonomy, currentSeason);
            if (suggestions.Any())
            {
                var commonName = GetCommonName(taxonomy.CommonNames);
                results.Add(new AutomationSuggestionDto
                {
                    PlantName = string.IsNullOrEmpty(commonName) ? taxonomy.ScientificName : commonName,
                    Season = currentSeason,
                    Conditions = suggestions
                });
            }
        }

        return results;
    }

    private string GetCurrentSeason()
    {
        var month = DateTime.Now.Month;
        return month switch
        {
            3 or 4 or 5 => "spring",
            6 or 7 or 8 => "summer",
            9 or 10 or 11 => "autumn",
            _ => "winter"
        };
    }

    private List<SuggestedConditionDto> ExtractSuggestions(PlantTaxonomy taxonomy, string season)
    {
        var suggestions = new List<SuggestedConditionDto>();
        if (taxonomy.AutomationMasterData == null) return suggestions;

        try
        {
            var root = taxonomy.AutomationMasterData.RootElement;
            if (root.TryGetProperty(season, out var seasonData))
            {
                // Iterate over metrics in the season data
                foreach (var metric in seasonData.EnumerateObject())
                {
                    var metricKey = metric.Name; // e.g., "temp", "humidity"
                    var componentKey = MapToComponentKey(metricKey);
                    
                    if (metric.Value.TryGetProperty("min", out var minProp))
                    {
                        var minVal = minProp.GetDouble();
                        var suggestion = new SuggestedConditionDto
                        {
                            ComponentKey = componentKey,
                            Operator = "<",
                            Threshold = minVal,
                            Description = $"When {MapToLabel(metricKey)} drops below ideal threshold ({minVal})"
                        };

                        if (metric.Value.TryGetProperty("minAction", out var minAction))
                        {
                            suggestion.SuggestedAction = new SuggestedActionDto
                            {
                                TargetComponentKey = minAction.GetProperty("target").GetString(),
                                ActionType = minAction.GetProperty("type").GetString(),
                                Value = "true" // Standard for turn_on/off
                            };
                        }
                        suggestions.Add(suggestion);
                    }

                    if (metric.Value.TryGetProperty("max", out var maxProp))
                    {
                        var maxVal = maxProp.GetDouble();
                        var suggestion = new SuggestedConditionDto
                        {
                            ComponentKey = componentKey,
                            Operator = ">",
                            Threshold = maxVal,
                            Description = $"When {MapToLabel(metricKey)} exceeds ideal threshold ({maxVal})"
                        };

                        if (metric.Value.TryGetProperty("maxAction", out var maxAction))
                        {
                            suggestion.SuggestedAction = new SuggestedActionDto
                            {
                                TargetComponentKey = maxAction.GetProperty("target").GetString(),
                                ActionType = maxAction.GetProperty("type").GetString(),
                                Value = "true"
                            };
                        }
                        suggestions.Add(suggestion);
                    }
                }
            }
        }
        catch { /* Ignore parsing errors */ }

        return suggestions;
    }

    private string MapToComponentKey(string metricKey)
    {
        return metricKey.ToLower() switch
        {
            "temp" or "temperature" => "temp_sensor",
            "humidity" or "air_humidity" => "humidity_sensor",
            "soil_moisture" or "moisture" => "soil_moisture",
            "light" or "lux" => "light_sensor",
            _ => metricKey
        };
    }

    private string MapToLabel(string metricKey)
    {
        return metricKey.ToLower() switch
        {
            "temp" or "temperature" => "Temperature",
            "humidity" or "air_humidity" => "Air Humidity",
            "soil_moisture" or "moisture" => "Soil Moisture",
            "light" or "lux" => "Light Intensity",
            _ => metricKey
        };
    }

    private string GetCommonName(JsonDocument? commonNames)
    {
        if (commonNames == null) return string.Empty;
        if (commonNames.RootElement.TryGetProperty("vi", out var vi)) return vi.GetString() ?? "";
        if (commonNames.RootElement.TryGetProperty("en", out var en)) return en.GetString() ?? "";
        return string.Empty;
    }
}
