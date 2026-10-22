using System.CommandLine;
using System.Text.Json;
using Xakpc.McpCanary.Contracts;

namespace Xakpc.McpCanary.Cli;

/// <summary>
/// The <c>canary</c> command tree. A thin client: it parses arguments, reads secrets from env vars, calls the API,
/// and prints the answer. Business rules stay on the server. Exit codes: <see cref="ExitCodes"/>.
/// </summary>
public static class CliApp
{
    public static async Task<int> RunAsync(string[] args, CliHost host, CancellationToken ct = default)
    {
        var json = new Option<bool>("--json") { Description = "Print only the API's JSON (stable, versioned).", Recursive = true };
        var root = new RootCommand("MCP Canary: external monitoring for MCP endpoints. The HTTP API is canonical; see /openapi.json.");
        root.Options.Add(json);
        var commands = new Commands(host, json);
        foreach (var command in commands.All())
            root.Subcommands.Add(command);

        if (args is ["--version"]) // the built-in option prints the entry assembly's version, which is the test host in tests
        {
            host.Out.WriteLine(ApiClient.Version);
            return ExitCodes.Success;
        }

        var parse = root.Parse(args.Length == 0 ? ["--help"] : args);
        if (parse.Errors.Count > 0)
        {
            new Output(host, parse.GetValue(json)).Error(CliError.Invalid(string.Join(" ", parse.Errors.Select(e => e.Message))
                + " Run 'canary --help'."));
            return ExitCodes.InvalidConfiguration;
        }
        return await parse.InvokeAsync(new InvocationConfiguration { Output = host.Out, Error = host.Err }, ct);
    }

    private sealed class Commands(CliHost host, Option<bool> json)
    {
        private readonly ApiClient _api = new(host);

        public IEnumerable<Command> All() =>
            [Inspect(), Monitor(), Incident(), Webhook(), Email(), Pricing(), Entitlement(), Billing(), Subscription(), Workspace()];

        private Command Inspect()
        {
            var url = new Argument<string>("url") { Description = "MCP endpoint (http or https)." };
            var (bearer, headers) = AuthOptions();
            return Cmd("inspect", "Connect, list tools and their side-effect class. Never calls a tool.", [url, bearer, headers],
                async (p, o, ct) =>
                {
                    var request = new InspectRequest(p.GetValue(url)!, ArgParsers.Headers(host, p.GetValue(bearer), p.GetValue(headers) ?? []));
                    var result = await _api.SendAsync(HttpMethod.Post, "/v1/inspect", request, ContractsJsonContext.Default.InspectRequest,
                        ContractsJsonContext.Default.InspectResponse, ct, authenticated: false);
                    var r = result.Value;
                    o.Result(result.Json, w =>
                    {
                        Output.CheckLine(w, r.Endpoint, r.Status, r.DurationMs, r.Failure);
                        if (r.Server is { } server)
                            w.WriteLine($"Server: {server.Name} {server.Version} ({r.Transport}, protocol {r.Protocol})");
                        Output.Table(w, ["TOOL", "SIDE EFFECTS", "DESCRIPTION"],
                            r.Tools.Select(t => new[] { t.Name, t.SideEffects, FirstLine(t.Description) }));
                    });
                    return ExitCodes.FromCheck(r.Status, r.Failure);
                });
        }

