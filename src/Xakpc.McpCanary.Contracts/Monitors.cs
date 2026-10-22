using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xakpc.McpCanary.Contracts;

/// <summary>
/// Body of <c>PUT /v1/monitors/{name}</c>: the full desired state. Left-out fields go back to defaults,
/// except <see cref="Headers"/>: null keeps the stored headers, an object replaces them all.
/// </summary>
/// <param name="Url">MCP endpoint, absolute http or https URL.</param>
/// <param name="IntervalSeconds">Seconds between checks (30-86400). Default 300. The plan sets the minimum.</param>
/// <param name="TimeoutSeconds">Whole-check timeout, 1–60. Default 30.</param>
/// <param name="Tool">Tool to call on each check. Null = Level 1 (connect + list tools only).</param>
/// <param name="Arguments">JSON object of tool arguments. Requires <see cref="Tool"/>.</param>
/// <param name="Assertions">Checks on the tool result. Requires <see cref="Tool"/>.</param>
/// <param name="AllowSideEffects">Required to call a tool that is not declared read-only.</param>
/// <param name="Headers">Secret headers sent to the target, e.g. <c>Authorization</c>. Stored encrypted, never returned.</param>
/// <param name="AlertOnChange">Open an incident when the tool contract changes. Default false: only the event fires.</param>
public sealed record MonitorSpec(
    string Url,
    int? IntervalSeconds = null,
    int? TimeoutSeconds = null,
    string? Tool = null,
    JsonElement? Arguments = null,
    IReadOnlyList<Assertion>? Assertions = null,
    bool AllowSideEffects = false,
    Dictionary<string, string>? Headers = null,
    bool AlertOnChange = false);

/// <summary>
/// One check on the tool result. Use <see cref="Path"/> with exactly one operator, or <see cref="Duration"/> alone.
/// <c>$</c> is <c>structuredContent</c>, else the first text block parsed as JSON.
/// </summary>
public sealed record Assertion(
    string? Path = null,
    [property: JsonPropertyName("equals")] JsonElement? EqualTo = null,
    JsonElement? NotEquals = null,
    bool? Exists = null,
    string? Contains = null,
    string? Matches = null,
    DurationAssertion? Duration = null);

public sealed record DurationAssertion(int LessThanMs);

/// <param name="Headers">Names of configured secret headers. Values are never returned.</param>
/// <param name="EffectiveIntervalSeconds">The interval the scheduler uses: max(interval_seconds, plan minimum).</param>
/// <param name="Status"><c>active</c> or <c>suspended</c> (<see cref="ResourceStatuses"/>). A suspended monitor is not checked; PUT and test return 402.</param>
/// <param name="SuspendedReason">Why it is suspended: <see cref="EntitlementReasons"/>. <c>PLAN_LIMIT_REACHED</c> = over the plan's monitor count (the oldest monitors stay active).</param>
/// <param name="OpenIncidentId">The open incident of this monitor, or null when there is none.</param>
public sealed record MonitorResponse(
    Guid Id,
    string Name,
    string Url,
    int IntervalSeconds,
    int EffectiveIntervalSeconds,
    int TimeoutSeconds,
    string? Tool,
    JsonElement? Arguments,
    IReadOnlyList<Assertion>? Assertions,
    bool AllowSideEffects,
    bool AlertOnChange,
    IReadOnlyList<string> Headers,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime NextRunAt,
    string Status,
    string? SuspendedReason,
    Guid? OpenIncidentId,
    CheckResult? LastCheck);

/// <param name="Created">True when this call created the monitor (HTTP 201).</param>
/// <param name="Changed">False when the stored state already matched; safe to retry.</param>
/// <param name="FirstCheck">The check run right after the write (stored, trigger "write").</param>
public sealed record PutMonitorResponse(MonitorResponse Monitor, bool Created, bool Changed, CheckResult FirstCheck);

public sealed record MonitorListResponse(IReadOnlyList<MonitorResponse> Monitors);

