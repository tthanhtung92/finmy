using System.Reflection;

namespace Finmy.ArchitectureTests;

/// <summary>
/// Loads the production assemblies by name. Every project is a ProjectReference of this
/// test project, so all of them sit next to the test assembly in the output folder.
/// Loading by name rather than through a marker type means adding a project to the
/// solution is enough to bring it under these rules.
/// </summary>
internal static class SolutionAssemblies
{
    public const string RootNamespace = "Finmy";

    public static readonly string[] ModuleNames = ["Budgeting", "Identity", "Ledger"];

    public static readonly string[] LayerNames = ["Domain", "Application", "Infrastructure", "Api"];

    public static Assembly Module(string module, string layer) => Load($"Finmy.{module}.{layer}");

    public static Assembly SharedKernel => Load("Finmy.SharedKernel");

    public static Assembly Contracts => Load("Finmy.Contracts");

    public static Assembly Modularity => Load("Finmy.Modularity");

    /// <summary>Every Finmy assembly except the composition root and this test project.</summary>
    public static IEnumerable<Assembly> AllModuleAssemblies()
    {
        foreach (var module in ModuleNames)
        {
            foreach (var layer in LayerNames)
                yield return Module(module, layer);
        }

        yield return SharedKernel;
        yield return Contracts;
        yield return Modularity;
    }

    private static Assembly Load(string simpleName) => Assembly.Load(new AssemblyName(simpleName));
}
