# ADR-0001: Modular Monolith instead of microservices

## Status

Accepted, 2026-07-12.

Module names in this record predate [ADR-0006](0006-pivot-to-shared-budgeting.md), which replaced the ticketing domain with shared budgeting. `Events` and `Ticketing` became `Budgeting` and `Ledger`. The structural decision itself is unchanged.

## Context

Finmy is built by one person with one to four hours a day. The goal is that each core backend concept (auth, caching, CDN, realtime, messaging, concurrency) gets a thin vertical slice that runs for real and can be explained.

The foundational question is how to split the system so that boundaries stay disciplined without drowning a single developer in operational overhead. Two constraints: `docker compose up` has to bring the system up in one command, and boundaries have to be machine-checkable rather than promised.

## Options considered

**Microservices**, one service and one database per module, communicating over the network. Strong on independent scaling and separate deploys, but it front-loads distribution: multiple repositories and pipelines, service discovery, distributed tracing, eventual consistency, heavy operations. Most of the effort would go into infrastructure rather than the concepts the project exists to work through.

**A plain layered monolith**, fast and simple, but with no internal boundaries. Nothing stops ticketing code from calling an Identity repository directly. The very thing this project wants to make explicit would be missing.

**Modular Monolith**, one process and one solution split into self-contained modules, each a four-layer vertical slice (Domain, Application, Infrastructure, Api). Real boundaries at the operational cost of a single process.

## Decision

Modular Monolith. Source splits into self-contained modules under `src/Modules/`, four projects each. `src/Bootstrap/Finmy.Api` is the only composition root, the single place where every module's services and endpoints are loaded, through the `AddModules()` / `UseModules()` pattern. Module `*.Api` projects are therefore class libraries rather than hosts: they declare endpoints, the host loads them.

The hard boundary: a module must not reference another module's `Domain` or `Infrastructure` directly. Cross-module communication goes only through `src/Shared/Finmy.Contracts` integration events published over the Wolverine bus. Project references enforce part of this, since a reference in the wrong direction breaks the build, and NetArchTest is meant to fail CI on the rest.

## Consequences

Boundaries become machine-checkable rather than a convention that erodes: a wrong-direction reference breaks the build immediately, and NetArchTest catches what the compiler cannot.

If a module genuinely needs to become a separate service later, the hard boundary is already in place and the extraction path is short, because the module never leaked its internals.

The accepted trade-off is no independent scaling per module and no separate deploys. Neither is a goal here.

The follow-on work is that every cross-module exchange has to be shaped into an integration event in `Finmy.Contracts`, even when a direct call would be more convenient. That cost is deliberate: it is what keeps the monolith modular rather than tangled.
