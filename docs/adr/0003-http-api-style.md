# ADR-0003: HTTP API Style

Status: Accepted

## Decision

JobPilot uses ASP.NET Core controllers for all public HTTP boundaries.

Controllers must stay thin: authentication, binding, authorization, response
mapping, and delegation to application services. Business logic remains in
services and domain components.

Application services return domain values or `ServiceResult<T>` and must not
depend on ASP.NET Core `IResult` or `Results`.

## Consequences

New public API features use controllers and preserve route contracts with HTTP
boundary tests.

The architecture quality gate rejects HTTP result types inside application
services, untyped application results, persistence-coupled controllers, and
Minimal API endpoint groups.
