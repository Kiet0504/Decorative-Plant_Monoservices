using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Storage.S3;

public class S3MediaStorageService : IMediaStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly S3Settings _settings;

    public S3MediaStorageService(IAmazonS3 s3, IOptions<S3Settings> settings)
    {
        _s3 = s3;
        _settings = settings.Value;
    }

    public async Task<string> UploadImageAsync(
        Stream content,
        string contentType,
        string fileExtension,
        string? folder,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.Bucket))
        {
            throw new InvalidOperationException("S3 bucket is not configured.");
        }

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var cleanFolder = string.IsNullOrWhiteSpace(folder) ? "uploads" : folder.Trim().Trim('/');

        var ext = fileExtension.StartsWith(".") ? fileExtension : "." + fileExtension;
        var key = $"decorative-plant/{env.ToLowerInvariant()}/{cleanFolder}/{Guid.NewGuid():N}{ext.ToLowerInvariant()}";

        var request = new PutObjectRequest
        {
            BucketName = _settings.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType
        };

        await _s3.PutObjectAsync(request, cancellationToken);

        if (_settings.UsePresignedUrl)
        {
            var presign = new GetPreSignedUrlRequest
            {
                BucketName = _settings.Bucket,
                Key = key,
                Expires = DateTime.UtcNow.AddMinutes(Math.Max(_settings.PresignedUrlExpiresMinutes, 1))
            };
            return _s3.GetPreSignedURL(presign);
        }

        if (!string.IsNullOrWhiteSpace(_settings.PublicBaseUrl))
        {
            return $"{_settings.PublicBaseUrl.TrimEnd('/')}/{key}";
        }

        // Default public S3 URL shape (works for public buckets or CloudFront origin access with rewrite)
        return $"https://{_settings.Bucket}.s3.amazonaws.com/{key}";
    }
}

