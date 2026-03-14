using PaymentGateway.Api.Application.Abstractions;
using PaymentGateway.Api.Application.Payments;
using PaymentGateway.Api.Domain.Payments;
using Microsoft.Extensions.Logging.Abstractions;

namespace PaymentGateway.Api.Tests;

public class PaymentServiceTests
{
    [Fact]
    public async Task ProcessAsync_AuthorizedPayment_SanitizesStoresAndReturnsPayment()
    {
        var bankClient = new CapturingBankClient(_ => Task.FromResult(new BankPaymentResult
        {
            Authorized = true,
            AuthorizationCode = "auth-123"
        }));
        var idempotencyRepository = new CapturingIdempotencyRepository();
        var repository = new CapturingPaymentRepository();
        var service = new PaymentService(bankClient, idempotencyRepository, repository, NullLogger<PaymentService>.Instance);

        var result = await service.ProcessAsync(
            new ProcessPaymentCommand
            {
                CardNumber = " 2222405343248877 ",
                ExpiryMonth = 12,
                ExpiryYear = DateTime.UtcNow.Year + 1,
                Currency = " gbp ",
                Amount = 1050,
                Cvv = " 123 "
            },
            CancellationToken.None);

        Assert.Equal(PaymentStatus.Authorized, result.Status);
        Assert.NotNull(result.Id);
        Assert.Equal("8877", result.CardNumberLastFour);

        Assert.Equal(1, bankClient.CallCount);
        Assert.NotNull(bankClient.LastRequest);
        Assert.Equal("2222405343248877", bankClient.LastRequest.CardNumber);
        Assert.Equal("GBP", bankClient.LastRequest.Currency);
        Assert.Equal("123", bankClient.LastRequest.Cvv);

        Assert.Equal(1, repository.AddCount);
        Assert.NotNull(repository.LastSavedPayment);
        Assert.Equal("************8877", repository.LastSavedPayment.MaskedCardNumber);
        Assert.Equal("auth-123", repository.LastSavedPayment.AuthorizationCode);
    }

    [Fact]
    public async Task ProcessAsync_DeclinedPayment_StoresDeclinedPaymentWithoutAuthorizationCode()
    {
        var bankClient = new CapturingBankClient(_ => Task.FromResult(new BankPaymentResult
        {
            Authorized = false
        }));
        var idempotencyRepository = new CapturingIdempotencyRepository();
        var repository = new CapturingPaymentRepository();
        var service = new PaymentService(bankClient, idempotencyRepository, repository, NullLogger<PaymentService>.Instance);

        var result = await service.ProcessAsync(ValidCommand(cardNumber: "2222405343248878"), CancellationToken.None);

        Assert.Equal(PaymentStatus.Declined, result.Status);
        Assert.NotNull(result.Id);
        Assert.NotNull(repository.LastSavedPayment);
        Assert.Equal(PaymentStatus.Declined, repository.LastSavedPayment.Status);
        Assert.Null(repository.LastSavedPayment.AuthorizationCode);
    }

    [Fact]
    public async Task ProcessAsync_InvalidPayment_ReturnsRejectedWithoutCallingBankOrSaving()
    {
        var bankClient = new CapturingBankClient(_ => Task.FromResult(new BankPaymentResult
        {
            Authorized = true,
            AuthorizationCode = "auth-123"
        }));
        var idempotencyRepository = new CapturingIdempotencyRepository();
        var repository = new CapturingPaymentRepository();
        var service = new PaymentService(bankClient, idempotencyRepository, repository, NullLogger<PaymentService>.Instance);

        var result = await service.ProcessAsync(
            new ProcessPaymentCommand
            {
                CardNumber = "1234",
                ExpiryMonth = 1,
                ExpiryYear = DateTime.UtcNow.Year - 1,
                Currency = "ABC",
                Amount = 0,
                Cvv = "12"
            },
            CancellationToken.None);

        Assert.Equal(PaymentStatus.Rejected, result.Status);
        Assert.Null(result.Id);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(0, bankClient.CallCount);
        Assert.Equal(0, repository.AddCount);
    }

