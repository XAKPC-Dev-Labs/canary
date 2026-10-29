namespace Xakpc.McpCanary.Contracts;

/// <summary>
/// Optional body of <c>POST /v1/monitors/{id}/auth/login</c>. Without a client, the server registers itself with the
/// target's authorization server (client ID metadata document, else dynamic client registration).
/// </summary>
/// <param name="ClientId">A pre-registered OAuth client, for servers that do not allow self-registration (e.g. GitHub).
/// Its callback URL must be <c>{api}/oauth/callback</c> (e.g. <c>https://api.mcpcanary.com/oauth/callback</c>).</param>
/// <param name="ClientSecret">Secret of <see cref="ClientId"/>. Stored encrypted, never returned.</param>
/// <param name="Scope">Space-separated scopes. Default: <c>scopes_supported</c> of the target's resource metadata.</param>
public sealed record AuthLoginRequest(string? ClientId = null, string? ClientSecret = null, string? Scope = null);

/// <summary>Result of <c>POST /v1/monitors/{id}/auth/login</c>.</summary>
/// <param name="AuthorizeUrl">The sign-in link for a human. Agents give it to a human; they never open it.</param>
/// <param name="ExpiresAt">The link works until this time.</param>
/// <param name="Registration">How the client was found: <see cref="OAuthRegistrations"/>.</param>
public sealed record AuthLoginResponse(string AuthorizeUrl, DateTime ExpiresAt, string Registration);

/// <summary>The OAuth sign-in of a monitor's target (<c>GET /v1/monitors/{id}/auth</c>). Tokens are never returned.</summary>
/// <param name="Status">One of <see cref="TargetAuthStatuses"/>.</param>
/// <param name="LoginPending">A sign-in link was made and is not used or expired yet.</param>
/// <param name="Registration">One of <see cref="OAuthRegistrations"/>.</param>
/// <param name="Scope">Scopes the authorization server granted (or asked for, before the first sign-in).</param>
/// <param name="AccessTokenExpiresAt">Null when the token does not expire or there is none.</param>
/// <param name="Refreshable">The server has a refresh token, so it renews the access token without a human.</param>
/// <param name="LastError">Why the last sign-in or token renewal failed; null after a success.</param>
public sealed record TargetAuthResponse(
    string Status,
    bool LoginPending,
    string Issuer,
    string ClientId,
    string Registration,
    string? Scope,
    DateTime? AuthorizedAt,
    DateTime? AccessTokenExpiresAt,
    bool Refreshable,
    string? LastError);

/// <summary>Values of <see cref="TargetAuthResponse.Status"/> and of <c>auth</c> on a monitor.</summary>
public static class TargetAuthStatuses
{
    /// <summary>A sign-in was started, but no token exists yet. Checks run without OAuth.</summary>
    public const string Pending = "pending";
    /// <summary>Checks send the access token, and the server renews it.</summary>
    public const string Active = "active";
    /// <summary>The token cannot be renewed (revoked or expired). Checks fail with TARGET_AUTH_EXPIRED until a new sign-in.</summary>
    public const string Expired = "expired";
}

/// <summary>Values of <c>registration</c>: how MCP Canary is known to the target's authorization server.</summary>
public static class OAuthRegistrations
{
    /// <summary>Client ID metadata document: the client id is MCP Canary's URL.</summary>
    public const string Cimd = "cimd";
    /// <summary>Dynamic client registration (RFC 7591).</summary>
    public const string Dynamic = "dynamic";
    /// <summary>A client the user registered and passed with <c>client_id</c>.</summary>
    public const string Manual = "manual";
}
