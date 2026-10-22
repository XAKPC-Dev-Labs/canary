using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Xakpc.McpCanary.Contracts;

/// <summary>
/// Stable error contract. Agents branch on <see cref="Code"/>, never on <see cref="Message"/>.
/// </summary>
/// <param name="Offers">
/// Set on <c>PAYMENT_REQUIRED</c> and <c>PLAN_LIMIT_REACHED</c>: the plans that allow the request, each with a
/// checkout link for a human. Errors never carry a payment challenge, so a request never spends money by itself.
/// </param>
public sealed record ErrorResponse(
    string Code,
    string Message,
    bool Retryable,
    JsonObject? Details = null,
    IReadOnlyList<Offer>? Offers = null)
{
    [JsonPropertyOrder(-1)]
    public bool Ok => false;
}
