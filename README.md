# canary

The command-line client of [MCP Canary](https://mcpcanary.com): synthetic monitoring for MCP servers that your coding agent sets up by itself.

This repository is the CLI's source code, its releases, and its issue tracker. The CLI is a thin client of the public HTTP API ([OpenAPI](https://api.mcpcanary.com/openapi.json)). It reads target secrets only from environment variables, and the code is here so you can check what it does with them.

## Install

```sh
# macOS, Linux
curl -fsSL https://mcpcanary.com/install.sh | sh

# Windows PowerShell
irm https://mcpcanary.com/install.ps1 | iex
```

The installers download from the [releases](https://github.com/xakpc-dev-labs/canary/releases) of this repository and verify the SHA-256 checksum in `checksums.txt`. Each release tag points at the source commit it was built from.

| Platform | Asset |
|---|---|
| macOS, Apple Silicon | `canary_darwin_arm64.tar.gz` |
| macOS, Intel | `canary_darwin_x64.tar.gz` |
| Linux x64 (glibc 2.34+) | `canary_linux_x64.tar.gz` |
| Linux arm64 (glibc 2.34+) | `canary_linux_arm64.tar.gz` |
| Windows x64 | `canary_windows_x64.zip` |

One native binary, no runtime. Alpine (musl) is not supported yet.

## Use

```sh
canary inspect https://api.example.com/mcp

# See exactly what would be set up. Nothing is saved, and the tool is not called.
canary monitor set customer-search-prod --url https://api.example.com/mcp \
  --tool search_customer --args '{"email":"canary@example.com"}' \
  --assert '$.customers[0].email == "canary@example.com"' \
  --assert '$.customers[0].plan == "pro"' \
  --interval 5m --dry-run

# Apply it: the same line without --dry-run.
canary monitor test customer-search-prod --json
```

Or give your coding agent this prompt:

> Set up continuous external monitoring for the production MCP endpoint in this repository with MCP Canary (https://mcpcanary.com/llms.txt). Choose a safe deterministic canary, keep the configuration in the repository, show me the dry run before you enable it, and verify that it works.

- Docs: https://mcpcanary.com/docs/
- For agents: https://mcpcanary.com/llms.txt
- HTTP API: https://api.mcpcanary.com/openapi.json

## Build from source

Needs the .NET 10 SDK and the [NativeAOT prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites) (clang and zlib on Linux, Xcode command-line tools on macOS, the C++ build tools on Windows).

```sh
dotnet build Canary.slnx
bash build/package-cli.sh linux-x64 0.0.0   # or osx-arm64, osx-x64, linux-arm64, win-x64
./artifacts/publish/linux-x64/canary --version
```

`build/package-cli.sh` is the same script the release uses. `MCP_CANARY_API_URL` points the CLI at another API.

| Path | What |
|---|---|
| `src/Xakpc.McpCanary.Cli` | The `canary` command tree, output, exit codes, token file |
| `src/Xakpc.McpCanary.Contracts` | The API's request and response types, shared with the server |

## Contributing

Issues and pull requests are welcome. The CLI's tests run against the API server, which is not in this repository, so a pull request is checked here by a build and tested by the maintainer before merge. Keep the JSON output, exit codes, and asset names stable: agents and CI pipelines depend on them.

## Report a problem

Open an issue with the output of `canary --version`, your OS, and the command with `--json`. Remove tokens and secrets first. For billing or account questions, write to support@mcpcanary.com.

## License

[MIT](LICENSE).
