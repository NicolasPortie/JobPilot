# ADR-0001: Runtime Boundaries

Status: Accepted

## Decision

- The API owns persisted business state and policy.
- The agent owns deterministic orchestration, risk evaluation, and recovery.
- The web app owns presentation and user workflows, organized by feature.
- Cross-process contracts must be explicit and versioned.
- A extensão do navegador foi removida do produto (ver ADR-0007 superseded);
  não há mais transporte de browser no backend.

## Consequences

Platform-specific behavior stays behind platform modules. Browser actions require
agent approval. New frontend behavior should not be added directly to the root
application component when it can live in a feature module.
