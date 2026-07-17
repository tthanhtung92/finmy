namespace Finmy.SharedKernel.Results;

public record Error(string Code, string Description, ErrorType Type)
{
    /// <summary>
    /// Error.None cho trường hợp Success()
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    /// <summary>
    /// Error.NullValue cho trường hợp trả Result<T> thành công nhưng T là null
    /// </summary>
    public static readonly Error NullValue = new("General.Null", "Null value was provided", ErrorType.Failure);
}