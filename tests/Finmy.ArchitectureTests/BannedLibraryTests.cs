using NetArchTest.Rules;

using Shouldly;

namespace Finmy.ArchitectureTests;

/// <summary>
/// ADR-0003: this project deliberately avoids the libraries that moved to commercial
/// licensing in 2025. The replacements are Wolverine for MediatR and MassTransit, Mapster
/// or hand-written mapping for AutoMapper, NSubstitute for Moq, Shouldly for FluentAssertions.
/// </summary>
public class BannedLibraryTests
{
    private static readonly string[] BannedNamespaces =
    [
        "MediatR",
        "AutoMapper",
        "MassTransit",
        "Moq",
        "FluentAssertions"
    ];

    public static TheoryData<string> ProductionAssemblies()
    {
        var data = new TheoryData<string>();

        foreach (var assembly in SolutionAssemblies.AllModuleAssemblies())
            data.Add(assembly.GetName().Name!);

        return data;
    }

    [Theory]
    [MemberData(nameof(ProductionAssemblies))]
    public void Assembly_uses_no_commercially_licensed_library(string assemblyName)
    {
        var assembly = SolutionAssemblies.AllModuleAssemblies()
            .Single(x => x.GetName().Name == assemblyName);

        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(BannedNamespaces)
            .GetResult();

        result.FailingTypeNames.ShouldBeNull(
            $"{assemblyName} uses a library banned by ADR-0003.");
    }

    /// <summary>
    /// The namespace rules above only see assemblies this project references, which leaves the
    /// other test projects uncovered. Central package management gives a single file to check
    /// instead, and it catches a banned package the moment it is pinned.
    /// </summary>
    [Fact]
    public void Central_package_management_pins_no_banned_package()
    {
        var packagesProps = Path.Combine(RepositoryRoot.Find(), "Directory.Packages.props");
        File.Exists(packagesProps).ShouldBeTrue($"Expected to find {packagesProps}.");

        var contents = File.ReadAllText(packagesProps);

        foreach (var banned in BannedNamespaces)
        {
            contents.ShouldNotContain(
                $"\"{banned}",
                Case.Sensitive,
                $"Directory.Packages.props pins {banned}, which ADR-0003 rules out.");
        }
    }
}
