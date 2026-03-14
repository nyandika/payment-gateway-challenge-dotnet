using PaymentGateway.Api.Application.Payments;

namespace PaymentGateway.Api.Application.Abstractions;

public interface IIdempotencyRepository
{
    Task<ProcessPaymentResult?> GetAsync(string key, CancellationToken cancellationToken);

    Task AddAsync(string key, ProcessPaymentResult result, CancellationToken cancellationToken);
}
