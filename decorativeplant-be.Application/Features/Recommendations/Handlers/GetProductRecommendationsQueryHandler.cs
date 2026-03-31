using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Recommendations;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Recommendations.Queries;
using decorativeplant_be.Application.Services.Recommendations;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Recommendations.Handlers;

public class GetProductRecommendationsQueryHandler : IRequestHandler<GetProductRecommendationsQuery, ProductRecommendationsResponse>
{
    private readonly IRecommendationEngine _engine;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _db;

    public GetProductRecommendationsQueryHandler(IRecommendationEngine engine, IUnitOfWork unitOfWork, IApplicationDbContext db)
    {
        _engine = engine;
        _unitOfWork = unitOfWork;
        _db = db;
    }

    public async Task<ProductRecommendationsResponse> Handle(GetProductRecommendationsQuery request, CancellationToken cancellationToken)
    {
        var response = await _engine.RecommendProductsAsync(request.UserId, request.Request, cancellationToken);

        // Persist log (best-effort)
        var log = new RecommendationLog
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Strategy = response.Strategy,
            RequestJson = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                request.Request.Limit,
                request.Request.GardenPlantId,
                request.Request.BranchId
            })),
            ResponseJson = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                response.GeneratedAt,
                response.Strategy,
                items = response.Items.Select(i => new { i.ListingId, i.Score, i.Reasons, i.BatchId, i.TaxonomyId }).ToList()
            })),
            CreatedAt = DateTime.UtcNow
        };

        _db.RecommendationLogs.Add(log);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }
}

