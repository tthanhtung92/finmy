# Architecture Decision Records

This directory is Finmy's record of why. Each ADR captures one architectural decision at the moment it was made, with its context and trade-offs. ADRs are immutable: when a decision changes, write a new ADR and mark the old one "Superseded" rather than editing it in place.

| No. | Title | Status | Date |
| ---- | ------- | -------- | ------ |
| [0001](0001-modular-monolith.md) | Modular Monolith instead of microservices | Accepted | 2026-07-12 |
| [0002](0002-wolverine.md) | Wolverine as mediator, message bus and transactional outbox | Accepted | 2026-07-12 |
| [0003](0003-avoid-commercial-libraries.md) | Avoid commercially licensed libraries; use Mapster, NSubstitute and Shouldly | Accepted | 2026-07-12 |
| [0004](0004-identity-option-a.md) | Identity module boundary via Option A (dependency inversion through IIdentityService) | Accepted | 2026-07-12 |
| [0005](0005-jwt-short-name-claim.md) | Issue JWTs with short-name claims, with IdentityClaimTypes as the source of truth | Accepted | 2026-07-12 |
| [0006](0006-pivot-to-shared-budgeting.md) | Move the domain to shared personal finance using envelope budgeting | Accepted | 2026-07-16 |
| [0007](0007-naming-conventions.md) | Settle naming conventions for folders, files and namespaces | Accepted | 2026-07-20 |
| [0008](0008-cdn-in-front-of-object-storage.md) | Serve receipt images with presigned URLs, with a CDN in front of the object-storage origin | Accepted | 2026-07-23 |
| [0009](0009-self-managed-version-concurrency-token.md) | Use an int `Version` column incremented by the domain as the concurrency token, not `xmin` | Accepted | 2026-07-29 |
| [0010](0010-single-writer-envelope-balance.md) | Budgeting owns the envelope balance; overspend protection is eventually consistent with a compensating reversal | Accepted | 2026-07-25 |
| [0011](0011-async-request-reply-202.md) | Recording a transaction is an async `202 Accepted` with a status resource | Accepted | 2026-07-26 |
