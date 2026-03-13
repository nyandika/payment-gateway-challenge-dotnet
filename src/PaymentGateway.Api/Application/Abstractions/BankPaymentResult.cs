namespace PaymentGateway.Api.Application.Abstractions;

public class BankPaymentResult
{
    public bool Authorized { get; init; }
    public string? AuthorizationCode { get; init; }
}
