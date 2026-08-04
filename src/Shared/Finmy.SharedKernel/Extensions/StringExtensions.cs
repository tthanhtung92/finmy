namespace Finmy.SharedKernel.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Returns null when the string is empty or whitespace only.
    /// Otherwise returns the trimmed string.
    /// </summary>
    public static string? TrimOrNull(this string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}