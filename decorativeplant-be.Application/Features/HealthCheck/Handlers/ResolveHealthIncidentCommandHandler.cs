using System.Text.Json;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using decorativeplant_be.Application.Features.HealthCheck.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;

using decorativeplant_be.Application.Services;
using decorativeplant_be.Application.Common.DTOs.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Application.Features.HealthCheck.Handlers;

public class ResolveHealthIncidentCommandHandler : IRequestHandler<ResolveHealthIncidentCommand, HealthIncidentDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<ResolveHealthIncidentCommandHandler> _logger;

    public ResolveHealthIncidentCommandHandler(
        IRepositoryFactory repositoryFactory, 
        IUnitOfWork unitOfWork,
        IApplicationDbContext context,
        IEmailService emailService,
        ILogger<ResolveHealthIncidentCommandHandler> logger)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<HealthIncidentDto> Handle(ResolveHealthIncidentCommand request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<HealthIncident>();
        var entity = await repo.GetByIdAsync(request.Id, cancellationToken);
        
        if (entity == null)
        {
            throw new NotFoundException(nameof(HealthIncident), request.Id);
        }

        // Update Description with Resolution Notes or append?
        // Let's append to Description or keep separate if entity had Resolution field. 
        // Entity doesn't have explicit Resolution field, so we might store it in Details or append to Description.
        // Let's append to description for now or assume TreatmentDetails holds it.
        // DTO has ResolutionNotes but entity doesn't. We'll put it in Details or TreatmentDetails.
        
        // Update Treatment Info
        if (request.TreatmentDetails != null)
        {
            entity.TreatmentInfo = HealthIncidentMapper.BuildJson(request.TreatmentDetails);
        }

        // Update Status Info
        // We need to preserve existing info (like reported_at) or merge.
        // Since we are using JsonDocument, we must deserialize, update, serialize.
        if (request.ImageUrls != null && request.ImageUrls.Count > 0)
        {
            var imagesPayload = new { urls = request.ImageUrls };
            entity.Images = JsonSerializer.SerializeToDocument(imagesPayload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        }

        var statusDict = new Dictionary<string, object>();
        if (entity.StatusInfo != null)
        {
            try 
            {
                var existing = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.StatusInfo.RootElement.GetRawText());
                if (existing != null) statusDict = existing;
            } 
            catch { }
        }

        if (request.Status == "Resolved" && !request.IsManagerApproval)
        {
            statusDict["status"] = "Pending Approval";
        }
        else
        {
            statusDict["status"] = request.Status ?? "Pending Approval";
        }
        
        statusDict["resolved_at"] = request.ResolvedAt ?? DateTime.UtcNow;
        if (request.ResolvedBy.HasValue)
        {
            statusDict["resolved_by"] = request.ResolvedBy.Value.ToString();
        }
        
        entity.StatusInfo = HealthIncidentMapper.BuildJson(statusDict);

        await repo.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fetch relations if needed
        if (entity.BatchId.HasValue && entity.Batch == null)
        {
            entity.Batch = await _context.PlantBatches
                .Include(b => b.Branch)
                .Include(b => b.Taxonomy)
                .FirstOrDefaultAsync(b => b.Id == entity.BatchId.Value, cancellationToken);
        }

        // Send Email to Branch Manager if Pending Approval
        if (entity.Batch?.BranchId != null && statusDict.ContainsKey("status") && statusDict["status"].ToString() == "Pending Approval")
        {
            try
            {
                var branchId = entity.Batch.BranchId.Value;
                var branchManagerEmails = await _context.UserAccounts
                    .Where(u => u.Role == "branch_manager" 
                             && u.IsActive 
                             && !string.IsNullOrEmpty(u.Email))
                    .Select(u => u.Email)
                    .ToListAsync(cancellationToken);

                if (branchManagerEmails.Any())
                {
                    string taxonomyTitleVi = entity.Batch.Taxonomy?.CommonNames?.RootElement.TryGetProperty("en", out var enName) == true ? enName.GetString() ?? "N/A" : "N/A";
                    string treatmentInfo = request.TreatmentDetails != null ? JsonSerializer.Serialize(request.TreatmentDetails) : "None";

                    string imageUrl = "";
                    if (entity.Images != null)
                    {
                        try 
                        {
                            if (entity.Images.RootElement.TryGetProperty("urls", out var urls) && urls.ValueKind == JsonValueKind.Array && urls.GetArrayLength() > 0)
                            {
                                imageUrl = urls[0].GetString() ?? "";
                            }
                            else if (entity.Images.RootElement.ValueKind == JsonValueKind.Array && entity.Images.RootElement.GetArrayLength() > 0)
                            {
                                imageUrl = entity.Images.RootElement[0].GetString() ?? "";
                            }
                        } 
                        catch {}
                    }
                    if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("http"))
                    {
                        imageUrl = $"http://localhost:8080/{imageUrl.TrimStart('/')}";
                    }
                    
                    string imageHtml = string.IsNullOrEmpty(imageUrl) ? "" : $"<div style='margin-bottom: 15px;'><img src='{imageUrl}' style='max-width: 300px; border-radius: 8px; border: 1px solid #ccc;' /></div>";

                    var subject = $"Action Required: Health Incident Pending Approval for Batch {entity.Batch.BatchCode}";
                    string emailBody = $@"
                        <h2>Health Incident Requires Your Approval</h2>
                        <p>Dear Branch Manager, the cultivation staff has submitted a treatment resolution for the health incident on batch <strong>{entity.Batch.BatchCode}</strong>.</p>
                        {imageHtml}
                        <table style='width:100%; border-collapse: collapse;'>
                            <tr style='border-bottom: 1px solid #eee;'>
                                <td style='padding: 10px; font-weight: bold; width: 250px;'>Plant Species</td>
                                <td style='padding: 10px;'>{taxonomyTitleVi}</td>
                            </tr>
                            <tr style='border-bottom: 1px solid #eee;'>
                                <td style='padding: 10px; font-weight: bold; width: 250px;'>Severity</td>
                                <td style='padding: 10px;'>{entity.Severity}</td>
                            </tr>
                            <tr style='border-bottom: 1px solid #eee;'>
                                <td style='padding: 10px; font-weight: bold; width: 250px;'>Description</td>
                                <td style='padding: 10px;'>{entity.Description}</td>
                            </tr>
                            <tr style='border-bottom: 1px solid #eee;'>
                                <td style='padding: 10px; font-weight: bold; width: 250px;'>Treatment Details</td>
                                <td style='padding: 10px;'>{treatmentInfo}</td>
                            </tr>
                            <tr style='border-bottom: 1px solid #eee;'>
                                <td style='padding: 10px; font-weight: bold; width: 250px;'>Branch</td>
                                <td style='padding: 10px;'>{entity.Batch.Branch?.Name ?? "N/A"}</td>
                            </tr>
                        </table>
                        <div style='margin-top: 30px;'>
                            <a href='http://localhost:8080/api/health-incidents/{entity.Id}/approve-via-email' style='background-color: #1a3a32; color: white; padding: 14px 25px; text-align: center; text-decoration: none; display: inline-block; border-radius: 6px; font-weight: bold;'>Approve Resolution</a>
                        </div>
                        <p style='margin-top: 20px; color: #666; font-size: 12px;'>This is an automated email from the Decorative Plant Management System.</p>
                    ";

                    foreach (var email in branchManagerEmails)
                    {
                        await _emailService.SendAsync(new EmailMessage
                        {
                            To = email,
                            Subject = subject,
                            BodyHtml = emailBody,
                            BodyPlainText = $"The health incident for batch {entity.Batch.BatchCode} has been successfully resolved."
                        }, cancellationToken);
                    }
                    _logger.LogInformation("Sent incident resolved email to {Count} branch managers for branch {BranchId}", branchManagerEmails.Count, branchId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Health Incident resolution email for incident {IncidentId}", entity.Id);
            }
        }

        return HealthIncidentMapper.ToDto(entity);
    }
}
