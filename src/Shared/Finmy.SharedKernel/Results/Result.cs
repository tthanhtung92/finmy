namespace Finmy.SharedKernel.Results;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Success can not have Error.");

        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Failure must have Error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Tạo Result thành công
    /// </summary>
    /// <returns>Result thành công</returns>
    public static Result Success() => new(true, Error.None);

    /// <summary>
    /// Tạo Result thất bại
    /// </summary>
    /// <param name="error">Lỗi mô tả vì sao thất bại.</param>
    /// <returns>Result thất bại</returns>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>
    /// Tạo Result thành công bọc value. <br/>
    /// VD: <br/>
    /// Result&lt;User&gt; r = Result.Success(user);
    /// </summary>
    /// <typeparam name="T">Kiểu giá trị mà Result bọc.</typeparam>
    /// <param name="value">Giá trị cần bọc vào Result.</param>
    /// <returns>Result thành công mang value.</returns>
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);

    /// <summary>
    /// Tạo Result thất bại mang error. <br/>
    /// VD: <br/>
    /// Result&lt;User&gt; r = Result.Failure&lt;User&gt;(someError);
    /// </summary>
    /// <typeparam name="T">Kiểu giá trị mà Result bọc.</typeparam>
    /// <param name="error">Lỗi mô tả vì sao thất bại.</param>
    /// <returns>Result thất bại mang error.</returns>
    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}
