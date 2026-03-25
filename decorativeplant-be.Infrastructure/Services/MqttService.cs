using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using System.Text;

namespace decorativeplant_be.Infrastructure.Services;

public class MqttService : IHostedService, IMqttService
{
    private IManagedMqttClient? _mqttClient;
    private readonly ILogger<MqttService> _logger;
    private readonly IConfiguration _config;

    public MqttService(ILogger<MqttService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var mqttFactory = new MqttFactory();
        _mqttClient = mqttFactory.CreateManagedMqttClient();

        var host = _config["MqttSettings:Host"] ?? "broker.hivemq.com";
        var port = _config.GetValue<int?>("MqttSettings:Port") ?? 1883;
        var username = _config["MqttSettings:Username"];
        var password = _config["MqttSettings:Password"];

        var builder = new MqttClientOptionsBuilder()
            .WithClientId($"DecorativePlant-Backend-{Guid.NewGuid()}")
            .WithTcpServer(host, port)
            .WithCleanSession();

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            builder.WithCredentials(username, password);
        }

        if (port == 8883)
        {
             var tlsOptions = new MqttClientTlsOptionsBuilder()
                .UseTls()
                .Build();
             builder.WithTlsOptions(tlsOptions);
        }

        var mqttClientOptions = builder.Build();

        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithClientOptions(mqttClientOptions)
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .Build();

        await _mqttClient.StartAsync(managedOptions);
        _logger.LogInformation($"MQTT Managed Client started connecting to {host}:{port}");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_mqttClient != null)
        {
            await _mqttClient.StopAsync();
            _mqttClient.Dispose();
            _logger.LogInformation("MQTT Managed Client stopped.");
        }
    }

    public async Task PublishRulesUpdateAsync(string deviceSecret, string jsonPayload, CancellationToken cancellationToken)
    {
        if (_mqttClient == null || !_mqttClient.IsStarted)
        {
            _logger.LogWarning("MQTT Client not started, cannot publish.");
            return;
        }

        var topic = $"decorativeplant/device/{deviceSecret}/rules";
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(jsonPayload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(true)
            .Build();

        await _mqttClient.EnqueueAsync(message);
        _logger.LogInformation($"[MQTT] Published updated rules to {topic}");
    }
}
