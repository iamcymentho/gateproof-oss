using System.Net;
using System.Text.Json;

namespace GateProof.Client;

/// <summary>An error response from the API, with the problem details it carried.</summary>
public sealed class GateProofApiException(HttpStatusCode status, string? title, IReadOnlyDictionary<string, string[]>? errors)
    : Exception(BuildMessage(status, title, errors))
{
    public HttpStatusCode Status { get; } = status;
    public string? Title { get; } = title;

    /// <summary>Field validation errors (400), keyed by field name, e.g. "customer_phone".</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();

    /// <summary>True for responses worth retrying later with the same idempotency key.</summary>
    public bool IsTransient => Status is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || (int)Status >= 500;

    internal static async Task<GateProofApiException> FromResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string? title = null;
        Dictionary<string, string[]>? errors = null;
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("title", out var t)) title = t.GetString();
                if (doc.RootElement.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Object)
                    errors = e.Deserialize<Dictionary<string, string[]>>();
            }
        }
        catch (JsonException)
        {
            // Not a problem-details body (e.g. a proxy error page); the status code still tells the story.
        }

        return new GateProofApiException(response.StatusCode, title, errors);
    }

    private static string BuildMessage(HttpStatusCode status, string? title, IReadOnlyDictionary<string, string[]>? errors)
    {
        var message = $"GateProof API returned {(int)status} {status}" + (title is null ? "." : $": {title}");
        return errors is { Count: > 0 }
            ? message + " " + string.Join("; ", errors.Select(e => $"{e.Key}: {string.Join(" ", e.Value)}"))
            : message;
    }
}
