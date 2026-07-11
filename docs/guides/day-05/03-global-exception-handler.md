# Bước 3. Global exception handler ở host

> Mục tiêu: bắt **mọi exception không lường** lọt ra khỏi endpoint và trả một ProblemDetails 500 **câm** (không lộ chi tiết), đồng thời log đầy đủ phía server. Dùng `IExceptionHandler` (.NET 8+/10), không middleware tay. Cuối bước: ép một endpoint ném thử → nhận `application/problem+json` 500, không thấy stack trace.
>
> Nhắc: code C# + sửa `Program.cs`, bạn tự gõ.

---

## 3.1. Cái gì

Trong host `src/Bootstrap/EventHub.Api`:

1. Một class **`GlobalExceptionHandler : IExceptionHandler`** (namespace `Microsoft.AspNetCore.Diagnostics`), implement `TryHandleAsync`: log exception, ghi một ProblemDetails 500 tối giản vào response, `return true`.
2. Wiring trong `Program.cs`:
   - `builder.Services.AddProblemDetails();` — bật `IProblemDetailsService` để có ProblemDetails thống nhất.
   - `builder.Services.AddExceptionHandler<GlobalExceptionHandler>();` — đăng ký handler.
   - `app.UseExceptionHandler();` — cắm middleware bắt exception, đặt **sớm nhất** trong pipeline.
   - `app.UseStatusCodePages();` — để các response lỗi *không có body* (vd 401/404 trần) cũng nhận body ProblemDetails.

## 3.2. Vì sao

**Vì sao cần lưới cuối này dù đã có `Result`:** `Result` chỉ lo lỗi **lường trước**. Vẫn còn lớp lỗi **không lường**: `SaveChangesAsync` ném vì Postgres rớt, một `NullReferenceException` do bug, `OperationCanceledException` khi client ngắt. Không ai `try/catch` được hết ở mọi endpoint. Một handler toàn cục là **lưới an toàn cuối**: bắt tất, trả một response chuẩn thay vì trang lỗi mặc định (có thể lộ nội bộ ở production).

