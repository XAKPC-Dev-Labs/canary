using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Xakpc.McpCanary.Contracts;

/// <summary>Public status of a server (its last check) or of a day (its checks) on the status pages.</summary>
public static class PublicStatuses
{
    /// <summary>The handshake and tool list worked.</summary>
    public const string Up = "up";
    /// <summary>The server answered 401/403: it runs and asks for credentials. Counts as up.</summary>
    public const string AuthRequired = "auth_required";
    /// <summary>The server answered 429 to our check. Not counted in uptime.</summary>
    public const string RateLimited = "rate_limited";
    /// <summary>A day with up and down checks.</summary>
    public const string Partial = "partial";
    public const string Down = "down";
    /// <summary>No check yet.</summary>
    public const string Unknown = "unknown";
}

/// <param name="Servers">Every server with all its data, so one read can build every status page.</param>
public sealed record PublicServerListResponse(List<PublicServerResponse> Servers);

/// <summary>One UTC day. <see cref="Uptime"/> is the percent of counted checks that were up or auth_required.</summary>
public sealed record PublicDay(DateTime Date, string Status, int Checks, double? Uptime);

public sealed record PublicIncident(DateTime OpenedAt, DateTime? ResolvedAt, string FailureCode);

public sealed record PublicToolChange(DateTime DetectedAt, List<ContractChange> Changes);

/// <param name="IntervalSeconds">How often the server is checked.</param>
/// <param name="Uptime30d">Percent of counted checks (up or auth_required out of all but rate_limited) in 30 days; null without data.</param>
/// <param name="LatencyMs">Average answer time of the up and auth_required checks in the last 24 h (a 401 answer counts).</param>
/// <param name="History">Up to 90 days, oldest first.</param>
/// <param name="Tools">Tool names from the last tool list, sorted.</param>
/// <param name="ToolChanges">Newest first, at most 5 in 90 days.</param>
/// <param name="Incidents">Outages (not auth or rate-limit incidents), newest first, at most 5 in 90 days.</param>
/// <param name="SignIn">
/// When the last check got 401/403: the sign-in discovery (<c>failure.details.auth</c>): <c>verdict</c> healthy | broken | no_oauth,
/// the challenge, protected resource and authorization server metadata, PKCE, client registration, scopes, <c>problems</c>.
/// </param>
public sealed record PublicServerResponse(
    string Slug,
    string Url,
    string Status,
    DateTime? LastCheckedAt,
    DateTime FirstCheckedAt,
    int IntervalSeconds,
    [property: JsonPropertyName("uptime_30d")] double? Uptime30d,
    [property: JsonPropertyName("uptime_90d")] double? Uptime90d,
    long? LatencyMs,
    string? Transport,
    string? Protocol,
    List<string> Tools,
    List<PublicDay> History,
    List<PublicToolChange> ToolChanges,
    List<PublicIncident> Incidents,
    JsonObject? SignIn = null);
