using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Vouchers.Commands;

public class CreateVoucherCommand : IRequest<VoucherResponse> { public CreateVoucherRequest Request { get; set; } = null!; }
public class UpdateVoucherCommand : IRequest<VoucherResponse> { public Guid Id { get; set; } public UpdateVoucherRequest Request { get; set; } = null!; }
public class DeleteVoucherCommand : IRequest<bool> { public Guid Id { get; set; } }
