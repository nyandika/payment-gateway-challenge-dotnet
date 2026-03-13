namespace PaymentGateway.Api.Application.Payments;

public class BankUnavailableException : Exception
{
    public BankUnavailableException(string message) : base(message)
    {
    }
}
