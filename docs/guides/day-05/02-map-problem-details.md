# Bước 2. Map `Result` → ProblemDetails trong Modularity

> Mục tiêu: viết `ResultExtensions` trong `EventHub.Modularity` để dịch một `Result`/`Result<T>` **thất bại** thành một `IResult` mang **ProblemDetails** đúng HTTP status theo `Error.Type`. Cuối bước: endpoint bất kỳ gọi `result.ToProblemDetails()` là ra body lỗi chuẩn. Chưa gắn vào endpoint thật (Bước 5).
>
> Nhắc: code C#, bạn tự gõ. Đây là tầng **web** nên được phép `using Microsoft.AspNetCore...` — khác hẳn SharedKernel ở Bước 1.

---

## 2.1. Cái gì

Trong `src/Shared/EventHub.Modularity`:

1. Thêm **ProjectReference** từ `Modularity` → `SharedKernel` (để `Modularity` thấy `Result`/`Error`/`ErrorType`).
2. Một static class **`ResultExtensions`** với các extension method:
   - `IResult ToProblemDetails(this Result result)` — cho `Result` thất bại → `IResult` (một `Results.Problem(...)`), map `Error.Type` → status.
   - Một cách xử `Result<T>` thành công (trả `Value`) và thất bại (trả ProblemDetails). Gợi ý một `Match` (mục 2.5) hoặc một overload `ToProblemDetails` + để endpoint tự trả `Results.Ok(value)` khi thành công.
3. Một hàm nội bộ map `ErrorType` → `int` (HTTP status): Validation → 400, NotFound → 404, Conflict → 409, Unauthorized → 401, Failure → 500.

## 2.2. Vì sao

**Vì sao mapping ở đây, không ở SharedKernel:** như Bước A đã nói, dịch "Conflict → 409" cần biết HTTP, tức cần ASP.NET Core. `SharedKernel` mù framework nên không chứa được. `Modularity` **đã** có `FrameworkReference Microsoft.AspNetCore.App` và được mọi `*.Api` reference → đúng nhà. Kết quả: `Result` (miền) và cách nó biến thành HTTP (web) tách bạch, đổi mapping không đụng miền.

**Vì sao trả `IResult` chứ không tự ghi vào `HttpResponse`:** minimal API endpoint trả một `IResult` (vd `Results.Ok`, `Results.Problem`) và framework lo serialize + set status + content-type. Trả `IResult` giữ extension **thuần hàm** (vào `Result`, ra `IResult`), dễ test, không cầm `HttpContext`. `Results.Problem(...)` (namespace `Microsoft.AspNetCore.Http`) dựng sẵn một ProblemDetails với `Content-Type: application/problem+json` — không phải tự serialize.

**Vì sao map `Error.Type` chứ không `Error.Code`:** `Code` là chuỗi tự do (`"Identity.DuplicateEmail"`), không muốn `switch` trên hàng chục chuỗi để đoán status. `Type` là enum hữu hạn (5 giá trị) → `switch` gọn, đủ để chọn status. `Code`/`Description` đi vào **thân** ProblemDetails (cho client/log), `Type` quyết **status**.

**Vì sao gom map status vào một hàm:** status HTTP cho mỗi `ErrorType` là **một quyết định, một chỗ**. Rải `switch` này ở mọi endpoint là mời gọi lệch (chỗ này Conflict→409, chỗ kia lỡ →400). Một hàm duy nhất trong `ResultExtensions` là source of truth.

Ánh xạ `ErrorType` → HTTP status (nhánh `_` gộp với `Failure` về 500 để một `ErrorType` mới lỡ chưa map cũng an toàn):

```mermaid
flowchart LR
    E{ErrorType} --> V[Validation]
    E --> N[NotFound]
    E --> C[Conflict]
    E --> U[Unauthorized]
    E --> F["Failure / _"]
    V --> S400["400 Bad Request"]
    N --> S404["404 Not Found"]
    C --> S409["409 Conflict"]
    U --> S401["401 Unauthorized"]
    F --> S500["500 Internal Server Error"]
```

## 2.3. Dữ kiện đã xác minh

- **`Results.Problem(...)`** (namespace `Microsoft.AspNetCore.Http`, kiểu trả `IResult`) dựng một response ProblemDetails; nhận `detail`, `statusCode`, `title`, `type`, `instance`, và `extensions` (dictionary field mở rộng). Content-Type là `application/problem+json`. Nguồn: [Create responses in Minimal API apps (MS Learn, aspnetcore-10.0)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0), [Handle errors in ASP.NET Core APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0).
- **ProblemDetails theo RFC 9457** (thay RFC 7807): field `type`/`title`/`status`/`detail`/`instance` + field mở rộng. Nguồn: [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457), [Handle errors in ASP.NET Core APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0).
- **`IResult`** là kiểu trả về chuẩn của minimal API endpoint; `TypedResults`/`Results` factory nằm `Microsoft.AspNetCore.Http`. Nguồn: [Minimal APIs responses](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0).

