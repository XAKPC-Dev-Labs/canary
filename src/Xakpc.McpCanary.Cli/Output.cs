using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Xakpc.McpCanary.Contracts;

namespace Xakpc.McpCanary.Cli;

/// <summary>
/// Writes a command's result. With <c>--json</c>: only the API's JSON (or the error JSON) on stdout, nothing else.
/// Without: short human text; errors go to stderr. No colors, so <c>NO_COLOR</c> and pipes need no special case.
/// </summary>
public sealed class Output(CliHost host, bool json)
{
    /// <summary>Readable JSON for errors the CLI makes itself: quotes and non-ASCII text are not escaped as \uXXXX.</summary>
    private static readonly JsonTypeInfo<ErrorResponse> ErrorJson = (JsonTypeInfo<ErrorResponse>)
        new JsonSerializerOptions(ContractsJsonContext.Default.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }
            .GetTypeInfo(typeof(ErrorResponse));

    public bool Json { get; } = json;

    public void Result(string apiJson, Action<TextWriter> human)
    {
        if (Json)
            host.Out.WriteLine(apiJson);
        else
            human(host.Out);
    }

    /// <summary>A note for humans only (stderr), e.g. where a new token was saved.</summary>
    public void Note(string text)
    {
        if (!Json)
            host.Err.WriteLine(text);
    }

    public void Error(CliError error)
    {
        if (Json)
        {
            host.Out.WriteLine(error.RawJson ?? JsonSerializer.Serialize(error.Error, ErrorJson));
            return;
        }
        var e = error.Error;
        host.Err.WriteLine($"error: {e.Code}: {e.Message}");
        if (e.Details is { Count: > 0 } details)
            host.Err.WriteLine($"  details: {details.ToJsonString()}");
        foreach (var offer in e.Offers ?? [])
            host.Err.WriteLine($"  {offer.Plan} {Money(offer.Amount, offer.Currency)}/{offer.Interval}: {offer.CheckoutUrl}");
        if (e.Offers is { Count: > 0 })
            host.Err.WriteLine("  Give a link to a human to pay. Checks and settings stay as they are.");
    }

    public static string Money(int cents, string currency) =>
        currency == "USD" ? $"${cents / 100m:0.##}" : $"{cents / 100m:0.##} {currency}";

    public static string Check(CheckResult? check) => check is null ? "-"
        : check.Status == CheckStatuses.Passed ? $"passed {check.DurationMs} ms"
        : $"FAILED {check.Failure?.Code}";

    public static void CheckLine(TextWriter w, string label, string status, long durationMs, CheckFailure? failure)
    {
        w.WriteLine(status == CheckStatuses.Passed
            ? $"{label}: PASSED ({durationMs} ms)"
            : $"{label}: FAILED ({durationMs} ms) {failure?.Code}: {failure?.Message}");
        if (failure?.Details is { Count: > 0 } details)
            w.WriteLine($"  details: {details.ToJsonString()}");
    }

    /// <summary>Left-aligned columns, two spaces apart.</summary>
    public static void Table(TextWriter w, string[] header, IEnumerable<string?[]> rows)
    {
        var all = new List<string?[]> { header };
        all.AddRange(rows);
        if (all.Count == 1)
        {
            w.WriteLine("(none)");
            return;
        }
        var widths = Enumerable.Range(0, header.Length).Select(i => all.Max(r => (r[i] ?? "").Length)).ToArray();
        foreach (var row in all)
            w.WriteLine(string.Join("  ", row.Select((cell, i) => (cell ?? "").PadRight(i == row.Length - 1 ? 0 : widths[i]))).TrimEnd());
    }

    /// <summary>"7 days" for whole days, else "72 h".</summary>
    public static string Hours(int hours) => hours % 24 == 0 ? $"{hours / 24} day{(hours == 24 ? "" : "s")}" : $"{hours} h";

    /// <summary>Seconds in the <c>--interval</c> form: <c>1h</c>, <c>5m</c>, or <c>90s</c>.</summary>
    public static string Duration(int seconds) =>
        seconds % 3600 == 0 ? $"{seconds / 3600}h" : seconds % 60 == 0 ? $"{seconds / 60}m" : $"{seconds}s";

    public static string Time(DateTime? value) => value is { } t ? t.ToString("yyyy-MM-dd HH:mm") + " UTC" : "-";
}
