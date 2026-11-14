using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Modules.Processing.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "acman";
    public string Password { get; set; } = "acman_rabbit_password";
    public string QueueName { get; set; } = "acman.processing.jobs";
    public bool Enabled { get; set; } = true;
}

public sealed class RabbitMqJobQueue : IJobQueue, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqJobQueue> _logger;
    private readonly IConnection? _connection;
    private readonly IChannel? _channel;
    private readonly bool _enabled;

    public RabbitMqJobQueue(IOptions<RabbitMqOptions> options, ILogger<RabbitMqJobQueue> logger)
    {
        _options = options.Value;
        _logger = logger;
        _enabled = _options.Enabled;

        if (!_enabled)
        {
            return;
        }

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password
            };

            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
            _channel.QueueDeclareAsync(
                queue: _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ unavailable; jobs will run inline when queued.");
            _enabled = false;
        }
    }

    public bool IsAvailable => _enabled && _channel is not null;

    public async Task PublishAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable || _channel is null)
        {
            throw new InvalidOperationException("RabbitMQ queue is not available.");
        }

        var payload = JsonSerializer.Serialize(new JobMessage { JobId = jobId });
        var body = Encoding.UTF8.GetBytes(payload);
        var props = new BasicProperties { Persistent = true, ContentType = "application/json" };
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _options.QueueName,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken);
        _logger.LogDebug("Published job {JobId} to {Queue}", jobId, _options.QueueName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
    }
}

/// <summary>Fallback that marks jobs for immediate in-process execution.</summary>
public sealed class InlineJobQueue : IJobQueue
{
    public bool IsAvailable => false;

    public Task PublishAsync(Guid jobId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
