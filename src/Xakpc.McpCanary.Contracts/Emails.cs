namespace Xakpc.McpCanary.Contracts;

/// <summary>
/// An alert email address of the workspace. It gets <see cref="EmailEvents.Sent"/> only after the owner of the address
/// clicks the confirm link (double opt-in). Every alert has a one-click unsubscribe link; unsubscribe removes the address.
/// </summary>
/// <param name="Status"><c>pending</c> (confirm link sent, no alerts yet), <c>active</c>, or <c>suspended</c>.</param>
/// <param name="SuspendedReason"><see cref="EntitlementReasons"/>; <c>PLAN_LIMIT_REACHED</c> = over the plan's email count (the oldest stay active).
/// A suspended address still gets <c>entitlement.*</c> alerts.</param>
public sealed record EmailResponse(
    Guid Id,
    string Address,
    string Status,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    string? SuspendedReason);

/// <param name="ConfirmationSent">True when this call sent a confirm link. A pending address gets a new link at most every 10 minutes.</param>
public sealed record PutEmailResponse(EmailResponse Email, bool Created, bool ConfirmationSent);

public sealed record EmailListResponse(IReadOnlyList<EmailResponse> Emails);

public static class EmailStatuses
{
    public const string Pending = "pending";
    public const string Active = "active";
    public const string Suspended = "suspended";
}

public static class EmailEvents
{
    /// <summary>The events that are sent as email. Never <c>check.*</c>.</summary>
    public static readonly IReadOnlyList<string> Sent =
        [EventTypes.IncidentOpened, EventTypes.IncidentResolved, EventTypes.EntitlementExpiring, EventTypes.EntitlementExpired];
}
