using System.Diagnostics.CodeAnalysis;

namespace Finmy.IntegrationTests;

/// <summary>
/// One container set for every HTTP test. Without this each test class would start its own
/// Postgres, Redis and MinIO, which is three containers per class and a slow suite.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Collection is xUnit's term for this concept, and the attribute below is [CollectionDefinition]. Renaming it would obscure what the type is.")]
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<FinmyApiFactory>
{
    public const string Name = "Finmy API";
}
