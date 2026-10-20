# canary

The command-line client of [MCP Canary](https://mcpcanary.com): synthetic monitoring for MCP servers that your coding agent sets up by itself.

This repository holds the release binaries and the issue tracker. The source code is not public.

## Install

```sh
# macOS, Linux
curl -fsSL https://mcpcanary.com/install.sh | sh

# Windows PowerShell
irm https://mcpcanary.com/install.ps1 | iex
```

The installers download from the [releases](https://github.com/xakpc-dev-labs/canary/releases) of this repository and verify the SHA-256 checksum in `checksums.txt`.

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
canary monitor set customer-search-prod --url https://api.example.com/mcp \
  --tool search_customer --args '{"email":"canary@example.com"}' \
  --assert '$.customers[0].email == "canary@example.com"'
canary monitor test customer-search-prod --json
```

Or give your coding agent this prompt:

> Set up continuous external monitoring for the production MCP endpoint in this repository with MCP Canary (https://mcpcanary.com/llms.txt). Choose a safe deterministic canary, keep the configuration in the repository, and verify that it works.

- Docs: https://mcpcanary.com/docs/
- For agents: https://mcpcanary.com/llms.txt
- HTTP API: https://api.mcpcanary.com/openapi.json

## Report a problem

Open an issue with the output of `canary --version`, your OS, and the command with `--json`. Remove tokens and secrets first. For billing or account questions, write to support@mcpcanary.com.

## License

The binaries are free to download and use with the MCP Canary service. See [LICENSE](LICENSE).
