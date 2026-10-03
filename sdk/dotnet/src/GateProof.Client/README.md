# GateProof.Client

.NET client for the GateProof proof-of-address API. Targets .NET 8 and .NET 10.

```csharp
var http = new HttpClient { BaseAddress = new Uri(gateProofApiUrl) };
var gateproof = new GateProofClient(http, Environment.GetEnvironmentVariable("GATEPROOF_API_KEY")!);

// Sandbox test number: finishes instantly as "verified" and fires your webhook.
var v = await gateproof.CreateVerificationAsync(new("08000000001", "12 Allen Avenue, Ikeja", ExternalRef: "loan-42"));
Console.WriteLine($"{v.Status} {v.Verdict}");

// In your webhook endpoint: verify first, then trust the body.
var evt = WebhookSignature.Parse(webhookSecret, rawBody, request.Headers["GateProof-Signature"], DateTimeOffset.UtcNow);
if (evt.Type == WebhookEventTypes.VerificationCompleted && evt.Data.Verdict == Verdicts.Verified) { /* proceed */ }

// For your records: the evidence pack, stored exactly as received.
var pack = await gateproof.GetEvidencePackAsync(v.Id);
await File.WriteAllTextAsync($"evidence-{v.Id}.json", pack.Raw.GetRawText());
```

Keep your API key on your server. See the repository README for the API, webhooks and sandbox.
