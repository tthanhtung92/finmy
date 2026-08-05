using Microsoft.Extensions.Configuration;

namespace Finmy.Modularity.Extensions;

public static class ConfigurationExtensions
{
    /// <summary>
    /// The same guard every module's ConfigureServices wrote out by hand: fail fast with a
    /// named exception rather than let a blank connection string reach Npgsql, where the
    /// failure would be a generic connection error with no mention of which setting is missing.
    /// </summary>
    public static string GetRequiredConnectionString(this IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name) is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Connection string '{name}' is not configured.");
}
