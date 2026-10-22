namespace Xakpc.McpCanary.Cli;

/// <summary>
/// Everything the CLI takes from its process: environment, HTTP, output streams, and the credentials file.
/// Tests pass their own host (in-memory API, captured output, temporary credentials file).
/// </summary>
public sealed class CliHost
{
    public required Func<string, string?> Env { get; init; }
    public required HttpClient Http { get; init; }
    public required TextWriter Out { get; init; }
    public required TextWriter Err { get; init; }
    public required string CredentialsPath { get; init; }

    public static CliHost FromProcess() => new()
    {
        Env = Environment.GetEnvironmentVariable,
        // Per-request timeouts come from the command (CancellationToken), not from the client.
        Http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan },
        Out = Console.Out,
        Err = Console.Error,
        CredentialsPath = DefaultCredentialsPath(),
    };

    /// <summary>
    /// macOS <c>~/Library/Application Support/McpCanary</c>, Linux <c>$XDG_CONFIG_HOME</c> or <c>~/.config/mcpcanary</c>,
    /// Windows <c>%LOCALAPPDATA%\McpCanary</c>.
    /// </summary>
    private static string DefaultCredentialsPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var folder = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "McpCanary")
            : OperatingSystem.IsMacOS()
                ? Path.Combine(home, "Library", "Application Support", "McpCanary")
                : Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg ? xdg : Path.Combine(home, ".config"),
                    "mcpcanary");
        return Path.Combine(folder, "credentials.json");
    }
}
