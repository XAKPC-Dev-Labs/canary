<a id="readme-top"></a>

<div align="center">

[![Release][release-shield]][release-url]
[![CI][ci-shield]][ci-url]
[![Downloads][downloads-shield]][release-url]
[![Issues][issues-shield]][issues-url]
[![MIT License][license-shield]][license-url]

</div>

<br />
<div align="center">
  <a href="https://mcpcanary.com">
    <img src="images/logo.svg" alt="MCP Canary logo" width="80" height="80">
  </a>

  <h3 align="center">canary</h3>

  <p align="center">
    The command-line client of <a href="https://mcpcanary.com">MCP Canary</a>: synthetic monitoring for MCP servers that your coding agent sets up by itself.
    <br />
    <a href="https://mcpcanary.com/docs/"><strong>Explore the docs »</strong></a>
    <br />
    <br />
    <a href="https://mcpcanary.com/check/">Test a server in the browser</a>
    &middot;
    <a href="https://github.com/xakpc-dev-labs/canary/issues/new?labels=bug">Report Bug</a>
    &middot;
    <a href="https://github.com/xakpc-dev-labs/canary/issues/new?labels=enhancement">Request Feature</a>
  </p>
</div>

<details>
  <summary>Table of Contents</summary>
  <ol>
    <li>
      <a href="#about-the-project">About The Project</a>
      <ul>
        <li><a href="#built-with">Built With</a></li>
      </ul>
    </li>
    <li>
      <a href="#getting-started">Getting Started</a>
      <ul>
        <li><a href="#installation">Installation</a></li>
        <li><a href="#build-from-source">Build from source</a></li>
      </ul>
    </li>
    <li><a href="#usage">Usage</a></li>
    <li><a href="#contributing">Contributing</a></li>
    <li><a href="#report-a-problem">Report a problem</a></li>
    <li><a href="#license">License</a></li>
    <li><a href="#contact">Contact</a></li>
  </ol>
</details>

## About The Project

[![canary monitor test in a CI deploy step][product-screenshot]](https://mcpcanary.com)

An MCP server can answer 200 while its tools are broken. MCP Canary calls a real tool on a schedule, checks the answer, and emails you when it breaks. `canary` is how you, your coding agent, or your CI pipeline set that up.

This repository is the CLI's source code, its releases, and its issue tracker. The CLI is a thin client of the public HTTP API ([OpenAPI](https://api.mcpcanary.com/openapi.json)). It reads target secrets only from environment variables, and the code is here so you can check what it does with them.

* **One native binary, no runtime.** About 8 MB, starts in milliseconds, works in any CI.
* **`--json` on every command.** Stdout carries the API's JSON, errors included, and nothing else.
* **Stable exit codes.** 0 passed, 3 target unhealthy, 31 target unreachable, 40 payment needed. A CI step needs no parsing.
* **Never calls a risky tool.** A tool without `readOnlyHint` is not called unless you pass `--allow-side-effects`.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

### Built With

* [![.NET][dotnet-shield]][dotnet-url]
* NativeAOT
* System.CommandLine

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Getting Started

### Installation

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

### Build from source

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

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Usage

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

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Contributing

Issues and pull requests are welcome. The CLI's tests run against the API server, which is not in this repository, so a pull request is checked here by a build and tested by the maintainer before merge. Keep the JSON output, exit codes, and asset names stable: agents and CI pipelines depend on them.

1. Fork the project
2. Create your branch (`git checkout -b fix/short-name`)
3. Commit your changes
4. Push the branch and open a pull request

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Report a problem

Open an issue with the output of `canary --version`, your OS, and the command with `--json`. Remove tokens and secrets first. For billing or account questions, write to support@mcpcanary.com.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## License

Distributed under the MIT License. See [`LICENSE`](LICENSE).

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Contact

Pavel Osadchuk - [@xakpc](https://x.com/xakpc) - support@mcpcanary.com

Website: [mcpcanary.com](https://mcpcanary.com)

<p align="right">(<a href="#readme-top">back to top</a>)</p>

[release-shield]: https://img.shields.io/github/v/release/xakpc-dev-labs/canary?style=for-the-badge
[release-url]: https://github.com/xakpc-dev-labs/canary/releases
[ci-shield]: https://img.shields.io/github/actions/workflow/status/xakpc-dev-labs/canary/ci.yml?branch=main&style=for-the-badge&label=ci
[ci-url]: https://github.com/xakpc-dev-labs/canary/actions/workflows/ci.yml
[downloads-shield]: https://img.shields.io/github/downloads/xakpc-dev-labs/canary/total?style=for-the-badge
[issues-shield]: https://img.shields.io/github/issues/xakpc-dev-labs/canary?style=for-the-badge
[issues-url]: https://github.com/xakpc-dev-labs/canary/issues
[license-shield]: https://img.shields.io/github/license/xakpc-dev-labs/canary?style=for-the-badge
[license-url]: https://github.com/xakpc-dev-labs/canary/blob/main/LICENSE
[product-screenshot]: images/screenshot.png
[dotnet-shield]: https://img.shields.io/badge/.NET_10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white
[dotnet-url]: https://dotnet.microsoft.com/
