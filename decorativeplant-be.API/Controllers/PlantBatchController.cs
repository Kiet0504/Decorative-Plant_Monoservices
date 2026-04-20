using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/inventory/batches")]
[Authorize]
public class PlantBatchController : BaseController
{
    /// <summary>
    /// Create a new plant batch (e.g., from supplier or propagation).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "admin,branch_manager,store_staff,cultivation_staff,fulfillment_staff")]
    public async Task<ActionResult<ApiResponse<PlantBatchDto>>> Create([FromBody] CreatePlantBatchCommand command)
    {
        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<PlantBatchDto>.SuccessResponse(result, "Plant batch created successfully.", 201));
    }

    /// <summary>
    /// Update an existing plant batch (specs, source info).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "admin,branch_manager,store_staff,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<PlantBatchDto>>> Update(Guid id, [FromBody] UpdatePlantBatchCommand command)
    {
        command.Id = id; // Always sync with route to avoid binding mismatches
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<PlantBatchDto>.SuccessResponse(result, "Plant batch updated successfully."));
    }

    /// <summary>
    /// Get details of a plant batch, including lineage.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "admin,branch_manager,store_staff,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<PlantBatchDto>>> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetPlantBatchQuery { Id = id });
        return Ok(ApiResponse<PlantBatchDto>.SuccessResponse(result, "Plant batch details retrieved."));
    }

    /// <summary>
    /// List plant batches with filtering.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "admin,branch_manager,store_staff,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<PagedResultDto<PlantBatchSummaryDto>>>> List(
        [FromQuery] string? search = null,
        [FromQuery] string? healthStatus = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] Guid? taxonomyId = null,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] Guid? branchId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new ListPlantBatchesQuery 
        { 
            SearchTerm = search,
            HealthStatus = healthStatus,
            SortOrder = sortOrder,
            TaxonomyId = taxonomyId,
            SupplierId = supplierId,
            BranchId = branchId,
            LocationId = locationId,
            Page = page, 
            PageSize = pageSize 
        };
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResultDto<PlantBatchSummaryDto>>.SuccessResponse(result, "Plant batches retrieved."));
    }

    /// <summary>
    /// Publish a batch to sales stock (BatchStock & ProductListing).
    /// </summary>
    [HttpPost("{id}/publish")]
    [Authorize(Roles = "admin,branch_manager,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<bool>>> PublishToStock(Guid id, [FromBody] PublishBatchToStockCommand command)
    {
        command.BatchId = id;
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<bool>.SuccessResponse(result, "Batch published to sales stock."));
    }

    /// <summary>
    /// Delete an existing plant batch.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "admin,branch_manager,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<Unit>>> Delete(Guid id)
    {
        await Mediator.Send(new DeletePlantBatchCommand(id));
        return Ok(ApiResponse<Unit>.SuccessResponse(Unit.Value, "Plant batch deleted successfully."));
    }
}
