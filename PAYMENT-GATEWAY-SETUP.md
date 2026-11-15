# swapDigit Razorpay integration foundation

The API is an ASP.NET Core 10 service and is hosting-neutral: deploy it to IIS, Google Cloud Run/Compute Engine, Azure App Service/Container Apps, or another .NET 10 host.

## Server-side settings

Set these as environment variables / secret-store entries on the API host. Do not put secret values in the MAUI app, APK/EXE, repository, or appsettings files.

- `RAZORPAY_KEY_ID` — Razorpay key ID (test key for sandbox)
- `RAZORPAY_KEY_SECRET` — Razorpay API secret
- `RAZORPAY_WEBHOOK_SECRET` — webhook signing secret

Configure Razorpay Dashboard webhook URL as `https://YOUR-API-HOST/api/v1/payments/webhook` and subscribe to payment events needed for reconciliation. Use HTTPS only.

## API

- `GET /health` — health probe
- `POST /api/v1/payments/orders` — body: `{ "amountPaise": 12500, "receipt": "INV-1001", "orderType": "POS" }`; creates an INR Razorpay Order and returns its ID and public Key ID.
- `POST /api/v1/payments/verify` — body: `{ "orderId": "...", "paymentId": "...", "signature": "..." }`; verifies the checkout signature server-side.
- `POST /api/v1/payments/webhook` — verifies `X-Razorpay-Signature` against the raw request body.

## Important deployment status / production hardening

This repository addition is the **backend integration foundation**, not yet an end-to-end live POS rollout. The current API uses in-memory pending-order state and acknowledges signed webhooks without durable event processing. Before production, add persistent storage and idempotent invoice/payment reconciliation, authentication/authorization for POS clients, rate limiting, audit logs, HTTPS/reverse-proxy hardening, and webhook event persistence. Connect the MAUI checkout to the deployed API and Razorpay Checkout SDK/web checkout, then test UPI and card flows with Razorpay test credentials. The existing UI still has its local demo checkout; do not treat that as a verified online payment.

## Hosting

- IIS: publish the API, host behind IIS with the ASP.NET Core Hosting Bundle, set environment variables in the app pool/site environment.
- GCP: container image can be deployed to Cloud Run; set secrets through Secret Manager.
- Azure: deploy container/App Service and reference secrets from Key Vault or app settings.

Use Razorpay Test Mode first. Switch to live credentials only after account activation and end-to-end verification.
