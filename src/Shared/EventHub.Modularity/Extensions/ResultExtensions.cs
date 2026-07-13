using EventHub.SharedKernel.Results;

using Microsoft.AspNetCore.Http;

namespace EventHub.Modularity.Extensions;

public static class ResultExtensions
{
    /// <summary>
    /// Chuyển đổi đối tượng Result thành HTTP IResult (ASP.NET Core Minimal APIs).
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu của giá trị thành công.</typeparam>
    /// <param name="result">Đối tượng kết quả cần kiểm tra.</param>
    /// <param name="onSuccess">Hàm callback định nghĩa kịch bản xử lý (HTTP Response) khi thành công.</param>
    /// <returns>Trả về kết quả từ hàm onSuccess nếu thành công; ngược lại trả về lỗi chuẩn ProblemDetails.</returns>
    public static IResult Match<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        // Cơ chế rẽ nhánh (Lazy Evaluation): 
        // - Nếu IsSuccess = true: Chỉ chạy nhánh trái, truyền dữ liệu vào hàm onSuccess (ví dụ: Results.Ok(user)).
        // - Nếu IsSuccess = false: Bỏ qua hoàn toàn hàm onSuccess, nhảy sang nhánh phải để format và trả về lỗi.
        return result.IsSuccess ? onSuccess(result.Value) : result.ToProblemDetails();
    }

    public static IResult ToProblemDetails(this Result result)
    {
        if (result.IsSuccess)
            throw new InvalidOperationException("Succeeded Result can not create ProblemDetails.");

        var error = result.Error;
        var status = error.Type.ToStatusCode();
        var extensions = new Dictionary<string, object?>
        {
            ["errorCode"] = error.Code,
        };

        return Results.Problem(
            statusCode: status,
            detail: error.Description,
            extensions: extensions
        );
    }

    private static int ToStatusCode(this ErrorType type)
    {
        int statusCode = type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Failure => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };

        return statusCode;
    }
}
