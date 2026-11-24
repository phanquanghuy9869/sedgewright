# Sedgewright

**Create any AI agent or workflow you can describe, without coding the host application.**

Sedgewright is a local-first desktop workbench for turning ideas into reusable agents and multi-agent workflows. Start with a focused assistant, build a team of specialists, or design a repeatable process for research, writing, coding, analysis, operations, and everything in between.

You define the role. You choose the model and capabilities. You decide how agents work together. Sedgewright gives you the workspace to build and run it.

## Why Sedgewright

Most agent projects become application-development projects. You need a chat surface, configuration, tool wiring, persistence, approvals, workflow execution, and a way to keep using the result. Sedgewright provides that surrounding workspace so you can focus on the work your agent should do.

Sedgewright is deliberately open-ended:

- Create reusable agents for any role, task, project, or working style.
- Combine agents into explicit workflows instead of relying on one giant prompt.
- Attach only the tools, MCP servers, skills, and optional shell access each agent needs.
- Bring your own provider and model access.
- Run everything through a streamed chat experience with tool approval and cancellation support.
- Keep definitions, settings, and sessions available locally for reuse.

The ceiling is the capabilities you choose to give an agent and the workflow you design around it.

## What you can build

Sedgewright does not prescribe one assistant or one "correct" workflow. Examples include:

- A research agent with selected MCP tools and workspace instructions.
- A writing workflow with researcher, writer, reviewer, and editor agents.
- A coding assistant with project skills and carefully controlled shell access.
- A document triage workflow that sends work through specialist agents.
- A release assistant that reviews changes, drafts notes, and prepares a checklist.
- A personal collection of reusable agents for different projects and responsibilities.

These are starting points, not fixed templates. Build the agent or workflow that matches your work.

## Core capabilities

### Create reusable agents

Define an agent's identity, instructions, provider, model, capabilities, and runtime options in the desktop UI. Save the result and return to the same setup instead of rebuilding it from a blank chat.

### Compose workflows

Coordinate specialized agents with the workflow pattern that fits the job:

- **Sequential**: agents run in a fixed order, each building on the previous step.
- **Concurrent**: agents work in parallel on the same input or on separate tasks.
- **Group chat**: agents discuss a problem in a shared conversation.
- **Handoff**: an agent routes the work to the specialist best suited for it.
- **Magentic**: a coordinating agent plans the work and adapts as it progresses.

Workflows are saved as definitions, so you can run them again from Chat.

### Connect tools and context

Give each agent a deliberate capability set:

- MCP servers over stdio (a local command) or HTTP.
- Workspace skills discovered from folders containing `SKILL.md`, searched recursively up to 8 levels deep.
- Hosted tools such as web search, file search, and code interpreter.
- Optional local or Docker shell execution with configurable controls.

### Bring your own model access

Configure the provider, base URL, API key, and model that fit your work. Supported provider kinds are GitHub Copilot, OpenAI, Azure OpenAI, Anthropic, Gemini, Grok, OpenRouter, NVIDIA NIM, and any OpenAI-compatible endpoint. Sedgewright is a workspace for using your model access, not a replacement for the provider's terms, limits, or pricing.

### Stay in the loop

Chat shows streamed progress and responses while work is running. When a tool call needs approval, you can approve or reject it. Long-running work can be cancelled from Chat instead of leaving you waiting for an opaque result.

### Keep your setup local

Settings, agent definitions, workflow definitions, MCP definitions, and session-related state are persisted in a local SQLite database, `Sentinel.UI\sentinel.db`, under your user application-data folder (`%AppData%` on Windows). Workspace skills are discovered from the working folder you select. Sedgewright is local-first; it is not currently a hosted team workspace or a managed deployment platform.

## How it works

The core loop is simple:

```text
choose provider and workspace
   -> create an agent
   -> attach tools, MCP servers, and skills
   -> compose a workflow when needed
   -> run it in Chat
   -> save and reuse the result
```

A typical first run looks like this:

1. Open Sedgewright. The first-run wizard asks for at least one provider's credentials and a default working directory. You can change both later in **Settings**.
2. Choose a working directory for the project or task.
3. Create an agent and describe what it should do.
4. Attach the capabilities and workspace skills it needs.
5. Create a workflow if the task benefits from multiple agents.
6. Open **Chat**, select the agent or workflow, and run a useful task.
7. Save the setup and reuse it for the next task.

## Product areas

- **Chat**: Run agents and workflows, follow streamed output, approve or reject tool calls, and cancel work.
- **Agents**: Create and manage reusable agent definitions and their capabilities.
- **Orchestrations**: Build and manage multi-agent workflows.
- **Tools**: Define tools and attach them to agents.
- **MCP**: Define external MCP servers and attach them to agents.
- **Skills**: Discover workspace `SKILL.md` files for use in agent definitions.
- **Settings**: Configure providers, default models, theme, working directory, and application behavior.

## Trust and boundaries

Sedgewright gives you control over provider, model, workspace, and attached capabilities. That control is not a guarantee that every agent action is safe.

