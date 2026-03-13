using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PaymentGateway.Api.Api.Requests;
using PaymentGateway.Api.Api.Responses;
using PaymentGateway.Api.Application.Abstractions;
using PaymentGateway.Api.Application.Payments;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    [Fact]
    public async Task ProcessesAnAuthorizedPaymentAndRetrievesIt()
    {
        var bankClient = new StubAcquiringBankClient(_ => Task.FromResult(new BankPaymentResult
        {
            Authorized = true,
            AuthorizationCode = "auth-123"
        }));

        using var factory = CreateFactory(bankClient);
        var client = factory.CreateClient();

        var request = ValidRequest(cardNumber: "2222405343248877");

        var createResponse = await client.PostAsJsonAsync("/api/Payments", request);
        var createPayload = await createResponse.Content.ReadFromJsonAsync<ProcessPaymentResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createPayload);
        Assert.Equal(PaymentGateway.Api.Domain.Payments.PaymentStatus.Authorized, createPayload.Status);
        Assert.NotNull(createPayload.Id);
        Assert.Equal("8877", createPayload.CardNumberLastFour);
        Assert.Equal(1, bankClient.CallCount);

        var getResponse = await client.GetAsync($"/api/Payments/{createPayload.Id}");
        var getPayload = await getResponse.Content.ReadFromJsonAsync<GetPaymentResponse>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(getPayload);
        Assert.Equal("************8877", getPayload.MaskedCardNumber);
        Assert.Equal("8877", getPayload.CardNumberLastFour);
        Assert.Equal(PaymentGateway.Api.Domain.Payments.PaymentStatus.Authorized, getPayload.Status);
    }

    [Fact]
    public async Task ProcessesADeclinedPayment()
    {
        var bankClient = new StubAcquiringBankClient(_ => Task.FromResult(new BankPaymentResult
        {
            Authorized = false
        }));

        using var factory = CreateFactory(bankClient);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Payments", ValidRequest(cardNumber: "2222405343248878"));
        var payload = await response.Content.ReadFromJsonAsync<ProcessPaymentResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(PaymentGateway.Api.Domain.Payments.PaymentStatus.Declined, payload.Status);
        Assert.NotNull(payload.Id);
        Assert.Equal("8878", payload.CardNumberLastFour);
    }

    [Fact]
    public async Task RejectsInvalidPaymentsWithoutCallingTheBank()
    {
        var bankClient = new StubAcquiringBankClient(_ => Task.FromResult(new BankPaymentResult
        {
            Authorized = true
        }));

        using var factory = CreateFactory(bankClient);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Payments", new ProcessPaymentRequest
        {
            CardNumber = "1234",
            ExpiryMonth = 1,
            ExpiryYear = DateTime.UtcNow.Year - 1,
            Currency = "ABC",
            Amount = 0,
            Cvv = "12"
        });
        var payload = await response.Content.ReadFromJsonAsync<ProcessPaymentResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(PaymentGateway.Api.Domain.Payments.PaymentStatus.Rejected, payload.Status);
        Assert.Null(payload.Id);
        Assert.NotEmpty(payload.Errors);
        Assert.Equal(0, bankClient.CallCount);
    }

    [Fact]
    public async Task Returns404IfPaymentIsNotFound()
    {
        using var factory = CreateFactory(new StubAcquiringBankClient(_ => Task.FromResult(new BankPaymentResult
        {
            Authorized = true
        })));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Returns503WhenTheBankIsUnavailable()
    {
        var bankClient = new StubAcquiringBankClient(_ => throw new BankUnavailableException("The acquiring bank is currently unavailable."));

        using var factory = CreateFactory(bankClient);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Payments", ValidRequest(cardNumber: "2222405343248870"));
        var payload = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Acquiring bank unavailable", payload.Title);
    }

    private static WebApplicationFactory<Program> CreateFactory(StubAcquiringBankClient bankClient)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    var existingDescriptor = services.SingleOrDefault(service => service.ServiceType == typeof(IAcquiringBankClient));
                    if (existingDescriptor is not null)
                    {
                        services.Remove(existingDescriptor);
                    }

                    services.AddSingleton<IAcquiringBankClient>(bankClient);
                }));
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

    private sealed class StubAcquiringBankClient : IAcquiringBankClient
    {
        private readonly Func<BankPaymentRequest, Task<BankPaymentResult>> _handler;

        public StubAcquiringBankClient(Func<BankPaymentRequest, Task<BankPaymentResult>> handler)
        {
            _handler = handler;
        }

        public int CallCount { get; private set; }

        public async Task<BankPaymentResult> ProcessAsync(BankPaymentRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            return await _handler(request);
        }
    }
}
