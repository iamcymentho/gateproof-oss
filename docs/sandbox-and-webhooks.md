# Sandbox and webhooks

## Sandbox

Use a `gp_test_` API key. Sandbox verifications never message anyone: the create response includes `link_url`, which you open yourself to play the customer.

### Test numbers

In the sandbox only, these numbers finish instantly with a fixed result and fire the real webhook, with no customer steps:

| `customer_phone` | Result | Webhook |
|---|---|---|
| `08000000001` | `completed`, `verified` | `verification.completed` |
| `08000000002` | `completed`, `needs_review` | `verification.completed` |
| `08000000003` | `completed`, `not_verified` | `verification.completed` |
| `08000000004` | `expired` | `verification.expired` |

Their reason codes include `SANDBOX_SCENARIO`. With a live key these are treated as ordinary numbers.

### NIPOST in the sandbox

Give GateProof a NIPOST **test** key with `PUT /v1/settings/nipost-key`. Sandbox refuses `nipost_live_` keys and live requires them, so test traffic never spends live NIPOST credits.

For local development without NIPOST access, the open-source [ng-postcode](https://github.com/poliha/ng-postcode) mock (MIT, by Peter Oliha) follows NIPOST's published API: `npx -p ng-postcode ng-postcode-mock`.

## Webhooks

GateProof sends a `POST` to your webhook URL when a verification finishes:

| Event | When |
|---|---|
| `verification.completed` | A verdict is ready. |
| `verification.expired` | The customer didn't finish before the link expired. |
| `verification.cancelled` | You cancelled an open verification. |

The body is an event wrapping the verification (see [examples/webhook-verification-completed.json](../examples/webhook-verification-completed.json)). Headers:

| Header | Meaning |
|---|---|
| `GateProof-Signature` | `t={unix seconds},v1={hex}` |
| `GateProof-Event` | The event type. |
| `GateProof-Delivery` | A unique id per delivery. Use it to ignore duplicates. |

### Verifying the signature

`v1` is HMAC-SHA256 of `"{t}.{raw body}"`, keyed with your webhook secret, as lowercase hex. Check it against the **raw** body before parsing, compare in constant time, and reject a `t` more than five minutes from your clock.

Test vector:

```
secret:  whsec_test_vector
body:    {"id":"01a0fe9c-6fc4-7204-bff3-4926f53fd92f","type":"verification.completed"}
t:       1791230400
header:  t=1791230400,v1=03cdd5fb18600dd7f716451b22157df25494c6da9921c2ae00ecc7723f90fc73
```

The .NET SDK does this in `WebhookSignature.Parse`.

### Delivery

Answer with any 2xx quickly and do slow work afterwards. Failed deliveries are retried with exponential backoff (30 seconds, doubling, up to 6 hours between attempts) for 8 attempts. Deliveries can arrive more than once; treat them idempotently using `GateProof-Delivery` or the verification id.
