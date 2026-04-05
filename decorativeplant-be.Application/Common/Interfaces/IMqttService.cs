namespace decorativeplant_be.Application.Common.Interfaces;

public interface IMqttService
{
    Task PublishRulesUpdateAsync(string deviceSecret, string jsonPayload, CancellationToken cancellationToken);
    Task PublishCommandAsync(string deviceSecret, string commandName, object payload, CancellationToken cancellationToken);
}
