namespace decorativeplant_be.Application.Common.Interfaces;

public interface IObjectStorageService
{
    Task<string> UploadFileAsync(
        Stream content,
        string contentType,
        string fileExtension,
        string folder,
        CancellationToken cancellationToken = default);
}

