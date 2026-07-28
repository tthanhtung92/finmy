namespace Finmy.Budgeting.Application.Abstractions;

public interface IEnvelopeCacheInvalidator
{
    Task InvalidateAsync(DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken);
}