        private Command Monitor()
        {
            var monitor = new Command("monitor", "Create, read, test, and delete monitors.");
            monitor.Subcommands.Add(MonitorSet());
            monitor.Subcommands.Add(Cmd("list", "List monitors.", [], async (p, o, ct) =>
            {
                var result = await _api.GetAsync("/v1/monitors", ContractsJsonContext.Default.MonitorListResponse, ct);
                o.Result(result.Json, w => Output.Table(w, ["NAME", "STATUS", "EVERY", "LAST CHECK", "INCIDENT"],
                    result.Value.Monitors.Select(m => new[]
                    {
                        m.Name, m.SuspendedReason is { } reason ? $"suspended ({reason})" : m.Status,
                        $"{m.EffectiveIntervalSeconds}s", Output.Check(m.LastCheck), m.OpenIncidentId is null ? "-" : "open",
                    })));
                return ExitCodes.Success;
            }));

            var id = IdArgument("Monitor name or id.");
            monitor.Subcommands.Add(Cmd("get", "Show one monitor with its last check.", [id], async (p, o, ct) =>
            {
                var result = await _api.GetAsync($"/v1/monitors/{Escape(p.GetValue(id))}", ContractsJsonContext.Default.MonitorResponse, ct);
                o.Result(result.Json, w => PrintMonitor(w, result.Value));
                return ExitCodes.Success;
            }));

            monitor.Subcommands.Add(Cmd("delete", "Delete a monitor with its checks and incidents.", [id], async (p, o, ct) =>
                await DeleteAsync($"/v1/monitors/{Escape(p.GetValue(id))}", $"Deleted monitor '{p.GetValue(id)}'.", o, ct)));

            var timeout = new Option<string>("--timeout") { Description = "How long to wait for the answer, e.g. 90s.", DefaultValueFactory = _ => "90s" };
            monitor.Subcommands.Add(Cmd("test", "Run one check now and wait for it (CI gate). Exit 0 = passed.", [id, timeout], async (p, o, ct) =>
            {
                var wait = TimeSpan.FromSeconds(ArgParsers.Seconds(p.GetValue(timeout)!, "--timeout"));
                var result = await _api.SendAsync(HttpMethod.Post, $"/v1/monitors/{Escape(p.GetValue(id))}/test", null,
                    ContractsJsonContext.Default.CheckResult, authenticated: true, wait, ct);
                var check = result.Value;
                o.Result(result.Json, w => Output.CheckLine(w, p.GetValue(id)!, check.Status, check.DurationMs, check.Failure));
                return ExitCodes.FromCheck(check.Status, check.Failure);
            }));

            var limit = LimitOption();
            monitor.Subcommands.Add(Cmd("checks", "List stored checks, newest first.", [id, limit], async (p, o, ct) =>
            {
                var result = await _api.GetAsync($"/v1/monitors/{Escape(p.GetValue(id))}/checks?limit={p.GetValue(limit)}",
                    ContractsJsonContext.Default.CheckListResponse, ct);
                o.Result(result.Json, w => Output.Table(w, ["STARTED", "TRIGGER", "RESULT", "CONTRACT"],
                    result.Value.Checks.Select(c => new[]
                    {
                        Output.Time(c.StartedAt), c.Trigger, Output.Check(c), c.ContractChanged ? "changed" : "",
                    })));
                return ExitCodes.Success;
            }));
            return monitor;
        }

