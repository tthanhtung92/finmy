# Bước 5. Refactor `/identity/register` sang `Result` + ProblemDetails

> Mục tiêu: thay cụm ad-hoc `RegisterOutcome` + enum `RegisterFailureReason` + `switch` tay bằng `Result<Guid>` chảy suốt từ `IdentityService` → `AuthService` → endpoint, rồi map ra HTTP bằng `ToProblemDetails`/`Match` (Bước 2). Cuối bước: email trùng → **409 ProblemDetails**, đăng ký thành công → **200/201**, không còn enum ad-hoc.
>
> Nhắc: code C#, bạn tự gõ. Đây là bước **sửa code có sẵn** — đọc kỹ "điểm xuất phát".

---

## 5.1. Cái gì

Đổi ba tầng dùng chung một ngôn ngữ lỗi (`Result`):

1. **`IIdentityService`** (Application): `RegisterUserAsync` đổi kiểu trả từ `Task<RegisterOutcome>` → `Task<Result<Guid>>`.
2. **`IdentityService`** (Infrastructure): map `IdentityResult` thất bại → `Error` (Conflict cho email trùng; Validation/Failure cho còn lại) thay vì `RegisterOutcome`.
3. **`AuthService`** (Application): `RegisterAsync` trả `Task<Result<Guid>>` (chuyển tiếp Result).
4. **Endpoint** (Api): `.Match(...)`/`ToProblemDetails` thay `switch` tay.
5. **Xóa** `RegisterOutcome` + `RegisterFailureReason` + extension `ToRegisterFailureReason` (nếu chỉ nó dùng).
6. (Tùy chọn — Quyết định của bạn) refactor nhánh lỗi `/login` (401) sang `Result`/ProblemDetails cho nhất quán.

## 5.2. Vì sao

**Vì sao bỏ `RegisterOutcome`:** nó là một `Result` "cây nhà lá vườn" chỉ dùng được cho register. Giờ đã có `Result<T>` chung + `ErrorType` chung + mapping chung, giữ `RegisterOutcome` là **hai hệ lỗi song song** — thừa và dễ lệch. Chuyển register sang `Result` khiến mọi endpoint tương lai nói cùng một ngôn ngữ lỗi, endpoint chỉ `Match` một dòng thay vì `switch` riêng.

**Vì sao map lỗi vẫn ở Infrastructure:** như Day 4 đã lập luận, `IdentityResult`/`IdentityError` là type **Infrastructure**. Việc đọc `Code` để quyết loại lỗi phải làm **trong Infra**, trả lên Application một `Error` (miền trung tính). Application/endpoint chỉ thấy `Result`/`Error`, **không** chạm `IdentityError` → không phá ranh giới. Ta chỉ đổi *đích* của phép map: trước ra `RegisterFailureReason`, giờ ra `Error` với `ErrorType`.

**Vì sao đa số lỗi password giờ là 400 do validator, không tới đây:** Bước 4 đã chặn password rỗng/quá ngắn ở filter (400). Lỗi còn lọt tới `CreateAsync` chủ yếu là **email trùng** (cần DB, filter không biết) → `Conflict` (409). Nếu Identity password policy chi tiết (đủ loại ký tự) vẫn chặn được gì đó, map về `Validation` (400) hoặc gộp về một `Error`. Ranh giới: validator lo hình dạng (trước), Identity lo phần cần DB/policy sâu (sau, thành `Result`).

## 5.3. Dữ kiện đã xác minh

- **`IdentityResult.Errors`** là `IEnumerable<IdentityError>`, mỗi cái có `Code` (string) + `Description`. `Code` = `nameof` method của `IdentityErrorDescriber` (vd `DuplicateEmail().Code == "DuplicateEmail"`). So bằng `nameof(IdentityErrorDescriber.DuplicateEmail)` (source of truth) + `==`/`Contains` ordinal, **không** `string.Contains`. Nguồn: [IdentityErrorDescriber (API)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.identityerrordescriber), [UserManager.CreateAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1.createasync). (Đây là kiến thức đã dùng ở Day 4 — xem [notes Day 4, Ghi chú 7](../day-04/notes.md).)
- `Result<Guid>` (Bước 1) + `ToProblemDetails`/`Match` (Bước 2) đã sẵn sàng.

## 5.4. Điểm xuất phát (code đang có)

Đọc kỹ — bước này sửa đúng các chỗ này:

