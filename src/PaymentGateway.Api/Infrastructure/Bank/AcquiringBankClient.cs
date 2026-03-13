using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PaymentGateway.Api.Application.Abstractions;
using PaymentGateway.Api.Application.Payments;

namespace PaymentGateway.Api.Infrastructure.Bank;

public class AcquiringBankClient : IAcquiringBankClient
{
    private readonly HttpClient _httpClient;

    public AcquiringBankClient(HttpClient httpClient, IOptions<AcquiringBankOptions> options)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl);
    }

    public async Task<BankPaymentResult> ProcessAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "payments",
            new BankPaymentRequestDto
            {
                CardNumber = request.CardNumber,
                ExpiryDate = $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
                Currency = request.Currency,
                Amount = request.Amount,
                Cvv = request.Cvv
            },
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            throw new BankUnavailableException("The acquiring bank is currently unavailable.");
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<BankPaymentResponseDto>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            throw new InvalidOperationException("The acquiring bank response could not be parsed.");
        }

        return new BankPaymentResult
        {
            Authorized = payload.Authorized,
            AuthorizationCode = payload.AuthorizationCode
        };
    }
}
