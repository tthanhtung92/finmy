# ADR-0003: Avoid commercially licensed libraries; use Mapster, NSubstitute and Shouldly

## Status

Accepted, 2026-07-12

## Context

From April 2025 onward a set of familiar .NET libraries moved to commercial licensing: MediatR, AutoMapper, MassTransit, Moq and FluentAssertions. Finmy is a public repository, so anyone who clones it has to be able to build and run it without hitting a licensing constraint or a fee.

MediatR and MassTransit were already handled in [ADR-0002](0002-wolverine.md), where Wolverine replaced both. This record settles the rest: mapping, mocking and assertions.

## Options considered

**Keep AutoMapper, Moq and FluentAssertions.** Familiar and well documented, but real use now requires a commercial license. For a public repository that means a licensing constraint and no free path.

**Mapster or hand-written mapping instead of AutoMapper.** Mapster is MIT licensed, fast, and supports source generators; hand-written mapping has no dependency at all and is explicit. Either way, AutoMapper's convention-based behaviour is lost.

**NSubstitute instead of Moq.** MIT licensed, with compact substitute syntax, enough for handler and domain unit tests.

**Shouldly instead of FluentAssertions.** MIT and BSD licensed, with readable assertion failures and `ShouldBe` in place of `Should().Be()`.

## Decision

MediatR, AutoMapper, MassTransit, Moq and FluentAssertions are not used in this project. The replacements are all MIT, Apache or BSD licensed: **Mapster** for mapping, written by hand where a map is simple enough not to need configuration; **NSubstitute** for mocking; **Shouldly** for assertions. Wolverine already replaced MediatR and MassTransit in ADR-0002.

## Consequences

Anyone who clones the repository can build and run it with no licensing obstacle, which is the point of publishing it.

The follow-on work is that AutoMapper's convention-based mapping is gone, so mappings are declared through Mapster configuration or written out. In exchange the mapping is explicit, with less hidden behaviour.

There is a short learning curve on the new syntax: `Substitute.For<T>()` rather than `new Mock<T>()`, `ShouldBe` rather than `Should().Be()`. A one-time cost.
