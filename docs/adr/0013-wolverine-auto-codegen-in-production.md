# ADR-0013: Run production Wolverine handlers with `TypeLoadMode.Auto`, not `Static`

## Status

Accepted, 2026-08-04.

## Context

[ROADMAP.md](../ROADMAP.md) recorded the intended production codegen mode before Phase 2 built and ran an actual image: "Production images use Static codegen to avoid recompiling on every cold start and to drop Roslyn's memory overhead." Nothing had exercised that claim end to end. `Program.cs` set `Production.GeneratedCodeMode = TypeLoadMode.Static`, while `Finmy.Api.csproj` referenced `WolverineFx.RuntimeCompilation` (the package that gives Wolverine a Roslyn compiler to generate handler wrappers with) only under `Condition="'$(Configuration)' == 'Debug'"`. A Release image therefore ships with neither pre-generated handler code nor a way to generate it at runtime.

Building the Dockerfile for Phase 2 and running that unmodified image against real Postgres, Redis and MinIO containers showed the gap does not fail at startup. `GET /health` returned `200`, and the host logged only a warning (quoted verbatim): "Static TypeLoadMode is active but no pre-generated HandlerRegistry was found — falling back to a runtime assembly scan." The actual failure surfaced only on the first real handler invocation. `POST /transactions` returned `202 Accepted` as designed (recording a transaction is an async request-reply per [ADR-0011](0011-async-request-reply-202.md)), but the message Wolverine dispatched through the outbox to `RecordTransactionHandler` never processed. The host logged:

`JasperFx.CodeGeneration.ExpectedTypeMissingException: Could not load expected pre-built types for code file RecordTransactionCommandHandler536993493 (RecordTransactionCommand handled by RecordTransactionHandler.HandleAsync()) from assembly Finmy.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null. You may want to verify that this is the correct assembly for pre-generated types.`

Because the failure lands in background outbox processing rather than in the HTTP response, it would have been easy to ship without an end-to-end check that actually drives a Wolverine handler past the request boundary.

## Options considered

**Keep `Static`; run `dotnet run -- codegen write` in the Docker build stage and bake the generated `Internal/Generated` sources into the image.** Leaves the runtime image without a Roslyn dependency, which is what `Static` mode is for. Rejected: it needs a two-pass build, dummy `ConnectionStrings__*` values injected as build-stage environment variables purely to satisfy `AddModules()`'s fail-fast (every module throws from `ConfigureServices` when its connection string is blank), and the build stage's `csproj` condition still has to widen to give that stage a Roslyn compiler, so Roslyn stays in the pipeline regardless, just at build time instead of runtime.

**Switch `Production.GeneratedCodeMode` to `TypeLoadMode.Auto` and make `WolverineFx.RuntimeCompilation` an unconditional `PackageReference`.** JasperFx 2.34.0 ships an `AutoTypeLoader` whose own diagnostic message describes the fallback: "AutoTypeLoader falls back to DynamicType...". It loads pre-built types when present and generates them at runtime otherwise. A two-line diff that boots correctly today and needs no further change if a later phase adds a `codegen write` pass for cold-start reasons: `Auto` would then just start finding the pre-built types instead of generating them. The accepted cost is roughly 25 MB of extra image size and one to three seconds of slower cold start on the first handler invocation, plus Roslyn now shipping inside the production image.

## Decision

Use `TypeLoadMode.Auto` in production. `Program.cs` sets `x.Production.GeneratedCodeMode = TypeLoadMode.Auto` with a comment pointing at this ADR. `Finmy.Api.csproj` references `WolverineFx.RuntimeCompilation` unconditionally, with a comment explaining why the reference cannot go back to Debug-only. The matching workaround in `tests/Finmy.IntegrationTests.csproj`, a duplicate `WolverineFx.RuntimeCompilation` reference that existed only to compensate for the host's Debug-only condition, is removed, since the host now supplies it unconditionally.

The Dockerfile's non-root `USER app` needs write access to `/app` for `Auto` mode to cache what it generates: the first build attempt threw `System.UnauthorizedAccessException: Access to the path '/app/Internal' is denied`, because `COPY --chown=app:app` only sets ownership on the files it copies, not on the `/app` directory `WORKDIR` had already created as `root`. The final image stage runs `RUN chown app:app /app` before the `COPY`.

## Consequences

Verified by rebuilding the image with the fix and re-running the same scenario against real backing services: created an envelope allocated 20, posted a 30 expense transaction against it, and confirmed in the container logs that Wolverine reported "The Wolverine code generation mode is Auto with pre-generated types being loaded from Finmy.Api", then "Generated code to /app/Internal/Generated/WolverineHandlers/...". The overspend alert fired ("Overspent alert pushed for envelope ... attempted 30, remaining 20.00"), the reversal applied (`UPDATE ledger."Transactions" SET "ReversedAtUtc" = ..., "State" = ...`), and no permission exception appeared in the log.

This reverses the claim in `ROADMAP.md`, which is corrected in the same change as this ADR. `Auto` mode writing generated code to disk on first use means the container's writable layer grows slightly at runtime and the first request touching a given handler after a cold start pays a small compile cost; every request after that hits the cached, pre-built type.

Revisit this decision in Phase 5 if cold-start latency or image size become measured problems, for instance if the deployment target moves toward scale-to-zero. At that point, baking `codegen write` output into the image becomes worth paying for the two-pass build this ADR rejects today, and `Auto` mode's fallback behavior means that change would not need a second reversal: pre-built types would simply start being found instead of generated.
