using System.Net;
using System.Text;
using System.Text.Json;
using Json.Schema;

namespace GateProof.Client.Tests;

public class WebhookSignatureTests
{
    // The same vector is tested on the GateProof server, so the SDK and the server can't drift apart.
    private const string Secret = "whsec_test_vector";
    private const string Body = """{"id":"01a0fe9c-6fc4-7204-bff3-4926f53fd92f","type":"verification.completed"}""";
    private const string Header = "t=1791230400,v1=03cdd5fb18600dd7f716451b22157df25494c6da9921c2ae00ecc7723f90fc73";
    private static readonly DateTimeOffset At = DateTimeOffset.FromUnixTimeSeconds(1791230400);

    [Fact]
    public void Accepts_the_published_test_vector() => Assert.True(WebhookSignature.IsValid(Secret, Body, Header, At));

    [Fact]
    public void Rejects_a_changed_body_wrong_secret_missing_header_and_old_signature()
    {
        Assert.False(WebhookSignature.IsValid(Secret, Body.Replace("completed", "expired"), Header, At));
        Assert.False(WebhookSignature.IsValid("whsec_other", Body, Header, At));
        Assert.False(WebhookSignature.IsValid(Secret, Body, null, At));
        Assert.False(WebhookSignature.IsValid(Secret, Body, Header, At.AddMinutes(6)));
    }

    [Fact]
    public void Parse_verifies_then_reads_the_event()
    {
        var body = File.ReadAllText(Path.Combine(Repo.Root, "examples", "webhook-verification-completed.json"));
        var header = Sign(Secret, body, At);

        var evt = WebhookSignature.Parse(Secret, body, header, At);

        Assert.Equal(WebhookEventTypes.VerificationCompleted, evt.Type);
        Assert.Equal(Verdicts.Verified, evt.Data.Verdict);
        Assert.Equal(2, evt.Data.CheckInCount);
        Assert.Throws<InvalidOperationException>(() => WebhookSignature.Parse("whsec_other", body, header, At));
    }

    private static string Sign(string secret, string body, DateTimeOffset at)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var t = at.ToUnixTimeSeconds();
        return $"t={t},v1={Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{t}.{body}")))}";
    }
}

public class GateProofClientTests
{
    private const string Key = "gp_test_0123456789abcdef0123456789abcdef";

    private static (GateProofClient Client, StubHandler Handler) Client(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        return (new GateProofClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.gateproof.test/") }, Key), handler);
    }

    [Fact]
    public async Task Create_sends_snake_case_json_the_key_and_an_idempotency_key()
    {
        var (client, handler) = Client(HttpStatusCode.Created,
            """{"id":"01a0fe9c-66a0-74cc-a245-d4c08ea56fd6","environment":"sandbox","status":"created","claimed_address":"12 Example Street","customer_phone_last4":"5678","check_in_count":0,"reason_codes":[],"link_url":"https://verify.example/v/abc","object":"verification","future_field":"ignored"}""");

        var v = await client.CreateVerificationAsync(new("08012345678", "12 Example Street", "loan-1"), idempotencyKey: "retry-safe-1");

        Assert.Equal("https://verify.example/v/abc", v.LinkUrl);
        Assert.Equal(VerificationStatuses.Created, v.Status);
        Assert.False(v.IsFinished);
        var request = handler.Requests.Single();
        Assert.Equal("https://api.gateproof.test/v1/verifications", request.Uri);
        Assert.Equal($"Bearer {Key}", request.Authorization);
        Assert.Equal("retry-safe-1", request.IdempotencyKey);
        Assert.StartsWith("gateproof-dotnet/", request.UserAgent);
        Assert.Contains("\"customer_phone\":\"08012345678\"", request.Body);
        Assert.Contains("\"external_ref\":\"loan-1\"", request.Body);
    }

    [Fact]
    public async Task An_idempotency_key_is_generated_when_not_given()
    {
        var (client, handler) = Client(HttpStatusCode.Created, """{"id":"01a0fe9c-66a0-74cc-a245-d4c08ea56fd6","status":"created"}""");

        await client.CreateVerificationAsync(new("08012345678", "x"));

        Assert.Equal(32, handler.Requests.Single().IdempotencyKey!.Length);
    }

    [Fact]
    public async Task Validation_errors_become_a_typed_exception()
    {
        var (client, _) = Client(HttpStatusCode.BadRequest,
            """{"title":"One or more validation errors occurred.","status":400,"errors":{"customer_phone":["Must be a Nigerian mobile number."]}}""");

        var ex = await Assert.ThrowsAsync<GateProofApiException>(() => client.CreateVerificationAsync(new("12345", "x")));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
        Assert.Equal(["Must be a Nigerian mobile number."], ex.Errors["customer_phone"]);
        Assert.False(ex.IsTransient);
    }

    [Fact]
    public async Task Rate_limits_and_server_errors_are_transient()
    {
        var (client, _) = Client(HttpStatusCode.TooManyRequests, "");

        var ex = await Assert.ThrowsAsync<GateProofApiException>(() => client.GetVerificationAsync(Guid.NewGuid()));

        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task Evidence_pack_is_typed_and_keeps_the_raw_document()
    {
        var example = File.ReadAllText(Path.Combine(Repo.Root, "examples", "evidence-pack-example.json"));
        var (client, _) = Client(HttpStatusCode.OK, example);

        var pack = await client.GetEvidencePackAsync(Guid.NewGuid());

        Assert.Equal("gateproof.evidence-pack/v1", pack.Schema);
        Assert.Equal(Verdicts.Verified, pack.Result!.Verdict);
        Assert.Equal("gate_photo", Assert.Single(pack.Evidence).Kind);
        Assert.Equal(JsonDocument.Parse(example).RootElement.GetProperty("check_ins").GetArrayLength(), pack.Raw.GetProperty("check_ins").GetArrayLength());
    }

    [Fact]
    public void Rejects_keys_that_are_not_gateproof_keys() =>
        Assert.Throws<ArgumentException>(() => new GateProofClient(new HttpClient { BaseAddress = new Uri("https://x.test/") }, "nipost_live_abc"));

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<(string Uri, string? Authorization, string? IdempotencyKey, string UserAgent, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("Idempotency-Key", out var k) ? k.Single() : null,
                request.Headers.UserAgent.ToString(),
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct)));
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}

public class PublishedContractTests
{
    [Fact]
    public void Example_evidence_pack_matches_the_schema()
    {
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(Repo.Root, "schemas", "evidence-pack-v1.schema.json")));
        using var example = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo.Root, "examples", "evidence-pack-example.json")));

        var result = schema.Evaluate(example.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true });

        Assert.True(result.IsValid, string.Join("\n", (result.Details ?? []).Where(d => d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))));
    }

    [Fact]
    public void OpenApi_document_is_the_lender_api()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo.Root, "openapi", "gateproof-v1.json")));

        Assert.StartsWith("3.", doc.RootElement.GetProperty("openapi").GetString());
        Assert.True(doc.RootElement.GetProperty("paths").TryGetProperty("/v1/verifications", out _));
    }
}

internal static class Repo
{
    /// <summary>The repository root, found by walking up from the test binaries to the folder holding LICENSE.</summary>
    public static readonly string Root = Find(AppContext.BaseDirectory);

    private static string Find(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "LICENSE")) && Directory.Exists(Path.Combine(dir.FullName, "schemas")))
                return dir.FullName;
        throw new InvalidOperationException("Repository root not found.");
    }
}
