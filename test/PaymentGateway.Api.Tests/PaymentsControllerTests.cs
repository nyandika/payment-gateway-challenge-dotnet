using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using PaymentGateway.Api.Api.Requests;
using PaymentGateway.Api.Api.Responses;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public PaymentsControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProcessesAnAuthorizedPaymentAndRetrievesIt()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/Payments", ValidRequest(cardNumber: "2222405343248877"));
        var createPayload = await createResponse.Content.ReadFromJsonAsync<ProcessPaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createPayload);
        Assert.Equal(PaymentStatus.Authorized, createPayload.Status);
        Assert.NotNull(createPayload.Id);
        Assert.Equal("8877", createPayload.CardNumberLastFour);

        var getResponse = await _client.GetAsync($"/api/Payments/{createPayload.Id}");
        var getPayload = await getResponse.Content.ReadFromJsonAsync<GetPaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(getPayload);
        Assert.Equal(PaymentStatus.Authorized, getPayload.Status);
        Assert.Equal("************8877", getPayload.MaskedCardNumber);
        Assert.Equal("8877", getPayload.CardNumberLastFour);
    }

    [Fact]
    public async Task ProcessesADeclinedPayment()
    {
        var response = await _client.PostAsJsonAsync("/api/Payments", ValidRequest(cardNumber: "2222405343248878"));
        var payload = await response.Content.ReadFromJsonAsync<ProcessPaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(PaymentStatus.Declined, payload.Status);
        Assert.NotNull(payload.Id);
        Assert.Equal("8878", payload.CardNumberLastFour);
    }

    [Fact]
    public async Task RejectsInvalidPayments()
    {
        var response = await _client.PostAsJsonAsync("/api/Payments", new ProcessPaymentRequest
        {
            CardNumber = "1234",
            ExpiryMonth = 1,
            ExpiryYear = DateTime.UtcNow.Year - 1,
            Currency = "ABC",
            Amount = 0,
            Cvv = "12"
        });
        var payload = await response.Content.ReadFromJsonAsync<ProcessPaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(PaymentStatus.Rejected, payload.Status);
        Assert.Null(payload.Id);
        Assert.NotEmpty(payload.Errors);
    }

    [Fact]
    public async Task Returns404IfPaymentIsNotFound()
    {
        var response = await _client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Returns503WhenTheBankIsUnavailable()
    {
        var response = await _client.PostAsJsonAsync("/api/Payments", ValidRequest(cardNumber: "2222405343248870"));
        var payload = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Acquiring bank unavailable", payload.Title);
    }

    private static ProcessPaymentRequest ValidRequest(string cardNumber) =>
        new()
        {
            CardNumber = cardNumber,
            ExpiryMonth = 12,
            ExpiryYear = DateTime.UtcNow.Year + 1,
            Currency = "GBP",
            Amount = 1050,
            Cvv = "123"
        };
}
