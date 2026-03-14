namespace decorativeplant_be.API.Middleware;

/// <summary>
/// Defines free tier quota defaults for premium features.
/// </summary>
public static class FreeTierDefaults
{
    /// <summary>
    /// Free tier quota for AI diagnosis feature (per month).
    /// </summary>
    public const int AiDiagnosisQuota = 5;

    /// <summary>
    /// Free tier quota for AI recommendation feature (per month).
    /// </summary>
    public const int AiRecommendationQuota = 3;

    /// <summary>
    /// Feature key for AI diagnosis.
    /// </summary>
    public const string AiDiagnosisFeatureKey = "ai_diagnosis";

    /// <summary>
    /// Feature key for AI recommendation.
    /// </summary>
    public const string AiRecommendationFeatureKey = "ai_recommendation";

    /// <summary>
    /// Gets the default quota for a given feature key.
    /// </summary>
    /// <param name="featureKey">The feature key to look up.</param>
    /// <returns>The default quota, or 0 if the feature key is unknown.</returns>
    public static int GetDefaultQuota(string featureKey)
    {
        return featureKey switch
        {
            AiDiagnosisFeatureKey => AiDiagnosisQuota,
            AiRecommendationFeatureKey => AiRecommendationQuota,
            _ => 0
        };
    }
}