        private Command MonitorSet()
        {
            var name = new Argument<string>("name") { Description = "Stable name: 2-63 chars, lowercase letters, digits, dashes." };
            var url = new Option<string>("--url") { Description = "MCP endpoint.", Required = true };
            var tool = new Option<string?>("--tool") { Description = "Tool to call on each check. Left out = connect and list tools only." };
            var args = new Option<string?>("--args") { Description = "Tool arguments as a JSON object." };
            var asserts = new Option<string[]>("--assert")
            {
                Description = "Check on the tool result, e.g. '$.items[0].id == \"x\"', '$.ok exists', 'duration < 3000ms'. Repeatable.",
            };
            var interval = new Option<string?>("--interval") { Description = "Time between checks, e.g. 1m. Default 5m; the plan sets the minimum." };
            var timeout = new Option<string?>("--timeout") { Description = "Whole-check timeout, e.g. 30s. Default 30s." };
            var sideEffects = new Option<bool>("--allow-side-effects") { Description = "Allow calling a tool that is not declared read-only." };
            var alert = new Option<bool>("--alert-on-change") { Description = "Open an incident when the tool contract changes." };
            var dryRun = new Option<bool>("--dry-run")
            {
                Description = "Show the tool, arguments, assertions, and schedule that would be set, without saving anything " +
                    "or calling the tool. Exit 0 = ready, 2 = warnings.",
            };
            var (bearer, headers) = AuthOptions();

            return Cmd("set", "Create or update a monitor by name (idempotent) and run its first check. " +
                "Creates a workspace when there is no token; the first monitor starts the trial.",
                [name, url, tool, args, asserts, interval, timeout, sideEffects, alert, dryRun, bearer, headers], async (p, o, ct) =>
                {
                    var spec = new MonitorSpec(
                        p.GetValue(url)!,
                        p.GetValue(interval) is { } i ? ArgParsers.Seconds(i, "--interval") : null,
                        p.GetValue(timeout) is { } t ? ArgParsers.Seconds(t, "--timeout") : null,
                        p.GetValue(tool),
                        p.GetValue(args) is { } a ? ArgParsers.Json(a, "--args") : null,
                        p.GetValue(asserts) is { Length: > 0 } list ? list.Select(ArgParsers.Assertion).ToList() : null,
                        p.GetValue(sideEffects),
                        ArgParsers.Headers(host, p.GetValue(bearer), p.GetValue(headers) ?? []),
                        p.GetValue(alert));

                    if (p.GetValue(dryRun))
                        return await PreviewAsync(p.GetValue(name)!, spec, o, ct);

                    await EnsureWorkspaceAsync(o, ct);
                    var result = await _api.SendAsync(HttpMethod.Put, $"/v1/monitors/{Escape(p.GetValue(name))}", spec,
                        ContractsJsonContext.Default.MonitorSpec, ContractsJsonContext.Default.PutMonitorResponse, ct);
                    var r = result.Value;
                    o.Result(result.Json, w =>
                    {
                        w.WriteLine($"{(r.Created ? "Created" : r.Changed ? "Updated" : "Unchanged")} monitor '{r.Monitor.Name}'.");
                        Output.CheckLine(w, "First check", r.FirstCheck.Status, r.FirstCheck.DurationMs, r.FirstCheck.Failure);
                    });
                    return r.FirstCheck.Status == CheckStatuses.Passed ? ExitCodes.Success : ExitCodes.Warning;
                });
        }

        /// <summary><c>monitor set --dry-run</c>: never creates a workspace or saves a token; sends the token only when there is one.</summary>
        private async Task<int> PreviewAsync(string name, MonitorSpec spec, Output o, CancellationToken ct)
        {
            var hasToken = _api.Token is not null;
            var result = await _api.SendAsync(HttpMethod.Post, $"/v1/monitors/{Escape(name)}/preview", spec,
                ContractsJsonContext.Default.MonitorSpec, ContractsJsonContext.Default.MonitorPreviewResponse, ct, authenticated: hasToken);
            var r = result.Value;
            var m = r.Monitor;
            o.Result(result.Json, w =>
            {
                w.WriteLine($"Preview of monitor '{m.Name}' ({(r.Exists ? "exists" : "new")}, nothing saved)");
                var rows = new List<string?[]>
                {
                    new[] { "Endpoint", $"{m.Url}  {(r.Probe.Status == CheckStatuses.Passed ? "PASSED" : "FAILED")}" +
                        (r.Probe.Transport is { } t ? $" ({t})" : "") },
                    new[] { "Tool", r.Tool is { } tool
                        ? $"{tool.Name}  {(tool.Found ? tool.SideEffects : "not found")}{(m.AllowSideEffects ? "  (side effects allowed)" : "")}"
                        : "none: connect and list tools only" },
                };
                if (m.Arguments is { } a)
                    rows.Add(["Arguments", a.GetRawText()]);
                rows.AddRange((m.Assertions ?? []).Select((x, i) => new[] { i == 0 ? "Assertions" : "", ArgParsers.Format(x) }));
                rows.Add(["Schedule", $"every {Output.Duration(m.EffectiveIntervalSeconds)}" +
                    (m.EffectiveIntervalSeconds != m.IntervalSeconds ? $" (plan minimum; asked {Output.Duration(m.IntervalSeconds)})" : "") +
                    $", timeout {Output.Duration(m.TimeoutSeconds)}"]);
                if (m.Headers.Count > 0)
                    rows.Add(["Headers", $"{string.Join(", ", m.Headers)} (values hidden)"]);
                if (m.AlertOnChange)
                    rows.Add(["Alerts", "a contract change opens an incident"]);
                rows.Add(["On apply", OnApply(r, hasToken)]);
                rows.AddRange(r.Changes.Select(c => new[] { "", $"  {c.Field}: {Raw(c.From)} -> {Raw(c.To)}" }));
                foreach (var line in Lines(rows))
                    w.WriteLine(line);
                foreach (var warning in r.Warnings)
                    w.WriteLine($"Warning: {warning.Code}: {warning.Message}");
            });
            return r.Warnings.Count == 0 ? ExitCodes.Success : ExitCodes.Warning;
        }

