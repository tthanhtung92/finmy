# Bước A (00). Hiểu trước khi gõ: ba loại lỗi, `Result<T>`, ProblemDetails & bản đồ code Day 5

> Mục tiêu bước này: **chỉ đọc, chưa gõ gì**. Nắm bốn thứ: (1) ba loại lỗi khác nhau và vì sao mỗi loại xử một kiểu; (2) vì sao lỗi nghiệp vụ nên dùng `Result` thay vì ném exception; (3) ProblemDetails RFC 9457 là gì và vì sao chuẩn hóa body lỗi lại đáng làm; (4) ba mảnh code hôm nay đặt ở đâu để **không phá ranh giới** — đặc biệt vì sao `SharedKernel` phải mù ASP.NET. Hiểu xong mới sang [Bước 1](01-result-sharedkernel.md).

---

## A.1. Bức tranh Day 5

Day 4 bạn dựng luồng auth chạy được. Nhưng phần **xử lý lỗi** còn thô: endpoint `/identity/register` gói kết quả vào một record `RegisterOutcome`, rồi một `switch` tay trong endpoint map enum `RegisterFailureReason` sang `Results.Conflict`/`Results.ValidationProblem`/`Results.Problem`. Nó chạy, nhưng có ba vấn đề:

- **Không dùng lại được.** Mỗi endpoint sau (Events, Ticketing) sẽ phải bịa ra một `SomethingOutcome` + `switch` riêng. Không có ngôn ngữ lỗi chung.
- **Không thống nhất body lỗi.** `Results.Unauthorized()` trả body rỗng; `Results.Problem()` trả ProblemDetails; `Results.ValidationProblem` trả một dạng khác nữa. Client nhận ba hình dạng lỗi khác nhau từ cùng một API.
- **Không ai bắt exception không lường.** Nếu `SaveChangesAsync` ném vì mất kết nối DB, ASP.NET Core trả một trang lỗi mặc định, ở production có thể **lộ stack trace**.

Day 5 dựng một **chiến lược lỗi thống nhất**. Ý tưởng cốt lõi: **có ba loại lỗi, đừng xử chung một kiểu.**

| Loại lỗi | Ví dụ | Lường trước? | Xử ở đâu (Day 5) | Kết quả HTTP |
|---|---|---|---|---|
| **Validation** (input rác) | email sai định dạng, mật khẩu rỗng, thiếu field | Có — client gửi sai | **Endpoint filter** (FluentValidation) chặn trước khi vào service | 400 + danh sách lỗi field |
| **Nghiệp vụ** (lường trước) | email đã tồn tại, không tìm thấy event, hết vé | Có — là một kết cục hợp lệ của nghiệp vụ | Trả **`Result` với `Error`**, map `Error.Type` sang status | 409 / 404 / 401… |
| **Hệ thống** (không lường) | mất kết nối DB, null bug, `KeyNotFoundException` | Không — là bug hoặc sự cố | **Global exception handler** bắt hết | 500 câm, không lộ chi tiết |

Ba bước 1–2, 3, 4–5 của Day 5 dựng đúng ba cột này. Hiểu bảng trên là hiểu cả ngày.

## A.2. Vì sao lỗi nghiệp vụ nên dùng `Result`, không ném exception

Câu hỏi tự nhiên: "email trùng thì cứ `throw new Exception("email trùng")` rồi để handler bắt, gọn mà?" Được về mặt chạy, nhưng **sai về thiết kế**, và đây là một điểm phỏng vấn hay.

**Exception là cho điều *ngoại lệ*, không phải cho luồng bình thường.** Email trùng khi đăng ký **không** phải sự cố hiếm, nó là một **kết cục thường ngày và lường trước được** của nghiệp vụ đăng ký. Dùng cơ chế "ngoại lệ" cho một nhánh logic bình thường có ba cái giá:

- **Đắt.** Ném và bắt một exception phải dựng **stack trace** (đi ngược toàn bộ ngăn xếp gọi hàm), tốn hơn một phép `return` nhiều lần. Trên đường nóng (login, đặt vé) mà mỗi lỗi nghiệp vụ là một exception thì lãng phí.
- **Mờ ý định.** Một hàm ký `Task<Guid> RegisterUserAsync(...)` **nói dối**: nhìn chữ ký tưởng luôn trả `Guid`, nhưng thực ra nó *có thể* ném. Người gọi không đọc thân hàm thì không biết phải `try/catch`. Chữ ký `Task<Result<Guid>>` thì **trung thực**: nó nói thẳng "tôi có thể thất bại, và đây là kiểu lỗi bạn nhận".
- **Khó kiểm soát luồng.** `try/catch` tách phần "thành công" khỏi phần "xử lỗi" ra hai chỗ; `if (result.IsFailure) return ...` giữ hai nhánh cạnh nhau, dễ đọc.

