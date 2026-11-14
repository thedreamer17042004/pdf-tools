namespace Modules.Processing.Messaging;

public interface IJobQueue
{
    bool IsAvailable { get; }
    Task PublishAsync(Guid jobId, CancellationToken cancellationToken = default);
}

public sealed class JobMessage
{
    public Guid JobId { get; set; }
}
