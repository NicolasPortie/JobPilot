# ADR-0002: Secure Configuration And Database Startup

Status: Accepted

## Decision

Production-like environments must provide explicit JWT and data-protection keys.
Database migration, legacy repair, and seed are independent startup policies and
are disabled in the base configuration.

O pareamento de extensão não autenticado foi removido junto com a extensão
(ver ADR-0007 superseded); não existe mais superfície de pareamento.

## Consequences

Deployment automation is responsible for migrations and secrets. Local
development remains convenient through `appsettings.Development.json`.