**Result pattern** (còn gọi là *functional error handling*) giải bài này: hàm trả một đối tượng `Result` gói **hoặc** giá trị thành công **hoặc** một `Error` mô tả vì sao thất bại. Không ném, không bắt; caller đọc `IsSuccess`. Đây là cách các reference app .NET hiện đại xử lý lỗi nghiệp vụ (xem [Milan Jovanović — Result pattern](https://milanjovanovic.tech/blog/functional-error-handling-in-dotnet-with-the-result-pattern), [Result pattern trong minimal API — Simple Talk](https://www.red-gate.com/simple-talk/development/dotnet-development/the-result-pattern-in-asp-net-core-minimal-apis/)).

> **Ranh giới quan trọng:** `Result` **không** thay thế exception hoàn toàn. Lỗi *thật sự ngoại lệ* (DB chết, bug null) vẫn **nên** ném exception và để global handler bắt (mục A.4). Quy tắc: **lường trước được và là kết cục hợp lệ của nghiệp vụ → `Result`; không lường được / là sự cố → exception.**

## A.3. ProblemDetails RFC 9457 là cái gì

Khi API trả lỗi, client cần một hình dạng body **đoán trước được** để đọc. Nếu mỗi endpoint trả một kiểu JSON lỗi khác nhau, client phải viết code parse riêng cho từng chỗ. **ProblemDetails** là một chuẩn IETF giải đúng bài này: một định dạng JSON thống nhất cho lỗi HTTP.

Chuẩn hiện hành là **RFC 9457** (*Problem Details for HTTP APIs*), bản cập nhật thay cho RFC 7807 cũ (nội dung gần như y hệt, chỉ là số hiệu mới — nhiều tài liệu vẫn quen gọi 7807). Một body ProblemDetails gồm các field chuẩn:

- **`type`** — một URI định danh *loại* lỗi (vd `https://tools.ietf.org/html/rfc9110#section-15.5.8`). Không bắt buộc phải resolve được, chỉ cần định danh.
- **`title`** — mô tả ngắn, con người đọc được, loại lỗi (vd `"Conflict"`).
- **`status`** — mã HTTP (vd `409`), lặp lại status của response cho tiện.
- **`detail`** — mô tả cụ thể *lần lỗi này* (vd `"Email đã được đăng ký."`).
- **`instance`** — URI của *request* gây lỗi (vd đường dẫn endpoint).
- **Field mở rộng** — bạn thêm được field riêng. FluentValidation dùng một field `errors` (dictionary field → danh sách thông báo) cho lỗi validation; đây chính là dạng `Results.ValidationProblem` sinh ra.

Response mang ProblemDetails có `Content-Type: application/problem+json` (khác `application/json` thường) để client biết đây là body lỗi chuẩn.

**Vì sao đáng làm:** một hình dạng lỗi duy nhất cho toàn API. Client (web, mobile, service khác) viết **một** bộ xử lý lỗi, đọc `status` + `detail` + `errors`, dùng cho mọi endpoint. ASP.NET Core hỗ trợ sẵn: `AddProblemDetails()` bật một `IProblemDetailsService` để framework tự sinh ProblemDetails cho các response lỗi, và `Results.Problem(...)` / `Results.ValidationProblem(...)` dựng body này cho bạn. Nguồn: [Handle errors in ASP.NET Core APIs (MS Learn)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0).

## A.4. Global exception handler: cách hiện đại (.NET 10) thay cho middleware tay

ROADMAP viết "global exception handling **middleware**". Trước .NET 8, cách làm đúng là **tự viết một middleware** bọc `try/catch` quanh phần còn lại của pipeline. Từ .NET 8 trở đi có cách **sạch hơn**, và Day 5 dạy cách này: interface **`IExceptionHandler`**.

`IExceptionHandler` chỉ có **một** method: `ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)`. Bạn viết một class implement nó, đăng ký bằng `AddExceptionHandler<T>()`, và bật middleware sẵn có bằng `UseExceptionHandler()`. Khi có exception không bắt được lọt ra, middleware gọi `TryHandleAsync`; bạn ghi một response ProblemDetails rồi `return true` (đã xử lý). Trả `false` thì chuyển cho handler kế tiếp, hoặc hành vi mặc định nếu bạn là cuối chuỗi.

Vì sao cách này hơn middleware tay:

- **Không phải tự bọc `try/catch`** quanh `await next()` — dễ quên, dễ nuốt lỗi sai chỗ.
- **Chuỗi được nhiều handler**: đăng ký nhiều `IExceptionHandler`, chạy theo thứ tự đăng ký, cái đầu tiên `return true` thắng. Tách xử theo loại exception gọn hơn một khối `switch` khổng lồ.
- **Tích hợp `IProblemDetailsService`** sẵn: bạn có thể để framework sinh ProblemDetails thay vì tự serialize.
- **.NET 10**: khi `TryHandleAsync` trả `true`, diagnostics được **suppress mặc định** (không log trùng như một unhandled exception), điều chỉnh được bằng `SuppressDiagnosticsCallback`.

Nguồn: [Handle errors in ASP.NET Core (MS Learn, aspnetcore-10.0)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0), [Milan Jovanović — từ middleware tới modern handlers](https://milanjovanovic.tech/blog/global-error-handling-in-aspnetcore-from-middleware-to-modern-handlers).

> **Handler phải "defensive".** Bên trong `TryHandleAsync` **đừng** gọi thứ có thể ném tiếp: không truy vấn DB, không gọi HTTP ngoài. Handler là **lưới cuối** — nếu chính nó ném thì không còn ai đỡ, client nhận 500 trần. Chỉ ghi một ProblemDetails tối giản rồi thoát.

> **Không lộ chi tiết ra client.** `TryHandleAsync` **biết** `exception` (message, stack trace) để **log phía server**, nhưng body trả về client chỉ nên là ProblemDetails chung chung ("Đã có lỗi xảy ra", status 500). Lộ message/stack ra ngoài là rò thông tin nội bộ (tên bảng, đường dẫn file, thư viện dùng) — món quà cho kẻ tấn công.

## A.5. Bản đồ code Day 5: ba tầng, đặt đâu để giữ ranh giới

Đây là phần dễ đặt sai nhất. Cả ba mảnh đều liên quan tới lỗi, nhưng chúng sống ở **ba tầng khác nhau** vì lệ thuộc khác nhau.

**Tầng 1 — `Result`/`Error` ở `SharedKernel` (thuần, mù framework).**

`Result`, `Result<T>`, `Error`, `ErrorType` là **khái niệm miền thuần**: "một thao tác thành công hay thất bại, và nếu thất bại thì vì lý do gì". Nó **không** biết gì về HTTP, ASP.NET, hay status code. Vì mọi tầng (Domain, Application của **mọi** module) đều cần trả `Result`, nó thuộc `SharedKernel` — nơi chứa primitive dùng chung. Cực kỳ quan trọng: **`SharedKernel` phải mù ASP.NET Core** (csproj của nó là plain `Microsoft.NET.Sdk`, không `FrameworkReference Microsoft.AspNetCore.App`). Nếu bạn nhét `ToProblemDetails()` vào đây, `SharedKernel` phải kéo ASP.NET, và mọi Domain/Application tự dưng "biết" HTTP — rò rỉ tầng nặng. `Result` biết "thất bại vì Conflict"; nó **không** được biết "Conflict = HTTP 409".

**Tầng 2 — mapping `Result` → ProblemDetails ở `Modularity` (web-aware).**

Việc dịch "`Error.Type == Conflict`" thành "HTTP 409 + một ProblemDetails" là **mối lo của tầng web**. Nó cần biết `Results.Problem`, status code — tức cần ASP.NET Core. Chỗ đúng cho nó: một `ResultExtensions` (extension method `ToProblemDetails()`/`ToHttpResult()`) trong **`EventHub.Modularity`**. Vì sao Modularity mà không phải host? Vì phần map này được **endpoint của từng module** gọi (trong `IdentityModule.MapEndpoints`), mà module `*.Api` reference `Modularity` (chúng dùng `IModule` từ đó), **không** reference host. Host reference module, không phải chiều ngược lại. `Modularity` **đã** có `FrameworkReference Microsoft.AspNetCore.App` (vì `IModule.MapEndpoints` nhận `IEndpointRouteBuilder`), nên đặt web-helper ở đây là hợp lý, không thêm project mới.

> Đây là **quyết định đã chốt** cho project (xem plan Day 5): tái dùng `Modularity` làm nhà cho web-helper thay vì tạo một project `EventHub.Web` riêng. Cái giá: `Modularity` phình từ "cách nạp module" sang "chứa cả helper lỗi + validation". Chấp nhận được ở quy mô này; nếu helper nhiều lên, tách project sau.

**Tầng 3 — `GlobalExceptionHandler` ở host (`Bootstrap/EventHub.Api`).**

Handler bắt exception là **mối lo toàn cục của pipeline HTTP**, không của riêng module nào. Nó đăng ký vào pipeline (`UseExceptionHandler`) mà pipeline do host dựng. Nên `GlobalExceptionHandler : IExceptionHandler` và phần wiring (`AddProblemDetails`, `AddExceptionHandler`, `UseExceptionHandler`, `UseStatusCodePages`) nằm ở **host**, cạnh `AddModules`/`UseModules`. Cùng lý do middleware auth (Day 4) nằm ở host: thứ tự và phạm vi pipeline là quyết định toàn cục.

**Tầng phụ — validator ở `Identity.Application`, filter ở `Modularity`.**

`RegisterRequestValidator` (luật validate cho `RegisterRequest`) sống cùng tầng với DTO nó validate — **`Identity.Application`** (nơi `RegisterRequest` đang ở). Còn `ValidationFilter<T>` (cơ chế *chạy* validator trong pipeline endpoint) là hạ tầng web dùng chung mọi module → **`Modularity`**, cạnh `ResultExtensions`.

Sơ đồ chiều tham chiếu (thêm mảnh Day 5, không đảo chiều nào):

```mermaid
graph LR
    Host["EventHub.Api (host)"] --> Api["Identity.Api"]
    Api --> Application["Identity.Application"]
    Infrastructure["Identity.Infrastructure"] --> Application
    Application --> Domain["Identity.Domain"]
    Api -.->|dùng| Modularity["EventHub.Modularity"]
    Application -.->|dùng| SharedKernel["EventHub.SharedKernel"]
    Modularity -.->|dùng| SharedKernel
```

```text
EventHub.SharedKernel        → Result, Result<T>, Error, ErrorType   (THUẦN, không ASP.NET)
EventHub.Modularity          → IModule (đã có) + ResultExtensions.ToProblemDetails() + ValidationFilter<T>
                               (đã có FrameworkReference Microsoft.AspNetCore.App)
EventHub.Identity.Application → RegisterRequestValidator (cạnh RegisterRequest); IIdentityService trả Result<Guid>
EventHub.Identity.Infrastructure → IdentityService: map IdentityResult → Result (thay RegisterOutcome)
EventHub.Identity.Api        → endpoint /register gắn .AddEndpointFilter<ValidationFilter<RegisterRequest>>,
                               dùng result.ToProblemDetails()
EventHub.Api (host)          → GlobalExceptionHandler + AddProblemDetails/UseExceptionHandler/UseStatusCodePages
Chiều ref: Api → Application → Domain; mọi thứ có thể xài SharedKernel/Modularity  (không đảo)
```

## A.6. Xong bước này khi

- [x] Bạn kể được **ba loại lỗi** (validation / nghiệp vụ / hệ thống) khác nhau ở đâu và mỗi loại xử ở tầng nào.
- [x] Bạn giải thích được **vì sao** dùng exception cho lỗi nghiệp vụ bình thường là đắt và mờ ý định, còn `Result` thì trung thực với chữ ký hàm.
- [x] Bạn nói được ProblemDetails RFC 9457 gồm field gì và vì sao chuẩn hóa body lỗi giúp client.
- [x] Bạn chỉ đúng chỗ đặt ba mảnh và giải thích được **vì sao `SharedKernel` phải mù ASP.NET** còn mapping ProblemDetails thì phải nằm ở tầng web.
- [x] Bạn hiểu vì sao dùng `IExceptionHandler` thay middleware tay, và vì sao handler phải "defensive" + không lộ chi tiết.

→ Sang [Bước 1. `Result<T>` trong SharedKernel](01-result-sharedkernel.md).
