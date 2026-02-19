using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.ProductReviews.Commands;
using decorativeplant_be.Application.Features.Commerce.ProductReviews.Queries;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.ProductReviews.Handlers;

public class CreateProductReviewHandler : IRequestHandler<CreateProductReviewCommand, ProductReviewResponse>
{
    private readonly IApplicationDbContext _context;
    public CreateProductReviewHandler(IApplicationDbContext context) => _context = context;

    public async Task<ProductReviewResponse> Handle(CreateProductReviewCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        var entity = new ProductReview
        {
            Id = Guid.NewGuid(), ListingId = req.ListingId, UserId = cmd.UserId, OrderId = req.OrderId,
            Content = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                rating = req.Rating, title = req.Title, comment = req.Comment,
                is_verified = req.OrderId.HasValue, helpful_count = 0
            })),
            StatusInfo = JsonDocument.Parse(JsonSerializer.Serialize(new { status = "pending" })),
            Images = req.Images?.Count > 0 ? JsonDocument.Parse(JsonSerializer.Serialize(
                req.Images.Select(i => new { url = i.Url, alt = i.Alt, sort = i.Sort }))) : null,
            CreatedAt = DateTime.UtcNow
        };
        _context.ProductReviews.Add(entity);
        await _context.SaveChangesAsync(ct);
        return MapToResponse(entity);
    }

    internal static ProductReviewResponse MapToResponse(ProductReview e)
    {
        var r = new ProductReviewResponse { Id = e.Id, ListingId = e.ListingId, UserId = e.UserId, OrderId = e.OrderId, CreatedAt = e.CreatedAt };
        if (e.Content != null)
        {
            var root = e.Content.RootElement;
            r.Rating = root.TryGetProperty("rating", out var rt) ? rt.GetInt32() : 0;
            r.Title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
            r.Comment = root.TryGetProperty("comment", out var c) ? c.GetString() : null;
            r.IsVerified = root.TryGetProperty("is_verified", out var iv) && iv.GetBoolean();
            r.HelpfulCount = root.TryGetProperty("helpful_count", out var hc) ? hc.GetInt32() : 0;
        }
        if (e.StatusInfo != null)
            r.Status = e.StatusInfo.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "pending" : "pending";
        if (e.Images?.RootElement.ValueKind == JsonValueKind.Array)
            r.Images = e.Images.RootElement.EnumerateArray().Select(img => new ReviewImageDto
            {
                Url = img.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                Alt = img.TryGetProperty("alt", out var a) ? a.GetString() : null,
                Sort = img.TryGetProperty("sort", out var so) ? so.GetInt32() : 0
            }).ToList();
        return r;
    }
}

public class UpdateReviewStatusHandler : IRequestHandler<UpdateReviewStatusCommand, ProductReviewResponse>
{
    private readonly IApplicationDbContext _context;
    public UpdateReviewStatusHandler(IApplicationDbContext context) => _context = context;
    public async Task<ProductReviewResponse> Handle(UpdateReviewStatusCommand cmd, CancellationToken ct)
    {
        var entity = await _context.ProductReviews.FindAsync(new object[] { cmd.Id }, ct) ?? throw new NotFoundException($"Review {cmd.Id} not found.");
        entity.StatusInfo = JsonDocument.Parse(JsonSerializer.Serialize(new { status = cmd.Request.Status }));
        await _context.SaveChangesAsync(ct);
        return CreateProductReviewHandler.MapToResponse(entity);
    }
}

public class DeleteProductReviewHandler : IRequestHandler<DeleteProductReviewCommand, bool>
{
    private readonly IApplicationDbContext _context;
    public DeleteProductReviewHandler(IApplicationDbContext context) => _context = context;
    public async Task<bool> Handle(DeleteProductReviewCommand cmd, CancellationToken ct)
    {
        var e = await _context.ProductReviews.FindAsync(new object[] { cmd.Id }, ct) ?? throw new NotFoundException($"Review {cmd.Id} not found.");
        _context.ProductReviews.Remove(e); await _context.SaveChangesAsync(ct); return true;
    }
}

public class GetReviewsByListingHandler : IRequestHandler<GetReviewsByListingQuery, List<ProductReviewResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetReviewsByListingHandler(IApplicationDbContext context) => _context = context;
    public async Task<List<ProductReviewResponse>> Handle(GetReviewsByListingQuery q, CancellationToken ct)
    {
        var list = await _context.ProductReviews.Where(r => r.ListingId == q.ListingId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return list.Select(CreateProductReviewHandler.MapToResponse).ToList();
    }
}

public class GetReviewByIdHandler : IRequestHandler<GetReviewByIdQuery, ProductReviewResponse?>
{
    private readonly IApplicationDbContext _context;
    public GetReviewByIdHandler(IApplicationDbContext context) => _context = context;
    public async Task<ProductReviewResponse?> Handle(GetReviewByIdQuery q, CancellationToken ct)
    {
        var e = await _context.ProductReviews.FindAsync(new object[] { q.Id }, ct);
        return e == null ? null : CreateProductReviewHandler.MapToResponse(e);
    }
}