## 2.4. Điểm xuất phát (code đang có)

- `EventHub.Modularity` hiện có đúng một file thật: `IModule.cs` (interface `IModule` với `ConfigureServices` + `MapEndpoints`). csproj có `FrameworkReference Microsoft.AspNetCore.App`, **chưa** reference `SharedKernel`.
- `SharedKernel` (sau Bước 1) đã có `Result`/`Result<T>`/`Error`/`ErrorType`.
- Bước này **thêm mới**: ProjectReference Modularity→SharedKernel, file `ResultExtensions.cs`. Không sửa `IModule`.

## 2.5. Bản đồ thi công

### Thứ tự build

1. Thêm ProjectReference Modularity → SharedKernel, build xanh (chứng minh Modularity thấy `Result`).
2. Viết hàm map `ErrorType` → status (private).
3. Viết `ToProblemDetails` cho `Result`.
4. Viết cách xử `Result<T>` (chọn `Match` hoặc overload). Build xanh.

### Mảnh 1 — ProjectReference

- Trong `EventHub.Modularity.csproj`, thêm `ProjectReference` trỏ tới `..\EventHub.SharedKernel\EventHub.SharedKernel.csproj` (đường dẫn tương đối từ thư mục Modularity). Không kèm version (ProjectReference không cần).
- **Vì sao chiều này đúng:** `Modularity` (web-aware) phụ thuộc `SharedKernel` (thuần) — chiều phụ thuộc đi **từ cụ thể tới trừu tượng/nền tảng**, hợp lệ. Chiều ngược (SharedKernel → Modularity) mới là sai (kéo ASP.NET vào miền).

### Mảnh 2 — map `ErrorType` → status (private)

- Chữ ký: `private static int ToStatusCode(ErrorType type)` (hoặc dùng `ErrorType` trực tiếp, không cần cả `Error`).
- Thân: `switch` expression trên `ErrorType`:
  - `Validation` → `StatusCodes.Status400BadRequest`
  - `NotFound` → `StatusCodes.Status404NotFound`
  - `Conflict` → `StatusCodes.Status409Conflict`
  - `Unauthorized` → `StatusCodes.Status401Unauthorized`
  - `Failure` **và** nhánh discard `_` → `StatusCodes.Status500InternalServerError`
- **Dùng hằng `StatusCodes.Status*`** (namespace `Microsoft.AspNetCore.Http`), **không** gõ literal `409` — hằng rõ nghĩa, khỏi nhớ số.
- **Micro-gotcha:** nhánh `_ =>` phải để **cuối** switch expression; đặt đầu là mọi nhánh sau unreachable (compiler cảnh báo). Gộp `Failure` với `_` cùng ra 500 để một `ErrorType` mới lỡ chưa map cũng an toàn về 500 (không nổ).

### Mảnh 3 — `ToProblemDetails(this Result result)`

- Chữ ký: `public static IResult ToProblemDetails(this Result result)`.
- Thân:
  1. **Guard:** nếu `result.IsSuccess` → đây là lỗi dùng sai (không map một success thành problem). Ném `InvalidOperationException("Không tạo ProblemDetails từ một Result thành công")`. Extension này chỉ cho nhánh thất bại.
  2. Lấy `var error = result.Error;` và `var status = ToStatusCode(error.Type);`.
  3. Trả `Results.Problem(...)` với: `statusCode: status`, `title:` một nhãn theo loại (vd map `ErrorType` → `"Bad Request"`/`"Conflict"`… hoặc để `Results.Problem` tự suy title từ status — nó tự điền title chuẩn theo status nếu bỏ trống), `detail: error.Description`, và **field mở rộng** `errorCode = error.Code` qua tham số `extensions` (dictionary một entry `{ ["errorCode"] = error.Code }`) để client/log thấy `Code` ổn định.
- **Micro-gotcha `extensions`:** tham số `extensions` của `Results.Problem` nhận `IDictionary<string, object?>`. Đưa `Code` vào đây thay vì nhét vào `detail` để `detail` giữ nguyên câu người đọc.

### Mảnh 4 — xử `Result<T>` (chọn một)

Có hai phong cách, chọn theo gu:

- **Phong cách `Match` (khuyến nghị — gọn ở endpoint):** thêm `public static IResult Match<T>(this Result<T> result, Func<T, IResult> onSuccess)`. Thân: `result.IsSuccess ? onSuccess(result.Value) : ((Result)result).ToProblemDetails()`. Endpoint viết `return result.Match(value => Results.Ok(value));` — một dòng, không `if` tay. Có thể thêm overload `Match(onSuccess, onFailure)` nếu muốn tùy biến nhánh lỗi.
  - **Micro-gotcha:** `Result<T>` kế thừa `Result` nên ép `(Result)result` để gọi `ToProblemDetails` của base — hoặc cho `ToProblemDetails` nhận `Result` là đủ (vì `Result<T>` *là* `Result`, truyền thẳng được, không cần ép; kiểm `Error` nằm ở base nên map chạy đúng).
