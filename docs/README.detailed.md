# Payment Gateway Challenge

This repo contains my implementation of the Checkout.com payment gateway take-home exercise using ASP.NET Core and .NET 10.

The service supports two core capabilities:

- processing a card payment through a simulated acquiring bank
- retrieving the details of a previously processed payment

## Reviewer guide

If you want the fastest path through the solution, I suggest:

1. read the `Architecture` and `Design decisions and tradeoffs` sections
2. run the project using the commands in `Running locally`
3. follow the `Swagger testing` section
4. review the automated coverage in `Test coverage`

## Summary

The API exposes:

- `POST /api/payments` to process a payment
- `GET /api/payments/{id}` to retrieve a stored payment

For a valid payment request, the gateway:

1. validates the request locally
2. rejects invalid requests without calling the bank
3. forwards valid requests to the acquiring bank simulator
4. stores the payment result in an in-memory repository
5. returns only masked card details to the caller

The `POST /api/payments` endpoint also supports an optional `Idempotency-Key` header. If the same key is reused while the application is still running, the gateway returns the original completed result instead of processing the payment again.

## Technology choices

- .NET 10
- ASP.NET Core Web API
- xUnit
- unit tests for `PaymentService`
- integration tests using `WebApplicationFactory`
- in-memory repository as allowed by the exercise
- Docker-based bank simulator provided by the exercise

## What I optimized for

The goal of this implementation was to stay pragmatic:

- satisfy the exercise requirements fully
- keep the design clean and easy to reason about
- avoid overengineering for the size of the problem
- make the reviewer experience straightforward through detailed documentation and reproducible examples

## Architecture

I kept clean architecture boundaries inside a single ASP.NET Core project using folders rather than separate projects.

The dependency direction is:

`Api -> Application -> Domain`

with `Infrastructure` providing implementations for `Application` abstractions.

- `Domain`
  Contains core business entities and enums such as `Payment` and `PaymentStatus`.
- `Application`
  Contains use-case orchestration, validation, and interfaces such as `IPaymentRepository` and `IAcquiringBankClient`.
- `Infrastructure`
  Contains implementation details such as the in-memory repository and the HTTP client for the bank simulator.
- `Api`
  Contains the HTTP-facing concerns, including controllers and transport contracts.

I intentionally kept these boundaries as folders instead of splitting them into separate projects. For an exercise of this size, the extra project overhead would add ceremony without improving the design materially. The dependency boundaries still apply, and if the service grew these folders could be promoted into separate projects with minimal change.

## Project structure

```text
src/
  PaymentGateway.Api/
    Api/
    Application/
    Domain/
    Infrastructure/
test/
  PaymentGateway.Api.Tests/
imposters/
  bank_simulator.ejs
docker-compose.yml
PaymentGateway.sln
```

## Supported business rules

### Request validation

The gateway validates the following before attempting a bank call:

- card number is required, numeric only, and 14 to 19 digits long
- expiry month is between 1 and 12
- expiry month and year together are in the future
- currency is one of `GBP`, `USD`, or `EUR`
- amount is a positive integer in minor units
- CVV is numeric only and 3 to 4 digits long

If validation fails, the API returns a rejected payment response and does not call the bank simulator.

### Payment outcomes

The gateway can return these payment statuses:

- `Authorized`
- `Declined`
- `Rejected`

Behavior is aligned to the simulator:

- odd final card digit: authorized
- even final card digit except `0`: declined
- final digit `0`: simulator unavailable, propagated as HTTP `503`

### Card data handling

The gateway never returns the full card number.

- `POST /api/payments` returns the last four digits only
- `GET /api/payments/{id}` returns a masked card number plus the last four digits

This keeps the implementation aligned with the exercise while avoiding unnecessary exposure of sensitive card data.

### Idempotency

`POST /api/payments` supports an optional `Idempotency-Key` header.

Current behavior:

- first request with a new key is processed normally
- repeated request with the same key returns the original completed result
- the idempotency store is in memory, so this behavior lasts only for the lifetime of the running app
- `503` bank-unavailable failures are not cached, so a retry can attempt the bank call again

## API contract

### Process payment

`POST /api/payments`

Request body:

