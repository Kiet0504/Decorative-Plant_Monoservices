using System.Text;
using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.RoomScan;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.Settings;
using decorativeplant_be.Application.Features.Commerce.ProductListings.Queries;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Application.Features.RoomScan.Services;

public sealed class RoomScanCatalogRankingService : IRoomScanCatalogRankingService
{
    private readonly IMediator _mediator;
    private readonly IRoomScanGeminiClient _gemini;
    private readonly RoomScanHandlerOptions _options;
    private readonly ILogger<RoomScanCatalogRankingService> _logger;

    public RoomScanCatalogRankingService(
        IMediator mediator,
        IRoomScanGeminiClient gemini,
        IOptions<RoomScanHandlerOptions> options,
        ILogger<RoomScanCatalogRankingService> logger)
    {
        _mediator = mediator;
        _gemini = gemini;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RoomScanCatalogRankingResult> GetRecommendationsAsync(
        RoomScanCatalogRankingRequest request,
        CancellationToken cancellationToken = default)
    {
        var catalogQuery = new GetProductListingsQuery
        {
            BranchId = request.BranchId,
            // For recommendations we want UNIQUE plants, not per-branch duplicates.
            // Grouping keeps one representative listing per species/title while preserving real inventory constraints.
            GroupBySpecies = true,
            Page = 1,
            PageSize = Math.Max(_options.MaxCatalogSnippets, 200),
            SortBy = "inventory",
            SortOrder = "desc"
        };

        var paged = await _mediator.Send(catalogQuery, cancellationToken);
        var listings = paged.Items.ToList();

        // Optional filters: never reduce to zero listings when the shop has stock — otherwise AI chat
        // returns no `newRecommendations` and the client cannot show "Suggested from our shop".
        if (request.MaxPrice is { } maxP && maxP > 0)
        {
            var withinBudget = listings.Where(p => ParsePrice(p.Price) <= maxP).ToList();
            if (withinBudget.Count > 0)
            {
                listings = withinBudget;
            }
            else
            {
                _logger.LogInformation(
                    "Room catalog ranking: max price {MaxPrice} removed all {Count} listings; ranking with full catalog.",
                    maxP,
                    listings.Count);
            }
        }

        if (request.PetSafeOnly)
        {
            var safe = listings.Where(p => !LooksToxic(CareGrowthText(p))).ToList();
            if (safe.Count > 0)
            {
                listings = safe;
            }
            else
            {
                _logger.LogInformation(
                    "Room catalog ranking: pet-safe filter removed all {Count} listings; ranking with unfiltered catalog.",
                    listings.Count);
            }
        }

        var exclude = request.ExcludeListingIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToHashSet();
        if (exclude is { Count: > 0 })
        {
            var filtered = listings.Where(p => !exclude.Contains(p.Id)).ToList();
            if (filtered.Count > 0)
            {
                listings = filtered;
            }
        }

        if (listings.Count == 0)
        {
            return new RoomScanCatalogRankingResult { NoMatches = true, Recommendations = new List<RoomScanRecommendationDto>() };
        }

        var maxSnippets = Math.Clamp(_options.MaxCatalogSnippets, 8, 120);
        var forPrompt = listings.Take(maxSnippets).ToList();
        var snippets = forPrompt.Select(p => new RoomScanCatalogSnippet
        {
            Id = p.Id,
            Title = p.Title,
            CareSummary = Truncate(CareGrowthText(p), 700),
            Tags = (p.Tags ?? new List<string>()).Take(12).ToList()
        }).ToList();

        var snippetIds = snippets.Select(s => s.Id).ToHashSet();
        var maxRec = Math.Clamp(_options.MaxRecommendations, 1, 10);

        IReadOnlyList<RoomScanGeminiRankItem>? ranked = null;
        try
        {
            ranked = await _gemini.RankListingsAsync(
                request.Profile,
                snippets,
                request.PetSafeOnly,
                request.SkillLevel,
                maxRec,
                request.PipelineMode,
                request.RankRefinementNotes,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Room catalog ranking: Gemini rank failed; using fallback ordering.");
        }

        var recommendations = MergeRecommendations(
            ranked,
            request.Profile,
            forPrompt,
            snippetIds,
            maxRec);

        return new RoomScanCatalogRankingResult
        {
            Recommendations = recommendations,
            NoMatches = false
        };
    }

    private static List<RoomScanRecommendationDto> MergeRecommendations(
        IReadOnlyList<RoomScanGeminiRankItem>? ranked,
        RoomProfileDto profile,
        List<ProductListingResponse> candidates,
        HashSet<Guid> validIds,
        int maxRec)
    {
        var byId = candidates.ToDictionary(x => x.Id);
        var ordered = new List<RoomScanRecommendationDto>();
        var seen = new HashSet<Guid>();

        if (ranked != null)
        {
            foreach (var item in ranked.OrderBy(x => x.Rank))
            {
                if (!validIds.Contains(item.ListingId) || !byId.TryGetValue(item.ListingId, out var p))
                {
                    continue;
                }

                if (!seen.Add(p.Id))
                {
                    continue;
                }

                ordered.Add(MapRecommendation(
                    p,
                    string.IsNullOrWhiteSpace(item.Reason)
                        ? BuildPersonalizedReason(p, profile)
                        : item.Reason.Trim()));

                if (ordered.Count >= maxRec)
                {
                    return ordered;
                }
            }
        }

        foreach (var p in OrderByFallback(candidates, profile))
        {
            if (!seen.Add(p.Id))
            {
                continue;
            }

            ordered.Add(MapRecommendation(p, BuildPersonalizedReason(p, profile)));

            if (ordered.Count >= maxRec)
            {
                break;
            }
        }

        return ordered;
    }

    private static RoomScanRecommendationDto MapRecommendation(ProductListingResponse p, string reason)
    {
        var img = p.Images?.FirstOrDefault(i => i.IsPrimary) ?? p.Images?.FirstOrDefault();
        var title =
            !string.IsNullOrWhiteSpace(p.CommonNameEn) ? p.CommonNameEn.Trim() :
            !string.IsNullOrWhiteSpace(p.Title) ? p.Title.Trim() :
            (p.ScientificName ?? string.Empty).Trim();
        return new RoomScanRecommendationDto
        {
            ListingId = p.Id,
            Title = title,
            Price = p.Price,
            ImageUrl = img?.Url,
            Reason = Truncate(reason.Trim(), 420)
        };
    }

    private static string BuildPersonalizedReason(ProductListingResponse p, RoomProfileDto profile)
    {
        var lower = CareGrowthText(p).ToLowerInvariant();
        var parts = new List<string>();

        var excerpt = PlainCareExcerpt(p, 260);
        if (!string.IsNullOrWhiteSpace(excerpt))
        {
            parts.Add(excerpt);
        }

        var light = profile.LightEstimate;
        if (light <= 2)
        {
            if (lower.Contains("low light", StringComparison.Ordinal) ||
                lower.Contains("shade", StringComparison.Ordinal) ||
                lower.Contains("indirect", StringComparison.Ordinal))
            {
                parts.Add("Care notes emphasize lower or indirect light — consistent with a dimmer corner.");
            }
        }
        else if (light >= 4)
        {
            if (lower.Contains("bright", StringComparison.Ordinal) ||
                lower.Contains("sun", StringComparison.Ordinal) ||
                lower.Contains("direct", StringComparison.Ordinal))
            {
                parts.Add("Care notes mention brighter light or sun tolerance — suits a stronger-lit spot.");
            }
        }

        if (string.Equals(profile.ApproxSpace, "small", StringComparison.OrdinalIgnoreCase))
        {
            if (lower.Contains("compact", StringComparison.Ordinal) ||
                lower.Contains("desk", StringComparison.Ordinal) ||
                lower.Contains("small", StringComparison.Ordinal))
            {
                parts.Add("Described as compact or desk-sized — helpful when footprint is tight.");
            }
        }
        else if (string.Equals(profile.ApproxSpace, "large", StringComparison.OrdinalIgnoreCase))
        {
            if (lower.Contains("tall", StringComparison.Ordinal) ||
                lower.Contains("floor", StringComparison.Ordinal) ||
                lower.Contains("large", StringComparison.Ordinal))
            {
                parts.Add("Growth notes suggest more vertical or floor space — fits a roomier layout.");
            }
        }

        if (parts.Count == 0)
        {
            return "Selected from current listings using catalog care text and your room scan; open the product for full care detail.";
        }

        return string.Join(" ", parts);
    }

    private static string? PlainCareExcerpt(ProductListingResponse p, int max)
    {
        var raw = CareGrowthText(p);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c is '.' or ',' or '-' or '/' or '%' or '(' or ')')
            {
                sb.Append(c);
            }
            else if (c is '{' or '}' or '[' or ']' or '"' or ':' or '\\')
            {
                sb.Append(' ');
            }
            else
            {
                sb.Append(' ');
            }
        }

        var collapsed = string.Join(' ', sb.ToString().Split(
            new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(collapsed))
        {
            return null;
        }

        return Truncate(collapsed.Trim(), max);
    }

    private static IEnumerable<ProductListingResponse> OrderByFallback(
        List<ProductListingResponse> candidates,
        RoomProfileDto profile) =>
        candidates
            .Select(p => (p, Score: FallbackScore(p, profile)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.p.Title)
            .Select(x => x.p);

    private static int FallbackScore(ProductListingResponse p, RoomProfileDto profile)
    {
        var lower = CareGrowthText(p).ToLowerInvariant();
        var score = 0;
        var light = profile.LightEstimate;
        if (light <= 2)
        {
            if (lower.Contains("low light", StringComparison.Ordinal) ||
                lower.Contains("shade", StringComparison.Ordinal) ||
                lower.Contains("indirect", StringComparison.Ordinal))
            {
                score += 4;
            }
        }
        else if (light >= 4)
        {
            if (lower.Contains("bright", StringComparison.Ordinal) ||
                lower.Contains("sun", StringComparison.Ordinal) ||
                lower.Contains("direct", StringComparison.Ordinal))
            {
                score += 4;
            }
        }
        else
        {
            score += 1;
        }

        if (string.Equals(profile.ApproxSpace, "small", StringComparison.OrdinalIgnoreCase))
        {
            if (lower.Contains("compact", StringComparison.Ordinal) ||
                lower.Contains("small", StringComparison.Ordinal) ||
                lower.Contains("desk", StringComparison.Ordinal))
            {
                score += 2;
            }
        }
        else if (string.Equals(profile.ApproxSpace, "large", StringComparison.OrdinalIgnoreCase))
        {
            if (lower.Contains("tall", StringComparison.Ordinal) ||
                lower.Contains("large", StringComparison.Ordinal) ||
                lower.Contains("floor", StringComparison.Ordinal))
            {
                score += 2;
            }
        }

        return score;
    }

    /// <summary>
    /// Heuristic for pet-safe filtering. Must NOT treat "non-toxic" / "pet safe" as toxic — naive
    /// <c>Contains("toxic")</c> matches the substring inside "non-toxic" and would drop safe plants.
    /// </summary>
    private static bool LooksToxic(string careText)
    {
        if (string.IsNullOrWhiteSpace(careText))
        {
            return false;
        }

        var lower = careText.ToLowerInvariant();
        if (lower.Contains("non-toxic", StringComparison.Ordinal) ||
            lower.Contains("non toxic", StringComparison.Ordinal) ||
            lower.Contains("not toxic", StringComparison.Ordinal) ||
            lower.Contains("pet safe", StringComparison.Ordinal) ||
            lower.Contains("pet-safe", StringComparison.Ordinal) ||
            lower.Contains("safe for pets", StringComparison.Ordinal) ||
            lower.Contains("safe around pets", StringComparison.Ordinal))
        {
            return false;
        }

        if (lower.Contains("toxic to cat", StringComparison.Ordinal) ||
            lower.Contains("toxic to dog", StringComparison.Ordinal) ||
            lower.Contains("toxic to pets", StringComparison.Ordinal) ||
            lower.Contains("poisonous", StringComparison.Ordinal))
        {
            return true;
        }

        if (lower.Contains("toxic", StringComparison.Ordinal) || lower.Contains("poison", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static string CareGrowthText(ProductListingResponse p)
    {
        var care = JsonDocRaw(p.CareInfo);
        var growth = JsonDocRaw(p.GrowthInfo);
        return $"{care} {growth}";
    }

    private static string JsonDocRaw(JsonDocument? doc)
    {
        if (doc == null)
        {
            return "";
        }

        try
        {
            return doc.RootElement.GetRawText();
        }
        catch
        {
            return "";
        }
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
        {
            return s;
        }

        return s[..max] + "…";
    }

    private static decimal ParsePrice(string? priceStr)
    {
        if (string.IsNullOrEmpty(priceStr))
        {
            return 0;
        }

        var digits = new string(priceStr.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, out var val) ? val : 0;
    }
}
