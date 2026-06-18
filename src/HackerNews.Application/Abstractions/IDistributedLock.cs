namespace HackerNews.Application.Abstractions;

public interface IDistributedLock
{
    Task<IAsyncDisposable> TryAcquireAsync(TimeSpan maxWait, CancellationToken cancellationToken);
}
