# Bước 4. FluentValidation cho `/identity/register`

> Mục tiêu: chặn input rác **ngay ở cửa** endpoint register bằng FluentValidation, qua một `ValidationFilter<T>` dùng lại được cho mọi endpoint. Cuối bước: gửi email sai định dạng / mật khẩu rỗng → **400 ProblemDetails** với danh sách lỗi field, **không** chạm tới service.
>
> Nhắc: code C# + cấu hình CPM, bạn tự gõ.

---

## 4.1. Cái gì

Bốn mảnh:

1. **Package:** khai `FluentValidation` + `FluentValidation.DependencyInjectionExtensions` (version `12.1.1`) trong CPM; reference đúng nơi cần.
2. **`RegisterRequestValidator`** (`Identity.Application`): luật validate cho `RegisterRequest` (email không rỗng + đúng định dạng; password không rỗng + đủ dài).
3. **`ValidationFilter<T>`** (`Modularity`): một `IEndpointFilter` chạy `IValidator<T>` (nếu có) trước handler; sai → trả `Results.ValidationProblem` 400; đúng → cho request đi tiếp.
4. **Wiring:** đăng ký validator vào DI trong `IdentityModule.ConfigureServices`; gắn `.AddEndpointFilter<ValidationFilter<RegisterRequest>>()` lên route `/identity/register`.

## 4.2. Vì sao

**Vì sao validation tách khỏi nghiệp vụ, chặn ở cửa:** kiểm "email có đúng định dạng không, password có rỗng không" là **luật hình dạng input**, không phải nghiệp vụ. Nhét nó vào `AuthService`/`IdentityService` làm service lẫn hai mối lo (hình dạng input + logic đăng ký) và lặp ở mọi use-case. Chặn ở **endpoint filter** khiến request rác **không bao giờ** vào tới service — service chỉ nhận input đã sạch hình dạng, tập trung vào nghiệp vụ (email này *đã tồn tại* chưa — cái đó cần DB, là việc của service).

**Vì sao FluentValidation thay Data Annotations:** `[Required]`/`[EmailAddress]` (Data Annotations) trộn luật validate vào chính DTO bằng attribute, khó test riêng, khó luật phức tạp (điều kiện chéo field, luật động). FluentValidation tách luật ra một class `AbstractValidator<T>` riêng: strongly-typed, test được như code thường, biểu đạt luật phức tạp bằng fluent API. Nó cũng là thư viện **Apache 2.0, miễn phí** — không dính làn sóng thương mại hóa 2025 (khác Data Annotations vẫn free, nhưng FluentValidation mạnh hơn hẳn cho API thật).

**Vì sao endpoint filter thay vì inject `IValidator<T>` tay vào mỗi handler:** (quyết định đã chốt của project) một `ValidationFilter<T>` **generic** viết một lần, gắn lên bất kỳ endpoint nào bằng `.AddEndpointFilter<ValidationFilter<X>>()` — DRY. Inject `IValidator` tay thì mỗi endpoint phải lặp đoạn `ValidateAsync` + map lỗi. Filter còn dạy được **pipeline filter** của minimal API (một điểm phỏng vấn). Cái giá: một lớp gián tiếp (filter chạy trước handler), phải hiểu thứ tự.