    [Fact]
    public async Task GetAsync_ReturnsPaymentFromRepository()
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "8877",
            MaskedCardNumber = "************8877",
            ExpiryMonth = 12,
            ExpiryYear = DateTime.UtcNow.Year + 1,
            Currency = "GBP",
            Amount = 1050,
            AuthorizationCode = "auth-123"
        };

        var bankClient = new CapturingBankClient(_ => Task.FromResult(new BankPaymentResult
        {
            Authorized = true
        }));
        var idempotencyRepository = new CapturingIdempotencyRepository();
        var repository = new CapturingPaymentRepository(payment);
        var service = new PaymentService(bankClient, idempotencyRepository, repository, NullLogger<PaymentService>.Instance);

        var result = await service.GetAsync(payment.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(payment.Id, result.Id);
        Assert.Equal(payment.MaskedCardNumber, result.MaskedCardNumber);
    }

    [Fact]
    public async Task ProcessAsync_WithSameIdempotencyKey_ReturnsCachedResultWithoutCallingBankTwice()
    {
        var bankClient = new CapturingBankClient(_ => Task.FromResult(new BankPaymentResult
        {
            Authorized = true,
            AuthorizationCode = "auth-123"
        }));
        var idempotencyRepository = new CapturingIdempotencyRepository();
        var repository = new CapturingPaymentRepository();
        var service = new PaymentService(bankClient, idempotencyRepository, repository, NullLogger<PaymentService>.Instance);

        var command = new ProcessPaymentCommand
        {
            IdempotencyKey = "payment-123",
            CardNumber = "2222405343248877",
            ExpiryMonth = 12,
            ExpiryYear = DateTime.UtcNow.Year + 1,
            Currency = "GBP",
            Amount = 1050,
            Cvv = "123"
        };

        var firstResult = await service.ProcessAsync(command, CancellationToken.None);
        var secondResult = await service.ProcessAsync(command, CancellationToken.None);

        Assert.Equal(1, bankClient.CallCount);
        Assert.Equal(1, repository.AddCount);
        Assert.NotNull(firstResult.Id);
        Assert.Equal(firstResult.Id, secondResult.Id);
        Assert.Equal(firstResult.Status, secondResult.Status);
    }

    private static ProcessPaymentCommand ValidCommand(string cardNumber) =>
        new()
        {
            CardNumber = cardNumber,
            ExpiryMonth = 12,
            ExpiryYear = DateTime.UtcNow.Year + 1,
            Currency = "GBP",
            Amount = 1050,
            Cvv = "123"
        };

    private sealed class CapturingBankClient : IAcquiringBankClient
    {
        private readonly Func<BankPaymentRequest, Task<BankPaymentResult>> _handler;

        public CapturingBankClient(Func<BankPaymentRequest, Task<BankPaymentResult>> handler)
        {
            _handler = handler;
        }

        public int CallCount { get; private set; }

        public BankPaymentRequest? LastRequest { get; private set; }

        public async Task<BankPaymentResult> ProcessAsync(BankPaymentRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            return await _handler(request);
        }
    }

    private sealed class CapturingPaymentRepository : IPaymentRepository
    {
        private readonly Dictionary<Guid, Payment> _payments = new();

        public CapturingPaymentRepository()
        {
        }

        public CapturingPaymentRepository(Payment seedPayment)
        {
            _payments[seedPayment.Id] = seedPayment;
        }

        public int AddCount { get; private set; }

        public Payment? LastSavedPayment { get; private set; }

        public Task AddAsync(Payment payment, CancellationToken cancellationToken)
        {
            AddCount++;
            LastSavedPayment = payment;
            _payments[payment.Id] = payment;
            return Task.CompletedTask;
        }

        public Task<Payment?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            _payments.TryGetValue(id, out var payment);
            return Task.FromResult(payment);
        }
    }

    private sealed class CapturingIdempotencyRepository : IIdempotencyRepository
    {
        private readonly Dictionary<string, ProcessPaymentResult> _results = new(StringComparer.Ordinal);

        public Task<ProcessPaymentResult?> GetAsync(string key, CancellationToken cancellationToken)
        {
            _results.TryGetValue(key, out var result);
            return Task.FromResult(result);
        }

        public Task AddAsync(string key, ProcessPaymentResult result, CancellationToken cancellationToken)
        {
            _results.TryAdd(key, result);
            return Task.CompletedTask;
        }
    }
}
