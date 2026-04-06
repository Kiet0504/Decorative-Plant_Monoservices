using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Features.PlantLibrary.Commands;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using decorativeplant_be.Application.Features.PlantLibrary.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/plant-library/suppliers")]
[Authorize]
public class SupplierController : BaseController
{
    /// <summary>
    /// Register a new supplier.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "admin,branch_manager,cultivation_staff,fulfillment_staff")]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> Create([FromBody] CreateSupplierCommand command)
    {
        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<SupplierDto>.SuccessResponse(result, "Supplier registered.", 201));
    }

    /// <summary>
    /// List suppliers with pagination.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "admin,branch_manager,store_staff,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<PagedResultDto<SupplierDto>>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var query = new ListSuppliersQuery { Page = page, PageSize = pageSize, SearchTerm = search };
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResultDto<SupplierDto>>.SuccessResponse(result));
    }
}