        private static string OnApply(MonitorPreviewResponse r, bool hasToken) =>
            r.Exists && !r.Changed ? "no change"
            : r.Exists ? "updates the monitor:"
            : string.Join(", ", new[]
            {
                !hasToken ? "creates a workspace" : null,
                r.WouldStartTrial ? "starts the trial" : null,
                "saves the monitor, runs a first check",
            }.OfType<string>());

        private static string Raw(JsonElement? value) => value?.GetRawText() ?? "null";

        /// <summary>Two columns: a label padded to the widest label, then the value. No header row.</summary>
        private static IEnumerable<string> Lines(List<string?[]> rows)
        {
            var width = rows.Max(r => (r[0] ?? "").Length) + 2;
            return rows.Select(r => ((r[0] ?? "").PadRight(width) + r[1]).TrimEnd());
        }

        private Command Incident()
        {
            var incident = new Command("incident", "Read incidents.");
            var status = new Option<string?>("--status") { Description = "open or resolved." };
            var monitor = new Option<string?>("--monitor") { Description = "Monitor name or id." };
            var limit = LimitOption();
            incident.Subcommands.Add(Cmd("list", "List incidents, newest first.", [status, monitor, limit], async (p, o, ct) =>
            {
                var query = $"?limit={p.GetValue(limit)}"
                    + (p.GetValue(status) is { } s ? $"&status={Uri.EscapeDataString(s)}" : "")
                    + (p.GetValue(monitor) is { } m ? $"&monitor={Uri.EscapeDataString(m)}" : "");
                var result = await _api.GetAsync("/v1/incidents" + query, ContractsJsonContext.Default.IncidentListResponse, ct);
                o.Result(result.Json, w => Output.Table(w, ["ID", "MONITOR", "STATUS", "CAUSE", "CODE", "OPENED"],
                    result.Value.Incidents.Select(i => new[]
                    {
                        i.Id.ToString(), i.MonitorName, i.Status, i.Cause, i.Failure.Code, Output.Time(i.OpenedAt),
                    })));
                return ExitCodes.Success;
            }));

            var id = IdArgument("Incident id.");
            incident.Subcommands.Add(Cmd("get", "Show one incident with its evidence.", [id], async (p, o, ct) =>
            {
                var result = await _api.GetAsync($"/v1/incidents/{Escape(p.GetValue(id))}", ContractsJsonContext.Default.IncidentResponse, ct);
                var i = result.Value;
                o.Result(result.Json, w =>
                {
                    w.WriteLine($"Incident {i.Id} on '{i.MonitorName}': {i.Status} ({i.Cause})");
                    w.WriteLine($"Opened: {Output.Time(i.OpenedAt)}  Resolved: {Output.Time(i.ResolvedAt)}");
                    w.WriteLine($"{i.Failure.Code}: {i.Failure.Message}");
                    if (i.Failure.Details is { Count: > 0 } details)
                        w.WriteLine($"  details: {details.ToJsonString()}");
                });
                return ExitCodes.Success;
            }));
            return incident;
        }

