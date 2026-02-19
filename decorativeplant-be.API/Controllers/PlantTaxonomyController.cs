using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Features.PlantLibrary.Commands;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using decorativeplant_be.Application.Features.PlantLibrary.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/plant-library/taxonomies")]
[Authorize]
public class PlantTaxonomyController : BaseController
{
    /// <summary>
    /// Create a new plant taxonomy (species).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ApiResponse<PlantTaxonomyDto>>> Create([FromBody] CreatePlantTaxonomyCommand command)
    {
        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<PlantTaxonomyDto>.SuccessResponse(result, "Plant taxonomy created successfully.", 201));
    }

    /// <summary>
    /// Update an existing plant taxonomy.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ApiResponse<PlantTaxonomyDto>>> Update(Guid id, [FromBody] UpdatePlantTaxonomyCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest(ApiResponse<PlantTaxonomyDto>.ErrorResponse("ID mismatch."));
        }

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<PlantTaxonomyDto>.SuccessResponse(result, "Plant taxonomy updated successfully."));
    }

    /// <summary>
    /// Delete a plant taxonomy.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id)
    {
        await Mediator.Send(new DeletePlantTaxonomyCommand { Id = id });
        return Ok(ApiResponse<object>.SuccessResponse(null, "Plant taxonomy deleted successfully."));
    }

    /// <summary>
    /// Get a plant taxonomy by ID.
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PlantTaxonomyDto>>> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetPlantTaxonomyQuery { Id = id });
        return Ok(ApiResponse<PlantTaxonomyDto>.SuccessResponse(result, "Plant taxonomy detailed retrieved."));
    }

    /// <summary>
    /// List plant taxonomies with pagination and filtering.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResultDto<PlantTaxonomySummaryDto>>>> List([FromQuery] ListPlantTaxonomiesQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResultDto<PlantTaxonomySummaryDto>>.SuccessResponse(result, "Plant taxonomies retrieved."));
    }
}
