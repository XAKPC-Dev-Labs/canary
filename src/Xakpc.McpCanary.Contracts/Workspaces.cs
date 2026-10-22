namespace Xakpc.McpCanary.Contracts;

/// <summary>
/// Response of <c>POST /v1/workspaces</c>. The token is shown only once; store it
/// (the CLI writes it to its credentials file) or pass it as <c>MCP_CANARY_TOKEN</c>.
/// A new workspace has no entitlement; its first monitor write starts the trial.
/// </summary>
public sealed record CreateWorkspaceResponse(Guid WorkspaceId, string Token);
