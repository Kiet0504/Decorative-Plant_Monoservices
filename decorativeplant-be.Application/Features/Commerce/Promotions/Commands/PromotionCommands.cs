using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Promotions.Commands;

public class CreatePromotionCommand : IRequest<PromotionResponse> { public CreatePromotionRequest Request { get; set; } = null!; }
public class UpdatePromotionCommand : IRequest<PromotionResponse> { public Guid Id { get; set; } public UpdatePromotionRequest Request { get; set; } = null!; }
public class DeletePromotionCommand : IRequest<bool> { public Guid Id { get; set; } }
