using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Api.Responses;

public class GetPaymentResponse
{
    public Guid Id { get; init; }
    public PaymentStatus Status { get; init; }
    public required string MaskedCardNumber { get; init; }
    public required string CardNumberLastFour { get; init; }
    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }
    public required string Currency { get; init; }
    public int Amount { get; init; }
}
