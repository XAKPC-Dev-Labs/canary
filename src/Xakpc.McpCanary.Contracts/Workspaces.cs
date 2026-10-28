namespace Xakpc.McpCanary.Contracts;

/// <summary>
/// Response of <c>POST /v1/workspaces</c>. The token is shown only once; store it
/// (the CLI writes it to its credentials file) or pass it as <c>MCP_CANARY_TOKEN</c>.
/// A new workspace has no entitlement; its first monitor write starts the trial.
/// </summary>
public sealed record CreateWorkspaceResponse(Guid WorkspaceId, string Token);

/// <summary>
/// Response of <c>GET /v1/workspace</c>: the workspace of the token. The id is what support, plan grants,
/// and invoices refer to. Access and usage are in <c>GET /v1/entitlement</c>.
/// </summary>
public sealed record WorkspaceResponse(Guid WorkspaceId, DateTime CreatedAt);
