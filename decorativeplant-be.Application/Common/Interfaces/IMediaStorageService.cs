namespace decorativeplant_be.Application.Common.Interfaces;

public interface IMediaStorageService
{
    Task<string> UploadImageAsync(
        Stream content,
        string contentType,
        string fileExtension,
        string? folder,
        CancellationToken cancellationToken = default);
}

