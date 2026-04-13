using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Recommendations;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Services.Recommendations;

public class RuleBasedRecommendationEngine : IRecommendationEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const int CandidatePoolSize = 200;

    private readonly IApplicationDbContext _db;
    private readonly IGardenRepository _gardenRepository;

    public RuleBasedRecommendationEngine(IApplicationDbContext db, IGardenRepository gardenRepository)
    {
        _db = db;
        _gardenRepository = gardenRepository;
    }

    public async Task<ProductRecommendationsResponse> RecommendProductsAsync(
        Guid userId,
        ProductRecommendationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(request.Limit, 1, 10);

        // Seed signals
        var purchasedTaxonomyIds = await GetPurchasedTaxonomyIds(userId, cancellationToken);
        var gardenTaxonomyIds = await GetGardenTaxonomyIds(userId, request.GardenPlantId, cancellationToken);
        var careProfile = await GetCareProfile(userId, cancellationToken);

        // Candidate generation
        var candidates = await GetCandidateListingsWithTaxonomy(request.BranchId, cancellationToken);

        // Score
        var scored = new List<(ProductListing Listing, double Score, List<string> Reasons, Guid? TaxonomyId)>();
        foreach (var c in candidates)
        {
            var (ruleScore, reasons, taxonomyId) = ScoreListing(c.Listing, c.TaxonomyId, purchasedTaxonomyIds, gardenTaxonomyIds, careProfile);
            scored.Add((c.Listing, ruleScore, reasons, taxonomyId));
        }

        var top = scored
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Listing.CreatedAt ?? DateTime.MinValue)
            .Take(limit)
            .ToList();

        var response = new ProductRecommendationsResponse
        {
            GeneratedAt = DateTime.UtcNow,
            Strategy = "rule",
            Items = top.Select(x =>
            {
                var info = DeserializeProductInfo(x.Listing.ProductInfo);
                var primaryImage = TryGetPrimaryImageUrl(x.Listing.Images);
                return new RecommendedProductDto
                {
                    ListingId = x.Listing.Id,
                    Score = Math.Round(x.Score, 4),
                    Reasons = x.Reasons,
                    BatchId = x.Listing.BatchId,
                    TaxonomyId = x.TaxonomyId,
                    Title = info?.Title,
                    Price = info?.Price,
                    ImageUrl = primaryImage
                };
            }).ToList()
        };

        // Optional hybrid: text similarity “embedding-like” blending, only if env var enabled.
        var enableHybrid = string.Equals(Environment.GetEnvironmentVariable("RECOMMENDATION_HYBRID_ENABLED"), "true", StringComparison.OrdinalIgnoreCase);
        if (enableHybrid)
        {
            response = BlendWithTextSimilarity(userId, request, response, purchasedTaxonomyIds, gardenTaxonomyIds, careProfile, candidates.Select(x => x.Listing).ToList());
        }

        return response;
    }

    private async Task<HashSet<Guid>> GetPurchasedTaxonomyIds(Guid userId, CancellationToken ct)
    {
        var orderItemBatchIds = _db.OrderItems
            .Where(oi => oi.Order != null && oi.Order.UserId == userId)
            .Select(oi => oi.BatchId)
            .Where(b => b != null)
            .Select(b => b!.Value);

        var taxonomyIds = await _db.PlantBatches
            .Where(b => b.TaxonomyId != null && orderItemBatchIds.Contains(b.Id))
            .Select(b => b.TaxonomyId!.Value)
            .ToListAsync(ct);

        return taxonomyIds.ToHashSet();
    }

    private async Task<HashSet<Guid>> GetGardenTaxonomyIds(Guid userId, Guid? gardenPlantId, CancellationToken ct)
    {
        if (gardenPlantId.HasValue)
        {
            var plant = await _gardenRepository.GetPlantByIdAsync(gardenPlantId.Value, includeTaxonomy: false, ct);
            if (plant == null || plant.UserId != userId) return new HashSet<Guid>();
            return plant.TaxonomyId.HasValue ? new HashSet<Guid> { plant.TaxonomyId.Value } : new HashSet<Guid>();
        }

        // Take up to 20 taxonomy ids as signal
        var plants = await _gardenRepository.GetPlantsByUserIdAsync(userId, includeArchived: false, healthFilter: null, page: 1, pageSize: 20, ct);
        return plants.Items.Where(p => p.TaxonomyId.HasValue).Select(p => p.TaxonomyId!.Value).ToHashSet();
    }

    private async Task<CareProfile> GetCareProfile(Guid userId, CancellationToken ct)
    {
        // Infer: if user logs care often -> higher engagement.
        var plantIds = _db.GardenPlants.Where(g => g.UserId == userId).Select(g => g.Id);
        var recent = await _db.CareLogs
            .Where(c => c.GardenPlantId != null && plantIds.Contains(c.GardenPlantId.Value))
            .OrderByDescending(c => c.PerformedAt)
            .Take(50)
            .ToListAsync(ct);

        var actionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var log in recent)
        {
            try
            {
                if (log.LogInfo == null) continue;
                var root = log.LogInfo.RootElement;
                if (root.TryGetProperty("action_type", out var a))
                {
                    var key = a.GetString() ?? "";
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    actionCounts[key] = actionCounts.TryGetValue(key, out var c) ? c + 1 : 1;
                }
            }
            catch { }
        }

        var total = actionCounts.Values.Sum();
        return new CareProfile
        {
            TotalLogs = recent.Count,
            TotalActions = total,
            ActionCounts = actionCounts
        };
    }

    private async Task<List<ListingCandidate>> GetCandidateListingsWithTaxonomy(Guid? branchId, CancellationToken ct)
    {
        var q = _db.ProductListings.AsQueryable();
        if (branchId.HasValue)
        {
            q = q.Where(l => l.BranchId == branchId.Value);
        }

        // Basic: only public + active/published listings. Stored in StatusInfo JSONB.
        // Since JSONB query translation may be tricky, we filter in-memory after taking a pool.
        var pool = await q
            .Include(l => l.Batch)
                .ThenInclude(b => b!.BatchStocks)
                    .ThenInclude(bs => bs.Location)
            .OrderByDescending(l => l.CreatedAt)
            .Take(CandidatePoolSize)
            .Select(l => new ListingCandidate
            {
                Listing = l,
                TaxonomyId = l.Batch != null ? l.Batch.TaxonomyId : null
            })
            .ToListAsync(ct);

        return pool
            .Where(x => IsListingEligible(x.Listing))
            .ToList();
    }

    private static bool IsListingEligible(ProductListing listing)
    {
        try
        {
            if (listing.StatusInfo == null) return true;
            var root = listing.StatusInfo.RootElement;
            var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
            var visibility = root.TryGetProperty("visibility", out var v) ? v.GetString() : null;

            if (!string.IsNullOrWhiteSpace(visibility) && !string.Equals(visibility, "public", StringComparison.OrdinalIgnoreCase))
                return false;
            
            // Default to "draft" if status is missing
            var finalStatus = status?.ToLower() ?? "draft";
            if (finalStatus != "published" && finalStatus != "active")
                return false;

            // CRITICAL: Stock Check
            var stock = 0;
            if (listing.Batch != null && listing.Batch.BatchStocks != null)
            {
                // If branch specific, we could tighten this. 
                // But generally for recommendations, we check if it is available ANYWHERE or at the specific branch.
                // Since this candidate list might be branch-filtered already (line 159), we sum the relevant stock.
                var relevantStocks = listing.BranchId.HasValue 
                    ? listing.Batch.BatchStocks.Where(s => s.Location?.BranchId == listing.BranchId.Value)
                    : listing.Batch.BatchStocks;

                foreach (var s in relevantStocks)
                {
                    if (s.Quantities != null && s.Quantities.RootElement.TryGetProperty("available_quantity", out var aq))
                    {
                        stock += aq.GetInt32();
                    }
                }
            }

            return stock > 0;
        }
        catch
        {
            return false;
        }
    }

    private (double Score, List<string> Reasons, Guid? TaxonomyId) ScoreListing(
        ProductListing listing,
        Guid? taxonomyId,
        HashSet<Guid> purchasedTaxonomyIds,
        HashSet<Guid> gardenTaxonomyIds,
        CareProfile careProfile)
    {
        var reasons = new List<string>();
        var score = 0.0;

        // Taxonomy affinity (now resolved via join): strongest signals
        if (taxonomyId.HasValue)
        {
            if (purchasedTaxonomyIds.Contains(taxonomyId.Value))
            {
                score += 0.25;
                reasons.Add("similar_to_past_purchase");
            }
            if (gardenTaxonomyIds.Contains(taxonomyId.Value))
            {
                score += 0.20;
                reasons.Add("matches_my_garden");
            }
        }

        // Featured boost
        if (listing.StatusInfo != null && listing.StatusInfo.RootElement.TryGetProperty("featured", out var f) && f.ValueKind == JsonValueKind.True)
        {
            score += 0.05;
            reasons.Add("featured");
        }

        // Tags boost: if user is active care logger, suggest more advanced tags (heuristic)
        var tags = ExtractTags(listing.StatusInfo);
        if (careProfile.TotalLogs < 5 && tags.Any(t => t.Contains("easy", StringComparison.OrdinalIgnoreCase) || t.Contains("low", StringComparison.OrdinalIgnoreCase)))
        {
            score += 0.05;
            reasons.Add("low_maintenance_fit");
        }

        // Popularity (sold_count, view_count)
        var sold = TryGetInt(listing.StatusInfo, "sold_count");
        var views = TryGetInt(listing.StatusInfo, "view_count");
        var pop = Math.Min(0.1, (sold / 1000.0) + (views / 10000.0));
        if (pop > 0)
        {
            score += pop;
            reasons.Add("popular");
        }

        // Base freshness
        if (listing.CreatedAt.HasValue)
        {
            var days = (DateTime.UtcNow - listing.CreatedAt.Value).TotalDays;
            var freshness = Math.Max(0, 0.05 - (days / 365.0) * 0.05);
            score += freshness;
        }

        // Without taxonomy join yet: treat as unknown affinity
        // Hybrid blend still uses text similarity with tags/title; taxonomy join already improves precision.

        return (Math.Min(score, 1.0), reasons, taxonomyId);
    }

    private static ProductInfoJsonb? DeserializeProductInfo(JsonDocument? doc)
    {
        if (doc == null) return null;
        try
        {
            return JsonSerializer.Deserialize<ProductInfoJsonb>(doc.RootElement.GetRawText(), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetPrimaryImageUrl(JsonDocument? images)
    {
        if (images == null || images.RootElement.ValueKind != JsonValueKind.Array) return null;
        try
        {
            var arr = images.RootElement.EnumerateArray().ToList();
            var primary = arr.FirstOrDefault(e => e.TryGetProperty("is_primary", out var p) && p.GetBoolean());
            var chosen = primary.ValueKind != JsonValueKind.Undefined ? primary : arr.FirstOrDefault();
            return chosen.ValueKind == JsonValueKind.Undefined ? null : (chosen.TryGetProperty("url", out var u) ? u.GetString() : null);
        }
        catch
        {
            return null;
        }
    }

    private static List<string> ExtractTags(JsonDocument? statusInfo)
    {
        var tags = new List<string>();
        if (statusInfo == null) return tags;
        try
        {
            var root = statusInfo.RootElement;
            if (root.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Array)
            {
                tags.AddRange(t.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrWhiteSpace(x)));
            }
        }
        catch { }
        return tags;
    }

    private static int TryGetInt(JsonDocument? doc, string prop)
    {
        if (doc == null) return 0;
        try
        {
            var root = doc.RootElement;
            if (root.TryGetProperty(prop, out var p) && p.TryGetInt32(out var i)) return i;
        }
        catch { }
        return 0;
    }

    private static ProductRecommendationsResponse BlendWithTextSimilarity(
        Guid userId,
        ProductRecommendationsRequest request,
        ProductRecommendationsResponse baseResponse,
        HashSet<Guid> purchasedTaxonomyIds,
        HashSet<Guid> gardenTaxonomyIds,
        CareProfile careProfile,
        List<ProductListing> candidates)
    {
        // Simple text-vector similarity (no external embedding). Blends into scores.
        var userText = BuildUserText(purchasedTaxonomyIds, gardenTaxonomyIds, careProfile);
        var userVec = ToTermVector(userText);

        var listingMap = candidates.ToDictionary(c => c.Id, c => c);
        var blended = baseResponse.Items.Select(item =>
        {
            if (!listingMap.TryGetValue(item.ListingId, out var listing)) return item;
            var listingText = BuildListingText(listing);
            var sim = Cosine(userVec, ToTermVector(listingText));
            var final = 0.7 * item.Score + 0.3 * sim;
            item.Score = Math.Round(final, 4);
            if (sim > 0.2) item.Reasons.Add("text_similarity");
            return item;
        }).OrderByDescending(i => i.Score).ToList();

        baseResponse.Items = blended;
        baseResponse.Strategy = "hybrid";
        return baseResponse;
    }

    private static string BuildUserText(HashSet<Guid> purchasedTaxonomyIds, HashSet<Guid> gardenTaxonomyIds, CareProfile careProfile)
    {
        // IDs are weak text; we mainly use care pattern labels for similarity.
        var parts = new List<string>();
        parts.AddRange(careProfile.ActionCounts.Keys.Select(k => $"action_{k}"));
        if (purchasedTaxonomyIds.Count > 0) parts.Add("has_purchase_history");
        if (gardenTaxonomyIds.Count > 0) parts.Add("has_garden");
        return string.Join(' ', parts);
    }

    private static string BuildListingText(ProductListing listing)
    {
        var parts = new List<string>();
        var tags = ExtractTags(listing.StatusInfo);
        parts.AddRange(tags.Select(t => $"tag_{t}"));
        if (listing.StatusInfo != null && listing.StatusInfo.RootElement.TryGetProperty("featured", out var f) && f.ValueKind == JsonValueKind.True) parts.Add("featured");
        if (listing.ProductInfo != null && listing.ProductInfo.RootElement.TryGetProperty("title", out var t)) parts.Add(t.GetString() ?? "");
        return string.Join(' ', parts);
    }

    private static Dictionary<string, double> ToTermVector(string text)
    {
        var vec = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in text.Split(new[] { ' ', '\n', '\r', '\t', ',', '.', ';', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var key = token.Trim().ToLowerInvariant();
            if (key.Length < 2) continue;
            vec[key] = vec.TryGetValue(key, out var c) ? c + 1 : 1;
        }
        return vec;
    }

    private static double Cosine(Dictionary<string, double> a, Dictionary<string, double> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        double dot = 0, na = 0, nb = 0;
        foreach (var kv in a)
        {
            na += kv.Value * kv.Value;
            if (b.TryGetValue(kv.Key, out var bv)) dot += kv.Value * bv;
        }
        foreach (var kv in b) nb += kv.Value * kv.Value;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb) + 1e-9);
    }

    private class CareProfile
    {
        public int TotalLogs { get; set; }
        public int TotalActions { get; set; }
        public Dictionary<string, int> ActionCounts { get; set; } = new();
    }

    private class ListingCandidate
    {
        public required ProductListing Listing { get; set; }
        public Guid? TaxonomyId { get; set; }
    }
}

