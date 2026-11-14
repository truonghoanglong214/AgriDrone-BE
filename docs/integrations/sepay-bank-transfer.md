# SePay bank-transfer QR integration

> **V3 scope notice — 2026-11-10.**
> Integration scope: SePay remains the MVP transport adapter. Business Rules v3 moves deposit to every first/follow-up Order and balance to post-review release; existing runtime has not been changed. Refunds are manual bank transfers with proof, not an assumed automatic gateway refund API.
> Current: [Business Rules v3](../../Codex-Plan/Business-Rules-v3.md), [ADR 0018](../adr/0018-v3-paid-map-combo-business-policy.md), [target contracts](../contracts/survey-business-v3.md). Documentation-only update; no runtime change.

The `AgriDrone.Integrations.SePay` integration creates a per-payment VietQR image URL and verifies incoming bank-transfer webhooks. Payment amount, currency, order readiness, and duplicate transaction checks remain in Surveys.

## Configuration

Set these environment variables for the API deployment. Do not commit the webhook secret or bank details to source control.

```text
SurveyPayments__Enabled=true
SurveyPayments__Provider=sepay
SePay__ApiKey=<SePay webhook API key>
SePay__BankAccountNumber=<beneficiary account number>
SePay__BankShortName=<bank short name or alias supported by VietQR>
SePay__AccountHolder=<account holder name>
SePay__PaymentCodePrefix=AGD
SePay__QrTemplate=compact
```

The payment reference is `AGD` plus a unique base-36 encoding of the payment ID. In SePay Company Settings, enable payment-code recognition and configure prefix `AGD` with an alphanumeric suffix of 1–25 characters. Configure a webhook for incoming transfers, select API Key authentication, and save the generated key in `SePay__ApiKey`. SePay sends it as `Authorization: Apikey <API_KEY>`. Use this URL:

```text
https://<public-api-host>/api/integrations/payments/sepay/callbacks
```

The callback compares the API key from the `Authorization` header using a constant-time comparison. It acknowledges irrelevant transfers and unknown payment references with `200 {"success":true}` so SePay does not retry transactions that cannot affect an order. Valid transfers are deduplicated by SePay transaction ID and matched by the extracted payment code.

## Payment flow

The TenantOwner initiates a payment using `POST /api/tenant-owner/survey-orders/{orderId}/payments`. The server selects the amount and purpose from the order and returns the QR URL in `redirectUrl`, the transfer code in `providerReference` and `clientToken`, and the payment expiry. The frontend displays the QR image and transfer code. The client never confirms the payment; the authenticated SePay webhook supplies provider evidence, and Surveys validates/deduplicates it before confirming the business payment. This adapter uses the API-key authentication described above, not a generic HMAC assumption.

SePay payment-code recognition must be enabled in both Test mode and Live mode separately. Test the webhook and transfer-code extraction before using real bank transfers.