**Vì sao `IExceptionHandler` thay middleware tay:** (đã bàn ở [Bước A](00-tong-quan.md#a4-global-exception-handler-cách-hiện-đại-net-10-thay-cho-middleware-tay)) — không tự bọc `try/catch`, chuỗi được nhiều handler, tích hợp `IProblemDetailsService`, .NET 10 tự suppress diagnostics trùng khi trả `true`.

**Vì sao `UseExceptionHandler` đặt sớm nhất:** middleware này bọc **mọi thứ chạy sau nó**. Đặt nó trước routing/auth/endpoint để lưới phủ toàn bộ. Đặt muộn thì exception ở các middleware trước nó lọt lưới.

**Vì sao thêm `UseStatusCodePages`:** `UseExceptionHandler` chỉ xử **exception**. Nhưng một response `401`/`404`/`403` do `RequireAuthorization`/routing sinh ra **không** kèm exception — mặc định body **rỗng**. `UseStatusCodePages` (hoặc `AddProblemDetails` + status code pages) khiến các status lỗi trần này cũng nhận một body ProblemDetails, để client luôn nhận một hình dạng lỗi. Nguồn: [Handle errors in ASP.NET Core APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0).

**Vì sao handler "defensive" + câm:** (đã bàn ở Bước A) handler là lưới cuối — nếu nó gọi DB/HTTP và ném tiếp thì không còn ai đỡ. Chỉ log (an toàn, in-memory/file) rồi ghi một ProblemDetails tĩnh. Và **không** đưa `exception.Message`/stack vào body client: log phía server để bạn debug, trả client một câu chung.

## 3.3. Dữ kiện đã xác minh

- **`IExceptionHandler`** (namespace `Microsoft.AspNetCore.Diagnostics`): một method `ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)`. Trả `true` = đã xử (đã ghi response); `false` = chuyển tiếp. Đăng ký bằng `services.AddExceptionHandler<T>()`; bật bằng `app.UseExceptionHandler()`. Nguồn: [Handle errors in ASP.NET Core (aspnetcore-10.0)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0), [IExceptionHandler (API)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.diagnostics.iexceptionhandler).
- **`AddProblemDetails()`** đăng ký `IProblemDetailsService` sinh ProblemDetails chuẩn cho các response lỗi; kết hợp với `UseExceptionHandler`/`UseStatusCodePages` để mọi lỗi có body thống nhất. Nguồn: [Handle errors in ASP.NET Core APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0).
- **`IProblemDetailsService.TryWriteAsync(ProblemDetailsContext)`** cho phép handler nhờ framework ghi ProblemDetails (thay vì tự serialize) — trả `bool` cho biết đã ghi được. Nguồn: [IProblemDetailsService (API)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.iproblemdetailsservice).
- **.NET 10**: khi `TryHandleAsync` trả `true`, diagnostics của exception được suppress mặc định (điều chỉnh qua `SuppressDiagnosticsCallback`). Nguồn: [Handle errors in ASP.NET Core (aspnetcore-10.0)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0).

## 3.4. Điểm xuất phát (code đang có)

`Program.cs` hiện tại (host) đã có:

- `builder.Services.AddModules(builder.Configuration);`
- `var app = builder.Build();`
- `app.UseAuthentication();` → `app.UseAuthorization();` → `app.UseModules();`
- `app.MapGet("/health", ...)` + `app.Run()`.

Chưa có exception handling nào. Bước này **thêm**: hai dòng `AddProblemDetails`/`AddExceptionHandler` (phần dịch vụ, trước `Build()`), hai dòng `UseExceptionHandler`/`UseStatusCodePages` (phần pipeline, sau `Build()`), và file `GlobalExceptionHandler.cs`. **Không** đụng logic module.

## 3.5. Bản đồ thi công

### Sơ đồ trace một request ném exception

```mermaid
sequenceDiagram
    participant Client
    participant ExH as UseExceptionHandler (bọc ngoài cùng)
    participant Pipe as UseAuthentication / UseAuthorization / UseModules
    participant Endpoint
    participant Handler as GlobalExceptionHandler
    Client->>ExH: HTTP request
    ExH->>Pipe: đi tiếp
    Pipe->>Endpoint: chạy handler
    Endpoint--xPipe: ném InvalidOperationException (không lường)
    Pipe--xExH: exception bong ra
    ExH->>Handler: TryHandleAsync(ctx, ex, ct)
    Handler->>Handler: log ex đầy đủ (server-side)
    Handler->>Handler: ghi ProblemDetails 500 tối giản (KHÔNG kèm ex.Message/stack)
    Handler-->>ExH: return true
    ExH-->>Client: 500 application/problem+json, body câm
```

**Ranh giới cốt tử:** body trả client **không bao giờ** chứa `exception.Message`, stack trace, tên type exception, hay chi tiết DB. Những thứ đó chỉ đi vào **log phía server**. Đây là ranh giới bảo mật, không phải thẩm mỹ.

### Thứ tự build

1. Viết `GlobalExceptionHandler` (build xanh — class độc lập).
2. Wire dịch vụ (`AddProblemDetails` + `AddExceptionHandler`) trước `Build()`.
3. Wire pipeline (`UseExceptionHandler` + `UseStatusCodePages`) sau `Build()`, đặt `UseExceptionHandler` **đầu** chuỗi `Use...`.
4. Chạy host, ép ném thử (mục 3.7).

### Mảnh 1 — `GlobalExceptionHandler`

- File: `GlobalExceptionHandler.cs` ở `src/Bootstrap/EventHub.Api/` (gợi ý thư mục `Middleware/` hoặc `Diagnostics/` cho gọn — ROADMAP mục 3 vẽ sẵn thư mục `Middleware/` cho host).
- Chữ ký class: `internal sealed class GlobalExceptionHandler : IExceptionHandler`. `internal` vì chỉ host dùng; `sealed` vì không định kế thừa.
- Inject qua primary constructor: `ILogger<GlobalExceptionHandler>` (để log), và (tùy chọn) `IProblemDetailsService` nếu muốn nhờ framework ghi body.
- Method: `public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)`.
  - **Bước con 1 — log:** `logger.LogError(exception, "Unhandled exception cho {Path}", httpContext.Request.Path)`. Đây là chỗ *duy nhất* chi tiết exception được ghi.
  - **Bước con 2 — set status:** `httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;` (phải set **trước** khi ghi body).
  - **Bước con 3 — ghi ProblemDetails câm.** Hai cách:
    - (a) **Nhờ `IProblemDetailsService`:** gọi `await problemDetailsService.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = new ProblemDetails { Status = 500, Title = "Đã có lỗi xảy ra", Detail = "Yêu cầu không thể xử lý. Vui lòng thử lại sau." } })`. `ProblemDetails` type ở `Microsoft.AspNetCore.Mvc`; `ProblemDetailsContext` ở `Microsoft.AspNetCore.Http`.
    - (b) **Tự ghi:** `await httpContext.Response.WriteAsJsonAsync(new ProblemDetails { ... }, cancellationToken)` — đơn giản hơn nhưng content-type mặc định là `application/json` (đặt `httpContext.Response.ContentType = "application/problem+json"` nếu muốn chuẩn). Mentor khuyến nghị **(a)** cho đúng chuẩn và đồng bộ với `AddProblemDetails`.
  - **Bước con 4:** `return true;` (đã xử lý).
- **Micro-gotcha `Title`/`Detail`:** cả hai là câu chung chung cho **mọi** exception — cố tình mơ hồ. Không nội suy `exception.Message` vào đây.

### Mảnh 2 — wiring dịch vụ (`Program.cs`, trước `Build()`)

- Thêm `builder.Services.AddProblemDetails();`.
- Thêm `builder.Services.AddExceptionHandler<GlobalExceptionHandler>();`.
- Đặt cạnh `AddModules` cho gọn. Thứ tự giữa chúng không quan trọng (đăng ký ≠ resolve).

### Mảnh 3 — wiring pipeline (`Program.cs`, sau `Build()`)

- Thêm `app.UseExceptionHandler();` **đầu tiên** trong chuỗi middleware, **trước** `UseAuthentication()`.
- Thêm `app.UseStatusCodePages();` (đặt sau `UseExceptionHandler`, trước `UseModules` — vị trí không quá nhạy, miễn trước khi map endpoint).
- **Micro-gotcha:** `UseExceptionHandler()` **không tham số** dùng được vì đã có `IExceptionHandler` đăng ký. (Overload nhận lambda `UseExceptionHandler(configure)` là cách *cũ* tự viết handler — không cần khi đã có `AddExceptionHandler<T>`.)

### DI + lifetime

- `AddExceptionHandler<T>()` đăng ký handler là **singleton** (mặc định). Nên `GlobalExceptionHandler` **không** được inject dịch vụ scoped (vd `DbContext`) — captive dependency, và vi phạm "defensive". `ILogger<T>` là singleton-safe, `IProblemDetailsService` cũng an toàn để inject. **Không** inject `DbContext`/`UserManager` vào handler.

### Naming rationale

- `GlobalExceptionHandler` (không `ErrorHandler`/`ExceptionMiddleware`): "Global" nhấn phạm vi toàn cục; "Handler" khớp interface `IExceptionHandler`, phân biệt với "Middleware" (cách cũ ta *không* dùng).

## 3.6. Ba bẫy dễ dính nhất

1. **Lộ `exception.Message`/stack ra body client.** Lỗi bảo mật. Chi tiết chỉ vào log; body client là câu chung. Kiểm bằng cách đọc response của endpoint ném thử — không được thấy tên type/stack.
2. **Đặt `UseExceptionHandler` muộn (sau `UseModules`).** Lưới không phủ được phần trước nó. Đặt **đầu** pipeline.
3. **Inject dịch vụ scoped vào handler.** Handler là singleton + phải defensive. Inject `DbContext` = captive dependency + rủi ro ném trong lưới cuối. Chỉ inject thứ singleton-safe.

## 3.7. Kiểm chứng

```bash
dotnet build EventHub.slnx
dotnet run --project src/Bootstrap/EventHub.Api
```

Ép một exception để thấy lưới hoạt động. Cách nhanh: tạm thêm một endpoint ném thử (nhớ **xóa trước khi commit**), vd `GET /debug/boom` ném `throw new InvalidOperationException("boom")`. Rồi:

```bash
curl -i http://localhost:5xxx/debug/boom
```

Kỳ vọng:

- Status `500`.
- Header `Content-Type: application/problem+json`.
- Body là ProblemDetails **câm**: có `title`/`status`/`detail` chung chung, **không** có chuỗi `"boom"`, không tên type, không stack trace.
- **Log phía server** (cửa sổ chạy host) **có** đầy đủ exception + stack — đó là nơi bạn debug.

Xóa endpoint `/debug/boom` sau khi kiểm. Tắt host (`Ctrl+C`).

> Kiểm thêm status trần: gọi một endpoint bảo vệ **không** token (`curl -i http://localhost:5xxx/identity/me`) → nhờ `UseStatusCodePages`, `401` giờ cũng nên kèm một body (thay vì rỗng hoàn toàn). Tùy cấu hình, body có thể tối giản — điều cần thấy là client không nhận response lỗi trống trơn.

## 3.8. Cạm bẫy thường gặp

- **Quên `AddProblemDetails()`.** Có `AddExceptionHandler` nhưng thiếu `AddProblemDetails` thì `IProblemDetailsService` không có, cách (a) ở Mảnh 1 không chạy. Đăng ký cả hai.
- **Set body trước status.** Ghi body rồi mới đặt `StatusCode` → response đã "bắt đầu" (headers gửi đi), set status trễ bị bỏ qua/nổ. Đặt `Response.StatusCode` **trước** khi ghi.
- **Bắt cả `OperationCanceledException` và log ầm ĩ.** Khi client hủy request (đóng tab), một `OperationCanceledException`/`TaskCanceledException` là **bình thường**, không phải bug. Cân nhắc: nếu `httpContext.RequestAborted.IsCancellationRequested` thì bỏ qua (return `false` hoặc log mức thấp) để log không nhiễu. Day 5 có thể ghi nhận đây là cải tiến; đừng để nó làm log đỏ giả.
- **Nghĩ handler thay được `Result`.** Handler là lưới **cuối** cho lỗi *không lường*. Đừng vì có nó mà bỏ `Result` rồi `throw` cho mọi lỗi nghiệp vụ — quay lại đúng cái phản mẫu Bước A cảnh báo.

## 3.9. Góc kể khi phỏng vấn

*"Tôi có hai tuyến lỗi: lỗi nghiệp vụ lường trước đi qua `Result`, còn exception không lường thì một `GlobalExceptionHandler` implement `IExceptionHandler` bắt hết ở lưới cuối. Tôi dùng `IExceptionHandler` thay middleware tay vì nó tích hợp `IProblemDetailsService` và .NET 10 tự suppress diagnostics trùng. Handler chỉ log exception đầy đủ phía server rồi trả một ProblemDetails 500 câm — không bao giờ lộ message hay stack ra client, đó là ranh giới bảo mật. Nó phải defensive: singleton, không đụng DbContext, không gọi gì có thể ném tiếp vì nó là lưới cuối. Tôi thêm `UseStatusCodePages` để cả những status trần như 401/404 cũng có body ProblemDetails, cho client một hình dạng lỗi duy nhất."*

## 3.10. Link tài liệu chính thức

- [Handle errors in ASP.NET Core (IExceptionHandler, aspnetcore-10.0)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0)
- [Handle errors in ASP.NET Core APIs (AddProblemDetails, status code pages)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0)
- [IExceptionHandler (API reference)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.diagnostics.iexceptionhandler)
- [IProblemDetailsService (API reference)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.iproblemdetailsservice)

## 3.11. Xong bước này khi

- [x] `GlobalExceptionHandler : IExceptionHandler` log exception + trả ProblemDetails 500 câm; `internal sealed`; chỉ inject dịch vụ singleton-safe.
- [x] `AddProblemDetails()` + `AddExceptionHandler<GlobalExceptionHandler>()` trước `Build()`.
- [x] `UseExceptionHandler()` đầu pipeline + `UseStatusCodePages()`; trước `UseModules()`.
- [x] Endpoint ném thử → 500 `application/problem+json`, body **không** lộ chi tiết; log server **có** chi tiết. (Đã xóa endpoint thử.)
- [x] `dotnet build` xanh.

→ Sang [Bước 4. FluentValidation cho Register](04-fluentvalidation-register.md).
