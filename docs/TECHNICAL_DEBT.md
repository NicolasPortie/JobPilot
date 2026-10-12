# Technical Debt Register

This register tracks structural work that should be completed before the system
is described as a public multi-tenant service.

| Priority | Area | Current state | Exit criterion |
|---|---|---|---|
| P0 | Authentication | Single bootstrap user and local-first login | External identity or hardened account lifecycle |
| P1 | Frontend contracts | Handwritten web types coexist with generated OpenAPI types | Use generated contracts for the supported web flow and verify drift in CI |
| P1 | Agent orchestration | `ApplicationAgent` remains a large coordinator | Execution, trace, and recovery coordinators extracted |
| P1 | Contracts | API and agent duplicate transport DTOs | Versioned shared contracts package |
| P1 | Resume presentation | Requirement matching is still projected from keyword lists in the web screen | Backend returns explicit per-requirement verdicts for direct display |
| P1 | Protocol services | Agent protocol service still coordinates several persistence operations | Split services further when command/query responsibilities grow |
| P1 | Database testing | Service tests use EF InMemory; CI verifies PostgreSQL startup only | Run business-flow, isolation and concurrency tests against PostgreSQL in CI |
| P1 | Session concurrency | Refresh rotation reads and revokes a token without an atomic database claim | Concurrent reuse allows only one successful renewal, proven against PostgreSQL |
| P1 | Container export | Production image does not bundle Chromium for PDF generation | PDF export smoke test passes using the production image |
| P1 | Resume text model | Structured sections and a full-text section coexist in generated versions | Define one authoritative editing representation and test edits without duplicate content |
| P1 | Observability | Agent primarily writes console logs | Structured logs, metrics, and retention policy |
| P2 | Static analysis | Full recommended analyzer set exposes a large historical warning baseline | Enable analyzers incrementally with a clean build |
| P2 | Historical encoding | Some archived documents contain mojibake | Archive normalized or explicitly preserved as source |

The architecture quality gate prevents the largest files and direct frontend API
coupling from growing while these items are reduced.

The supported product remains a private local installation. Agent-specific debt
applies to quarantined code and does not justify enabling browser automation.
