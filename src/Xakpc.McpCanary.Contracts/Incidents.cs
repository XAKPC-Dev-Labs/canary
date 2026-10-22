namespace Xakpc.McpCanary.Contracts;

/// <summary>
/// An open or resolved failure state of a monitor. A monitor has at most one open incident.
/// </summary>
/// <param name="Status"><c>open</c> or <c>resolved</c>.</param>
/// <param name="Cause"><c>check_failed</c> (2 scheduled failures in a row) or <c>contract_changed</c> (<c>alert_on_change</c>).</param>
/// <param name="Failure">Evidence: the failure of the check that opened the incident, or <c>TOOL_SCHEMA_CHANGED</c> with <c>details.changes</c>.</param>
public sealed record IncidentResponse(
    Guid Id,
    Guid MonitorId,
    string MonitorName,
    string Status,
    string Cause,
    CheckFailure Failure,
    DateTime OpenedAt,
    DateTime? ResolvedAt,
    Guid OpenedByCheckId,
    Guid? ResolvedByCheckId);

/// <summary>Newest first.</summary>
public sealed record IncidentListResponse(IReadOnlyList<IncidentResponse> Incidents);

public static class IncidentStatuses
{
    public const string Open = "open";
    public const string Resolved = "resolved";
}

public static class IncidentCauses
{
    public const string CheckFailed = "check_failed";
    public const string ContractChanged = "contract_changed";
}
