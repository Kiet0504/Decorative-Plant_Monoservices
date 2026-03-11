using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Common.Interfaces;

public interface IIotRepository
{
    Task<IotDevice?> GetDeviceBySecretAsync(string secretKey, CancellationToken cancellationToken);
    Task AddSensorReadingAsync(SensorReading reading, CancellationToken cancellationToken);
    
    // CRUD for IotDevice
    Task<IEnumerable<IotDevice>> GetIotDevicesAsync(CancellationToken cancellationToken);
    Task<IotDevice?> GetIotDeviceByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IotDevice> CreateIotDeviceAsync(IotDevice device, CancellationToken cancellationToken);
    Task UpdateIotDeviceAsync(IotDevice device, CancellationToken cancellationToken);
    Task DeleteIotDeviceAsync(IotDevice device, CancellationToken cancellationToken);
}
