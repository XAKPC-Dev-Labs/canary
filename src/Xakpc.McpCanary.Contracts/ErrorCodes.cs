namespace Xakpc.McpCanary.Contracts;

/// <summary>
/// Stable error codes. Never rename or reuse a value; agents depend on them.
/// </summary>
public static class ErrorCodes
{
    public const string BadRequest = "BAD_REQUEST";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string Conflict = "CONFLICT";
    public const string RateLimited = "RATE_LIMITED";
    public const string InternalError = "INTERNAL_ERROR";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
    public const string InvalidConfiguration = "INVALID_CONFIGURATION";
    /// <summary>HTTP 402. No active trial or paid plan. Details: <c>reason</c> (<see cref="EntitlementReasons"/>); plus <c>offers</c>.</summary>
    public const string PaymentRequired = "PAYMENT_REQUIRED";
    /// <summary>
    /// A plan limit. HTTP 402 with <c>offers</c> when a higher plan allows the request, 403 when none does.
    /// Details: <c>limit</c>, <c>current_plan</c>, <c>required_plan</c>, <c>allowed</c>, <c>requested</c>.
    /// </summary>
    public const string PlanLimitReached = "PLAN_LIMIT_REACHED";

    // Target (customer MCP endpoint) failures.
    public const string TargetUnavailable = "TARGET_UNAVAILABLE";
    public const string TargetTimeout = "TARGET_TIMEOUT";
    public const string TargetAuthFailed = "TARGET_AUTH_FAILED";
    public const string TargetAddressForbidden = "TARGET_ADDRESS_FORBIDDEN";
    public const string McpProtocolError = "MCP_PROTOCOL_ERROR";
    public const string McpToolNotFound = "MCP_TOOL_NOT_FOUND";
    public const string ToolReturnedError = "TOOL_RETURNED_ERROR";
    public const string SideEffectsNotAllowed = "SIDE_EFFECTS_NOT_ALLOWED";
    public const string AssertionFailed = "ASSERTION_FAILED";
    public const string AssertionTargetNotJson = "ASSERTION_TARGET_NOT_JSON";
    /// <summary>Evidence of a contract-change incident (<c>alert_on_change</c>). Details: <c>changes</c>.</summary>
    public const string ToolSchemaChanged = "TOOL_SCHEMA_CHANGED";
}
