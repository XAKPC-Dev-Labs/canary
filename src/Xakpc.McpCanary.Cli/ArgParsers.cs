using System.Text.Json;
using System.Text.RegularExpressions;
using Xakpc.McpCanary.Contracts;

namespace Xakpc.McpCanary.Cli;

/// <summary>Turns command-line text into API values. Every error names the option and shows the fix.</summary>
public static partial class ArgParsers
{
    [GeneratedRegex(@"^(\d+)(ms|s|m|h)?$")]
    private static partial Regex DurationPattern();

    /// <summary><c>90</c> or <c>90s</c>, <c>15m</c>, <c>1h</c>, in seconds.</summary>
    public static int Seconds(string value, string option)
    {
        var match = DurationPattern().Match(value.Trim());
        if (!match.Success || match.Groups[2].Value == "ms" || !int.TryParse(match.Groups[1].Value, out var number))
            throw CliError.Invalid($"{option} '{value}': use a number with s, m, or h, e.g. 30s, 15m, 1h.", option);
        return match.Groups[2].Value switch { "m" => number * 60, "h" => number * 3600, _ => number };
    }

    /// <summary>
    /// <c>'$.a.b == "x"'</c>, <c>!=</c>, <c>exists</c>, <c>contains "x"</c>, <c>matches "regex"</c>, or <c>duration &lt; 3000ms</c>.
    /// The value is JSON, so strings need quotes.
    /// </summary>
    public static Assertion Assertion(string expression)
    {
        var text = expression.Trim();
        var parts = text.Split((char[]?)null, 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts is ["duration", "<", var limit])
        {
            var match = DurationPattern().Match(limit);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var number))
                throw Invalid(expression, "use 'duration < 3000ms' or 'duration < 3s'");
            return new Assertion(Duration: new DurationAssertion(match.Groups[2].Value switch
            {
                "s" => number * 1000, "m" => number * 60_000, "h" => number * 3_600_000, _ => number,
            }));
        }

        if (parts.Length < 2 || !parts[0].StartsWith('$'))
            throw Invalid(expression, "use '<path> <op> <value>' with a path that starts with $, e.g. '$.status == \"ok\"'");
        var (path, op) = (parts[0], parts[1]);
        if (op == "exists")
            return parts.Length == 2 ? new Assertion(path, Exists: true) : throw Invalid(expression, "'exists' takes no value");
        if (parts.Length < 3)
            throw Invalid(expression, $"'{op}' needs a value");

        var value = Json(parts[2], expression);
        return op switch
        {
            "==" => new Assertion(path, EqualTo: value),
            "!=" => new Assertion(path, NotEquals: value),
            "contains" => new Assertion(path, Contains: String(value, expression, op)),
            "matches" => new Assertion(path, Matches: String(value, expression, op)),
            _ => throw Invalid(expression, $"unknown operator '{op}'; use ==, !=, exists, contains, matches"),
        };
    }

    /// <summary>The <c>--assert</c> text of an assertion: the reverse of <see cref="Assertion(string)"/>.</summary>
    public static string Format(Assertion a) => a switch
    {
        { Duration: { } d } => $"duration < {d.LessThanMs}ms",
        { EqualTo: { } e } => $"{a.Path} == {e.GetRawText()}",
        { NotEquals: { } e } => $"{a.Path} != {e.GetRawText()}",
        { Exists: true } => $"{a.Path} exists",
        { Exists: false } => $"{a.Path} not exists", // API only: the CLI grammar has no negated form
        { Contains: { } s } => $"{a.Path} contains {Quote(s)}",
        { Matches: { } s } => $"{a.Path} matches {Quote(s)}",
        _ => a.Path ?? "",
    };

    /// <summary>A JSON string as a person types it (no \uXXXX escapes for &lt;, &gt;, ', or non-ASCII).</summary>
    private static string Quote(string value) => $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    /// <summary>A JSON value from the command line; a bare word gets a hint to quote it.</summary>
    public static JsonElement Json(string text, string context)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw Invalid(context, $"the value must be JSON; did you mean \"{text.Replace("\"", "\\\"")}\"");
        }
    }

    /// <summary>Secret headers from env vars: <c>--bearer-env VAR</c> and <c>--header-env Name=VAR</c>. Null when none is given.</summary>
    public static Dictionary<string, string>? Headers(CliHost host, string? bearerEnv, IReadOnlyList<string> headerEnvs)
    {
        if (bearerEnv is null && headerEnvs.Count == 0)
            return null;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (bearerEnv is not null)
            headers["Authorization"] = $"Bearer {EnvValue(host, bearerEnv, "--bearer-env")}";
        foreach (var pair in headerEnvs)
        {
            var at = pair.IndexOf('=');
            if (at <= 0 || at == pair.Length - 1)
                throw CliError.Invalid($"--header-env '{pair}': use Name=ENV_VAR, e.g. X-Api-Key=MCP_API_KEY.", "--header-env");
            headers[pair[..at]] = EnvValue(host, pair[(at + 1)..], "--header-env");
        }
        return headers;
    }

    public static string EnvValue(CliHost host, string name, string option) =>
        host.Env(name) is { Length: > 0 } value ? value
            : throw CliError.Invalid($"{option}: environment variable '{name}' is not set or empty.", option);

    private static string String(JsonElement value, string expression, string op) =>
        value.ValueKind == JsonValueKind.String ? value.GetString()!
            : throw Invalid(expression, $"'{op}' needs a JSON string, e.g. \"text\"");

    private static CliError Invalid(string expression, string reason) =>
        CliError.Invalid($"--assert '{expression}': {reason}.", "--assert");
}
