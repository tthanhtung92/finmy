namespace Finmy.SharedKernel.Results;

public record Error(string Code, string Description, ErrorType Type)
{
    /// <summary>
    /// Error.None, for Success()
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    /// <summary>
    /// Error.NullValue, for a successful Result<T> whose T is null
    /// </summary>
    public static readonly Error NullValue = new("General.Null", "Null value was provided", ErrorType.Failure);
}