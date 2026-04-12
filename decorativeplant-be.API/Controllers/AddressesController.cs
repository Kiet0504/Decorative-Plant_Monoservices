using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Interfaces;

namespace decorativeplant_be.API.Controllers;

public class AddressDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AddressDetail { get; set; } = string.Empty;
    public int ProvinceId { get; set; }
    public string ProvinceName { get; set; } = string.Empty;
    public int DistrictId { get; set; }
    public string DistrictName { get; set; } = string.Empty;
    public string WardCode { get; set; } = string.Empty;
    public string WardName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

[Authorize]
[ApiController]
[Route("api/v1/users/me/addresses")]
public class AddressesController : BaseController
{
    private readonly IApplicationDbContext _context;

    public AddressesController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<AddressDto>>>> GetAddresses()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var user = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));
        if (user == null) return NotFound(ApiResponse<List<AddressDto>>.ErrorResponse("User not found"));

        var addresses = new List<AddressDto>();
        if (user.Addresses != null)
        {
            addresses = JsonSerializer.Deserialize<List<AddressDto>>(user.Addresses.RootElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<AddressDto>();
        }

        return Ok(ApiResponse<List<AddressDto>>.SuccessResponse(addresses));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<AddressDto>>> AddAddress([FromBody] AddressDto request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var user = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));
        if (user == null) return NotFound(ApiResponse<AddressDto>.ErrorResponse("User not found"));

        var addresses = new List<AddressDto>();
        if (user.Addresses != null)
        {
            addresses = JsonSerializer.Deserialize<List<AddressDto>>(user.Addresses.RootElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<AddressDto>();
        }

        if (string.IsNullOrEmpty(request.Id))
        {
            request.Id = Guid.NewGuid().ToString();
        }

        if (addresses.Count == 0)
        {
            request.IsDefault = true;
        }
        else if (request.IsDefault)
        {
            foreach (var a in addresses) a.IsDefault = false;
        }

        addresses.Add(request);

        user.Addresses = JsonSerializer.SerializeToDocument(addresses, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await _context.SaveChangesAsync(new CancellationToken());

        return Ok(ApiResponse<AddressDto>.SuccessResponse(request));
    }

    [HttpPut("{id}")]
    [HttpPatch("{id}")]
    public async Task<ActionResult<ApiResponse<AddressDto>>> UpdateAddress(string id, [FromBody] AddressDto request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var user = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));
        if (user == null) return NotFound(ApiResponse<AddressDto>.ErrorResponse("User not found"));

        var addresses = new List<AddressDto>();
        if (user.Addresses != null)
        {
            addresses = JsonSerializer.Deserialize<List<AddressDto>>(user.Addresses.RootElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<AddressDto>();
        }

        var existing = addresses.FirstOrDefault(a => a.Id == id);
        if (existing == null) return NotFound(ApiResponse<AddressDto>.ErrorResponse("Address not found"));

        existing.Name = request.Name;
        existing.Phone = request.Phone;
        existing.AddressDetail = request.AddressDetail;
        existing.ProvinceId = request.ProvinceId;
        existing.ProvinceName = request.ProvinceName;
        existing.DistrictId = request.DistrictId;
        existing.DistrictName = request.DistrictName;
        existing.WardCode = request.WardCode;
        existing.WardName = request.WardName;

        if (request.IsDefault && !existing.IsDefault)
        {
            foreach (var a in addresses) a.IsDefault = false;
            existing.IsDefault = true;
        }
        else if (!request.IsDefault && existing.IsDefault)
        {
            existing.IsDefault = false;
        }

        user.Addresses = JsonSerializer.SerializeToDocument(addresses, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await _context.SaveChangesAsync(new CancellationToken());

        return Ok(ApiResponse<AddressDto>.SuccessResponse(existing));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteAddress(string id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var user = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));
        if (user == null) return NotFound(ApiResponse<bool>.ErrorResponse("User not found"));

        var addresses = new List<AddressDto>();
        if (user.Addresses != null)
        {
            addresses = JsonSerializer.Deserialize<List<AddressDto>>(user.Addresses.RootElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<AddressDto>();
        }

        var count = addresses.RemoveAll(a => a.Id == id);
        if (count == 0) return NotFound(ApiResponse<bool>.ErrorResponse("Address not found"));

        if (addresses.Count > 0 && !addresses.Any(a => a.IsDefault))
        {
            addresses.First().IsDefault = true;
        }

        user.Addresses = JsonSerializer.SerializeToDocument(addresses, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await _context.SaveChangesAsync(new CancellationToken());

        return Ok(ApiResponse<bool>.SuccessResponse(true));
    }
}
