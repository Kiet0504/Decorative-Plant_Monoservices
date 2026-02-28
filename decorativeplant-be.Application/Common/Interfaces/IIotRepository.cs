using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Common.Interfaces;

public interface IIotRepository
{
    Task<IotDevice?> GetDeviceBySecretAsync(string secretKey, CancellationToken cancellationToken);
    Task AddSensorReadingAsync(SensorReading reading, CancellationToken cancellationToken);
}
