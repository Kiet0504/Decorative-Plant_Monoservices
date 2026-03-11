using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/inventory/locations")]
[Authorize] // Require staff/admin
public class InventoryLocationController : BaseController
{
    /// <summary>
    /// Create a new inventory location (Warehouse, Zone, Shelf).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<InventoryLocationDto>>> CreateLocation([FromBody] CreateInventoryLocationCommand command)
    {
        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<InventoryLocationDto>.SuccessResponse(result, "Location created.", 201));
    }

    /// <summary>
    /// Update an existing inventory location.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<InventoryLocationDto>>> UpdateLocation(Guid id, [FromBody] UpdateInventoryLocationCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest(ApiResponse<InventoryLocationDto>.ErrorResponse("ID mismatch between route and body."));
        }

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<InventoryLocationDto>.SuccessResponse(result, "Location updated."));
    }

    /// <summary>
    /// Delete an inventory location.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<Unit>>> DeleteLocation(Guid id)
    {
        var command = new DeleteInventoryLocationCommand(id);
        await Mediator.Send(command);
        return Ok(ApiResponse<Unit>.SuccessResponse(MediatR.Unit.Value, "Location deleted."));
    }

    /// <summary>
    /// Get all inventory locations.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<InventoryLocationDto>>>> GetLocations([FromQuery] Guid? branchId)
    {
        var query = new GetInventoryLocationsQuery { BranchId = branchId };
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<InventoryLocationDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Get an inventory location by id.
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<InventoryLocationDto>>> GetLocationById(Guid id)
    {
        var query = new GetInventoryLocationsQuery { BranchId = null };
        var result = await Mediator.Send(query);
        var location = result.FirstOrDefault(x => x.Id == id);
        
        if (location == null)
        {
            return NotFound(ApiResponse<InventoryLocationDto>.ErrorResponse("Location not found.", null, 404));
        }
        
        return Ok(ApiResponse<InventoryLocationDto>.SuccessResponse(location));
    }
}
