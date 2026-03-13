namespace PaymentGateway.Api.Infrastructure.Bank;

public class AcquiringBankOptions
{
    public const string SectionName = "AcquiringBank";

    public string BaseUrl { get; init; } = "http://localhost:8080/";
}