        private Command Webhook()
        {
            var webhook = new Command("webhook", "Signed event delivery to your URL.");
            var name = new Argument<string>("name") { Description = "Stable name." };
            var url = new Option<string>("--url") { Description = "Receiver URL (public http or https).", Required = true };
            var events = new Option<string[]>("--event")
            {
                Description = "Event type to deliver, or '*'. Repeatable. Default: incident.*, contract.changed, entitlement.expiring, entitlement.expired.",
            };
            webhook.Subcommands.Add(Cmd("set", "Create or update a webhook by name (idempotent). The secret is shown once, on create.",
                [name, url, events], async (p, o, ct) =>
                {
                    await EnsureWorkspaceAsync(o, ct);
                    var spec = new WebhookSpec(p.GetValue(url)!, p.GetValue(events) is { Length: > 0 } e ? e : null);
                    var result = await _api.SendAsync(HttpMethod.Put, $"/v1/webhooks/{Escape(p.GetValue(name))}", spec,
                        ContractsJsonContext.Default.WebhookSpec, ContractsJsonContext.Default.PutWebhookResponse, ct);
                    var r = result.Value;
                    o.Result(result.Json, w =>
                    {
                        w.WriteLine($"{(r.Created ? "Created" : r.Changed ? "Updated" : "Unchanged")} webhook '{r.Webhook.Name}': {string.Join(", ", r.Webhook.Events)}");
                        if (r.Secret is { } secret)
                            w.WriteLine($"Signing secret (shown once, save it now): {secret}");
                    });
                    return ExitCodes.Success;
                }));

            webhook.Subcommands.Add(Cmd("list", "List webhooks.", [], async (p, o, ct) =>
            {
                var result = await _api.GetAsync("/v1/webhooks", ContractsJsonContext.Default.WebhookListResponse, ct);
                o.Result(result.Json, w => Output.Table(w, ["NAME", "STATUS", "URL", "EVENTS"],
                    result.Value.Webhooks.Select(h => new[] { h.Name, h.Status, h.Url, string.Join(",", h.Events) })));
                return ExitCodes.Success;
            }));

            var id = IdArgument("Webhook name or id.");
            webhook.Subcommands.Add(Cmd("get", "Show one webhook.", [id], async (p, o, ct) =>
            {
                var result = await _api.GetAsync($"/v1/webhooks/{Escape(p.GetValue(id))}", ContractsJsonContext.Default.WebhookResponse, ct);
                var h = result.Value;
                o.Result(result.Json, w => w.WriteLine(
                    $"{h.Name}: {h.Status}{(h.SuspendedReason is { } r ? $" ({r})" : "")}, {h.Url}, events {string.Join(", ", h.Events)}"));
                return ExitCodes.Success;
            }));

            webhook.Subcommands.Add(Cmd("delete", "Delete a webhook with its delivery log.", [id], async (p, o, ct) =>
                await DeleteAsync($"/v1/webhooks/{Escape(p.GetValue(id))}", $"Deleted webhook '{p.GetValue(id)}'.", o, ct)));

            var limit = LimitOption();
            webhook.Subcommands.Add(Cmd("deliveries", "List deliveries, newest first.", [id, limit], async (p, o, ct) =>
            {
                var result = await _api.GetAsync($"/v1/webhooks/{Escape(p.GetValue(id))}/deliveries?limit={p.GetValue(limit)}",
                    ContractsJsonContext.Default.DeliveryListResponse, ct);
                o.Result(result.Json, w => Output.Table(w, ["CREATED", "EVENT", "STATUS", "ATTEMPTS", "LAST"],
                    result.Value.Deliveries.Select(d => new[]
                    {
                        Output.Time(d.CreatedAt), d.Event, d.Status, d.Attempts.ToString(),
                        d.LastHttpStatus?.ToString() ?? d.LastError ?? "-",
                    })));
                return ExitCodes.Success;
            }));
            return webhook;
        }

