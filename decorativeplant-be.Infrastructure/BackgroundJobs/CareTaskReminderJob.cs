using System.Text.Json;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Application.Common.DTOs.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Infrastructure.BackgroundJobs;

public class CareTaskReminderJob : IHostedService, IDisposable
{
    private readonly ILogger<CareTaskReminderJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private Timer? _timer;

    public CareTaskReminderJob(
        ILogger<CareTaskReminderJob> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Care Task Reminder Job is starting.");

        _timer = new Timer(
            (state) => _ = DoWorkAsync(),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(10)
        );

        return Task.CompletedTask;
    }

    private async Task DoWorkAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var now = DateTime.UtcNow;

            // Fetch pending care tasks with Batch and Location info included to get BranchId
            var pendingLogs = await context.CultivationLogs
                .Include(l => l.Batch)
                .Where(l => l.PerformedAt == null)
                .ToListAsync();

            foreach (var log in pendingLogs)
            {
                if (log.Details == null) continue;

                var detailsStr = log.Details.RootElement.GetRawText();
                var details = JsonSerializer.Deserialize<Dictionary<string, string>>(detailsStr);

                if (details == null || !details.ContainsKey("status") || details["status"] != "Pending")
                    continue;

                if (!details.ContainsKey("due_date")) continue;

                if (DateTime.TryParse(details["due_date"], out var dueDate))
                {
                    if (dueDate <= now.AddMinutes(5))
                    {
                        bool alreadyNotified = details.ContainsKey("last_notified_at") &&
                                             DateTime.TryParse(details["last_notified_at"], out var lastNotified) &&
                                             (now - lastNotified).TotalHours < 12;

                        if (!alreadyNotified)
                        {
                            // Get BranchId from Batch or Location
                            var branchId = log.Batch?.BranchId ?? log.Location?.BranchId;
                            
                            if (branchId.HasValue)
                            {
                                // Find staff assigned to THIS branch
                                var staffEmails = await context.UserAccounts
                                    .Where(u => u.Role == "cultivation_staff" && u.IsActive)
                                    .Where(u => context.StaffAssignments.Any(sa => sa.StaffId == u.Id && sa.BranchId == branchId))
                                    .Select(u => u.Email)
                                    .ToListAsync();

                                if (staffEmails.Any())
                                {
                                    await SendReminderEmails(emailService, staffEmails, details, log.ActivityType ?? "Care Activity");
                                }
                            }
                            
                            details["last_notified_at"] = now.ToString("yyyy-MM-ddTHH:mm:ss");
                            log.Details = BuildJson(details);
                            await context.SaveChangesAsync();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Care Task Reminder Job: {Message}", ex.Message);
        }
    }

    private async Task SendReminderEmails(IEmailService emailService, List<string> emails, Dictionary<string, string> details, string activity)
    {
        var productName = details.GetValueOrDefault("product_name", "Unknown Plant");
        var batchCode = details.GetValueOrDefault("batch", "N/A");

        foreach (var email in emails)
        {
            try
            {
                await emailService.SendAsync(new EmailMessage
                {
                    To = email,
                    Subject = $"[REMINDER] Time to {activity} for {productName}",
                    BodyHtml = $@"
                        <div style='font-family: sans-serif; padding: 20px; border: 1px solid #e7f6ef; border-radius: 15px;'>
                            <h2 style='color: #2d5f4d;'>Care Activity Reminder</h2>
                            <p>This is a scheduled reminder for a plant care task at your branch:</p>
                            <div style='background-color: #f0f9f6; padding: 15px; border-radius: 10px;'>
                                <p><b>Activity:</b> {activity}</p>
                                <p><b>Plant:</b> {productName}</p>
                                <p><b>Batch:</b> {batchCode}</p>
                                <p><b>Scheduled Time:</b> {details.GetValueOrDefault("due_date", "N/A")}</p>
                            </div>
                        </div>"
                }, default);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send reminder email to {Email}", email);
            }
        }
    }

    private static JsonDocument BuildJson(object obj)
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(obj));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
