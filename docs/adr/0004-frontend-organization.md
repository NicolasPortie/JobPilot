# ADR-0004: Frontend Organization

Status: Accepted

## Decision

The web application is organized by product feature. `App.tsx` only composes
the authenticated shell and active feature screen. Feature screens live under
`src/features`, shared visual components under `src/components`, application
orchestration under `src/app`, and all HTTP access under `src/api`.

Feature screens must remain below 500 lines and reusable UI components must not
depend directly on backend DTOs.

## Consequences

New workflows receive a feature folder instead of being added to `App.tsx`.
The architecture gate rejects direct HTTP calls outside API clients and guards
the main composition and orchestration line budgets.