**Vì sao `FluentValidation.AspNetCore` (auto-validation) KHÔNG dùng:** package `FluentValidation.AspNetCore` (auto-validation cũ) đã **deprecated** và hướng về MVC model binding, không hợp minimal API. Cách đúng cho minimal API .NET 10 là tự chạy validator (qua filter hoặc tay). Ta chỉ cần `FluentValidation` (core) + `FluentValidation.DependencyInjectionExtensions` (cho `AddValidatorsFromAssembly...`). Nguồn: [FluentValidation trong ASP.NET Core .NET 10 (codewithmukesh)](https://codewithmukesh.com/blog/fluentvalidation-in-aspnet-core/).

## 4.3. Dữ kiện đã xác minh

- **FluentValidation `12.1.1`**, license **Apache 2.0**. Hai package cần: `FluentValidation` (core: `AbstractValidator<T>`, `IValidator<T>`) và `FluentValidation.DependencyInjectionExtensions` (cho `AddValidatorsFromAssemblyContaining<T>()`/`AddValidatorsFromAssembly(...)`). Nguồn: [NuGet FluentValidation 12.1.1](https://www.nuget.org/packages/fluentvalidation/), [License Apache 2.0](https://github.com/FluentValidation/FluentValidation/blob/main/License.txt), [Installation docs](https://docs.fluentvalidation.net/en/latest/installation.html).
- **Luôn `await validator.ValidateAsync(model)`**, **không** `Validate()` sync — nếu validator có luật async mà gọi sync sẽ ném `AsyncValidatorInvokedSynchronouslyException`. `ValidateAsync` trả `ValidationResult` có `IsValid` (bool) + `Errors` (danh sách `ValidationFailure`). Nguồn: [FluentValidation .NET 10 minimal API (codewithmukesh)](https://codewithmukesh.com/blog/fluentvalidation-in-aspnet-core/).
- **`validationResult.ToDictionary()`** trả `IDictionary<string, string[]>` (field → mảng thông báo), khớp thẳng tham số của **`Results.ValidationProblem(...)`** (namespace `Microsoft.AspNetCore.Http`) — sinh ProblemDetails 400 với field mở rộng `errors`. Nguồn: [Minimal APIs responses (Results.ValidationProblem)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0).
- **`IEndpointFilter`** (namespace `Microsoft.AspNetCore.Http`): method `ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)`. Gắn lên route bằng `.AddEndpointFilter<T>()`. Nguồn: [Filters in Minimal API apps (aspnetcore-10.0)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/min-api-filters?view=aspnetcore-10.0).

## 4.4. Điểm xuất phát (code đang có)

- `Directory.Packages.props` chưa có FluentValidation.
- `Identity.Application` có `RegisterRequest(string Email, string Password)` (trong `Authentication/AuthService.cs`, cùng file các DTO). Chưa validator nào.
- `Modularity` (sau Bước 2) đã có `ResultExtensions`; chưa có filter.
- `IdentityModule.ConfigureServices` hiện chỉ gọi `services.AddInfrastructure(configuration)`. Còn `IdentityModule.MapEndpoints` **không** khai route trực tiếp — nó uỷ quyền cho `IdentityCoreEndpoints.MapEndpoints(endpoints)` (file `Api/Endpoints/IdentityCoreEndpoints.cs`, Day 4 đã tách ra). Route `/identity/register` (dạng cũ, sẽ refactor ở Bước 5) nằm trong file đó, cạnh `/login`, `/refresh`, `/logout`.
- Bước này **thêm**: package, `RegisterRequestValidator.cs`, `ValidationFilter.cs`, đăng ký validator, gắn filter. **Chưa** đổi thân handler register (Bước 5 làm).

## 4.5. Bản đồ thi công

### Sơ đồ trace một request `/register` (sau khi gắn filter)

```mermaid
flowchart TD
    A["POST /identity/register { email, password }"] --> B["ValidationFilter&lt;RegisterRequest&gt;.InvokeAsync"]
    B --> C[resolve IValidator từ RequestServices]
    C --> D[lấy RegisterRequest từ context.Arguments]
    D --> E["await validator.ValidateAsync(request)"]
    E --> F{IsValid?}
    F -->|false| G["Results.ValidationProblem(result.ToDictionary())<br/>DỪNG — 400, handler KHÔNG chạy"]
    F -->|true| H["await next(context)"]
    H --> I["handler thật của endpoint<br/>(Bước 5: gọi AuthService, trả Result)"]
```

**Ranh giới cốt tử:** khi validation fail, **handler không chạy** — service không hề thấy request rác. Filter trả `IResult` ngay, request dừng tại đây.

### Thứ tự build

1. Package (CPM + reference) → build xanh.
2. `RegisterRequestValidator` (Application) → build xanh.
3. `ValidationFilter<T>` (Modularity) → build xanh.
4. Đăng ký validator + gắn filter (Api) → build xanh → verify.

### Mảnh 1 — Package

- **CPM:** trong `Directory.Packages.props` thêm hai `PackageVersion`: `FluentValidation` `12.1.1` và `FluentValidation.DependencyInjectionExtensions` `12.1.1`.
- **Reference:**
  - `EventHub.Identity.Application` → `FluentValidation` (validator kế thừa `AbstractValidator<T>`).
  - `EventHub.Modularity` → `FluentValidation` (filter dùng `IValidator<T>`).
  - Nơi gọi `AddValidatorsFromAssembly...` → `FluentValidation.DependencyInjectionExtensions`. Vì ta đăng ký trong `IdentityModule.ConfigureServices` (project `Identity.Api`), thêm reference DI-extensions vào **`Identity.Api`**. (`Identity.Api` đã reference `Identity.Application` nên thấy được assembly chứa validator.)
- **Không** version ở `.csproj` (CPM lo), chỉ `PackageReference` trần.

### Mảnh 2 — `RegisterRequestValidator` (Application)

- Chữ ký: `public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>`.
- File: `RegisterRequestValidator.cs` ở `src/Modules/Identity/EventHub.Identity.Application/Authentication/` — **cạnh `RegisterRequest`**. Vì sao ở Application: validator validate một DTO của Application, và luật input là mối lo của tầng use-case, không phải Infrastructure. Nó **không** đụng DB (đó là luật của service), chỉ luật hình dạng.
- Trong constructor, dùng fluent API:
  - `RuleFor(x => x.Email).NotEmpty().EmailAddress()` — không rỗng + đúng định dạng email.
  - `RuleFor(x => x.Password).NotEmpty().MinimumLength(8)` — không rỗng + tối thiểu 8 ký tự.
  - Kèm `.WithMessage("...")` tiếng Việt nếu muốn thông báo thân thiện.
- **Quyết định của bạn — luật password mạnh tới đâu?** ASP.NET Core Identity **đã** có password policy (hoa/thường/số/ký tự đặc biệt, ≥ 6) chặn ở tầng `UserManager.CreateAsync` (Day 4). Vậy validator nên lặp lại luật đó, hay chỉ chặn thô (rỗng/quá ngắn) và để Identity lo luật chi tiết? Mentor khuyến nghị **validator chỉ chặn thô** (không rỗng, độ dài tối thiểu) để bắt lỗi hiển nhiên sớm với thông báo đẹp; **không** nhân đôi toàn bộ password policy (kẻo hai nơi luật lệch nhau). Luật phức tạp (đủ loại ký tự) để Identity — nhưng khi đó lỗi Identity trả về là lỗi *nghiệp vụ* (Bước 5 map qua `Result`), không phải validation. Ghi rõ ranh giới này để không lẫn.

### Mảnh 3 — `ValidationFilter<T>` (Modularity)

- Chữ ký: `public sealed class ValidationFilter<T> : IEndpointFilter where T : class`.
- File: `ValidationFilter.cs` ở `src/Shared/EventHub.Modularity/` — cạnh `ResultExtensions`, vì nó là hạ tầng web dùng chung mọi module (không riêng Identity).
- Method: `public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)`.
  - **Bước con 1 — resolve validator:** `var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();`. Dùng `GetService` (trả `null` nếu không đăng ký) **không** `GetRequiredService` — endpoint gắn filter mà quên đăng ký validator thì **cho đi tiếp** (`return await next(context)`) thay vì nổ. (Quyết định phòng thủ: thiếu validator = không validate, không phải crash. Đánh đổi: quên đăng ký sẽ *âm thầm* không validate — cân nhắc log cảnh báo.)
  - **Bước con 2 — lấy model:** `var model = context.Arguments.OfType<T>().FirstOrDefault();`. Nếu `null` → request thiếu body kiểu `T`: trả `Results.Problem(statusCode: 400, detail: "Thiếu request body.")`.
  - **Bước con 3 — validate:** `var result = await validator.ValidateAsync(model);`. `IsValid == false` → `return Results.ValidationProblem(result.ToDictionary());` (**DỪNG**, handler không chạy).
  - **Bước con 4 — hợp lệ:** `return await next(context);`.
- **Micro-gotcha kiểu trả:** `InvokeAsync` trả `object?`. Trả thẳng một `IResult` (`Results.ValidationProblem(...)`) là hợp lệ — framework hiểu. Không cần cast.
- **Micro-gotcha `where T : class`:** ràng buộc để `OfType<T>()` + `GetService<IValidator<T>>()` hoạt động với reference type (các request DTO đều là record/class).

### Mảnh 4 — Wiring (Api)

- **Đăng ký validator** trong `IdentityModule.ConfigureServices`: `services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();` — quét assembly `Identity.Application`, tự đăng ký **mọi** `AbstractValidator<T>` trong đó là `IValidator<T>` (Scoped). Vì sao assembly-scan trong module: validator mới (Events, Ticketing) tự được bắt khi thêm, không phải sửa Bootstrap; và **module tự lo** validator của mình (Bootstrap mỏng).
  - **Quyết định của bạn:** thay vì scan, có thể `services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();` từng cái — tường minh hơn nhưng phải nhớ thêm dòng mỗi validator mới. Mentor khuyến nghị **scan** (`AddValidatorsFromAssemblyContaining`) cho tiện.
- **Gắn filter** lên route trong `IdentityCoreEndpoints.MapEndpoints` (file `Api/Endpoints/IdentityCoreEndpoints.cs`, **không** phải `IdentityModule.cs`): sau `endpoints.MapPost("/identity/register", handler)`, nối `.AddEndpointFilter<ValidationFilter<RegisterRequest>>();`.
  - **Micro-gotcha:** `.AddEndpointFilter` gắn trên **route trả về** của `MapPost` (kiểu `RouteHandlerBuilder`). Nối chuỗi ngay sau `MapPost(...)` hoặc lưu biến rồi gọi.

### DI + lifetime

- Validator đăng ký **Scoped** (mặc định của `AddValidatorsFromAssembly...`). Đủ; validator ở đây stateless nhưng Scoped an toàn (và nếu validator sau này cần dịch vụ scoped như DbContext để check async thì lifetime đã đúng).
- `ValidationFilter<T>` **không** cần đăng ký riêng — `.AddEndpointFilter<T>()` tự dựng nó (activator), chỉ resolve `IValidator<T>` từ `RequestServices` lúc chạy.

### Naming rationale

- `RegisterRequestValidator` (không `RegisterValidator`): gắn tên với DTO nó validate (`RegisterRequest`), khớp convention `AbstractValidator<RegisterRequest>`, dễ tìm.
- `ValidationFilter<T>` generic: một filter cho mọi DTO, tên nói rõ vai trò (validate) + cơ chế (filter).

## 4.6. Ba bẫy dễ dính nhất

1. **Gắn filter nhưng quên đăng ký validator** (hoặc ngược lại). Filter dùng `GetService` nên thiếu validator sẽ **âm thầm bỏ qua** validate — request rác lọt vào service. Kiểm: gửi email sai định dạng phải ra 400; nếu ra 409/200 thì validator chưa được đăng ký/quét.
2. **Dùng `Validate()` sync thay `ValidateAsync()`.** Nếu (về sau) thêm luật async, sync sẽ ném `AsyncValidatorInvokedSynchronouslyException`. Luôn `await ValidateAsync`.
3. **Nhân đôi toàn bộ password policy ở validator và Identity.** Hai nơi luật dễ lệch (validator đòi ≥ 8, Identity đòi ≥ 6 + ký tự đặc biệt). Chọn ranh giới rõ: validator chặn thô, Identity lo luật chi tiết (lỗi Identity thành `Result` nghiệp vụ ở Bước 5).

## 4.7. Kiểm chứng

```bash
dotnet build EventHub.slnx
dotnet run --project src/Bootstrap/EventHub.Api
```

```bash
# email sai định dạng + password rỗng -> 400 ProblemDetails có errors
curl -i -X POST http://localhost:5xxx/identity/register \
  -H "Content-Type: application/json" \
  -d '{"email":"khong-phai-email","password":""}'

# hợp lệ -> đi qua filter, tới handler (Bước 5 quyết kết quả; giờ có thể vẫn là 200/409 theo code cũ)
curl -i -X POST http://localhost:5xxx/identity/register \
  -H "Content-Type: application/json" \
  -d '{"email":"valid@eventhub.local","password":"Passw0rd!"}'
```

Kỳ vọng:

- Request rác → **400**, `Content-Type: application/problem+json`, body có field `errors` liệt kê `email`/`password` với thông báo.
- Request hợp lệ về hình dạng → **không** bị filter chặn (đi tới handler). Kết quả cuối cùng (200/409) do code register — Bước 5 sẽ chuẩn hóa.

Tắt host (`Ctrl+C`).

## 4.8. Cạm bẫy thường gặp

- **`AddEndpointFilter` gắn nhầm chỗ.** Phải gắn trên `RouteHandlerBuilder` của **đúng** route register, không phải một route khác. Kiểm 400 chỉ xảy ra ở `/register`.
- **Thứ tự filter vs binding.** Filter chạy **sau** khi minimal API bind body thành `RegisterRequest` — nên `context.Arguments` đã có object để lấy. Nếu body JSON hỏng hoàn toàn (không parse được), lỗi bind xảy ra trước filter (400 khác). Đó là hành vi đúng, không phải bug.
- **Validator đụng DB để check "email tồn tại".** Đừng. "Email đã tồn tại" cần DB và là lỗi **nghiệp vụ** (409), thuộc `IdentityService` + `Result` (Bước 5), không phải validation (400). Validator chỉ lo hình dạng. Trộn vào là kéo DB lên tầng validation input.
- **Thông báo lỗi lộ quá nhiều.** `.WithMessage` nên hữu ích nhưng đừng tiết lộ luật nội bộ nhạy cảm. Với register, thông báo hình dạng email/password là an toàn.

## 4.9. Góc kể khi phỏng vấn

*"Tôi validate input bằng FluentValidation qua một `ValidationFilter<T>` generic implement `IEndpointFilter` — viết một lần, gắn `.AddEndpointFilter<ValidationFilter<X>>()` lên bất kỳ endpoint nào. Filter chạy **trước** handler, sai thì trả `Results.ValidationProblem` 400 và handler không bao giờ chạy, nên service chỉ nhận input đã sạch hình dạng. Tôi phân ranh rõ: luật *hình dạng* (email đúng định dạng, password không rỗng) là validation ở filter; luật cần DB như 'email đã tồn tại' là nghiệp vụ, đi qua `Result` và ra 409 — không trộn hai cái. FluentValidation là Apache 2.0 nên không dính làn sóng thương mại hóa; tôi cũng không dùng package auto-validation cũ vì nó deprecated và hợp MVC hơn minimal API."*

## 4.10. Link tài liệu chính thức

- [FluentValidation — Installation & docs](https://docs.fluentvalidation.net/en/latest/installation.html)
- [FluentValidation trong ASP.NET Core .NET 10 (codewithmukesh)](https://codewithmukesh.com/blog/fluentvalidation-in-aspnet-core/)
- [Filters in Minimal API apps (IEndpointFilter, aspnetcore-10.0)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/min-api-filters?view=aspnetcore-10.0)
- [Create responses (Results.ValidationProblem)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0)

## 4.11. Xong bước này khi

- [x] `FluentValidation` + `FluentValidation.DependencyInjectionExtensions` `12.1.1` trong CPM; reference đúng nơi (Application, Modularity, Api); không version ở `.csproj`.
- [x] `RegisterRequestValidator : AbstractValidator<RegisterRequest>` ở `Identity.Application`, chỉ luật hình dạng (không DB).
- [x] `ValidationFilter<T> : IEndpointFilter` ở `Modularity`, dùng `ValidateAsync`, `GetService` (không nổ khi thiếu validator).
- [x] `AddValidatorsFromAssemblyContaining<RegisterRequestValidator>()` trong `IdentityModule.ConfigureServices`; `.AddEndpointFilter<ValidationFilter<RegisterRequest>>()` trên route register.
- [x] Email/password sai hình dạng → 400 ProblemDetails có `errors`, handler không chạy.
- [x] `dotnet build` xanh.

→ Sang [Bước 5. Refactor register sang `Result` + ProblemDetails](05-refactor-register.md).
