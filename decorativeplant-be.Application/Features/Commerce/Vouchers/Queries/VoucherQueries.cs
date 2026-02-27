using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

using decorativeplant_be.Application.Common.DTOs.Common;

namespace decorativeplant_be.Application.Features.Commerce.Vouchers.Queries;

public class GetVouchersQuery : IRequest<PagedResult<VoucherResponse>> 
{ 
    public Guid? BranchId { get; set; } 
    public bool? ActiveOnly { get; set; } 
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
public class GetVoucherByIdQuery : IRequest<VoucherResponse?> { public Guid Id { get; set; } }
public class ValidateVoucherQuery : IRequest<ValidateVoucherResponse> { public string Code { get; set; } = string.Empty; public Guid? BranchId { get; set; } }