        private Command Email()
        {
            var email = new Command("email", "Alert emails for incidents and access changes (double opt-in).");
            var address = new Argument<string>("address") { Description = "Email address." };
            email.Subcommands.Add(Cmd("add", "Add an alert address (idempotent). Its owner must click the confirm link before alerts start.",
                [address], async (p, o, ct) =>
                {
                    await EnsureWorkspaceAsync(o, ct);
                    var result = await _api.SendAsync(HttpMethod.Put, $"/v1/emails/{Escape(p.GetValue(address))}", null,
                        ContractsJsonContext.Default.PutEmailResponse, authenticated: true, ApiClient.DefaultTimeout, ct);
                    var r = result.Value;
                    o.Result(result.Json, w => w.WriteLine(
                        $"{(r.Created ? "Added" : "Exists:")} {r.Email.Address} ({r.Email.Status})." +
                        (r.ConfirmationSent ? " A confirm link was sent; alerts start after a human clicks it." : "")));
                    return ExitCodes.Success;
                }));

            email.Subcommands.Add(Cmd("list", "List alert addresses.", [], async (p, o, ct) =>
            {
                var result = await _api.GetAsync("/v1/emails", ContractsJsonContext.Default.EmailListResponse, ct);
                o.Result(result.Json, w => Output.Table(w, ["ADDRESS", "STATUS", "ADDED"],
                    result.Value.Emails.Select(e => new[]
                    {
                        e.Address, e.SuspendedReason is { } reason ? $"suspended ({reason})" : e.Status, Output.Time(e.CreatedAt),
                    })));
                return ExitCodes.Success;
            }));

            email.Subcommands.Add(Cmd("remove", "Remove an alert address.", [address], async (p, o, ct) =>
                await DeleteAsync($"/v1/emails/{Escape(p.GetValue(address))}", $"Removed {p.GetValue(address)}.", o, ct)));
            return email;
        }

        private Command Workspace()
        {
            var workspace = new Command("workspace", "The workspace itself.");
            var yes = new Option<bool>("--yes") { Description = "Required: confirms that all data is deleted for good." };
            workspace.Subcommands.Add(Cmd("delete", "Delete the workspace with all monitors, history, webhooks, and emails. Cannot be undone.",
                [yes], async (p, o, ct) =>
                {
                    if (!p.GetValue(yes))
                        throw CliError.Invalid("This deletes all data of the workspace for good. Run again with --yes.", "--yes");
                    await _api.SendRawAsync(HttpMethod.Delete, "/v1/workspace", null, authenticated: true, ApiClient.DefaultTimeout, ct);
                    CredentialStore.Remove(host.CredentialsPath, _api.ApiUrl);
                    o.Result(JsonSerializer.Serialize(new OkResponse(true), CliJsonContext.Default.OkResponse),
                        w => w.WriteLine("Deleted the workspace and its saved token."));
                    return ExitCodes.Success;
                }));
            return workspace;
        }

        private Command Pricing() => Cmd("pricing", "Plans, prices, and limits. No token needed.", [], async (p, o, ct) =>
        {
            var result = await _api.GetAsync("/v1/pricing", ContractsJsonContext.Default.PricingResponse, ct, authenticated: false);
            var r = result.Value;
            o.Result(result.Json, w =>
            {
                w.WriteLine($"Trial: {Output.Hours(r.Trial.Hours)}, no payment, starts with the first monitor.");
                Output.Table(w, ["PLAN", "PRICE", "MONITORS", "MIN INTERVAL", "HISTORY", "WEBHOOKS", "EMAILS"],
                    [
                        Row("trial", "-", r.Trial.Limits),
                        .. r.Plans.Select(plan => Row(plan.Id, $"{Output.Money(plan.Price, r.Currency)}/{plan.Interval}", plan.Limits)),
                    ]);
            });
            return ExitCodes.Success;

            static string?[] Row(string plan, string price, PlanLimits l) =>
                [plan, price, l.Monitors.ToString(), $"{l.MinIntervalSeconds / 60} min", $"{l.HistoryDays} days", l.Webhooks.ToString(), l.Emails.ToString()];
        });