- `Application/Authentication/IIdentityService.cs`: `RegisterUserAsync(string email, string password)` trả `Task<RegisterOutcome>`; file này **cũng** khai `record RegisterOutcome(bool Succeeded, Guid? UserId, RegisterFailureReason Reason, string[] Errors)` + `enum RegisterFailureReason { None, DuplicateEmail, WeakPassword, Unknown }` + `record RotatedRefreshToken(...)`.
- `Infrastructure/Authentication/IdentityService.cs`: impl `RegisterUserAsync` dựng `ApplicationUser`, gọi `CreateAsync`, map thành công → `RegisterOutcome(true, user.Id, None, [])`, thất bại → `RegisterOutcome(false, null, <reason>, errors)` qua extension `ToRegisterFailureReason` (ở Infra).
- `Application/Authentication/AuthService.cs`: `RegisterAsync(RegisterRequest request)` trả `Task<RegisterOutcome>`, chỉ gọi `RegisterUserAsync` và trả thẳng.
- `Api/IdentityModule.cs`: endpoint `/identity/register` gọi `svc.RegisterAsync(req)`, guard `outcome.Succeeded → Results.Ok()`, else `switch` trên `outcome.Reason` map `DuplicateEmail → Conflict`, `WeakPassword → ValidationProblem`, `_ → Problem`.
- **Reference:** kiểm `Identity.Application.csproj` — có reference `SharedKernel` chưa? Nhiều khả năng **chưa**. Cần thêm để Application thấy `Result`/`Error`/`ErrorType`. (Infra reference Application nên thấy transitively; Api reference Modularity nên thấy `Match`/`ToProblemDetails`.)

## 5.5. Bản đồ thi công

### Sơ đồ trace `/register` sau refactor

```mermaid
sequenceDiagram
    participant Endpoint as Endpoint (Api)
    participant Auth as AuthService (Application)
    participant Id as IdentityService (Infra)
    Note over Endpoint: đã qua ValidationFilter → hình dạng OK
    Endpoint->>Auth: RegisterAsync(req)
    Auth->>Id: RegisterUserAsync(email, password)
    Id->>Id: CreateAsync(user, password)
    alt Succeeded
        Id-->>Auth: Result~Guid~.Success(user.Id)
    else Fail
        Id->>Id: đọc IdentityResult.Errors → Error(Code, Description, ErrorType)
        Id-->>Auth: Result~Guid~.Failure(error)
    end
    Auth-->>Endpoint: Result~Guid~
    alt IsSuccess
        Endpoint-->>Endpoint: result.Match(id => Results.Ok(...))
    else IsFailure
        Endpoint-->>Endpoint: ToProblemDetails() → 409/400/500 ProblemDetails
    end
```

**Ranh giới cốt tử:** `IdentityError`/`IdentityResult` **chỉ** xuất hiện trong `IdentityService` (Infra). `AuthService` và endpoint thấy **chỉ** `Result<Guid>`/`Error`. Không leak type Identity lên Application/Api.

### Thứ tự build (sửa từ trong ra ngoài)

1. **Reference:** thêm ProjectReference `Identity.Application` → `SharedKernel` (nếu thiếu). Build xanh.
2. **Interface:** đổi kiểu trả `RegisterUserAsync` → `Task<Result<Guid>>`. Build sẽ **đỏ** ở impl + AuthService (đúng như mong đợi — compiler chỉ chỗ cần sửa).
3. **Impl Infra:** sửa `IdentityService.RegisterUserAsync` map sang `Result<Guid>`. Build xanh dần.
4. **AuthService:** đổi kiểu trả. Build xanh.
5. **Endpoint:** thay `switch` bằng `Match`/`ToProblemDetails`. Build xanh.
6. **Xóa** `RegisterOutcome`/`RegisterFailureReason` (+ extension nếu mồ côi). Build xanh → verify.

### Mảnh 1 — `IIdentityService.RegisterUserAsync`

- Đổi chữ ký: `Task<Result<Guid>> RegisterUserAsync(string email, string password)`.
- `Result<Guid>` từ `EventHub.SharedKernel` (thêm `using`).

### Mảnh 2 — `IdentityService.RegisterUserAsync` (Infra)

- Thành công (`result.Succeeded`): `return user.Id;` (implicit `Guid → Result<Guid>.Success`) **hoặc** `Result<Guid>.Success(user.Id)`.
- Thất bại: dựng một `Error` từ `IdentityResult`. Đổi extension `ToRegisterFailureReason` thành một extension mới, gợi ý `internal static Error ToError(this IdentityResult result)` (cùng file Infra):
  1. Nếu có code `DuplicateEmail`/`DuplicateUserName` → `new Error("Identity.DuplicateEmail", "Email đã được đăng ký.", ErrorType.Conflict)`.
  2. Nếu có code nhóm `Password*` (khi Identity policy chặn) → `new Error("Identity.WeakPassword", <gộp Description>, ErrorType.Validation)`.
  3. Còn lại → `new Error("Identity.RegisterFailed", <gộp Description hoặc câu chung>, ErrorType.Failure)`.
  - So code bằng `nameof(IdentityErrorDescriber.DuplicateEmail)` + `==`/`Array.Contains`, **không** `string.Contains`.
  - `return result.ToError();` → implicit `Error → Result<Guid>.Failure` (hoặc `Result<Guid>.Failure(error)`).
