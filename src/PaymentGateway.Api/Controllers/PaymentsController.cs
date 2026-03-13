using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Api.Api.Requests;
using PaymentGateway.Api.Api.Responses;
using PaymentGateway.Api.Application.Payments;
using PaymentGateway.Api.Domain.Payments;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly PaymentService _paymentService;

    public PaymentsController(PaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost]
    public async Task<ActionResult<ProcessPaymentResponse>> ProcessPaymentAsync(
        [FromBody] ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _paymentService.ProcessAsync(
                new ProcessPaymentCommand
                {
                    CardNumber = request.CardNumber,
                    ExpiryMonth = request.ExpiryMonth,
                    ExpiryYear = request.ExpiryYear,
                    Currency = request.Currency,
                    Amount = request.Amount,
                    Cvv = request.Cvv
                },
                cancellationToken);

            var response = new ProcessPaymentResponse
            {
                Id = result.Id,
                Status = result.Status,
                CardNumberLastFour = result.CardNumberLastFour,
                ExpiryMonth = result.ExpiryMonth,
                ExpiryYear = result.ExpiryYear,
                Currency = result.Currency,
                Amount = result.Amount,
                Errors = result.Errors
            };

            if (result.Status == PaymentStatus.Rejected)
            {
                return BadRequest(response);
            }

            return Created($"/api/payments/{result.Id}", response);
        }
        catch (BankUnavailableException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Acquiring bank unavailable",
                Detail = exception.Message,
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GetPaymentResponse>> GetPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await _paymentService.GetAsync(id, cancellationToken);
        if (payment is null)
        {
            return NotFound();
        }

        return Ok(new GetPaymentResponse
        {
            Id = payment.Id,
            Status = payment.Status,
            MaskedCardNumber = payment.MaskedCardNumber,
            CardNumberLastFour = payment.CardNumberLastFour,
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount
        });
    }
}
