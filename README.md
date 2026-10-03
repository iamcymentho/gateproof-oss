# GateProof (open source)

GateProof is proof of address for Nigerian lenders. A lender sends a customer a link; the customer confirms their home from their phone (two check-ins at different times of day, plus a photo of their gate); GateProof returns a verdict (verified, needs review, not verified) with the reasons and the evidence behind it.

This repository holds the open parts: everything you need to integrate with GateProof and to understand and check its results. The service itself, including the scoring rules and fraud detection, is not here.

| Folder | What it is |
|---|---|
| [`sdk/dotnet`](sdk/dotnet) | .NET client (net8.0, net10.0): create verifications, read verdicts and evidence packs, verify webhooks. |
| [`schemas`](schemas) | JSON Schema for the evidence pack (`gateproof.evidence-pack/v1`). |
| [`openapi`](openapi) | OpenAPI 3.1 description of the lender API. |
| [`examples`](examples) | An example evidence pack and webhook body (made-up sandbox data). |
| [`docs/methodology.md`](docs/methodology.md) | How a verdict is reached, in plain English. |
| [`docs/sandbox-and-webhooks.md`](docs/sandbox-and-webhooks.md) | Sandbox test numbers, webhook events and signature verification. |

## Quick start (.NET)

```csharp
var http = new HttpClient { BaseAddress = new Uri(gateProofApiUrl) };
var gateproof = new GateProofClient(http, Environment.GetEnvironmentVariable("GATEPROOF_API_KEY")!);

// Sandbox test number: finishes instantly as "verified" and fires your webhook.
var v = await gateproof.CreateVerificationAsync(new("08000000001", "12 Allen Avenue, Ikeja", ExternalRef: "loan-42"));
```

Build and test the SDK:

```bash
cd sdk/dotnet
dotnet test
```

## The API in brief

Authenticate with `Authorization: Bearer gp_test_…` (sandbox) or `gp_live_…` (live). JSON is snake_case. Full description: [openapi/gateproof-v1.json](openapi/gateproof-v1.json).

| Method | Path | |
|---|---|---|
| `POST` | `/v1/verifications` | Create. Send an `Idempotency-Key` header so retries never create duplicates. |
| `GET` | `/v1/verifications/{id}` | Status, verdict, confidence, reason codes. |
| `GET` | `/v1/verifications/{id}/evidence-pack` | Everything behind the verdict. |
| `GET` | `/v1/verifications/{id}/files/{fileId}` | The gate photo or utility bill. |
| `GET` `PUT` `DELETE` | `/v1/settings/nipost-key` | Your own NIPOST key for the environment you call from. |

## Evidence pack

The evidence pack is the record a lender keeps for auditors: consent, home pin, check-ins, files with SHA-256 hashes, NIPOST result, verdict with reasons and the audit trail. The schema is closed (`additionalProperties: false`), so any change to the shape is a new schema version. Validate a stored pack with any JSON Schema 2020-12 validator.

## NIPOST

GateProof uses Nigeria's national postcode system (NDAPS) as one signal, called with each lender's own NIPOST key. GateProof is not affiliated with NIPOST, and a GateProof verdict is not a NIPOST verification: a postcode identifies a building, not who lives there.

## License

[Apache 2.0](LICENSE).