- **Micro-gotcha `Description`:** gộp `result.Errors.Select(e => e.Description)` cẩn thận — đừng đổ **mọi** description kỹ thuật vào `Error.Description` nếu nhạy cảm. Với register, description của Identity ("Email 'x' is already taken") khá an toàn, nhưng cân nhắc viết `Description` của riêng bạn (tiếng Việt, thân thiện) thay vì bê nguyên.

### Mảnh 3 — `AuthService.RegisterAsync`

- Đổi chữ ký: `Task<Result<Guid>> RegisterAsync(RegisterRequest request)`.
- Thân giữ nguyên tinh thần: `return await _identityService.RegisterUserAsync(request.Email, request.Password);` (giờ trả `Result<Guid>`).
- **Quyết định của bạn:** `Result<Guid>` (trả userId) hay `Result` (không giá trị)? Mentor khuyến nghị **`Result<Guid>`** — trả userId hữu ích cho response (`Results.Created($"/users/{id}", ...)`) và về sau auto-login/test. Nếu Day 4 bạn chốt register không trả gì thì `Result` cũng được; đổi cả chuỗi cho khớp.

### Mảnh 4 — Endpoint `/identity/register`

- Thay toàn bộ khối `if (outcome.Succeeded) ... switch ...` bằng:
  - `var result = await svc.RegisterAsync(req);`
  - `return result.Match(id => Results.Created($"/identity/users/{id}", new { userId = id }));` — hoặc `Results.Ok(new { userId = id })` nếu không muốn Location header.
- `Match` lo cả hai nhánh: thành công trả `Created/Ok`, thất bại tự `ToProblemDetails()` ra 409/400/500.
- Route vẫn giữ `.AddEndpointFilter<ValidationFilter<RegisterRequest>>()` (Bước 4).
- **Micro-gotcha `Created`:** `Results.Created(uri, value)` cần `uri` không rỗng; nếu chưa có endpoint GET user thì URI chỉ mang tính định danh, hoặc dùng `Results.Ok`.

### Mảnh 5 — Xóa cũ

- Xóa `record RegisterOutcome` + `enum RegisterFailureReason` khỏi `IIdentityService.cs` (giữ `RotatedRefreshToken` — refresh vẫn dùng).
- Xóa extension `ToRegisterFailureReason` (nếu không còn ai gọi). Nếu bạn đổi tên nó thành `ToError` thì xong luôn.
- Build: nếu còn chỗ nào tham chiếu `RegisterOutcome`/`RegisterFailureReason`, compiler đỏ → dọn nốt.

### Mảnh 6 (tùy chọn) — nhất quán `/login`

- **Quyết định của bạn:** `/login` hiện trả `Results.Unauthorized()` (body rỗng) khi sai credentials. Muốn nhất quán ProblemDetails, đổi `AuthService.LoginAsync` trả `Result<AuthResult>` với `Error("Identity.InvalidCredentials", "Email hoặc mật khẩu không đúng.", ErrorType.Unauthorized)` khi fail, endpoint `Match(...)`. Mentor khuyến nghị **có làm** cho toàn API một hình dạng lỗi — nhưng nếu muốn giữ scope Day 5 nhỏ, để `/login` lại và ghi nợ. **Giữ thông báo mơ hồ** (không phân biệt sai email vs sai mật khẩu) để không lộ user enumeration — nhắc lại từ Day 4.

### DI + lifetime

- **Không đổi** DI. `AuthService`/`IdentityService` vẫn Scoped như Day 4. Chỉ đổi kiểu trả, không đổi lifetime.

### Naming rationale

- Đổi `ToRegisterFailureReason` → `ToError`: đích map giờ là `Error` (chung) không phải một enum riêng register. Tên phản ánh đúng output.

## 5.6. Ba bẫy dễ dính nhất

1. **Quên thêm reference `Application → SharedKernel`.** Không có nó, `Result`/`Error` không nhìn thấy ở Application → build đỏ khó hiểu (type không tồn tại). Thêm ProjectReference trước.
2. **Leak `IdentityError` lên Application.** Nếu bạn map `IdentityResult` → `Error` ở **AuthService** thay vì IdentityService, Application phải thấy `IdentityResult` (type Infra) → phá ranh giới. Map **trong Infra**, trả lên `Result`.
3. **Xóa sót, để hai hệ lỗi song song.** Nếu quên xóa `RegisterOutcome` hoặc còn endpoint khác dùng nó, bạn có hai ngôn ngữ lỗi. Grep `RegisterOutcome`/`RegisterFailureReason` → phải **0** kết quả sau bước này.

