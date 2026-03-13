using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Infrastructure.Bank;

public class BankPaymentResponseDto
{
    [JsonPropertyName("authorized")]
    public bool Authorized { get; init; }

    [JsonPropertyName("authorization_code")]
    public string? AuthorizationCode { get; init; }
}
