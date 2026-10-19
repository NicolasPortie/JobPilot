# ADR-0001: Runtime Boundaries

Status: Accepted

## Decision

- The API owns persisted business state and policy.
- The web app owns presentation and user workflows, organized by feature.
- HTTP contracts between the web app and the API must be explicit.
- The product does not control a browser or submit applications; applying is always manual.

## Consequences

New frontend behavior should not be added directly to the root
application component when it can live in a feature module.