- **Phong cách overload tường minh:** thêm `ToProblemDetails` cho `Result<T>` (chỉ nhánh fail) và để endpoint tự `if (result.IsSuccess) return Results.Ok(result.Value); return result.ToProblemDetails();`. Rõ ràng hơn nhưng lặp `if` ở mỗi endpoint.

Mentor khuyến nghị **`Match`**: đẩy cái `if` vào một chỗ, endpoint chỉ khai "thành công thì trả cái này".

### DI

**Không** đăng ký gì. `ResultExtensions` là static class thuần hàm, gọi trực tiếp, không qua container.

### Naming rationale

- `ToProblemDetails` (không `ToResult`/`ToHttp`): tên nói thẳng ra cái gì — một ProblemDetails. Đọc `result.ToProblemDetails()` ở endpoint là rõ.
- `Match` mượn thuật ngữ functional (pattern matching hai nhánh success/failure), quen thuộc với ai từng thấy Result/Either. Giữ tên English.

## 2.6. Ba bẫy dễ dính nhất

1. **Map `ToProblemDetails` cho cả success.** Extension này chỉ hợp lệ cho **thất bại**. Gọi trên success là dùng sai — cho nó ném để lộ bug sớm, đừng lặng lẽ trả 500.
2. **Gõ literal status (`409`) thay hằng, và quên nhánh `_` về 500.** Literal khó đọc; thiếu nhánh discard là một `ErrorType` mới sẽ khiến switch không khớp → exception runtime. Gộp `Failure`/`_` → 500.
3. **Nhét `Error.Code` vào `detail`.** `detail` là câu cho người đọc; `Code` là định danh máy. Trộn hai cái làm client khó parse. `Code` để `extensions`.

## 2.7. Kiểm chứng

```bash
dotnet build EventHub.slnx
```

- Build xanh; `Modularity` giờ reference `SharedKernel`.
- Chưa có endpoint dùng nó ở bước này. Kiểm bằng mắt: `switch` map đủ 5 `ErrorType` + nhánh `_`; `ToProblemDetails` ném khi success.
- (Tùy chọn) sẽ verify thật ở Bước 6 khi `/register` trả 409 qua đúng đường này.

## 2.8. Cạm bẫy thường gặp

- **Quên set `Content-Type` đúng.** Nếu tự dựng response tay thay vì `Results.Problem`, dễ trả `application/json` thường thay vì `application/problem+json`. Dùng `Results.Problem` để khỏi lo — nó set đúng.
- **`title` lộ chi tiết nội bộ.** `title` nên là nhãn loại lỗi chung ("Conflict"), không phải câu kỹ thuật ("Unique constraint AspNetUsers_Email violated"). Chi tiết an toàn cho người dùng để `detail` (từ `Error.Description` bạn tự viết), không phải message của DB.
- **Trộn tầng: gọi `ToProblemDetails` trong Application/Domain.** Extension này ở `Modularity` (web) và chỉ nên gọi ở tầng **Api** (endpoint). Nếu thấy mình gọi nó trong `AuthService` (Application), bạn đang kéo HTTP xuống nghiệp vụ — service trả `Result`, endpoint mới map.

## 2.9. Góc kể khi phỏng vấn

*"Tôi giữ `Result` mù HTTP ở SharedKernel, rồi dịch nó sang ProblemDetails ở tầng web bằng một extension trong Modularity — nơi đã có sẵn ASP.NET và mọi module Api đều thấy. Việc chọn status là một chỗ duy nhất: `switch` trên `ErrorType`, Conflict→409, NotFound→404, mặc định Failure→500. `Error.Description` đi vào `detail` cho người đọc, `Error.Code` vào field mở rộng cho máy, còn `title` chỉ là nhãn loại — không bao giờ lộ message kỹ thuật của DB. Endpoint chỉ viết `result.Match(v => Results.Ok(v))`, cái `if` success/fail nằm gọn trong một hàm."*

## 2.10. Link tài liệu chính thức

- [Handle errors in ASP.NET Core APIs — Problem Details (aspnetcore-10.0)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0)
- [Create responses in Minimal API apps (Results.Problem)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0)
- [RFC 9457 — Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc9457)
- [Milan Jovanović — Problem Details for ASP.NET Core APIs](https://milanjovanovic.tech/blog/problem-details-for-aspnetcore-apis)

## 2.11. Xong bước này khi

- [x] `Modularity` reference `SharedKernel`; build xanh.
- [x] `ResultExtensions` map đủ 5 `ErrorType` → status (Validation 400, NotFound 404, Conflict 409, Unauthorized 401, Failure/`_` 500), dùng hằng `StatusCodes.Status*`.
- [x] `ToProblemDetails` trả `IResult` qua `Results.Problem`; `detail` = `Description`, `errorCode` (extensions) = `Code`; ném nếu gọi trên success.
- [x] Có `Match<T>` (hoặc overload) cho `Result<T>`.
- [x] `dotnet build` xanh.

→ Sang [Bước 3. Global exception handler ở host](03-global-exception-handler.md).
