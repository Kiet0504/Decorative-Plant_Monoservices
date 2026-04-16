using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Common.Interfaces;

public interface IIotRepository
{
    Task<IotDevice?> GetDeviceBySecretAsync(string secretKey, CancellationToken cancellationToken);
    Task AddSensorReadingAsync(SensorReading reading, CancellationToken cancellationToken);
    Task<IEnumerable<SensorReading>> GetSensorMetricsAsync(Guid deviceId, string? componentKey, DateTime? startTime, DateTime? endTime, CancellationToken cancellationToken);

    // CRUD for IotDevice
    Task<IEnumerable<IotDevice>> GetIotDevicesAsync(CancellationToken cancellationToken);
    Task<IotDevice?> GetIotDeviceByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IotDevice> CreateIotDeviceAsync(IotDevice device, CancellationToken cancellationToken);
    Task UpdateIotDeviceAsync(IotDevice device, CancellationToken cancellationToken);
    Task DeleteIotDeviceAsync(IotDevice device, CancellationToken cancellationToken);

    // CRUD for IotAlert
    Task<IEnumerable<IotAlert>> GetIotAlertsAsync(Guid? deviceId, Guid? branchId, CancellationToken cancellationToken);
    Task<IotAlert?> GetIotAlertByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IotAlert> CreateIotAlertAsync(IotAlert alert, CancellationToken cancellationToken);
    Task UpdateIotAlertAsync(IotAlert alert, CancellationToken cancellationToken);
    Task DeleteIotAlertAsync(IotAlert alert, CancellationToken cancellationToken);

    // CRUD for AutomationRule
    Task<IEnumerable<AutomationRule>> GetAutomationRulesAsync(Guid? deviceId, Guid? branchId, CancellationToken cancellationToken);
    Task<AutomationRule?> GetAutomationRuleByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<AutomationRule> CreateAutomationRuleAsync(AutomationRule rule, CancellationToken cancellationToken);
    Task UpdateAutomationRuleAsync(AutomationRule rule, CancellationToken cancellationToken);
    Task DeleteAutomationRuleAsync(AutomationRule rule, CancellationToken cancellationToken);

    // Read-only for AutomationExecutionLog
    Task<IEnumerable<AutomationExecutionLog>> GetExecutionLogsAsync(Guid? ruleId, DateTime? from, DateTime? to, CancellationToken cancellationToken);
    Task<AutomationExecutionLog?> GetExecutionLogByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddExecutionLogAsync(AutomationExecutionLog log, CancellationToken cancellationToken);
}