        private Command Entitlement() => Cmd("entitlement", "Show the workspace's access: trial, paid plan, or none.", [], async (p, o, ct) =>
        {
            var result = await _api.GetAsync("/v1/entitlement", ContractsJsonContext.Default.EntitlementResponse, ct);
            var e = result.Value;
            o.Result(result.Json, w =>
            {
                w.WriteLine(e.Type switch
                {
                    EntitlementTypes.Trial => $"Trial until {Output.Time(e.ExpiresAt)}.",
                    EntitlementTypes.Paid => $"Plan {e.Plan}, {(e.Renews ? "renews" : "ends")} {Output.Time(e.ExpiresAt)}.",
                    _ => e.TrialAvailable ? "No access yet. The first 'canary monitor set' starts the trial."
                        : $"No access ({e.Reason}). Checks and alerts are stopped. 'canary billing upgrade --plan solo' gives a payment link.",
                });
                w.WriteLine($"Usage: {e.Usage.Monitors}{(e.Limits is { } l ? $"/{l.Monitors}" : "")} monitors, " +
                    $"{e.Usage.Webhooks}{(e.Limits is { } l2 ? $"/{l2.Webhooks}" : "")} webhooks, " +
                    $"{e.Usage.Emails}{(e.Limits is { } l4 ? $"/{l4.Emails}" : "")} alert emails, " +
                    $"{e.Usage.OnDemandChecks}{(e.Limits is { } l3 ? $"/{l3.OnDemandChecksPerDay}" : "")} on-demand checks in 24 h.");
            });
            return ExitCodes.Success;
        });

        private Command Billing()
        {
            var billing = new Command("billing", "Payment links for a human.");
            var plan = new Option<string>("--plan") { Description = "solo or pro.", Required = true };
            plan.AcceptOnlyFromAmong(PlanNames.Paid.ToArray());
            billing.Subcommands.Add(Cmd("upgrade", "Print a payment link (valid 7 days) for a human.", [plan], async (p, o, ct) =>
            {
                var result = await _api.SendAsync(HttpMethod.Post, "/v1/billing/checkout", new CheckoutRequest(p.GetValue(plan)!),
                    ContractsJsonContext.Default.CheckoutRequest, ContractsJsonContext.Default.CheckoutResponse, ct);
                o.Result(result.Json, w =>
                {
                    w.WriteLine("Give this payment link to a human (valid 7 days):");
                    w.WriteLine(result.Value.CheckoutUrl);
                });
                return ExitCodes.Success;
            }));
            billing.Subcommands.Add(Cmd("portal", "Print a short-lived customer portal link (card, invoices, plan change).", [], async (p, o, ct) =>
            {
                var result = await _api.SendAsync(HttpMethod.Post, "/v1/billing/portal", null, ContractsJsonContext.Default.PortalResponse,
                    authenticated: true, ApiClient.DefaultTimeout, ct);
                o.Result(result.Json, w => w.WriteLine(result.Value.PortalUrl));
                return ExitCodes.Success;
            }));
            return billing;
        }

