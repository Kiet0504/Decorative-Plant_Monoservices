using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Vouchers.Queries;

public class GetVouchersQuery : IRequest<List<VoucherResponse>> { public Guid? BranchId { get; set; } public bool? ActiveOnly { get; set; } }
public class GetVoucherByIdQuery : IRequest<VoucherResponse?> { public Guid Id { get; set; } }
public class ValidateVoucherQuery : IRequest<ValidateVoucherResponse> { public string Code { get; set; } = string.Empty; public Guid? BranchId { get; set; } }
