namespace PaymentGateway.Api.Application.Payments;

public class ProcessPaymentCommand
{
    public string? CardNumber { get; init; }
    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }
    public string? Currency { get; init; }
    public int Amount { get; init; }
    public string? Cvv { get; init; }
}