- Provider requests follow the provider and model you configure.
- API keys are saved in the local SQLite database. Protect your user account and that file, and rotate any key that may have been exposed.
- MCP servers, skills, and shell access may be powerful or untrusted.
- Approval prompts and shell controls help you review actions, but they are not a complete security boundary.
- Use synthetic or non-sensitive data when evaluating an unfamiliar agent, skill, MCP server, or workflow.

Read the project documentation and the relevant provider terms before granting an agent access to sensitive data or local commands.

## Project status

Sedgewright is an actively developed desktop application. The core composition loop is in place, while release packaging, provider compatibility, workflow maturity, and some management surfaces continue to evolve. Installed builds check GitHub releases for updates through Velopack. Check the repository issues, releases, and documentation for current details before relying on a specific capability.

## Getting started

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build from source. The packaging scripts use an existing .NET 10+ SDK or install .NET 11 locally if needed.
- A desktop environment. The UI uses Avalonia, and release packages are produced for Windows (`win-x64`), macOS (`osx-x64`), and Linux (`linux-x64`).
- Credentials for at least one provider, or GitHub Copilot access.
- Docker, only if an agent uses the Docker shell executor.

### Build installers

The Velopack build scripts publish a self-contained app for the current OS and create install/update packages under `artifacts/releases/<runtime-id>`. Run each script on the OS you want to package:

```powershell
# Windows (defaults to version 0.0.1 and the host architecture)
powershell -ExecutionPolicy Bypass -File builds/build.ps1
powershell -ExecutionPolicy Bypass -File builds/build.ps1 -Version 1.2.0 -RuntimeIdentifier win-x64
```

```bash
# macOS
bash builds/build-macos.sh
bash builds/build-macos.sh --version 1.2.0 --rid osx-arm64

# Linux
bash builds/build-linux
bash builds/build-linux --version 1.2.0 --rid linux-x64
```

If .NET 10 or newer is not available, a script installs the .NET 11 SDK into the current user's home directory using Microsoft's [`dotnet-install` script](https://learn.microsoft.com/dotnet/core/tools/dotnet-install-script). The scripts install or update the global `vpk` tool to version 1.2.161 to match the Velopack package used by the app. No administrator access is required for these per-user installs. The selected runtime's publish and release folders are cleared before each build.

Velopack packaging is host-specific; use the Windows, macOS, and Linux scripts on their corresponding operating systems. Windows produces a setup executable, macOS an app bundle and ZIP, and Linux an AppImage rather than a setup installer. See the [Velopack packaging overview](https://docs.velopack.io/packaging/overview) for release-file details.

### Automated GitHub releases

Each push to `main` runs the GitHub Actions release workflow. It builds `win-x64`, `osx-x64`, and `linux-x64` on native Windows, macOS, and Linux hosted runners, validates the Velopack outputs, and publishes a stable GitHub Release. The macOS job uses an Intel macOS runner.

### Run from source

```powershell
dotnet build src/Sedgewright.slnx
dotnet run --project src/Sedgewright/Sedgewright.csproj
```

You can also open `src/Sedgewright.slnx` in your editor or IDE and run the `Sedgewright` project.

To start Sedgewright through Aspire and inspect its resource in the dashboard, run `dotnet run --project src/Sedgewright.AppHost` from the repository root. The Aspire Dashboard is the web UI for resource logs, traces, and metrics.

## For contributors

This repository contains the source code for the Sedgewright app. The desktop application lives in the `Sedgewright` project, with `Sedgewright.AppHost` as an optional .NET Aspire development orchestrator:

- `src/Sedgewright/Views` contains Avalonia windows and controls.
- `src/Sedgewright/ViewModels` contains binding state, commands, and UI coordination.
- `src/Sedgewright/Apps` contains application workflows and use cases.
- `src/Sedgewright/Models` contains shared data models and state structures.
- `src/Sedgewright/Infras` contains persistence, runtime integration, storage, and concrete adapters.

The Avalonia and Copilot SDK reference material is available in `doc/`, alongside notes on MAF, Velopack, project architecture, and UI components.

### Tests

The tests use NUnit and cover infrastructure, ViewModels, use cases, and models:

```powershell
dotnet test src/Sedgewright.Tests/Sedgewright.Tests.csproj
```

### Telemetry

Sedgewright emits structured application logs, `sedgewright.chat.send` spans, the `sedgewright.chat.turns` and `sedgewright.chat.turn.duration` metrics, outgoing HTTP telemetry, and .NET runtime metrics. Under Aspire, these appear in the Aspire Dashboard.

## GitHub Copilot SDK and BYOK notice

Sedgewright uses the [GitHub Copilot SDK](https://github.com/github/copilot-sdk). This app supports BYOK (Bring Your Own Key) for model access. You are responsible for complying with the terms of the model provider whose credentials you use.

## License

Sedgewright is licensed under the **GNU Affero General Public License v3.0 only** (`AGPL-3.0-only`). See [LICENSE](LICENSE) for the complete terms.

Third-party dependencies remain subject to their respective licenses. A `THIRD_PARTY_NOTICES.md` file is not yet included in this repository.
