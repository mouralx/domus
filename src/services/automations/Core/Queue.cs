using System.Text;
using RabbitMQ.Client;

namespace Automations.Core;

public sealed class JobQueue : IAsyncDisposable
{
    public const string QueueName = "automations.executions";
    public IConnection Connection { get; private set; } = null!;
    public IChannel Publisher { get; private set; } = null!;
    public async Task Initialize(CancellationToken ct = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
            Port = int.Parse(Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? "5672"),
            UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "automations",
            Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "local-rabbit-change-me",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };
        Connection = await factory.CreateConnectionAsync(ct);
        Publisher = await Connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        await Publisher.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
    }
    public bool Healthy => Connection is { IsOpen: true } && Publisher is { IsOpen: true };
    public async Task Publish(Guid id, CancellationToken ct, bool installation = false)
    {
        await Publisher.BasicPublishAsync("", QueueName, mandatory: true, new BasicProperties { Persistent = true, MessageId = id.ToString(), ContentType = "text/plain" }, Encoding.UTF8.GetBytes(installation ? "installation:" + id : id.ToString()), ct);
    }
    public async ValueTask DisposeAsync()
    {
        if (Publisher is not null) await Publisher.DisposeAsync();
        if (Connection is not null) await Connection.DisposeAsync();
    }
}
