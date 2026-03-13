namespace PaymentGateway.Api.Application.Abstractions;

public interface IAcquiringBankClient
{
    Task<BankPaymentResult> ProcessAsync(BankPaymentRequest request, CancellationToken cancellationToken);
}
