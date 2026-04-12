using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Features.Garden.Commands;
using decorativeplant_be.Application.Features.Garden.Queries;

namespace decorativeplant_be.API.Controllers;

/// <summary>
/// API for managing user's garden plants (My Garden feature).
/// </summary>
[ApiController]
[Route("api/garden")]
[Authorize]
public class GardenController : BaseController
{
    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>Create a new garden plant.</summary>
    [HttpPost("plants")]
    public async Task<ActionResult<ApiResponse<GardenPlantDto>>> CreatePlant([FromBody] CreateGardenPlantCommand command)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<GardenPlantDto>.ErrorResponse("User ID is required."));
        }
        command.UserId = userId.Value;

        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<GardenPlantDto>.SuccessResponse(result, "Plant created successfully.", 201));
    }

    /// <summary>
    /// Import garden plants from purchased order items.
    /// </summary>
    [HttpPost("plants/from-purchase")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<GardenPlantDto>>>> ImportFromPurchase([FromBody] ImportFromPurchaseRequest request)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<IReadOnlyList<GardenPlantDto>>.ErrorResponse("User ID is required."));
        }

        var command = new ImportGardenPlantsFromPurchaseCommand
        {
            UserId = userId.Value,
            OrderItemIds = request.OrderItemIds ?? new List<Guid>(),
            CreateMode = request.CreateMode,
            Nickname = request.Nickname,
            Location = request.Location,
            AdoptedDate = request.AdoptedDate,
            ImageUrl = request.ImageUrl,
            Health = request.Health,
            Size = request.Size
        };

        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<IReadOnlyList<GardenPlantDto>>.SuccessResponse(result, "Plants imported successfully.", 201));
    }

    /// <summary>Preview taxonomy resolved from a purchase order line (same rules as import-from-purchase).</summary>
    [HttpGet("purchase-preview/taxonomy/{orderItemId:guid}")]
    public async Task<ActionResult<ApiResponse<OrderItemTaxonomyPreviewDto>>> GetPurchaseTaxonomyPreview(Guid orderItemId)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<OrderItemTaxonomyPreviewDto>.ErrorResponse("User ID is required."));
        }

        var result = await Mediator.Send(new GetOrderItemTaxonomyPreviewQuery
        {
            UserId = userId.Value,
            OrderItemId = orderItemId,
        });
        return Ok(ApiResponse<OrderItemTaxonomyPreviewDto>.SuccessResponse(result));
    }

    /// <summary>List garden plants for the current user.</summary>
    [HttpGet("plants")]
    public async Task<ActionResult<ApiResponse<PagedResultDto<GardenPlantDto>>>> ListPlants(
        [FromQuery] bool includeArchived = false,
        [FromQuery] string? health = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<PagedResultDto<GardenPlantDto>>.ErrorResponse("User ID is required."));
        }

        var query = new ListGardenPlantsQuery
        {
            UserId = userId.Value,
            IncludeArchived = includeArchived,
            HealthFilter = health,
            Page = page,
            PageSize = pageSize
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResultDto<GardenPlantDto>>.SuccessResponse(result));
    }

    /// <summary>Get a single garden plant by ID.</summary>
    [HttpGet("plants/{id:guid}")]
    public async Task<ActionResult<ApiResponse<GardenPlantDto>>> GetPlant(Guid id)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<GardenPlantDto>.ErrorResponse("User ID is required."));
        }

        var query = new GetGardenPlantQuery { UserId = userId.Value, Id = id };
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<GardenPlantDto>.SuccessResponse(result));
    }

    /// <summary>Get plant profile (plant + taxonomy + recent logs + schedules).</summary>
    [HttpGet("plants/{id:guid}/profile")]
    public async Task<ActionResult<ApiResponse<PlantProfileDto>>> GetPlantProfile(
        Guid id,
        [FromQuery] int recentLogsLimit = 5,
        [FromQuery] bool includeArchivedSchedules = false)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<PlantProfileDto>.ErrorResponse("User ID is required."));
        }

        var query = new GetGardenPlantProfileQuery
        {
            UserId = userId.Value,
            PlantId = id,
            RecentLogsLimit = recentLogsLimit,
            IncludeArchivedSchedules = includeArchivedSchedules
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PlantProfileDto>.SuccessResponse(result));
    }

    /// <summary>Generate or fetch cached AI care advice for a plant.</summary>
    [HttpGet("plants/{id:guid}/ai-care")]
    public async Task<ActionResult<ApiResponse<AiCareAdviceDto>>> GetAiCareAdvice(
        Guid id,
        [FromQuery] bool force = false)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<AiCareAdviceDto>.ErrorResponse("User ID is required."));
        }

        var result = await Mediator.Send(new GenerateGardenPlantAiCareAdviceQuery
        {
            UserId = userId.Value,
            PlantId = id,
            Force = force
        });

        return Ok(ApiResponse<AiCareAdviceDto>.SuccessResponse(result));
    }

    /// <summary>Get growth gallery (photo diary) for a plant.</summary>
    [HttpGet("plants/{id:guid}/gallery")]
    public async Task<ActionResult<ApiResponse<GrowthTimelineDto>>> GetGrowthGallery(
        Guid id,
        [FromQuery] DateTime? before = null,
        [FromQuery] int limit = 20)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<GrowthTimelineDto>.ErrorResponse("User ID is required."));
        }

        var query = new GetGrowthGalleryQuery
        {
            UserId = userId.Value,
            PlantId = id,
            Before = before,
            Limit = limit
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<GrowthTimelineDto>.SuccessResponse(result));
    }

    /// <summary>Add a growth photo entry (stored as care log with images).</summary>
    [HttpPost("plants/{id:guid}/gallery")]
    public async Task<ActionResult<ApiResponse<GrowthPhotoEntryDto>>> AddGrowthPhoto(Guid id, [FromBody] AddGrowthPhotoRequest request)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<GrowthPhotoEntryDto>.ErrorResponse("User ID is required."));
        }

        var command = new AddGrowthPhotoCommand
        {
            UserId = userId.Value,
            PlantId = id,
            ImageUrl = request.ImageUrl,
            Caption = request.Caption,
            SetAsAvatar = request.SetAsAvatar,
            PerformedAt = request.PerformedAt
        };

        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<GrowthPhotoEntryDto>.SuccessResponse(result, "Photo added.", 201));
    }

    /// <summary>Update a garden plant.</summary>
    [HttpPut("plants/{id:guid}")]
    public async Task<ActionResult<ApiResponse<GardenPlantDto>>> UpdatePlant(Guid id, [FromBody] UpdateGardenPlantCommand command)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<GardenPlantDto>.ErrorResponse("User ID is required."));
        }

        command.UserId = userId.Value;
        command.Id = id;

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<GardenPlantDto>.SuccessResponse(result, "Plant updated successfully."));
    }

    /// <summary>Delete (archive or permanent) a garden plant.</summary>
    [HttpDelete("plants/{id:guid}")]
    public async Task<IActionResult> DeletePlant(Guid id, [FromQuery] bool permanent = false)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("User ID is required."));
        }

        await Mediator.Send(new DeleteGardenPlantCommand { UserId = userId.Value, Id = id, Permanent = permanent });
        return NoContent();
    }

    /// <summary>Update only the health status of a garden plant.</summary>
    [HttpPatch("plants/{id:guid}/health")]
    public async Task<ActionResult<ApiResponse<GardenPlantDto>>> UpdatePlantHealth(Guid id, [FromBody] UpdatePlantHealthRequest request)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<GardenPlantDto>.ErrorResponse("User ID is required."));
        }

        var command = new UpdatePlantHealthCommand { UserId = userId.Value, Id = id, Health = request.Health };
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<GardenPlantDto>.SuccessResponse(result, "Health updated successfully."));
    }

    /// <summary>Add a growth milestone to a plant.</summary>
    [HttpPost("plants/{id:guid}/milestones")]
    public async Task<ActionResult<ApiResponse<GrowthMilestoneDto>>> AddMilestone(Guid id, [FromBody] AddGrowthMilestoneRequest request)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<GrowthMilestoneDto>.ErrorResponse("User ID is required."));
        }

        var command = new AddGrowthMilestoneCommand
        {
            UserId = userId.Value,
            PlantId = id,
            Type = request.Type,
            OccurredAt = request.OccurredAt,
            Notes = request.Notes,
            ImageUrl = request.ImageUrl
        };
        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<GrowthMilestoneDto>.SuccessResponse(result, "Milestone added.", 201));
    }

    /// <summary>Update a growth milestone.</summary>
    [HttpPut("plants/{id:guid}/milestones/{milestoneId:guid}")]
    public async Task<ActionResult<ApiResponse<GrowthMilestoneDto>>> UpdateMilestone(Guid id, Guid milestoneId, [FromBody] UpdateGrowthMilestoneRequest request)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<GrowthMilestoneDto>.ErrorResponse("User ID is required."));
        }

        var command = new UpdateGrowthMilestoneCommand
        {
            UserId = userId.Value,
            PlantId = id,
            MilestoneId = milestoneId,
            Type = request.Type,
            OccurredAt = request.OccurredAt,
            Notes = request.Notes,
            ImageUrl = request.ImageUrl
        };
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<GrowthMilestoneDto>.SuccessResponse(result, "Milestone updated."));
    }

    /// <summary>Add a care log (photo diary entry) to a plant.</summary>
    [HttpPost("plants/{id:guid}/care-logs")]
    public async Task<ActionResult<ApiResponse<CareLogDto>>> AddCareLog(Guid id, [FromBody] AddCareLogRequest request)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<CareLogDto>.ErrorResponse("User ID is required."));
        }

        var command = new AddCareLogCommand
        {
            UserId = userId.Value,
            PlantId = id,
            ScheduleId = request.ScheduleId,
            ActionType = request.ActionType,
            Description = request.Description,
            Products = request.Products,
            Observations = request.Observations,
            Mood = request.Mood,
            PerformedAt = request.PerformedAt,
            Images = request.Images
        };
        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<CareLogDto>.SuccessResponse(result, "Care log added.", 201));
    }

    /// <summary>List care logs for a plant.</summary>
    [HttpGet("plants/{id:guid}/care-logs")]
    public async Task<ActionResult<ApiResponse<object>>> GetCareLogs(Guid id)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("User ID is required."));
        }

        var query = new GetCareLogsQuery { UserId = userId.Value, PlantId = id };
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<object>.SuccessResponse(new { items = result }));
    }

    /// <summary>Get a single care log by ID.</summary>
    [HttpGet("plants/{id:guid}/care-logs/{logId:guid}")]
    public async Task<ActionResult<ApiResponse<CareLogDto>>> GetCareLog(Guid id, Guid logId)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<CareLogDto>.ErrorResponse("User ID is required."));
        }

        var query = new GetCareLogQuery { UserId = userId.Value, PlantId = id, LogId = logId };
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<CareLogDto>.SuccessResponse(result));
    }

    /// <summary>Remove a growth milestone.</summary>
    [HttpDelete("plants/{id:guid}/milestones/{milestoneId:guid}")]
    public async Task<IActionResult> RemoveMilestone(Guid id, Guid milestoneId)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("User ID is required."));
        }

        await Mediator.Send(new RemoveGrowthMilestoneCommand { UserId = userId.Value, PlantId = id, MilestoneId = milestoneId });
        return NoContent();
    }

    /// <summary>Get merged timeline (care logs, milestones, diagnoses) for a plant.</summary>
    [HttpGet("plants/{id:guid}/timeline")]
    public async Task<ActionResult<ApiResponse<object>>> GetTimeline(
        Guid id,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int limit = 50)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("User ID is required."));
        }

        var query = new GetGardenTimelineQuery
        {
            UserId = userId.Value,
            PlantId = id,
            From = from,
            To = to,
            Limit = limit
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<object>.SuccessResponse(new { items = result }));
    }
}

