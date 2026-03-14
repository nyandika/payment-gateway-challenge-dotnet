using System.Collections.Concurrent;
using PaymentGateway.Api.Application.Abstractions;
using PaymentGateway.Api.Application.Payments;

namespace PaymentGateway.Api.Infrastructure.Persistence;

public class InMemoryIdempotencyRepository : IIdempotencyRepository
{
    private readonly ConcurrentDictionary<string, ProcessPaymentResult> _results = new(StringComparer.Ordinal);

    public Task<ProcessPaymentResult?> GetAsync(string key, CancellationToken cancellationToken)
    {
        _results.TryGetValue(key, out var result);
        return Task.FromResult(result);
    }

    public Task AddAsync(string key, ProcessPaymentResult result, CancellationToken cancellationToken)
    {
        _results.TryAdd(key, result);
        return Task.CompletedTask;
    }
}
