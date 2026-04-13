using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using decorativeplant_be.Application.Features.PlantLibrary.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/plant-categories")]
public class PlantCategoryController : BaseController
{
    /// <summary>
    /// List high-level plant categories (Indoor, Outdoor, Succulents, etc.)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<PlantCategoryDto>>>> List([FromQuery] ListPlantCategoriesQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResult<PlantCategoryDto>>.SuccessResponse(result, "Plant categories retrieved successfully."));
    }
}