## 5.7. Kiểm chứng

```bash
dotnet build EventHub.slnx
dotnet run --project src/Bootstrap/EventHub.Api
```

```bash
# đăng ký hợp lệ lần 1 -> 200/201 + userId
curl -i -X POST http://localhost:5xxx/identity/register \
  -H "Content-Type: application/json" \
  -d '{"email":"dup@eventhub.local","password":"Passw0rd!"}'

# đăng ký lại cùng email -> 409 ProblemDetails (Conflict)
curl -i -X POST http://localhost:5xxx/identity/register \
  -H "Content-Type: application/json" \
  -d '{"email":"dup@eventhub.local","password":"Passw0rd!"}'

# email sai hình dạng -> 400 ProblemDetails có errors (do ValidationFilter, chưa tới service)
curl -i -X POST http://localhost:5xxx/identity/register \
  -H "Content-Type: application/json" \
  -d '{"email":"bad","password":""}'
```

Kỳ vọng:

- Lần 1: `200`/`201`, body có `userId`.
- Lần 2 (trùng): `409`, `application/problem+json`, `detail` báo email đã đăng ký, có field mở rộng `errorCode` = `"Identity.DuplicateEmail"`.
- Sai hình dạng: `400`, body có `errors` từ FluentValidation.
- Grep repo: `RegisterOutcome`/`RegisterFailureReason` → 0 kết quả.

Tắt host (`Ctrl+C`).

## 5.8. Cạm bẫy thường gặp

- **Success trả `Value` mà quên implicit conversion.** Nếu chưa làm implicit operator (Bước 1), `return user.Id;` không compile — dùng `Result<Guid>.Success(user.Id)` tường minh.
- **`Match` truyền nhầm nhánh.** `Match(onSuccess)` chỉ nhận hàm cho **thành công**; nhánh lỗi tự `ToProblemDetails`. Đừng đưa logic lỗi vào `onSuccess`.
- **Đổi register nhưng quên route vẫn cần filter.** Sau khi viết lại `MapPost`, kiểm `.AddEndpointFilter<ValidationFilter<RegisterRequest>>()` vẫn còn — dễ xóa nhầm khi sửa.
- **Description lộ chi tiết.** Nếu bê nguyên `IdentityError.Description` vào `Error.Description` rồi ra client, cân nhắc câu tiếng Việt của bạn cho thân thiện + kiểm soát thông tin.

## 5.9. Góc kể khi phỏng vấn

*"Ban đầu register của tôi có một `RegisterOutcome` riêng với enum lỗi riêng và một `switch` map HTTP ngay trong endpoint. Khi có `Result<T>` chung, tôi refactor cả chuỗi register sang nói cùng ngôn ngữ lỗi: `IdentityService` map `IdentityResult` thất bại thành một `Error` với `ErrorType` — vẫn map **trong Infrastructure** để `IdentityError` không leak lên Application — rồi `Result<Guid>` chảy qua `AuthService` tới endpoint, endpoint chỉ `result.Match(id => Results.Created(...))`, nhánh lỗi tự thành ProblemDetails đúng status. Email trùng ra 409, còn input sai hình dạng thì đã bị `ValidationFilter` chặn 400 trước cả khi vào service. Kết quả: một ngôn ngữ lỗi, một hình dạng body, không còn `switch` ad-hoc nào."*

## 5.10. Link tài liệu chính thức

- [UserManager.CreateAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1.createasync)
- [IdentityErrorDescriber](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.identityerrordescriber)
- [Create responses (Results.Created / Results.Ok)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0)
- [Milan Jovanović — Result pattern (Match ở endpoint)](https://milanjovanovic.tech/blog/functional-error-handling-in-dotnet-with-the-result-pattern)

## 5.11. Xong bước này khi

- [x] `RegisterUserAsync` trả `Result<Guid>`; `AuthService.RegisterAsync` trả `Result<Guid>`.
- [x] Map `IdentityResult` → `Error` nằm **trong Infrastructure**; Application/Api không thấy `IdentityError`.
- [x] Endpoint dùng `Match`/`ToProblemDetails`, không còn `switch` ad-hoc.
- [x] `RegisterOutcome` + `RegisterFailureReason` đã xóa (grep = 0).
- [x] Register trùng email → 409 ProblemDetails; hợp lệ → 200/201; sai hình dạng → 400 (từ Bước 4).
- [x] `dotnet build` xanh.

→ Sang [Bước 6. Verify end-to-end & commit](06-verify-commit.md).