/// <summary>Request body for PATCH health update.</summary>
public class UpdatePlantHealthRequest
{
    public string Health { get; set; } = string.Empty;
}

/// <summary>Request body for adding a growth milestone.</summary>
public class AddGrowthMilestoneRequest
{
    public string Type { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string? Notes { get; set; }
    public string? ImageUrl { get; set; }
}

/// <summary>Request body for updating a growth milestone.</summary>
public class UpdateGrowthMilestoneRequest
{
    public string? Type { get; set; }
    public DateTime? OccurredAt { get; set; }
    public string? Notes { get; set; }
    public string? ImageUrl { get; set; }
}

/// <summary>Request body for adding a care log.</summary>
public class AddCareLogRequest
{
    public Guid? ScheduleId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public object? Products { get; set; }
    public string? Observations { get; set; }
    public string? Mood { get; set; }
    public DateTime? PerformedAt { get; set; }
    public List<CareLogImageDto>? Images { get; set; }
}

public class ImportFromPurchaseRequest
{
    public List<Guid>? OrderItemIds { get; set; }

    public PurchaseImportCreateMode CreateMode { get; set; } = PurchaseImportCreateMode.OnePerItem;

    public string? Nickname { get; set; }

    public string? Location { get; set; }

    public string? AdoptedDate { get; set; }

    public string? ImageUrl { get; set; }

    public string? Health { get; set; }

    public string? Size { get; set; }
}

public class AddGrowthPhotoRequest
{
    public string ImageUrl { get; set; } = string.Empty;

    public string? Caption { get; set; }

    public bool SetAsAvatar { get; set; } = false;

    public DateTime? PerformedAt { get; set; }
}
