# Swagger Testing Guide

This API exposes Swagger UI in the Development environment, which makes it easy to test the endpoints in a browser without using `curl`.

## Prerequisites

1. Start the bank simulator:

```bash
docker-compose up
```

2. Start the API:

```bash
dotnet run --project src/PaymentGateway.Api
```

## Open Swagger UI

Open this URL in your browser:

- `https://localhost:7092/swagger`

## Endpoints to test

Swagger should show two payment endpoints:

- `POST /api/Payments`
- `GET /api/Payments/{id}`

## Suggested browser test flow

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

### 6. Idempotent retry

Open `POST /api/Payments`, click `Try it out`, and add a header:

- `Idempotency-Key: retry-123`

Submit an authorized payment, then submit the same request again with the same header value.

Expected result:

- both responses return the same payment `id`
- the original completed result is replayed

Note:

- this minimal idempotency behavior lasts only while the app is running
- `503` bank-unavailable failures are not cached
