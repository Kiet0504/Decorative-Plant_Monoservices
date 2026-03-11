-- Check if there's data in feature_usage_quota table
SELECT
    "FeatureKey",
    COUNT(*) as "UserCount",
    AVG("QuotaUsed"::numeric / "QuotaLimit"::numeric * 100) as "AvgUsagePercent",
    MAX("QuotaUsed"::numeric / "QuotaLimit"::numeric * 100) as "MaxUsagePercent",
    MIN("QuotaUsed"::numeric / "QuotaLimit"::numeric * 100) as "MinUsagePercent"
FROM feature_usage_quota
WHERE "QuotaLimit" > 0
GROUP BY "FeatureKey"
ORDER BY "MaxUsagePercent" DESC;

-- Also check total records
SELECT COUNT(*) as "TotalQuotaRecords" FROM feature_usage_quota;

-- Check some sample records
SELECT
    "UserId",
    "FeatureKey",
    "QuotaLimit",
    "QuotaUsed",
    ("QuotaUsed"::numeric / "QuotaLimit"::numeric * 100) as "UsagePercent"
FROM feature_usage_quota
WHERE "QuotaLimit" > 0
ORDER BY ("QuotaUsed"::numeric / "QuotaLimit"::numeric) DESC
LIMIT 10;
