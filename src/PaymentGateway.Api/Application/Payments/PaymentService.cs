using PaymentGateway.Api.Application.Abstractions;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Application.Payments;

public class PaymentService
{
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "GBP",
        "USD",
        "EUR"
    };

    private readonly IAcquiringBankClient _acquiringBankClient;
    private readonly ILogger<PaymentService> _logger;
    private readonly IPaymentRepository _paymentRepository;

    public PaymentService(
        IAcquiringBankClient acquiringBankClient,
        IPaymentRepository paymentRepository,
        ILogger<PaymentService> logger)
    {
        _acquiringBankClient = acquiringBankClient;
        _paymentRepository = paymentRepository;
        _logger = logger;
    }

    public async Task<ProcessPaymentResult> ProcessAsync(ProcessPaymentCommand command, CancellationToken cancellationToken)
    {
        var validationErrors = Validate(command);
        if (validationErrors.Count > 0)
        {
            _logger.LogWarning(
                "Payment request rejected by validation with {ErrorCount} errors.",
                validationErrors.Count);
            return ProcessPaymentResult.Rejected(validationErrors);
        }

        var sanitizedCardNumber = command.CardNumber!.Trim();
        var sanitizedCurrency = command.Currency!.Trim().ToUpperInvariant();
        var sanitizedCvv = command.Cvv!.Trim();

        var bankResponse = await _acquiringBankClient.ProcessAsync(
            new BankPaymentRequest
            {
                CardNumber = sanitizedCardNumber,
                ExpiryMonth = command.ExpiryMonth,
                ExpiryYear = command.ExpiryYear,
                Currency = sanitizedCurrency,
                Amount = command.Amount,
                Cvv = sanitizedCvv
            },
            cancellationToken);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Status = bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = sanitizedCardNumber[^4..],
            MaskedCardNumber = MaskCardNumber(sanitizedCardNumber),
            ExpiryMonth = command.ExpiryMonth,
            ExpiryYear = command.ExpiryYear,
            Currency = sanitizedCurrency,
            Amount = command.Amount,
            AuthorizationCode = bankResponse.AuthorizationCode
        };

        await _paymentRepository.AddAsync(payment, cancellationToken);

        _logger.LogInformation(
            "Payment {PaymentId} processed with status {PaymentStatus} for {Currency} {Amount}.",
            payment.Id,
            payment.Status,
            payment.Currency,
            payment.Amount);

        return new ProcessPaymentResult
        {
            Id = payment.Id,
            Status = payment.Status,
            CardNumberLastFour = payment.CardNumberLastFour,
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount
        };
    }

    public async Task<Payment?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetAsync(id, cancellationToken);

        if (payment is null)
        {
            _logger.LogInformation("Payment {PaymentId} was not found.", id);
            return null;
        }

        _logger.LogInformation("Payment {PaymentId} was retrieved.", id);
        return payment;
    }

    private static List<string> Validate(ProcessPaymentCommand command)
    {
        var errors = new List<string>();

        var cardNumber = command.CardNumber?.Trim();
        if (string.IsNullOrWhiteSpace(cardNumber) || cardNumber.Length is < 14 or > 19 || cardNumber.Any(c => !char.IsDigit(c)))
        {
            errors.Add("Card number must contain only digits and be between 14 and 19 characters long.");
        }

        if (command.ExpiryMonth is < 1 or > 12)
        {
            errors.Add("Expiry month must be between 1 and 12.");
        }

        var now = DateTime.UtcNow;
        var currentMonth = new DateOnly(now.Year, now.Month, 1);
        if (command.ExpiryYear < now.Year || IsExpired(command.ExpiryYear, command.ExpiryMonth, currentMonth))
        {
            errors.Add("Expiry month and year must be in the future.");
        }

        var currency = command.Currency?.Trim();
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3 || !SupportedCurrencies.Contains(currency))
        {
            errors.Add("Currency must be one of GBP, USD, or EUR.");
        }

        if (command.Amount <= 0)
        {
            errors.Add("Amount must be a positive integer in the minor currency unit.");
        }

        var cvv = command.Cvv?.Trim();
        if (string.IsNullOrWhiteSpace(cvv) || cvv.Length is < 3 or > 4 || cvv.Any(c => !char.IsDigit(c)))
        {
            errors.Add("CVV must contain only digits and be 3 or 4 characters long.");
        }

        return errors;
    }

    private static bool IsExpired(int expiryYear, int expiryMonth, DateOnly currentMonth)
    {
        if (expiryMonth is < 1 or > 12)
        {
            return false;
        }

        var expiry = new DateOnly(expiryYear, expiryMonth, 1);
        return expiry <= currentMonth;
    }

    private static string MaskCardNumber(string cardNumber)
    {
        var maskedPrefix = new string('*', Math.Max(cardNumber.Length - 4, 0));
        return $"{maskedPrefix}{cardNumber[^4..]}";
    }
}
