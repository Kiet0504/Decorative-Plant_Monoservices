using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Promotions.Queries;

public class GetPromotionsQuery : IRequest<List<PromotionResponse>> { public Guid? BranchId { get; set; } }
public class GetPromotionByIdQuery : IRequest<PromotionResponse?> { public Guid Id { get; set; } }
