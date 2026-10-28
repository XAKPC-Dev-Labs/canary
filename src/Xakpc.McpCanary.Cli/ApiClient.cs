using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Xakpc.McpCanary.Contracts;

namespace Xakpc.McpCanary.Cli;

/// <summary>A failed command: the error to print (API or CLI-made, same contract) and the exit code.</summary>
public sealed class CliError(int exitCode, ErrorResponse error, string? rawJson = null) : Exception(error.Message)
{
    public int ExitCode { get; } = exitCode;
    public ErrorResponse Error { get; } = error;
    /// <summary>The API's error body as it came, printed as is with <c>--json</c>.</summary>
    public string? RawJson { get; } = rawJson;

    public static CliError Invalid(string message, string? field = null) =>
        new(ExitCodes.InvalidConfiguration, new ErrorResponse(ErrorCodes.InvalidConfiguration, message, Retryable: false,
            field is null ? null : new() { ["field"] = field }));
}

/// <summary>A successful API answer: the raw JSON (printed as is with <c>--json</c>) and its typed value.</summary>
public sealed record ApiResult<T>(string Json, T Value);

/// <summary>
/// Thin HTTP client for the API. Base URL: <c>MCP_CANARY_API_URL</c>, default <see cref="DefaultApiUrl"/>.
/// Token: <c>MCP_CANARY_TOKEN</c>, else the credentials file entry of the base URL.
/// </summary>
public sealed class ApiClient(CliHost host)
{
    public const string DefaultApiUrl = "https://api.mcpcanary.com";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(100);

    public static readonly string Version = typeof(ApiClient).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    /// <summary>Without a trailing slash; also the key in the credentials file.</summary>
    public string ApiUrl { get; } = (host.Env("MCP_CANARY_API_URL") is { Length: > 0 } url ? url : DefaultApiUrl).TrimEnd('/');

    private string? EnvToken => host.Env("MCP_CANARY_TOKEN") is { Length: > 0 } token ? token : null;

    public string? Token => EnvToken ?? CredentialStore.Read(host.CredentialsPath, ApiUrl);

    /// <summary>Where <see cref="Token"/> comes from: <c>MCP_CANARY_TOKEN</c>, the credentials file path, or null.</summary>
    public string? TokenSource => EnvToken is not null ? "MCP_CANARY_TOKEN"
        : CredentialStore.Read(host.CredentialsPath, ApiUrl) is not null ? host.CredentialsPath : null;

    /// <summary>
    /// The token, creating an anonymous workspace when there is none (first <c>monitor set</c> or <c>webhook set</c>).
    /// Returns the new workspace id when one was created.
    /// </summary>
    public async Task<Guid?> EnsureWorkspaceAsync(CancellationToken ct)
    {
        if (Token is not null)
            return null;
        var created = await SendAsync(HttpMethod.Post, "/v1/workspaces", null, ContractsJsonContext.Default.CreateWorkspaceResponse,
            authenticated: false, DefaultTimeout, ct);
        CredentialStore.Write(host.CredentialsPath, ApiUrl, created.Value.Token);
        return created.Value.WorkspaceId;
    }

    public Task<ApiResult<T>> GetAsync<T>(string path, JsonTypeInfo<T> type, CancellationToken ct, bool authenticated = true) =>
        SendAsync(HttpMethod.Get, path, null, type, authenticated, DefaultTimeout, ct);

    public Task<ApiResult<T>> SendAsync<TBody, T>(
        HttpMethod method, string path, TBody body, JsonTypeInfo<TBody> bodyType, JsonTypeInfo<T> type,
        CancellationToken ct, bool authenticated = true, TimeSpan? timeout = null) =>
        SendAsync(method, path, JsonContent.Create(body, bodyType), type, authenticated, timeout ?? DefaultTimeout, ct);

    public async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method, string path, HttpContent? content, JsonTypeInfo<T> type, bool authenticated, TimeSpan timeout, CancellationToken ct)
    {
        var json = await SendRawAsync(method, path, content, authenticated, timeout, ct);
        try
        {
            return new(json, JsonSerializer.Deserialize(json, type)!);
        }
        catch (JsonException ex)
        {
            throw new CliError(ExitCodes.ServiceUnavailable, new ErrorResponse(ErrorCodes.ServiceUnavailable,
                $"{ApiUrl} returned an unexpected answer: {ex.Message}", Retryable: true));
        }
    }

    /// <summary>Sends the request and returns the success body ("" for 204). Throws <see cref="CliError"/> otherwise.</summary>
    public async Task<string> SendRawAsync(
        HttpMethod method, string path, HttpContent? content, bool authenticated, TimeSpan timeout, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, ApiUrl + path) { Content = content };
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("canary", Version));
        if (authenticated)
        {
            var token = Token ?? throw new CliError(ExitCodes.AuthFailed, new ErrorResponse(ErrorCodes.Unauthorized,
                "No workspace token. 'canary monitor set' creates a workspace; or set MCP_CANARY_TOKEN.", Retryable: false));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutSource.CancelAfter(timeout);
        HttpResponseMessage response;
        string body;
        try
        {
            response = await host.Http.SendAsync(request, timeoutSource.Token);
            body = await response.Content.ReadAsStringAsync(timeoutSource.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new CliError(ExitCodes.ServiceUnavailable, new ErrorResponse(ErrorCodes.ServiceUnavailable,
                ex is OperationCanceledException
                    ? $"{ApiUrl} did not answer within {timeout.TotalSeconds:0} s."
                    : $"Cannot reach {ApiUrl}: {ex.Message}", Retryable: true));
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
                return body;
            var status = (int)response.StatusCode;
            ErrorResponse? error = null;
            try
            {
                error = JsonSerializer.Deserialize(body, ContractsJsonContext.Default.ErrorResponse);
            }
            catch (JsonException)
            {
            }
            if (error?.Code is null)
                throw new CliError(ExitCodes.FromHttpStatus(status), new ErrorResponse(
                    status >= 500 ? ErrorCodes.InternalError : ErrorCodes.BadRequest,
                    $"{ApiUrl} answered HTTP {status} without an error body.", Retryable: status >= 500));
            throw new CliError(ExitCodes.FromHttpStatus(status), error, body);
        }
    }
}
