# ADR-0002: Secure Configuration And Database Startup

Status: Accepted

## Decision

Production-like environments must provide explicit JWT and data-protection keys.
Database migration, legacy repair, and seed are independent startup policies and
are disabled in the base configuration.

## Consequences

Deployment automation is responsible for migrations and secrets. Local
development remains convenient through `appsettings.Development.json`.
