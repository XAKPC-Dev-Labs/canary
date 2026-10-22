using System.Text.Json;
using System.Text.Json.Nodes;

namespace Xakpc.McpCanary.Contracts;

/// <summary>Request for <c>POST /v1/inspect</c>.</summary>
/// <param name="Url">The MCP endpoint URL (http or https, public address).</param>
/// <param name="Headers">Optional headers sent to the target, e.g. <c>Authorization</c>. Never stored.</param>
public sealed record InspectRequest(string Url, Dictionary<string, string>? Headers = null);

/// <summary>
/// Result of a read-only inspection. HTTP 200 whenever the probe ran;
/// a target problem is <see cref="Status"/> = <c>failed</c> with <see cref="Failure"/>.
/// </summary>
public sealed record InspectResponse(
    string Endpoint,
    string Status,
    string? Transport,
    string? Protocol,
    ServerInfo? Server,
    IReadOnlyList<InspectedTool> Tools,
    long DurationMs,
    CheckFailure? Failure);

public sealed record ServerInfo(string? Name, string? Version);

/// <param name="SideEffects">One of <see cref="SideEffectClasses"/>, derived from <see cref="Hints"/> only.</param>
/// <param name="Hints">Annotations as the server declared them. An absent hint was not declared.</param>
public sealed record InspectedTool(
    string Name,
    string? Description,
    string SideEffects,
    ToolHints Hints,
    JsonElement InputSchema,
    JsonElement? OutputSchema);

public sealed record ToolHints(bool? ReadOnlyHint, bool? DestructiveHint, bool? IdempotentHint, bool? OpenWorldHint);

/// <summary>Why a probe or check failed. <see cref="Code"/> is a stable <see cref="ErrorCodes"/> value.</summary>
public sealed record CheckFailure(string Code, string Message, JsonObject? Details = null);

public static class CheckStatuses
{
    public const string Passed = "passed";
    public const string Failed = "failed";
}

public static class Transports
{
    public const string StreamableHttp = "streamable_http";
    public const string LegacySse = "legacy_sse";
}

public static class SideEffectClasses
{
    public const string ReadOnly = "read_only";
    public const string PossiblyMutating = "possibly_mutating";
    public const string ExplicitlyMutating = "explicitly_mutating";
    public const string Unknown = "unknown";
}
