using NetArchTest.Rules;

using Shouldly;

namespace Finmy.ArchitectureTests;

/// <summary>
/// Layering inside a module. The Application rule is the load-bearing one: ADR-0009 and
/// ADR-0010 both place infrastructure policy on the host precisely because Application
/// must not see EF Core. That is what the IEnvelopeRepository port exists to protect, and
/// it is what makes translating DbUpdateConcurrencyException an Infrastructure concern.
/// </summary>
public class LayerDependencyTests
{
    private static readonly string[] PersistenceAndWeb =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql"
    ];

    public static TheoryData<string> Modules()
    {
        var data = new TheoryData<string>();

        foreach (var module in SolutionAssemblies.ModuleNames)
            data.Add(module);

        return data;
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Domain_stays_free_of_infrastructure(string module)
    {
        var result = Types.InAssembly(SolutionAssemblies.Module(module, "Domain"))
            .ShouldNot()
            .HaveDependencyOnAny([.. PersistenceAndWeb, "Wolverine"])
            .GetResult();

        result.FailingTypeNames.ShouldBeNull(
            $"Finmy.{module}.Domain must hold business rules only. Persistence, transport and " +
            $"web concerns belong further out.");
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Application_stays_free_of_EF_Core_and_ASP_NET(string module)
    {
        var result = Types.InAssembly(SolutionAssemblies.Module(module, "Application"))
            .ShouldNot()
            .HaveDependencyOnAny(PersistenceAndWeb)
            .GetResult();

        result.FailingTypeNames.ShouldBeNull(
            $"Finmy.{module}.Application reaches persistence or the web stack directly. Go through " +
            $"a port in Abstractions instead; see ADR-0009 and ADR-0010 on retry policy placement.");
    }

    [Fact]
    public void SharedKernel_stays_free_of_the_web_stack()
    {
        var result = Types.InAssembly(SolutionAssemblies.SharedKernel)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.FailingTypeNames.ShouldBeNull(
            "Result<T> and the domain event base types are transport-agnostic. Mapping them onto " +
            "HTTP is Finmy.Modularity's job.");
    }
}
