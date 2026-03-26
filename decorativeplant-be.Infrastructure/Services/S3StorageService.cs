using Amazon.S3;
using Amazon.S3.Model;
using decorativeplant_be.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Infrastructure.Services;

/// <summary>
/// AWS S3 implementation of IStorageService.
/// </summary>
public class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _region;
    private readonly ILogger<S3StorageService> _logger;

    public S3StorageService(IAmazonS3 s3Client, IConfiguration configuration, ILogger<S3StorageService> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
        _bucketName = configuration["AwsS3:BucketName"]
            ?? throw new InvalidOperationException("AwsS3:BucketName is not configured.");
        _region = configuration["AwsS3:Region"]
            ?? throw new InvalidOperationException("AwsS3:Region is not configured.");
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var key = $"{Guid.NewGuid():N}/{fileName}";

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = fileStream,
            ContentType = contentType
            // Note: Public access is controlled by Bucket Policy, not CannedACL,
            // because AWS blocks public ACLs on new buckets by default since 2023.
        };

        await _s3Client.PutObjectAsync(request, cancellationToken);

        var url = $"https://{_bucketName}.s3.{_region}.amazonaws.com/{key}";
        _logger.LogInformation("Uploaded file to S3: {Url}", url);
        return url;
    }

    public async Task DeleteFileAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = fileKey
        };

        await _s3Client.DeleteObjectAsync(request, cancellationToken);
        _logger.LogInformation("Deleted file from S3: {Key}", fileKey);
    }

    public string GeneratePresignedUrl(string fileKey, int expiryMinutes = 60)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = fileKey,
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes)
        };

        return _s3Client.GetPreSignedURL(request);
    }
}
