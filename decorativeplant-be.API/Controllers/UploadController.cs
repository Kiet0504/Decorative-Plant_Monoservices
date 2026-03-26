using decorativeplant_be.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : BaseController
{
    private readonly IStorageService _storage;

    public UploadController(IStorageService storage)
    {
        _storage = storage;
    }

    /// <summary>Upload a single file to AWS S3 and returns its public URL.</summary>
    [HttpPost]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        const long maxSize = 10 * 1024 * 1024; // 10 MB
        if (file.Length > maxSize)
            return BadRequest(new { message = "File size exceeds 10 MB limit." });

        await using var stream = file.OpenReadStream();
        var url = await _storage.UploadFileAsync(stream, file.FileName, file.ContentType, cancellationToken);

        return Ok(new { url });
    }

    /// <summary>Upload a file without authentication (for testing purposes only).</summary>
    [HttpPost("public")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadPublic(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        await using var stream = file.OpenReadStream();
        var url = await _storage.UploadFileAsync(stream, file.FileName, file.ContentType, cancellationToken);

        return Ok(new { url });
    }
}
