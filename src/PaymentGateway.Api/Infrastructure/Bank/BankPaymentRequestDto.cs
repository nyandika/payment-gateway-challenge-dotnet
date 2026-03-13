using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Infrastructure.Bank;

public class BankPaymentRequestDto
{
    [JsonPropertyName("card_number")]
    public required string CardNumber { get; init; }

    [JsonPropertyName("expiry_date")]
    public required string ExpiryDate { get; init; }

    [JsonPropertyName("currency")]
    public required string Currency { get; init; }

    [JsonPropertyName("amount")]
    public int Amount { get; init; }

    [JsonPropertyName("cvv")]
    public required string Cvv { get; init; }
}
