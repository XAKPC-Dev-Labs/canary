using Xakpc.McpCanary.Contracts;

namespace Xakpc.McpCanary.Cli;

/// <summary>Stable exit codes. Never change a published value; agents and CI branch on them.</summary>
public static class ExitCodes
{
    public const int Success = 0;
    /// <summary>The command did its work, but something needs attention (e.g. a monitor saved with a failing first check).</summary>
    public const int Warning = 2;
    /// <summary>The target answered, but the check failed (assertion, tool error, auth, protocol).</summary>
    public const int TargetUnhealthy = 3;
    public const int AuthFailed = 10;
    public const int PermissionDenied = 11;
    public const int InvalidConfiguration = 20;
    public const int NotFound = 21;
    public const int ServiceUnavailable = 30;
    /// <summary>The target could not be reached or timed out.</summary>
    public const int TargetUnavailable = 31;
    /// <summary><c>PAYMENT_REQUIRED</c> or <c>PLAN_LIMIT_REACHED</c> with offers (HTTP 402).</summary>
    public const int PaymentRequired = 40;

    public static int FromHttpStatus(int status) => status switch
    {
        401 => AuthFailed,
        402 => PaymentRequired,
        403 => PermissionDenied,
        404 => NotFound,
        429 or >= 500 => ServiceUnavailable,
        _ => InvalidConfiguration,
    };

    /// <summary>Exit code of a check or inspect result.</summary>
    public static int FromCheck(string status, CheckFailure? failure) =>
        status == CheckStatuses.Passed ? Success
        : failure?.Code is ErrorCodes.TargetUnavailable or ErrorCodes.TargetTimeout ? TargetUnavailable
        : TargetUnhealthy;
}
