namespace decorativeplant_be.Application.Services;

/// <summary>
/// Abstraction for cloud object storage (AWS S3).
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Uploads a file to S3 and returns its public URL.
    /// </summary>
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from S3 by its key (file name / path within the bucket).
    /// </summary>
    Task DeleteFileAsync(string fileKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a pre-signed URL for temporary private access.
    /// </summary>
    string GeneratePresignedUrl(string fileKey, int expiryMinutes = 60);
}
