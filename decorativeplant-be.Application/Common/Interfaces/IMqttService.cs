namespace decorativeplant_be.Application.Common.Interfaces;

public interface IMqttService
{
    Task PublishRulesUpdateAsync(string deviceSecret, string jsonPayload, CancellationToken cancellationToken);
}
