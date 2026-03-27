namespace decorativeplant_be.Infrastructure.Storage.S3;

public class S3Settings
{
    public const string SectionName = "S3";

    public string Bucket { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>
    /// If set, use this as base URL (e.g. CloudFront domain) instead of default S3 URL.
    /// Example: https://cdn.example.com
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// If true, return a presigned URL instead of public URL.
    /// </summary>
    public bool UsePresignedUrl { get; set; } = false;

    public int PresignedUrlExpiresMinutes { get; set; } = 60;
}

