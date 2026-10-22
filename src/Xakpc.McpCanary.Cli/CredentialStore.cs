using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xakpc.McpCanary.Cli;

/// <summary>
/// Workspace tokens per API URL in one JSON file (mode 0600 on macOS and Linux). One file per API URL would also work;
/// a map keeps a localhost or staging token from replacing the production one.
/// </summary>
public static class CredentialStore
{
    public static string? Read(string path, string apiUrl)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            var file = JsonSerializer.Deserialize(File.ReadAllText(path), CliJsonContext.Default.CredentialsFile);
            return file?.Tokens.GetValueOrDefault(apiUrl);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void Write(string path, string apiUrl, string token)
    {
        var tokens = ReadAll(path);
        tokens[apiUrl] = token;
        Save(path, tokens);
    }

    /// <summary>Forgets the token of one API URL (after the workspace is deleted). Other entries stay.</summary>
    public static void Remove(string path, string apiUrl)
    {
        var tokens = ReadAll(path);
        if (tokens.Remove(apiUrl))
            Save(path, tokens);
    }

    private static Dictionary<string, string> ReadAll(string path)
    {
        if (!File.Exists(path))
            return [];
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), CliJsonContext.Default.CredentialsFile)?.Tokens ?? [];
        }
        catch (JsonException)
        {
            return []; // A broken file is replaced.
        }
    }

    private static void Save(string path, Dictionary<string, string> tokens)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (var stream = new FileStream(path, options))
            JsonSerializer.Serialize(stream, new CredentialsFile(tokens), CliJsonContext.Default.CredentialsFile);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); // also when the file existed before
    }
}

public sealed record CredentialsFile(Dictionary<string, string> Tokens);

/// <summary>Success body of commands whose API answer has none (delete).</summary>
public sealed record OkResponse(bool Ok);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(CredentialsFile))]
[JsonSerializable(typeof(OkResponse))]
public partial class CliJsonContext : JsonSerializerContext;
