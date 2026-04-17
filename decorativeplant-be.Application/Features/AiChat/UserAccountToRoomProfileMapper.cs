using decorativeplant_be.Application.Common.DTOs.RoomScan;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.AiChat;

/// <summary>
/// Builds a <see cref="RoomProfileDto"/> from onboarding / AI profile fields so catalog ranking can run without a room photo.
/// </summary>
public static class UserAccountToRoomProfileMapper
{
    public static RoomProfileDto Map(UserAccount user)
    {
        var light = MapLight(user.SunlightExposure);
        var space = MapSpace(user.SpaceSize);
        var placement = MapPlacement(user.PlacementLocation);
        var indoor = string.Equals(user.PlacementLocation, "balcony", StringComparison.OrdinalIgnoreCase)
            ? "mixed"
            : "indoor";

        var tags = new List<string>();
        if (!string.IsNullOrWhiteSpace(user.PreferredStyle))
        {
            tags.Add(user.PreferredStyle.Trim());
        }

        if (user.PlantGoals != null)
        {
            try
            {
                foreach (var el in user.PlantGoals.RootElement.EnumerateArray())
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        tags.Add(s.Trim());
                    }
                }
            }
            catch
            {
                // ignore malformed goals
            }
        }

        tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();

        return new RoomProfileDto
        {
            LightEstimate = light,
            IndoorOutdoor = indoor,
            ApproxSpace = space,
            PlacementHint = placement,
            StyleTags = tags,
            Caveats = new List<string>
            {
                "Based on saved profile preferences (light, space, style) — not a photo of a room."
            },
            Confidence = 0.72,
            AnalysisSourceHint = "Profile-based suggestion (no room photo)"
        };
    }

    /// <summary>Same tiers as the React <c>mapBudgetToMaxPriceHint</c> (VND).</summary>
    public static decimal? MapBudgetToMaxPrice(string? budgetRange)
    {
        var b = (budgetRange ?? string.Empty).Trim().ToLowerInvariant();
        return b switch
        {
            "low" => 300_000m,
            "medium" => 1_000_000m,
            _ => null
        };
    }

    public static string MapSkillLevel(string? experienceLevel)
    {
        var v = (experienceLevel ?? string.Empty).Trim().ToLowerInvariant();
        return v is "beginner" or "intermediate" or "expert" ? v : "beginner";
    }

    private static int MapLight(string? sunlight)
    {
        var s = (sunlight ?? string.Empty).Trim().ToLowerInvariant();
        return s switch
        {
            "low_light" => 2,
            "indirect" => 3,
            "filtered" => 3,
            "morning_3h" => 4,
            "full_sun_6h" => 5,
            _ => 3
        };
    }

    private static string MapSpace(string? spaceSize)
    {
        var s = (spaceSize ?? string.Empty).Trim().ToLowerInvariant();
        return s is "small" or "medium" or "large" ? s : "medium";
    }

    private static string MapPlacement(string? placement)
    {
        var p = (placement ?? string.Empty).Trim().ToLowerInvariant();
        return p switch
        {
            "desk" => "table",
            "office" => "table",
            "living_room" => "floor",
            "bedroom" => "floor",
            "hallway" => "floor",
            "balcony" => "unknown",
            _ => "unknown"
        };
    }
}
