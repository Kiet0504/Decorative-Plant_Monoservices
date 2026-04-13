using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Features.Garden.Commands;
using decorativeplant_be.Application.Features.Garden.Queries;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Infrastructure.Auth;

namespace decorativeplant_be.API.Controllers;

/// <summary>
/// API for managing user's garden plants (My Garden feature).
/// </summary>
[ApiController]
[Route("api/garden")]
[Authorize]
public class GardenController : BaseController
{
    private readonly IDistributedCache _cache;
    private readonly GoogleOAuthService _googleOAuthService;
    private readonly GoogleCalendarService _googleCalendarService;
    private readonly GoogleOAuthSettings _google;
    private readonly FrontendSettings _frontend;
    private readonly IUserAccountService _userAccountService;

    public GardenController(
        IDistributedCache cache,
        GoogleOAuthService googleOAuthService,
        GoogleCalendarService googleCalendarService,
        IOptions<GoogleOAuthSettings> google,
        IOptions<FrontendSettings> frontend,
        IUserAccountService userAccountService)
    {
        _cache = cache;
        _googleOAuthService = googleOAuthService;
        _googleCalendarService = googleCalendarService;
        _google = google.Value;
        _frontend = frontend.Value;
        _userAccountService = userAccountService;
    }

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

    /// <summary>Generate an AI schedule plan (tasks + next due dates) for a plant.</summary>
    [HttpGet("plants/{id:guid}/ai-schedule-plan")]
    public async Task<ActionResult<ApiResponse<AiSchedulePlanDto>>> GetAiSchedulePlan(
        Guid id,
        [FromQuery] int horizonDays = 30,
        [FromQuery] DateTime? startAtUtc = null,
        [FromQuery] int? utcOffsetMinutes = null)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<AiSchedulePlanDto>.ErrorResponse("User ID is required."));
        }

        var result = await Mediator.Send(new GenerateGardenPlantAiSchedulePlanQuery
        {
            UserId = userId.Value,
            PlantId = id,
            HorizonDays = horizonDays,
            StartAtUtc = startAtUtc,
            UtcOffsetMinutes = utcOffsetMinutes
        });

        return Ok(ApiResponse<AiSchedulePlanDto>.SuccessResponse(result));
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

    /// <summary>Create a care schedule for a plant.</summary>
    [HttpPost("plants/{id:guid}/care-schedules")]
    public async Task<ActionResult<ApiResponse<CareScheduleDto>>> CreateCareSchedule(Guid id, [FromBody] CreateCareScheduleRequest request)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<CareScheduleDto>.ErrorResponse("User ID is required."));
        }

        var command = new CreateCareScheduleCommand
        {
            UserId = userId.Value,
            PlantId = id,
            TaskInfo = request.TaskInfo,
            IsActive = request.IsActive
        };

        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<CareScheduleDto>.SuccessResponse(result, "Schedule created.", 201));
    }

    /// <summary>Create multiple care schedules for a plant.</summary>
    [HttpPost("plants/{id:guid}/care-schedules/bulk")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CareScheduleDto>>>> BulkCreateCareSchedules(Guid id, [FromBody] BulkCreateCareSchedulesRequest request)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<IReadOnlyList<CareScheduleDto>>.ErrorResponse("User ID is required."));
        }

        var command = new BulkCreateCareSchedulesCommand
        {
            UserId = userId.Value,
            PlantId = id,
            Tasks = request.Tasks ?? new List<CareScheduleTaskInfoDto>()
        };

        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<IReadOnlyList<CareScheduleDto>>.SuccessResponse(result, "Schedules created.", 201));
    }

    /// <summary>Update a care schedule (task info or active flag).</summary>
    [HttpPatch("care-schedules/{scheduleId:guid}")]
    public async Task<ActionResult<ApiResponse<CareScheduleDto>>> UpdateCareSchedule(Guid scheduleId, [FromBody] UpdateCareScheduleRequest request)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<CareScheduleDto>.ErrorResponse("User ID is required."));
        }

        var command = new UpdateCareScheduleCommand
        {
            UserId = userId.Value,
            ScheduleId = scheduleId,
            TaskInfo = request.TaskInfo,
            IsActive = request.IsActive
        };

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<CareScheduleDto>.SuccessResponse(result, "Schedule updated."));
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

    /// <summary>Return Google Calendar connection status for current user.</summary>
    [HttpGet("google-calendar/status")]
    public async Task<ActionResult<ApiResponse<object>>> GetGoogleCalendarStatus(CancellationToken cancellationToken)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("User ID is required."));
        }

        var tokenJson = await _cache.GetStringAsync(CacheKeys.GoogleCalendarToken(userId.Value), cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenJson))
        {
            return Ok(ApiResponse<object>.SuccessResponse(new { connected = false }));
        }

        GoogleCalendarTokenCacheEntry? token;
        try { token = JsonSerializer.Deserialize<GoogleCalendarTokenCacheEntry>(tokenJson); }
        catch { token = null; }

        if (token == null || string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            return Ok(ApiResponse<object>.SuccessResponse(new { connected = false }));
        }

        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            connected = true,
            scope = token.Scope,
            connectedAtUtc = token.ConnectedAtUtc
        }));
    }

    /// <summary>Start OAuth connect flow for Google Calendar.</summary>
    [HttpGet("google-calendar/connect/start")]
    public async Task<ActionResult<ApiResponse<object>>> StartGoogleCalendarConnect([FromQuery] string? returnTo, CancellationToken cancellationToken)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("User ID is required."));
        }

        if (string.IsNullOrWhiteSpace(_google.ClientId) ||
            string.IsNullOrWhiteSpace(_google.ClientSecret) ||
            string.IsNullOrWhiteSpace(_google.BaseUrl) ||
            string.IsNullOrWhiteSpace(_frontend.BaseUrl))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "Google OAuth is not configured. Set GOOGLE__ClientId, GOOGLE__ClientSecret, GOOGLE__BaseUrl, and FRONTEND__BaseUrl.",
                statusCode: 400));
        }

        var state = Guid.NewGuid().ToString("N");
        var redirectUri = $"{_google.BaseUrl.TrimEnd('/')}/garden/google-calendar/connect/callback";
        var safeReturnTo = string.IsNullOrWhiteSpace(returnTo) || !returnTo.StartsWith("/", StringComparison.Ordinal)
            ? "/my-plants"
            : returnTo.Trim();

        var payload = JsonSerializer.Serialize(new GoogleCalendarStatePayload(userId.Value, safeReturnTo));
        await _cache.SetStringAsync(
            CacheKeys.GoogleCalendarState(state),
            payload,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) },
            cancellationToken);

        var scope = "openid email profile https://www.googleapis.com/auth/calendar.events";
        var url = _googleOAuthService.BuildAuthorizeUrl(redirectUri, state, scope);
        return Ok(ApiResponse<object>.SuccessResponse(new { url }));
    }

    /// <summary>OAuth callback for Google Calendar connect flow.</summary>
    [AllowAnonymous]
    [HttpGet("google-calendar/connect/callback")]
    public async Task<IActionResult> GoogleCalendarConnectCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery] string? error_description,
        CancellationToken cancellationToken)
    {
        var frontendBase = _frontend.BaseUrl.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(error))
        {
            var err = Uri.EscapeDataString(error);
            var desc = Uri.EscapeDataString(error_description ?? string.Empty);
            return Redirect($"{frontendBase}/my-plants?gcError={err}&gcErrorDescription={desc}");
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return Redirect($"{frontendBase}/my-plants?gcError=missing_code_or_state");
        }

        var stateJson = await _cache.GetStringAsync(CacheKeys.GoogleCalendarState(state), cancellationToken);
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            return Redirect($"{frontendBase}/my-plants?gcError=invalid_or_expired_state");
        }

        await _cache.RemoveAsync(CacheKeys.GoogleCalendarState(state), cancellationToken);

        GoogleCalendarStatePayload? statePayload;
        try { statePayload = JsonSerializer.Deserialize<GoogleCalendarStatePayload>(stateJson); }
        catch { statePayload = null; }

        if (statePayload == null)
        {
            return Redirect($"{frontendBase}/my-plants?gcError=invalid_state_payload");
        }

        var user = await _userAccountService.GetByIdAsync(statePayload.UserId, cancellationToken);
        if (user == null)
        {
            return Redirect($"{frontendBase}/my-plants?gcError=user_not_found");
        }

        try
        {
            var redirectUri = $"{_google.BaseUrl.TrimEnd('/')}/garden/google-calendar/connect/callback";
            var tokens = await _googleOAuthService.ExchangeCodeForOAuthTokenAsync(code, redirectUri, cancellationToken);

            if (!string.IsNullOrWhiteSpace(tokens.IdToken))
            {
                var identity = await _googleOAuthService.VerifyIdTokenAsync(tokens.IdToken, cancellationToken);
                if (!string.Equals(identity.Email, user.Email, StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect($"{frontendBase}{statePayload.ReturnTo}?gcError=email_mismatch");
                }
            }

            if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
            {
                // Google may omit refresh_token on subsequent consents; keep existing token if present.
                var existingJson = await _cache.GetStringAsync(CacheKeys.GoogleCalendarToken(statePayload.UserId), cancellationToken);
                GoogleCalendarTokenCacheEntry? existing = null;
                try { existing = string.IsNullOrWhiteSpace(existingJson) ? null : JsonSerializer.Deserialize<GoogleCalendarTokenCacheEntry>(existingJson); }
                catch { existing = null; }
                if (existing != null && !string.IsNullOrWhiteSpace(existing.RefreshToken))
                {
                    tokens = tokens with { RefreshToken = existing.RefreshToken };
                }
            }

            if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
            {
                return Redirect($"{frontendBase}{statePayload.ReturnTo}?gcError=missing_refresh_token");
            }

            var cacheEntry = new GoogleCalendarTokenCacheEntry(
                RefreshToken: tokens.RefreshToken!,
                Scope: tokens.Scope,
                ConnectedAtUtc: DateTime.UtcNow);

            await _cache.SetStringAsync(
                CacheKeys.GoogleCalendarToken(statePayload.UserId),
                JsonSerializer.Serialize(cacheEntry),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(180) },
                cancellationToken);

            return Redirect($"{frontendBase}{statePayload.ReturnTo}?gc=connected");
        }
        catch
        {
            return Redirect($"{frontendBase}{statePayload.ReturnTo}?gcError=oauth_exchange_failed");
        }
    }

    /// <summary>Push care reminders to Google Calendar (primary calendar).</summary>
    [HttpPost("google-calendar/sync")]
    public async Task<ActionResult<ApiResponse<object>>> SyncToGoogleCalendar(
        [FromBody] GoogleCalendarSyncRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("User ID is required."));
        }

        if (request.Events == null || request.Events.Count == 0)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("At least one event is required.", statusCode: 400));
        }

        var tokenJson = await _cache.GetStringAsync(CacheKeys.GoogleCalendarToken(userId.Value), cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenJson))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Google Calendar is not connected.", statusCode: 400));
        }

        GoogleCalendarTokenCacheEntry? token;
        try { token = JsonSerializer.Deserialize<GoogleCalendarTokenCacheEntry>(tokenJson); }
        catch { token = null; }

        if (token == null || string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Google Calendar token is invalid.", statusCode: 400));
        }

        GoogleOAuthTokenResult refreshed;
        try
        {
            refreshed = await _googleOAuthService.RefreshAccessTokenAsync(token.RefreshToken, cancellationToken);
        }
        catch
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "Could not refresh Google Calendar access. Please reconnect Google Calendar.",
                statusCode: 400));
        }

        if (!string.IsNullOrWhiteSpace(refreshed.RefreshToken) &&
            !string.Equals(refreshed.RefreshToken, token.RefreshToken, StringComparison.Ordinal))
        {
            var updated = token with { RefreshToken = refreshed.RefreshToken! };
            await _cache.SetStringAsync(
                CacheKeys.GoogleCalendarToken(userId.Value),
                JsonSerializer.Serialize(updated),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(180) },
                cancellationToken);
        }

        var inputs = request.Events
            .Where(e => !string.IsNullOrWhiteSpace(e.EventId)
                        && !string.IsNullOrWhiteSpace(e.Title)
                        && e.StartUtc != default
                        && e.EndUtc != default
                        && e.EndUtc > e.StartUtc)
            .Select(e => new GoogleCalendarEventInput(
                EventId: NormalizeGoogleEventId(e.EventId!),
                Summary: e.Title!.Trim(),
                StartUtc: DateTime.SpecifyKind(e.StartUtc, DateTimeKind.Utc),
                EndUtc: DateTime.SpecifyKind(e.EndUtc, DateTimeKind.Utc),
                Description: string.IsNullOrWhiteSpace(e.Description) ? null : e.Description.Trim()))
            .ToList();

        if (inputs.Count == 0)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("No valid events to sync.", statusCode: 400));
        }

        var result = await _googleCalendarService.UpsertEventsAsync(
            refreshed.AccessToken,
            inputs,
            cancellationToken);

        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            created = result.Created,
            skipped = result.Skipped,
            failed = result.Failed,
            errors = result.Errors
        }, "Google Calendar sync completed."));
    }

    private static string NormalizeGoogleEventId(string raw)
    {
        var cleaned = new string(raw.ToLowerInvariant().Where(ch =>
            (ch >= 'a' && ch <= 'z') ||
            (ch >= '0' && ch <= '9') ||
            ch == '-' || ch == '_').ToArray());
        if (cleaned.Length < 5) cleaned = $"dp-{cleaned.PadRight(2, '0')}";
        if (cleaned.Length > 128) cleaned = cleaned.Substring(0, 128);
        return cleaned;
    }

    private static class CacheKeys
    {
        public static string GoogleCalendarState(string state) => $"google_calendar_state:{state}";
        public static string GoogleCalendarToken(Guid userId) => $"google_calendar_token:{userId:N}";
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
    /// <summary>Preset slug or free-text care label.</summary>
    public string ActionType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public object? Products { get; set; }
    public string? Observations { get; set; }
    /// <summary>Optional: preset slug or free-text mood.</summary>
    public string? Mood { get; set; }
    public DateTime? PerformedAt { get; set; }
    public List<CareLogImageDto>? Images { get; set; }
}

public class CreateCareScheduleRequest
{
    public CareScheduleTaskInfoDto TaskInfo { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class BulkCreateCareSchedulesRequest
{
    public List<CareScheduleTaskInfoDto>? Tasks { get; set; }
}

public class UpdateCareScheduleRequest
{
    public CareScheduleTaskInfoDto? TaskInfo { get; set; }
    public bool? IsActive { get; set; }
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

public sealed record GoogleCalendarStatePayload(Guid UserId, string ReturnTo);
public sealed record GoogleCalendarTokenCacheEntry(string RefreshToken, string? Scope, DateTime ConnectedAtUtc);

public class GoogleCalendarSyncRequest
{
    public List<GoogleCalendarSyncEventRequest>? Events { get; set; }
}

public class GoogleCalendarSyncEventRequest
{
    public string? EventId { get; set; }
    public string? Title { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string? Description { get; set; }
}
