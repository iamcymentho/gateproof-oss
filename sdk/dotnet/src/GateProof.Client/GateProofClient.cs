using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace GateProof.Client;

/// <summary>
/// Client for the GateProof lender API. Use one instance per API key; it is safe to share across threads.
/// Keep the key on your server: <c>gp_live_</c> keys must never reach a browser or mobile app.
/// </summary>
public sealed class GateProofClient
{
    private const string UserAgent = "gateproof-dotnet/0.1.0";
    private readonly HttpClient _http;

    /// <param name="http">An HttpClient whose <see cref="HttpClient.BaseAddress"/> is the GateProof API URL (e.g. from IHttpClientFactory).</param>
    /// <param name="apiKey">Your <c>gp_test_</c> (sandbox) or <c>gp_live_</c> key.</param>
    public GateProofClient(HttpClient http, string apiKey)
    {
        if (http.BaseAddress is null) throw new ArgumentException("HttpClient.BaseAddress must be set to the GateProof API URL.", nameof(http));
        if (!apiKey.StartsWith("gp_test_", StringComparison.Ordinal) && !apiKey.StartsWith("gp_live_", StringComparison.Ordinal))
            throw new ArgumentException("API keys start with gp_test_ or gp_live_.", nameof(apiKey));

        _http = http;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    /// <summary>
    /// Creates a verification. Live: the customer is texted the link. Sandbox: use <see cref="Verification.LinkUrl"/>
    /// yourself, or a test number (08000000001-4) for an instant fixed result.
    /// </summary>
    /// <param name="idempotencyKey">Send the same key when retrying, so a timeout never creates a duplicate. One is generated if omitted.</param>
    public async Task<Verification> CreateVerificationAsync(CreateVerificationRequest request, string? idempotencyKey = null, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/verifications")
        {
            Content = JsonContent.Create(request, options: Json.Options),
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
        return await SendAsync<Verification>(message, ct);
    }

    public Task<Verification> GetVerificationAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<Verification>(new HttpRequestMessage(HttpMethod.Get, $"v1/verifications/{id}"), ct);

    /// <summary>Everything behind the verdict. Each call is recorded in the verification's audit trail.</summary>
    public async Task<EvidencePack> GetEvidencePackAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"v1/verifications/{id}/evidence-pack"), ct);
        await EnsureSuccessAsync(response, ct);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var pack = document.RootElement.Deserialize<EvidencePack>(Json.Options)!;
        pack.Raw = document.RootElement.Clone();
        return pack;
    }

    /// <summary>Downloads a file listed in an evidence pack. Throws with status 410 once it has been deleted.</summary>
    public async Task<Stream> DownloadFileAsync(PackFile file, CancellationToken ct = default)
    {
        if (file.DownloadUrl is null)
            throw new GateProofApiException(HttpStatusCode.Gone, "File deleted at the end of its retention period.", null);

        var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Get, file.DownloadUrl.TrimStart('/')),
            HttpCompletionOption.ResponseHeadersRead, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadAsStreamAsync(ct);
    }

    /// <summary>Saves your own NIPOST key for the environment of this client's API key.</summary>
    public async Task SetNipostKeyAsync(string nipostApiKey, CancellationToken ct = default)
    {
        using var response = await _http.PutAsJsonAsync("v1/settings/nipost-key", new { api_key = nipostApiKey }, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public Task<NipostKeyStatus> GetNipostKeyStatusAsync(CancellationToken ct = default) =>
        SendAsync<NipostKeyStatus>(new HttpRequestMessage(HttpMethod.Get, "v1/settings/nipost-key"), ct);

    public async Task RemoveNipostKeyAsync(CancellationToken ct = default)
    {
        using var response = await _http.DeleteAsync("v1/settings/nipost-key", ct);
        await EnsureSuccessAsync(response, ct);
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage message, CancellationToken ct)
    {
        using (message)
        using (var response = await _http.SendAsync(message, ct))
        {
            await EnsureSuccessAsync(response, ct);
            return (await response.Content.ReadFromJsonAsync<T>(Json.Options, ct))!;
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        throw await GateProofApiException.FromResponseAsync(response, ct);
    }
}