        private Command Subscription()
        {
            var subscription = new Command("subscription", "The paid subscription.");
            subscription.Subcommands.Add(Cmd("cancel", "Cancel at the end of the paid period (idempotent).", [], async (p, o, ct) =>
            {
                var result = await _api.SendAsync(HttpMethod.Post, "/v1/subscription/cancel", null,
                    ContractsJsonContext.Default.CancelSubscriptionResponse, authenticated: true, ApiClient.DefaultTimeout, ct);
                o.Result(result.Json, w => w.WriteLine($"Cancelled. The plan stays active until {Output.Time(result.Value.AccessUntil)}."));
                return ExitCodes.Success;
            }));
            return subscription;
        }

        /// <summary>A command whose action prints <see cref="CliError"/>s and returns their exit code.</summary>
        private Command Cmd(string name, string description, Symbol[] symbols, Func<ParseResult, Output, CancellationToken, Task<int>> run)
        {
            var command = new Command(name, description);
            foreach (var symbol in symbols)
            {
                if (symbol is Argument argument)
                    command.Arguments.Add(argument);
                else if (symbol is Option option)
                    command.Options.Add(option);
            }
            command.SetAction(async (parse, ct) =>
            {
                var output = new Output(host, parse.GetValue(json));
                try
                {
                    return await run(parse, output, ct);
                }
                catch (CliError error)
                {
                    output.Error(error);
                    return error.ExitCode;
                }
            });
            return command;
        }

        private async Task EnsureWorkspaceAsync(Output o, CancellationToken ct)
        {
            if (await _api.EnsureWorkspaceAsync(ct) is { } workspaceId)
                o.Note($"Created workspace {workspaceId}; token saved to {host.CredentialsPath}.");
        }

        private async Task<int> DeleteAsync(string path, string message, Output o, CancellationToken ct)
        {
            await _api.SendRawAsync(HttpMethod.Delete, path, null, authenticated: true, ApiClient.DefaultTimeout, ct);
            o.Result(JsonSerializer.Serialize(new OkResponse(true), CliJsonContext.Default.OkResponse), w => w.WriteLine(message));
            return ExitCodes.Success;
        }

        private static (Option<string?> Bearer, Option<string[]> Headers) AuthOptions() => (
            new Option<string?>("--bearer-env") { Description = "Env var with the target's bearer token (sent as 'Authorization: Bearer ...')." },
            new Option<string[]>("--header-env") { Description = "Secret header from an env var: Name=ENV_VAR. Repeatable." });

        private static Argument<string> IdArgument(string description) => new("id") { Description = description };

        private static Option<int> LimitOption() => new("--limit") { Description = "How many, 1-200.", DefaultValueFactory = _ => 50 };

        private static string Escape(string? value) => Uri.EscapeDataString(value ?? "");

        private static string? FirstLine(string? text) =>
            text?.Split('\n', 2)[0].Trim() is { } line && line.Length > 70 ? line[..67] + "..." : text?.Split('\n', 2)[0].Trim();

        private static void PrintMonitor(TextWriter w, MonitorResponse m)
        {
            w.WriteLine($"{m.Name} ({m.Id})");
            w.WriteLine($"  url:       {m.Url}");
            w.WriteLine($"  status:    {m.Status}{(m.SuspendedReason is { } r ? $" ({r})" : "")}");
            w.WriteLine($"  interval:  {m.IntervalSeconds}s (effective {m.EffectiveIntervalSeconds}s), timeout {m.TimeoutSeconds}s");
            if (m.Tool is { } tool)
                w.WriteLine($"  tool:      {tool} {(m.Arguments is { } a ? a.GetRawText() : "")}");
            foreach (var assertion in m.Assertions ?? [])
                w.WriteLine($"  assert:    {JsonSerializer.Serialize(assertion, ContractsJsonContext.Default.Assertion)}");
            if (m.Headers.Count > 0)
                w.WriteLine($"  headers:   {string.Join(", ", m.Headers)} (values hidden)");
            w.WriteLine($"  last:      {Output.Check(m.LastCheck)}  next {Output.Time(m.NextRunAt)}");
            if (m.OpenIncidentId is { } incident)
                w.WriteLine($"  incident:  {incident} (open)");
        }
    }
}
