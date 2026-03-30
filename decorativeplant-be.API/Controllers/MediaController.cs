using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/media")]
[Authorize]
public class MediaController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly IMediaStorageService _mediaStorage;

    public MediaController(IMediaStorageService mediaStorage)
    {
        _mediaStorage = mediaStorage;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<ActionResult<ApiResponse<object>>> Upload(IFormFile file, [FromForm] string? folder = null, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("File is required."));
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("File is too large."));
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Unsupported file type."));
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg"
                : file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png"
                : ".webp";
        }

        await using var stream = file.OpenReadStream();
        var url = await _mediaStorage.UploadImageAsync(stream, file.ContentType, ext, folder, cancellationToken);

        return Ok(ApiResponse<object>.SuccessResponse(new { url }, "Uploaded successfully."));
    }
}

