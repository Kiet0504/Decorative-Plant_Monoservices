using decorativeplant_be.Application.Common.DTOs.ArPreview;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.ArPreview.Commands;
using decorativeplant_be.Application.Features.ArPreview.Queries;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace decorativeplant_be.API.Controllers;

[Route("api/v{version:apiVersion}/ar-preview")]
public class ArPreviewController : BaseController
{
    private const long MaxGlbSizeBytes = 60 * 1024 * 1024; // 60MB

    [HttpPost("sessions")]
    [Authorize]
    public async Task<IActionResult> CreateSession([FromBody] CreateArPreviewSessionRequest request)
    {
        var result = await Mediator.Send(new CreateArPreviewSessionCommand
        {
            UserId = GetUserId(),
            Request = request
        });
        return Ok(ApiResponse<ArPreviewSessionResponse>.SuccessResponse(result));
    }

    [HttpGet("sessions/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSession(Guid id, [FromQuery] string token)
    {
        var result = await Mediator.Send(new GetArPreviewSessionQuery
        {
            SessionId = id,
            ViewerToken = token
        });
        if (result == null) return NotFound(ApiResponse<object>.ErrorResponse("Session not found", statusCode: 404));
        return Ok(ApiResponse<ArPreviewSessionResponse>.SuccessResponse(result));
    }

    [HttpGet("products/{productListingId:guid}/models")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProductModel(Guid productListingId)
    {
        var result = await Mediator.Send(new GetProductModelAssetQuery { ProductListingId = productListingId });
        if (result == null) return NotFound(ApiResponse<object>.ErrorResponse("Model not found", statusCode: 404));
        return Ok(ApiResponse<ProductModelAssetResponse>.SuccessResponse(result));
    }

    [HttpPost("models/upload")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize(Roles = "admin,store_staff,branch_manager,cultivation_staff,fulfillment_staff,Staff,staff")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxGlbSizeBytes)]
    public async Task<IActionResult> UploadProductModel(
        [FromForm] Guid productListingId,
        [FromForm] IFormFile file,
        [FromForm] decimal? defaultScale,
        [FromForm] string? boundingBoxJson,
        [FromServices] IObjectStorageService storage,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.ErrorResponse("File is required."));
        if (file.Length > MaxGlbSizeBytes)
            return BadRequest(ApiResponse<object>.ErrorResponse("File is too large."));

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".glb";
        if (!ext.Equals(".glb", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.ErrorResponse("Only .glb is supported for MVP."));

        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "model/gltf-binary" : file.ContentType;

        JsonDocument? bbox = null;
        if (!string.IsNullOrWhiteSpace(boundingBoxJson))
        {
            try { bbox = JsonDocument.Parse(boundingBoxJson); }
            catch { return BadRequest(ApiResponse<object>.ErrorResponse("Invalid boundingBoxJson.")); }
        }

        await using var stream = file.OpenReadStream();
        var url = await storage.UploadFileAsync(stream, contentType, ext, "ar-preview/models", cancellationToken);

        // Upsert asset (unique ProductListingId)
        var existing = await context.ProductModelAssets
            .FirstOrDefaultAsync(x => x.ProductListingId == productListingId, cancellationToken);

        if (existing == null)
        {
            existing = new ProductModelAsset
            {
                ProductListingId = productListingId,
                GlbUrl = url,
                DefaultScale = defaultScale ?? 1m,
                BoundingBox = bbox,
                CreatedAt = DateTime.UtcNow
            };
            context.ProductModelAssets.Add(existing);
        }
        else
        {
            existing.GlbUrl = url;
            existing.DefaultScale = defaultScale ?? existing.DefaultScale;
            existing.BoundingBox = bbox;
        }

        await context.SaveChangesAsync(cancellationToken);

        var resp = new ProductModelAssetResponse
        {
            ProductListingId = existing.ProductListingId,
            GlbUrl = existing.GlbUrl,
            DefaultScale = existing.DefaultScale,
            BoundingBox = existing.BoundingBox
        };

        return Ok(ApiResponse<ProductModelAssetResponse>.SuccessResponse(resp, "Uploaded successfully."));
    }
}