/// <summary>
/// Result of <c>POST /v1/monitors/{name}/preview</c>: what a <c>PUT</c> with the same body would store and run.
/// Nothing is saved, no trial starts, and no tool is called.
/// </summary>
/// <param name="Exists">A monitor with this name exists in the token's workspace.</param>
/// <param name="Changed">The PUT would change the stored monitor (always true when it does not exist).</param>
/// <param name="Changes">Per-field differences from the stored monitor. Header values are never shown.</param>
/// <param name="Tool">The tool as <c>tools/list</c> reports it; null for a monitor without a tool.</param>
/// <param name="Probe">The read-only probe: connect and list tools.</param>
/// <param name="Warnings">What would make the PUT fail or its first check fail. Empty = the PUT would pass.</param>
/// <param name="WouldStartTrial">The PUT would start the trial (and, without a token, create the workspace).</param>
public sealed record MonitorPreviewResponse(
    MonitorPreview Monitor,
    bool Exists,
    bool Changed,
    IReadOnlyList<FieldChange> Changes,
    PreviewTool? Tool,
    PreviewProbe Probe,
    IReadOnlyList<CheckFailure> Warnings,
    bool WouldStartTrial);

/// <summary>The monitor as a PUT would store it, with defaults filled in.</summary>
/// <param name="EffectiveIntervalSeconds">max(interval_seconds, plan minimum) of the plan the PUT would run under.</param>
/// <param name="Headers">Names of the secret headers the monitor would send.</param>
public sealed record MonitorPreview(
    string Name,
    string Url,
    int IntervalSeconds,
    int EffectiveIntervalSeconds,
    int TimeoutSeconds,
    string? Tool,
    JsonElement? Arguments,
    IReadOnlyList<Assertion>? Assertions,
    bool AllowSideEffects,
    bool AlertOnChange,
    IReadOnlyList<string> Headers);

/// <summary>One field the PUT would change. Values are JSON as in <see cref="MonitorPreview"/>.</summary>
public sealed record FieldChange(string Field, JsonElement? From, JsonElement? To);

/// <param name="Found">The tool is in the target's <c>tools/list</c>. False also when the probe could not list tools.</param>
/// <param name="SideEffects">One of <see cref="SideEffectClasses"/>; null when not found.</param>
public sealed record PreviewTool(string Name, bool Found, string? SideEffects);

public sealed record PreviewProbe(string Status, string? Transport, string? Protocol, long DurationMs, CheckFailure? Failure);

/// <summary>One stored check.</summary>
/// <param name="Trigger"><c>write</c> (PUT), <c>schedule</c>, or <c>manual</c> (POST .../test).</param>
/// <param name="Status"><c>passed</c> or <c>failed</c>.</param>
/// <param name="ContractChanged">The tool contract differs from the previous check. Does not fail the check.</param>
/// <param name="ContractChanges">What changed, when <see cref="ContractChanged"/> is true.</param>
public sealed record CheckResult(
    Guid Id,
    string Trigger,
    DateTime StartedAt,
    string Status,
    long DurationMs,
    string? Transport,
    string? Protocol,
    bool ContractChanged,
    CheckFailure? Failure,
    IReadOnlyList<ContractChange>? ContractChanges = null);

/// <summary>Newest first.</summary>
public sealed record CheckListResponse(IReadOnlyList<CheckResult> Checks);

/// <summary>One difference between the previous and the current tool contract.</summary>
/// <param name="Type">One of <see cref="ContractChangeTypes"/>.</param>
/// <param name="Argument">Argument name, for the <c>argument_*</c> types.</param>
/// <param name="From">Old value: the JSON Schema <c>type</c> of the argument, or its old <c>required</c> flag.</param>
/// <param name="To">New value: the JSON Schema <c>type</c> of the argument, or its new <c>required</c> flag.</param>
public sealed record ContractChange(string Type, string Tool, string? Argument = null, JsonElement? From = null, JsonElement? To = null);

public static class ContractChangeTypes
{
    public const string ToolAdded = "tool_added";
    public const string ToolRemoved = "tool_removed";
    /// <summary><c>to</c> = whether the new argument is required.</summary>
    public const string ArgumentAdded = "argument_added";
    public const string ArgumentRemoved = "argument_removed";
    public const string ArgumentTypeChanged = "argument_type_changed";
    public const string ArgumentRequiredChanged = "argument_required_changed";
    /// <summary>The argument schema changed in another way (enum, format, nested properties).</summary>
    public const string ArgumentSchemaChanged = "argument_schema_changed";
    /// <summary>The input schema changed outside its arguments.</summary>
    public const string InputSchemaChanged = "input_schema_changed";
    public const string OutputSchemaChanged = "output_schema_changed";
}

/// <summary>Values of <c>status</c> of a monitor or webhook.</summary>
public static class ResourceStatuses
{
    public const string Active = "active";
    /// <summary>Not checked (monitor) or not delivered to (webhook). See <c>suspended_reason</c>.</summary>
    public const string Suspended = "suspended";
}
