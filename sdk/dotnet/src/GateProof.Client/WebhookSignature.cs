using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GateProof.Client;

/// <summary>
/// Verifies webhook deliveries. Header: <c>GateProof-Signature: t={unix seconds},v1={hex}</c>, where
/// v1 is HMAC-SHA256 of <c>"{t}.{raw body}"</c> keyed with your webhook secret.
/// </summary>
public static class WebhookSignature
{
    public const string HeaderName = "GateProof-Signature";

    /// <summary>Signatures older than this are rejected as possible replays.</summary>
    public static readonly TimeSpan DefaultTolerance = TimeSpan.FromMinutes(5);

    /// <summary>
    /// True when the header is a valid, fresh signature of <paramref name="rawBody"/>. Pass the body
    /// exactly as received; re-serialized JSON will not match.
    /// </summary>
    public static bool IsValid(string secret, string rawBody, string? header, DateTimeOffset now, TimeSpan? tolerance = null)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(header)) return false;

        long? timestamp = null;
        string? v1 = null;
        foreach (var part in header.Split(','))
        {
            var kv = part.Trim().Split(['='], 2);
            if (kv.Length != 2) continue;
            if (kv[0] == "t" && long.TryParse(kv[1], NumberStyles.None, CultureInfo.InvariantCulture, out var t)) timestamp = t;
            else if (kv[0] == "v1") v1 = kv[1];
        }

        if (timestamp is null || v1 is null) return false;
        if (Math.Abs(now.ToUnixTimeSeconds() - timestamp.Value) > (tolerance ?? DefaultTolerance).TotalSeconds) return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = ToHex(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp.Value}.{rawBody}")));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(v1));
    }

    /// <summary>Verifies and parses a delivery. Throws <see cref="InvalidOperationException"/> if the signature is not valid.</summary>
    public static WebhookEvent Parse(string secret, string rawBody, string? header, DateTimeOffset now, TimeSpan? tolerance = null)
    {
        if (!IsValid(secret, rawBody, header, now, tolerance))
            throw new InvalidOperationException("Webhook signature is missing, invalid or too old.");
        return JsonSerializer.Deserialize<WebhookEvent>(rawBody, Json.Options)!;
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}
