# ADR-0005: Explicit HTTP Contracts

Status: Accepted

## Decision

Public API boundaries use explicit request and response contracts. Controllers
must not expose EF/domain entities directly. Mapping between persistence/domain
models and public contracts happens before the controller response.

Contracts are grouped under `Application/Contracts` and may be moved closer to
a feature when that feature becomes independently owned.

## Consequences

Public contracts can evolve without leaking persistence details. Boundary tests
and architecture checks reject direct domain entities in controller signatures.
