namespace Xakpc.McpCanary.Contracts;

public static class PlanNames
{
    /// <summary>No payment, length <c>Trial:Hours</c>. Starts on the first monitor write; one per workspace and per target host.</summary>
    public const string Trial = "trial";
    public const string Solo = "solo";
    public const string Pro = "pro";

    /// <summary>Lowest first.</summary>
    public static readonly IReadOnlyList<string> Ordered = [Trial, Solo, Pro];

    /// <summary>Plans that can be bought, lowest first.</summary>
    public static readonly IReadOnlyList<string> Paid = [Solo, Pro];
}

/// <summary>Values of <see cref="EntitlementResponse.Type"/>.</summary>
public static class EntitlementTypes
{
    /// <summary>No active trial or payment.</summary>
    public const string None = "none";
    public const string Trial = "trial";
    public const string Paid = "paid";
}

/// <summary>
/// Why access is missing or limited: <c>details.reason</c> of <c>PAYMENT_REQUIRED</c>, and
/// <c>suspended_reason</c> of a monitor or webhook.
/// </summary>
public static class EntitlementReasons
{
    /// <summary>The trial ended and nothing was paid.</summary>
    public const string TrialExpired = "TRIAL_EXPIRED";
    /// <summary>A paid plan ended.</summary>
    public const string EntitlementExpired = "ENTITLEMENT_EXPIRED";
    /// <summary>No trial is possible: the target host already had a trial in another workspace.</summary>
    public const string TrialNotAvailable = "TRIAL_NOT_AVAILABLE";
    /// <summary>Over the plan's monitor or webhook count; the oldest ones stay active.</summary>
    public const string PlanLimitReached = "PLAN_LIMIT_REACHED";
}

/// <summary>Values of <c>details.limit</c> in a <c>PLAN_LIMIT_REACHED</c> error.</summary>
public static class PlanLimitNames
{
    public const string Monitors = "monitors";
    public const string MinInterval = "min_interval";
    public const string OnDemandChecks = "on_demand_checks";
    public const string Webhooks = "webhooks";
    public const string Emails = "emails";
}

/// <param name="HistoryDays">How long incidents, failed checks, and checks with a contract change are kept.</param>
/// <param name="PassedCheckDays">How long passed checks are kept.</param>
/// <param name="OnDemandChecksPerDay">Checks with trigger <c>manual</c> or <c>write</c> in a rolling 24 h.</param>
/// <param name="Emails">Alert email addresses.</param>
public sealed record PlanLimits(
    int Monitors,
    int MinIntervalSeconds,
    int HistoryDays,
    int PassedCheckDays,
    int OnDemandChecksPerDay,
    int Webhooks,
    int Emails);

/// <summary>One way to buy a plan, in a <c>PAYMENT_REQUIRED</c> or <c>PLAN_LIMIT_REACHED</c> error.</summary>
/// <param name="Amount">Price per <see cref="Interval"/> in the smallest currency unit (cents).</param>
/// <param name="CheckoutUrl">Signed link for a human, valid 7 days. Agents give it to a human; they never open it to pay.</param>
public sealed record Offer(string Plan, int Amount, string Currency, string Interval, string CheckoutUrl);

/// <summary>Response of <c>GET /v1/pricing</c>. No token needed.</summary>
public sealed record PricingResponse(string Currency, PricingTrial Trial, IReadOnlyList<PricingPlan> Plans);

/// <param name="Hours">Trial length. It starts on the first monitor write.</param>
public sealed record PricingTrial(int Hours, PlanLimits Limits);

/// <param name="Price">Per <see cref="Interval"/>, in the smallest currency unit (cents).</param>
public sealed record PricingPlan(string Id, int Price, string Interval, PlanLimits Limits);

/// <param name="OnDemandChecks">Checks with trigger <c>manual</c> or <c>write</c> in the last 24 h.</param>
public sealed record BillingUsage(int Monitors, int Webhooks, int OnDemandChecks, int Emails);

/// <summary>Response of <c>GET /v1/entitlement</c>.</summary>
/// <param name="Type"><see cref="EntitlementTypes"/>.</param>
/// <param name="Plan">The active plan; null when <see cref="Type"/> is <c>none</c>.</param>
/// <param name="ExpiresAt">End of the trial or of the paid period.</param>
/// <param name="Renews">True when a paid period renews by itself (Polar subscription not cancelled).</param>
/// <param name="Reason">Set when <see cref="Type"/> is <c>none</c> after a trial or payment: <see cref="EntitlementReasons"/>.</param>
/// <param name="TrialAvailable">True until the workspace starts its trial. The first monitor write starts it.</param>
/// <param name="Limits">Of the active plan; of the trial while it is available; else null.</param>
/// <param name="BillingAccount">True after the first Polar payment; then <c>POST /v1/billing/portal</c> works.</param>
public sealed record EntitlementResponse(
    string Type,
    string? Plan,
    DateTime? ExpiresAt,
    bool Renews,
    string? Reason,
    bool TrialAvailable,
    PlanLimits? Limits,
    BillingUsage Usage,
    bool BillingAccount);

/// <summary>Body of <c>POST /v1/billing/checkout</c>.</summary>
/// <param name="Plan"><c>solo</c> or <c>pro</c>.</param>
public sealed record CheckoutRequest(string Plan);

/// <param name="CheckoutUrl">Signed link for a human, valid 7 days.</param>
public sealed record CheckoutResponse(string CheckoutUrl);

/// <param name="PortalUrl">Short-lived Polar customer portal link for a human: card, invoices, plan change.</param>
public sealed record PortalResponse(string PortalUrl);

/// <summary>Response of <c>POST /v1/subscription/cancel</c>.</summary>
/// <param name="Status">Always <c>cancelled</c>: the subscription does not renew.</param>
/// <param name="AccessUntil">The paid plan stays active until then.</param>
public sealed record CancelSubscriptionResponse(string Status, DateTime? AccessUntil);
