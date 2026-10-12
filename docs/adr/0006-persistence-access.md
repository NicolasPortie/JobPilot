# ADR-0006: Persistence Access

Status: Accepted

## Decision

Controllers never access `JobPilotDbContext`. Application services may use
focused repositories and `IUnitOfWork`; direct `JobPilotDbContext` access is
temporarily accepted for cohesive query/orchestration services where a
repository would only mirror EF Core.

New direct context usage must stay inside services or infrastructure and must
not leak tracked entities through public HTTP contracts.

## Consequences

The project can migrate incrementally without creating ceremonial repositories.
Persistence boundaries remain testable and controllers stay independent of EF.
