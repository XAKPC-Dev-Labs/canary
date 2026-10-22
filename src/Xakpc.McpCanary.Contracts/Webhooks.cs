namespace Xakpc.McpCanary.Contracts;

/// <summary>Body of <c>PUT /v1/webhooks/{name}</c>: the full desired state.</summary>
/// <param name="Url">Receiver URL, absolute http or https, public address.</param>
/// <param name="Events">
/// Event types to deliver (<see cref="EventTypes"/>), or <c>["*"]</c> for all.
/// Null = <see cref="EventTypes.Default"/>: incident.opened, incident.resolved, contract.changed, entitlement.expiring, entitlement.expired.
/// </param>
public sealed record WebhookSpec(string Url, IReadOnlyList<string>? Events = null);

/// <param name="Status"><c>active</c> or <c>suspended</c>. A suspended webhook gets only <c>entitlement.*</c> events.</param>
/// <param name="SuspendedReason"><see cref="EntitlementReasons"/>; <c>PLAN_LIMIT_REACHED</c> = over the plan's webhook count (the oldest stay active).</param>
public sealed record WebhookResponse(
    Guid Id,
    string Name,
    string Url,
    IReadOnlyList<string> Events,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string Status,
    string? SuspendedReason);

/// <param name="Secret">
/// Signing secret (<c>whsec_...</c>). Returned only once, by the call that created the webhook.
/// </param>
public sealed record PutWebhookResponse(WebhookResponse Webhook, bool Created, bool Changed, string? Secret);

public sealed record WebhookListResponse(IReadOnlyList<WebhookResponse> Webhooks);

/// <summary>One delivery of one event to one webhook.</summary>
/// <param name="Status"><c>pending</c>, <c>succeeded</c>, or <c>failed</c> (no retries left).</param>
/// <param name="NextAttemptAt">When the next attempt runs; set only while pending.</param>
/// <param name="LastError">Network error, timeout, or address-guard rejection of the last attempt.</param>
public sealed record DeliveryResponse(
    Guid Id,
    Guid EventId,
    string Event,
    string Status,
    int Attempts,
    DateTime CreatedAt,
    DateTime? LastAttemptAt,
    DateTime? NextAttemptAt,
    int? LastHttpStatus,
    string? LastError);

/// <summary>Newest first.</summary>
public sealed record DeliveryListResponse(IReadOnlyList<DeliveryResponse> Deliveries);

public static class DeliveryStatuses
{
    public const string Pending = "pending";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
}

/// <summary>
/// Webhook body. Signed with <c>X-McpCanary-Signature: t=unix,v1=hex(HMAC-SHA256(secret, t + "." + body))</c>.
/// Retries send the same <see cref="Id"/>; receivers deduplicate by it.
/// </summary>
/// <param name="Type">One of <see cref="EventTypes"/>.</param>
public sealed record WebhookEvent(Guid Id, string Type, DateTime CreatedAt, Guid WorkspaceId, EventData Data);

/// <summary>Only the parts that apply to the event type are set.</summary>
public sealed record EventData(
    EventMonitor? Monitor = null, CheckResult? Check = null, IncidentResponse? Incident = null, EventEntitlement? Entitlement = null);

/// <summary>The workspace entitlement after the change, for <c>entitlement.*</c> events.</summary>
/// <param name="Type"><see cref="EntitlementTypes"/>.</param>
/// <param name="Reason">Set on <c>entitlement.expired</c>: <see cref="EntitlementReasons"/>.</param>
public sealed record EventEntitlement(string Type, string? Plan, DateTime? ExpiresAt, string? Reason = null);

public sealed record EventMonitor(Guid Id, string Name, string Url);

public static class EventTypes
{
    public const string MonitorCreated = "monitor.created";
    public const string MonitorUpdated = "monitor.updated";
    public const string MonitorDeleted = "monitor.deleted";
    public const string CheckPassed = "check.passed";
    public const string CheckFailed = "check.failed";
    public const string ContractChanged = "contract.changed";
    public const string IncidentOpened = "incident.opened";
    public const string IncidentResolved = "incident.resolved";
    /// <summary>A trial started, or a paid plan started or changed.</summary>
    public const string EntitlementActivated = "entitlement.activated";
    /// <summary>A trial or a cancelled paid plan ends soon (48 h before a trial ends, 72 h before a paid plan ends).</summary>
    public const string EntitlementExpiring = "entitlement.expiring";
    /// <summary>Access ended: checks and alerts stop until a payment.</summary>
    public const string EntitlementExpired = "entitlement.expired";
    /// <summary>Subscribes to every event type.</summary>
    public const string All = "*";

    public static readonly IReadOnlyList<string> Known =
    [
        MonitorCreated, MonitorUpdated, MonitorDeleted, CheckPassed, CheckFailed,
        ContractChanged, IncidentOpened, IncidentResolved,
        EntitlementActivated, EntitlementExpiring, EntitlementExpired,
    ];

    /// <summary>Used when a webhook does not list its events. <c>check.*</c> events are opt-in (one per check).</summary>
    public static readonly IReadOnlyList<string> Default =
        [ContractChanged, IncidentOpened, IncidentResolved, EntitlementExpiring, EntitlementExpired];

    /// <summary><c>entitlement.*</c> events reach every subscribed webhook, also suspended ones: they tell how to restore access.</summary>
    public static bool IsEntitlement(string type) => type.StartsWith("entitlement.", StringComparison.Ordinal);
}
