# ADR-0007: Extension Protocol

> Status: **Superseded — extensão removida do produto.** Código (`apps/extension`),
> protocolo (`api/extension/*`), tabelas (`BrowserExtensionSessions`,
> `BrowserExtensionCommandJobs`) e opções de pareamento/CORS foram removidos
> (migration `RemoveExtensionProtocol`). Mantido aqui como registro histórico.

Status original: Accepted

## Decision

The browser extension is an execution and observation transport. Policy,
answer selection, risk classification, and planning remain in the agent/API.
Commands sent to the extension are atomic and auditable.

Compatibility is versioned through the extension manifest version reported in
pairing and heartbeat messages. The agent declares a minimum compatible
extension version and rejects older sessions before queueing commands.

## Consequences

Extension changes that alter command or observation semantics require a manifest
version bump, contract tests, and an update to the agent minimum version.
