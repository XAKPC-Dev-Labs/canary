using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xakpc.McpCanary.Contracts;

/// <summary>
/// Source-generated JSON metadata shared by the API and the NativeAOT CLI.
/// Wire format is snake_case; nulls are omitted.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(PublicServerListResponse))]
[JsonSerializable(typeof(PublicServerResponse))]
[JsonSerializable(typeof(InspectRequest))]
[JsonSerializable(typeof(InspectResponse))]
[JsonSerializable(typeof(CreateWorkspaceResponse))]
[JsonSerializable(typeof(WorkspaceResponse))]
[JsonSerializable(typeof(MonitorSpec))]
[JsonSerializable(typeof(PutMonitorResponse))]
[JsonSerializable(typeof(MonitorListResponse))]
[JsonSerializable(typeof(MonitorPreviewResponse))]
[JsonSerializable(typeof(AuthLoginRequest))]
[JsonSerializable(typeof(AuthLoginResponse))]
[JsonSerializable(typeof(TargetAuthResponse))]
[JsonSerializable(typeof(CheckResult))]
[JsonSerializable(typeof(CheckListResponse))]
[JsonSerializable(typeof(CheckFailure))]
[JsonSerializable(typeof(List<Assertion>))]
[JsonSerializable(typeof(List<ContractChange>))]
[JsonSerializable(typeof(IncidentResponse))]
[JsonSerializable(typeof(IncidentListResponse))]
[JsonSerializable(typeof(WebhookSpec))]
[JsonSerializable(typeof(PutWebhookResponse))]
[JsonSerializable(typeof(WebhookResponse))]
[JsonSerializable(typeof(WebhookListResponse))]
[JsonSerializable(typeof(DeliveryListResponse))]
[JsonSerializable(typeof(WebhookEvent))]
[JsonSerializable(typeof(PutEmailResponse))]
[JsonSerializable(typeof(EmailResponse))]
[JsonSerializable(typeof(EmailListResponse))]
[JsonSerializable(typeof(PricingResponse))]
[JsonSerializable(typeof(EntitlementResponse))]
[JsonSerializable(typeof(CancelSubscriptionResponse))]
[JsonSerializable(typeof(CheckoutRequest))]
[JsonSerializable(typeof(CheckoutResponse))]
[JsonSerializable(typeof(PortalResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
public partial class ContractsJsonContext : JsonSerializerContext;

public static class ContractsJson
{
    /// <summary>
    /// Applies the wire format to options owned by someone else (e.g. ASP.NET Core).
    /// Needed because a context used only as a resolver does not carry its naming or null policy.
    /// </summary>
    public static void Apply(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.TypeInfoResolverChain.Insert(0, ContractsJsonContext.Default);
    }
}
