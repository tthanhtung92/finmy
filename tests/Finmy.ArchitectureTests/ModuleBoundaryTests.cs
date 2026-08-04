using NetArchTest.Rules;

using Shouldly;

namespace Finmy.ArchitectureTests;

/// <summary>
/// The boundary rule from ROADMAP.md and ADR-0001: a module must never reach into another
/// module. Cross-module traffic goes through Finmy.Contracts as integration events on the
/// Wolverine bus. Finmy.Api is the composition root and is the one place allowed to see
/// every module, so it is not covered here.
/// </summary>
public class ModuleBoundaryTests
{
    public static TheoryData<string, string> ModuleLayers()
    {
        var data = new TheoryData<string, string>();

        foreach (var module in SolutionAssemblies.ModuleNames)
        {
            foreach (var layer in SolutionAssemblies.LayerNames)
                data.Add(module, layer);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ModuleLayers))]
    public void Module_does_not_depend_on_another_module(string module, string layer)
    {
        var foreignNamespaces = SolutionAssemblies.ModuleNames
            .Where(other => other != module)
            .Select(other => $"Finmy.{other}")
            .ToArray();

        var result = Types.InAssembly(SolutionAssemblies.Module(module, layer))
            .ShouldNot()
            .HaveDependencyOnAny(foreignNamespaces)
            .GetResult();

        result.FailingTypeNames.ShouldBeNull(
            $"Finmy.{module}.{layer} reaches into another module. Cross-module communication " +
            $"goes through Finmy.Contracts only.");
    }

    [Fact]
    public void Contracts_depends_on_no_other_Finmy_assembly()
    {
        var result = Types.InAssembly(SolutionAssemblies.Contracts)
            .ShouldNot()
            .HaveDependencyOnAny("Finmy.Budgeting", "Finmy.Identity", "Finmy.Ledger", "Finmy.SharedKernel", "Finmy.Modularity")
            .GetResult();

        result.FailingTypeNames.ShouldBeNull(
            "Finmy.Contracts is the shared vocabulary every module depends on, so it must stay a leaf.");
    }

    [Fact]
    public void SharedKernel_depends_on_no_module()
    {
        var result = Types.InAssembly(SolutionAssemblies.SharedKernel)
            .ShouldNot()
            .HaveDependencyOnAny("Finmy.Budgeting", "Finmy.Identity", "Finmy.Ledger")
            .GetResult();

        result.FailingTypeNames.ShouldBeNull("Finmy.SharedKernel must not know about any module.");
    }
}