```json
{
  "cardNumber": "2222405343248877",
  "expiryMonth": 12,
  "expiryYear": 2027,
  "currency": "GBP",
  "amount": 1050,
  "cvv": "123"
}
```

Successful response for an authorized or declined payment:

- status code: `201 Created`

Example body:

```json
{
  "id": "6d7a5c90-cdc1-4db2-8c3c-f9d9af22d4e1",
  "status": "Authorized",
  "cardNumberLastFour": "8877",
  "expiryMonth": 12,
  "expiryYear": 2027,
  "currency": "GBP",
  "amount": 1050,
  "errors": []
}
```

Rejected response:

- status code: `400 Bad Request`

Example body:

```json
{
  "id": null,
  "status": "Rejected",
  "cardNumberLastFour": null,
  "expiryMonth": 0,
  "expiryYear": 0,
  "currency": null,
  "amount": 0,
  "errors": [
    "Card number must contain only digits and be between 14 and 19 characters long."
  ]
}
```

Bank unavailable response:

- status code: `503 Service Unavailable`
- body uses `application/problem+json`

Idempotent replay:

- if the same `Idempotency-Key` is reused for a previously completed request, the API returns the original response body rather than creating a new payment

### Retrieve payment

`GET /api/payments/{id}`

Successful response:

- status code: `200 OK`

Example body:

```json
{
  "id": "6d7a5c90-cdc1-4db2-8c3c-f9d9af22d4e1",
  "status": "Authorized",
  "maskedCardNumber": "************8877",
  "cardNumberLastFour": "8877",
  "expiryMonth": 12,
  "expiryYear": 2027,
  "currency": "GBP",
  "amount": 1050
}
```

Not found response:

- status code: `404 Not Found`

## Running locally

### Prerequisites

- .NET SDK 10
- Docker Desktop or a running Docker daemon

### 1. Start the bank simulator

```bash
docker-compose up
```

The simulator listens on:

- `http://localhost:8080/payments`

### 2. Start the API

From the root of the repo:

```bash
dotnet run --project src/PaymentGateway.Api
```

The launch settings expose the API on:

- `http://localhost:5067`
- `https://localhost:7092`

In practice, the app uses HTTPS redirection.

### 3. Run the automated tests

```bash
dotnet test
```

The integration tests run end to end against the real bank simulator, so the simulator should be running before the full test suite is executed.

## Swagger testing

The primary manual testing path for this project is Swagger UI.

Open:

- `https://localhost:7092/swagger`

Recommended browser flow:

1. open `POST /api/Payments`
2. submit an authorized payment
3. submit a declined payment
4. submit a rejected payment
5. submit a bank-unavailable payment
6. copy the `id` from an authorized response
7. open `GET /api/Payments/{id}` and retrieve the stored payment

For exact request bodies and expected outcomes, see [swagger-testing.md](swagger-testing.md).

## Optional curl verification

If you prefer command-line testing, see [curl-testing.md](curl-testing.md).

## Design decisions and tradeoffs

This exercise intentionally leaves room for implementation choice. The decisions below were made to keep the solution small, explicit, and easy to discuss in an interview setting.

### Why an in-memory persistence layer

The exercise explicitly allows a test double repository. I used an in-memory implementation to keep the solution focused on the payment flow rather than persistence concerns.

Tradeoff:

- payment data is lost when the process restarts

The same applies to the minimal idempotency store in this exercise: retries are handled only while the app remains running.

### Why folder-based clean architecture

The exercise is small enough that multiple projects would add overhead without much value. I kept the boundaries explicit in code and DI registration, while avoiding unnecessary project and package ceremony.

Tradeoff:

- compile-time boundary enforcement is lighter than it would be with separate projects

### Why statuses are returned as strings

The exercise describes status values as `Authorized`, `Declined`, and `Rejected`. Returning those as strings makes the API contract clearer for clients.

## Test coverage

The automated test suite includes both unit and integration tests.

Unit tests cover:

- payment processing logic in `PaymentService`
- validation and rejection behavior
- sanitization of card number, currency, and CVV
- persistence behavior through the repository abstraction

Integration tests cover:

- authorized payment
- declined payment
- rejected payment
- payment retrieval
- bank unavailable response
- idempotent replay for repeated requests with the same key

These integration tests run through the API and the live bank simulator.
