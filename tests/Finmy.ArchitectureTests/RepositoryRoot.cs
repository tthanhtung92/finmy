namespace Finmy.ArchitectureTests;

/// <summary>
/// Finds the repository root by walking up from the test binaries until Finmy.slnx appears.
/// Tests that read source files need this; a hard-coded relative path breaks the moment the
/// output layout changes.
/// </summary>
internal static class RepositoryRoot
{
    private const string Marker = "Finmy.slnx";

    public static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, Marker)))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find {Marker} above {AppContext.BaseDirectory}. " +
            $"Tests that read repository source files cannot run from here.");
    }
}
