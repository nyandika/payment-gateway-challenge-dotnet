# Payment Gateway

A simple payment gateway API built for the Checkout.com take-home exercise.

## Features

- Process a card payment
- Retrieve a previously processed payment
- Validate requests before calling the bank
- Mask card details in responses

## Tech stack

- .NET 10
- ASP.NET Core Web API
- xUnit
- Docker for the bank simulator

## Architecture

The solution keeps clean architecture boundaries inside one project using folders:

- `Domain`
- `Application`
- `Infrastructure`
- `Api`

I kept these as folders rather than separate projects to avoid unnecessary overhead for a small exercise, while still keeping the same logical boundaries

## API

### Process payment

`POST /api/payments`

Example request:

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

Possible outcomes:

- `201 Created` with status `Authorized`
- `201 Created` with status `Declined`
- `400 Bad Request` with status `Rejected`
- `503 Service Unavailable` if the bank simulator is unavailable

### Retrieve payment

`GET /api/payments/{id}`

Returns the stored payment details with:

- masked card number
- last four card digits
- payment status

## Running locally

### Start the bank simulator

```bash
docker-compose up
```

The simulator listens on:

- `http://localhost:8080/payments`

### Start the API

```bash
dotnet run --project src/PaymentGateway.Api
```

The API runs on:

- `https://localhost:7092`
- `http://localhost:5067`

HTTP is redirected to HTTPS, so `https://localhost:7092` is the endpoint to test manually

### Open Swagger UI

Open this URL in your browser:

- `https://localhost:7092/swagger`

### 1. Authorized payment

Open `POST /api/Payments`, click `Try it out`, and use:

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

Expected result:

- `201 Created`
- response body contains `"status": "Authorized"`
- response body contains an `id`

Copy the `id` for the retrieval test below.

### 2. Declined payment

Use:

```json
{
  "cardNumber": "2222405343248878",
  "expiryMonth": 12,
  "expiryYear": 2027,
  "currency": "GBP",
  "amount": 1050,
  "cvv": "123"
}
```

Expected result:

- `201 Created`
- response body contains `"status": "Declined"`

### 3. Rejected payment

Use:

```json
{
  "cardNumber": "1234",
  "expiryMonth": 1,
  "expiryYear": 2020,
  "currency": "ABC",
  "amount": 0,
  "cvv": "12"
}
```

Expected result:

- `400 Bad Request`
- response body contains `"status": "Rejected"`
- response body contains validation errors

### 4. Bank unavailable

Use a card number ending in `0`:

```json
{
  "cardNumber": "2222405343248870",
  "expiryMonth": 12,
  "expiryYear": 2027,
  "currency": "GBP",
  "amount": 1050,
  "cvv": "123"
}
```

Expected result:

- `503 Service Unavailable`

### 5. Retrieve payment

Open `GET /api/Payments/{id}`, click `Try it out`, and paste the `id` from the authorized payment.

Expected result:

- `200 OK`
- masked card number only
- last four digits visible
- no full card number returned

## Business rules

- Card number must be numeric and 14 to 19 digits long
- Expiry month must be between 1 and 12
- Expiry date must be in the future
- Supported currencies are `GBP`, `USD`, and `EUR`
- Amount must be a positive integer in minor units
- CVV must be numeric and 3 to 4 digits long

## Tests

The automated tests include both unit and integration tests.
The tests are run using `dotnet test`.

The integration tests run end to end against the real bank simulator, so the simulator should be running before the full test suite is executed.

Unit tests cover:

- payment processing logic
- validation and rejection rules
- request sanitization
- repository interaction through the application service

Integration tests cover:

- API behavior for authorized, declined, and rejected payments
- payment retrieval
- bank unavailable responses

These integration tests run through the API and the live bank simulator.

## Notes

- Payments are stored in memory. This means they are lost when the application is restarted
- The API returns payment statuses as strings: `Authorized`, `Declined`, and `Rejected`
- The detailed implementation and verification notes are available in [docs/README.detailed.md](docs/README.detailed.md)
