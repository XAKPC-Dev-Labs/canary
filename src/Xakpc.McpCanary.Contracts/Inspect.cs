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
/// <param name="Instructions">The server's <c>instructions</c> text for agents, as it sent it.</param>
/// <param name="AgentReadiness">What is wrong with what the server tells agents, and how to fix it. Null when the probe got no answer.</param>
public sealed record InspectResponse(
    string Endpoint,
    string Status,
    string? Transport,
    string? Protocol,
    ServerInfo? Server,
    IReadOnlyList<InspectedTool> Tools,
    long DurationMs,
    CheckFailure? Failure,
    string? Instructions = null,
    AgentReadinessReport? AgentReadiness = null);

public sealed record ServerInfo(string? Name, string? Version);

/// <param name="SideEffects">One of <see cref="SideEffectClasses"/>, derived from <see cref="Hints"/> only.</param>
/// <param name="Hints">Annotations as the server declared them. An absent hint was not declared.</param>
public sealed record InspectedTool(
    string Name,
    string? Description,
    string SideEffects,
    ToolHints Hints,
    JsonElement InputSchema,
    JsonElement? OutputSchema,
    string? Title = null);

public sealed record ToolHints(bool? ReadOnlyHint, bool? DestructiveHint, bool? IdempotentHint, bool? OpenWorldHint);

/// <summary>
/// The agent-readiness report of an inspect: findings on the instructions a server gives agents (tool names,
/// descriptions, arguments, hints) and on its protocol compliance. Computed per request; never stored.
/// </summary>
/// <param name="Grade">A to F from <see cref="Score"/>; null when the tool list was not seen (e.g. sign-in required).</param>
/// <param name="LatestProtocol">The newest MCP protocol revision MCP Canary knows.</param>
public sealed record AgentReadinessReport(
    string? Grade,
    int? Score,
    int Errors,
    int Warnings,
    int Infos,
    string LatestProtocol,
    IReadOnlyList<ReadinessFinding> Findings);

/// <param name="Rule">A stable <see cref="ReadinessRules"/> value.</param>
/// <param name="Category"><c>instructions</c> or <c>protocol</c>.</param>
/// <param name="Severity"><c>error</c>, <c>warn</c>, or <c>info</c>.</param>
/// <param name="Why">What an agent or client does wrong because of it.</param>
/// <param name="Fix">One instruction for the server owner.</param>
/// <param name="Tool">The tool it is about; null for the whole server.</param>
/// <param name="Items">The arguments, tools, or problems it names.</param>
/// <param name="Example">A corrected JSON fragment.</param>
public sealed record ReadinessFinding(
    string Rule,
    string Category,
    string Severity,
    string Message,
    string Why,
    string Fix,
    string? Tool = null,
    IReadOnlyList<string>? Items = null,
    string? Example = null);

public static class ReadinessSeverities
{
    public const string Error = "error";
    public const string Warn = "warn";
    public const string Info = "info";
}

public static class ReadinessCategories
{
    public const string Instructions = "instructions";
    public const string Protocol = "protocol";
}

public static class ReadinessRules
{
    // instructions: server
    public const string NoTools = "no_tools";
    public const string DuplicateToolNames = "duplicate_tool_names";
    public const string ToolNamesDifferInCase = "tool_names_differ_in_case";
    public const string NoInstructions = "no_instructions";
    public const string LongInstructions = "long_instructions";
    public const string ToolListLarge = "tool_list_large";
    public const string ManyTools = "many_tools";
    public const string NoReadOnlyTool = "no_read_only_tool";
    public const string NoOutputSchema = "no_output_schema";
    // instructions: tool
    public const string MissingDescription = "missing_description";
    public const string ThinDescription = "thin_description";
    public const string LongDescription = "long_description";
    public const string MissingSafetyHint = "missing_safety_hint";
    public const string ConflictingHints = "conflicting_hints";
    public const string InvalidName = "invalid_name";
    public const string SchemaNotObject = "schema_not_object";
    public const string RequiredUnknown = "required_unknown";
    public const string ArgNoDescription = "arg_no_description";
    public const string ArgNoType = "arg_no_type";
    // protocol
    public const string OutdatedProtocol = "outdated_protocol";
    public const string UnknownProtocol = "unknown_protocol";
    public const string LegacySseOnly = "legacy_sse_only";
    public const string MissingServerInfo = "missing_server_info";
    public const string NoToolsCapability = "no_tools_capability";
    public const string PingFailed = "ping_failed";
    public const string UnknownMethodError = "unknown_method_error";
    public const string MissingCacheHints = "missing_cache_hints";
    public const string SignInDiscoveryBroken = "sign_in_discovery_broken";
    public const string DynamicRegistrationOnly = "dynamic_registration_only";
}

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
