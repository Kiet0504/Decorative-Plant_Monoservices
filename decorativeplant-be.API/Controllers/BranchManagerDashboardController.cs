using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.BranchManager.DTOs;
using decorativeplant_be.Application.Features.BranchManager.Queries;

namespace decorativeplant_be.API.Controllers;

[Route("api/v{version:apiVersion}/branch-manager")]
[Authorize]
public class BranchManagerDashboardController : BaseController
{
    /// <summary>Aggregated KPIs for branch oversight (includes store-level metrics plus manager-only stats).</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] Guid? branchId,
        [FromServices] IApplicationDbContext context)
    {
        var isAdmin = User.IsInRole("admin");
        var isBranchManager = User.IsInRole("branch_manager");

        if (!isAdmin && !isBranchManager)
            return StatusCode(403, ApiResponse<object>.ErrorResponse("Forbidden", statusCode: 403));

        Guid? effectiveBranchId = branchId;

        if (!isAdmin && !effectiveBranchId.HasValue)
        {
            var staffUserId = GetUserId();
            if (!staffUserId.HasValue)
                return Unauthorized(ApiResponse<object>.ErrorResponse("Unauthorized", statusCode: 401));

            effectiveBranchId = await context.StaffAssignments.AsNoTracking()
                .Where(s => s.StaffId == staffUserId && s.IsPrimary)
                .Select(s => (Guid?)s.BranchId)
                .FirstOrDefaultAsync();
        }

        if (!effectiveBranchId.HasValue)
            return BadRequest(ApiResponse<object>.ErrorResponse("branchId is required for this user.", statusCode: 400));

        BranchManagerDashboardDto result =
            await Mediator.Send(new GetBranchManagerDashboardQuery(effectiveBranchId.Value));
        return Ok(ApiResponse<BranchManagerDashboardDto>.SuccessResponse(result));
    }
}
