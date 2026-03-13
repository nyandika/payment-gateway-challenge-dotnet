using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.Abstractions;

public interface IPaymentRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken);

    Task<Payment?> GetAsync(Guid id, CancellationToken cancellationToken);
}
