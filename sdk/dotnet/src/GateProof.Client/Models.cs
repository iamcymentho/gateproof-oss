using System.Text.Json;
using System.Text.Json.Serialization;

namespace GateProof.Client;

/// <summary>
/// Known values for string fields. Fields stay plain strings so a value added by a newer API
/// version doesn't break your deserialization; compare against these constants.
/// </summary>
public static class VerificationStatuses
{
    public const string Created = "created";
    public const string LinkSent = "link_sent";
    public const string InProgress = "in_progress";
    public const string Submitted = "submitted";
    public const string Completed = "completed";
    public const string Expired = "expired";
    public const string Cancelled = "cancelled";
}

public static class Verdicts
{
    public const string Verified = "verified";
    public const string NeedsReview = "needs_review";
    public const string NotVerified = "not_verified";
}

public static class WebhookEventTypes
{
    public const string VerificationCompleted = "verification.completed";
    public const string VerificationExpired = "verification.expired";
}

/// <param name="CustomerPhone">Nigerian mobile number, e.g. 08012345678 or +2348012345678.</param>
/// <param name="ClaimedAddress">The address the customer gave you, up to 500 characters.</param>
/// <param name="ExternalRef">Your own reference, e.g. a loan application id, up to 100 characters.</param>
public sealed record CreateVerificationRequest(string CustomerPhone, string ClaimedAddress, string? ExternalRef = null);

/// <summary>A verification as the API returns it, in responses and inside webhooks.</summary>
public sealed record Verification
{
    public Guid Id { get; init; }
    public string Environment { get; init; } = "";
    public string Status { get; init; } = "";
    public string? ExternalRef { get; init; }
    public string ClaimedAddress { get; init; } = "";
    public string CustomerPhoneLast4 { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? ConsentedAt { get; init; }
    public int CheckInCount { get; init; }
    public DateTimeOffset? NextCheckInOpensAt { get; init; }
    public bool HasGatePhoto { get; init; }
    public bool HasUtilityBill { get; init; }
    public string? NipostStatus { get; init; }
    public string? NipostPostcode { get; init; }
    public string? Verdict { get; init; }
    public string? Confidence { get; init; }
    public IReadOnlyList<string> ReasonCodes { get; init; } = [];
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>The customer's link. Only present in the response that created the verification.</summary>
    public string? LinkUrl { get; init; }

    public bool IsFinished => Status is VerificationStatuses.Completed or VerificationStatuses.Expired or VerificationStatuses.Cancelled;
}

/// <summary>A webhook delivery body.</summary>
public sealed record WebhookEvent
{
    public Guid Id { get; init; }
    public string Type { get; init; } = "";
    public string Environment { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public Verification Data { get; init; } = new();
}

public sealed record NipostKeyStatus(string Environment, bool Configured);

/// <summary>
/// The evidence pack (schema <c>gateproof.evidence-pack/v1</c>). For the full field list see
/// schemas/evidence-pack-v1.schema.json. <see cref="Raw"/> keeps the exact document for archiving.
/// </summary>
public sealed record EvidencePack
{
    public string Schema { get; init; } = "";
    public DateTimeOffset GeneratedAt { get; init; }
    public string Environment { get; init; } = "";
    public PackResult? Result { get; init; }
    public IReadOnlyList<PackFile> Evidence { get; init; } = [];

    /// <summary>The pack exactly as received. Store this, not a re-serialized copy, for audit purposes.</summary>
    [JsonIgnore]
    public JsonElement Raw { get; internal set; }
}

public sealed record PackResult(string Verdict, string Confidence, IReadOnlyList<PackReason> Reasons);

public sealed record PackReason(string Code, string Kind, bool Severe, string Description);

public sealed record PackFile
{
    public Guid Id { get; init; }
    public string Kind { get; init; } = "";
    public string ContentType { get; init; } = "";
    public long SizeBytes { get; init; }
    public string Sha256 { get; init; } = "";
    public DateTimeOffset UploadedAt { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }
    public string? DownloadUrl { get; init; }
}

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
