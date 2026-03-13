using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Api.Responses;

public class ProcessPaymentResponse
{
    public Guid? Id { get; init; }
    public PaymentStatus Status { get; init; }
    public string? CardNumberLastFour { get; init; }
    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }
    public string? Currency { get; init; }
    public int Amount { get; init; }
    public IReadOnlyCollection<string> Errors { get; init; } = Array.Empty<string>();
}
