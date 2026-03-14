# Curl Testing Guide

This guide provides a command-line way to verify the payment gateway and the bank simulator without using Swagger UI.

## Prerequisites

1. Start the bank simulator:

```bash
docker-compose up
```

2. Start the API:

```bash
dotnet run --project src/PaymentGateway.Api
```

Use the HTTPS endpoint for the API:

- `https://localhost:7092`

For `curl`, use `-k` to ignore the local development certificate.

## 1. Call the simulator directly

This verifies the provided bank simulator independently of the gateway.

```bash
curl -i http://localhost:8080/payments \
  -H "Content-Type: application/json" \
  -d '{
    "card_number": "2222405343248877",
    "expiry_date": "12/2027",
    "currency": "GBP",
    "amount": 1050,
    "cvv": "123"
  }'
```

Expected result:

- `200 OK`
- body includes `"authorized": true`

## 2. Authorized payment through the gateway

```bash
curl -i -k https://localhost:7092/api/payments \
  -H "Content-Type: application/json" \
  -d '{
    "cardNumber": "2222405343248877",
    "expiryMonth": 12,
    "expiryYear": 2027,
    "currency": "GBP",
    "amount": 1050,
    "cvv": "123"
  }'
```

Expected result:

- `201 Created`
- response body contains `"status": "Authorized"`
- response body contains an `id`

Copy the `id` for the retrieval test below.

## 3. Declined payment through the gateway

```bash
curl -i -k https://localhost:7092/api/payments \
  -H "Content-Type: application/json" \
  -d '{
    "cardNumber": "2222405343248878",
    "expiryMonth": 12,
    "expiryYear": 2027,
    "currency": "GBP",
    "amount": 1050,
    "cvv": "123"
  }'
```

Expected result:

- `201 Created`
- response body contains `"status": "Declined"`

## 4. Rejected payment through the gateway

```bash
curl -i -k https://localhost:7092/api/payments \
  -H "Content-Type: application/json" \
  -d '{
    "cardNumber": "1234",
    "expiryMonth": 1,
    "expiryYear": 2020,
    "currency": "ABC",
    "amount": 0,
    "cvv": "12"
  }'
```

Expected result:

- `400 Bad Request`
- response body contains `"status": "Rejected"`
- response body contains validation errors

## 5. Bank unavailable through the gateway

Use a card number ending in `0`:

```bash
curl -i -k https://localhost:7092/api/payments \
  -H "Content-Type: application/json" \
  -d '{
    "cardNumber": "2222405343248870",
    "expiryMonth": 12,
    "expiryYear": 2027,
    "currency": "GBP",
    "amount": 1050,
    "cvv": "123"
  }'
```

Expected result:

- `503 Service Unavailable`

## 6. Retrieve a previously created payment

Use the `id` from a successful payment response:

```bash
curl -i -k https://localhost:7092/api/payments/{paymentId}
```

Expected result:

- `200 OK`
- masked card number only
- last four digits visible
- no full card number returned

## 7. Idempotent retry

Send the same request twice with the same `Idempotency-Key` header:

```bash
curl -i -k https://localhost:7092/api/payments \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: retry-123" \
  -d '{
    "cardNumber": "2222405343248877",
    "expiryMonth": 12,
    "expiryYear": 2027,
    "currency": "GBP",
    "amount": 1050,
    "cvv": "123"
  }'
```

Run the same command again with the same header value.

Expected result:

- both responses return the same payment `id`
- the original completed result is replayed

Note:

- this minimal idempotency behavior lasts only while the app is running
- `503` bank-unavailable failures are not cached
