using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.ProductListings.Commands;

public class CreateProductListingCommand : IRequest<ProductListingResponse>
{
    public CreateProductListingRequest Request { get; set; } = null!;
}

public class UpdateProductListingCommand : IRequest<ProductListingResponse>
{
    public Guid Id { get; set; }
    public UpdateProductListingRequest Request { get; set; } = null!;
    public string? UserRole { get; set; }
}

public class DeleteProductListingCommand : IRequest<bool>
{
    public Guid Id { get; set; }
}
