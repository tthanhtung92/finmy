# Bước 1. `Result`, `Result<T>`, `Error`, `ErrorType` trong SharedKernel

> Mục tiêu: dựng bốn type nền của Result pattern trong `EventHub.SharedKernel`, **thuần miền, không một dòng ASP.NET**. Cuối bước: mọi tầng gọi được `Result.Success()`, `Result.Failure(error)`, `Result<Guid>.Success(id)`, và đọc `IsSuccess`/`Error`. Chưa map sang HTTP (Bước 2).
>
> Nhắc: đây là code C#, **bạn tự gõ**. Mentor cho chữ ký đầy đủ + chỗ đặt + bẫy; bạn viết thân.

---

## 1.1. Cái gì

Bốn type, trong project `src/Shared/EventHub.SharedKernel`:

1. **`enum ErrorType`** — phân loại lỗi nghiệp vụ ở mức trung tính miền (Validation, NotFound, Conflict, Unauthorized, Failure). Đây là thứ Bước 2 sẽ map sang HTTP status; nhưng bản thân enum **không** biết HTTP.
2. **`record Error`** — một lỗi: `Code` (định danh máy đọc), `Description` (mô tả người đọc), `Type` (`ErrorType`). Kèm một `Error.None` đại diện "không lỗi".
3. **`class Result`** — kết quả một thao tác **không trả giá trị**: `IsSuccess`/`IsFailure` + `Error`. Có factory `Success()` / `Failure(error)`.
4. **`class Result<T>`** — kết quả một thao tác **có trả giá trị** kiểu `T` khi thành công: thêm property `Value`. Có implicit conversion để viết gọn.

Quan hệ bốn type (`Result<T>` kế thừa `Result`; `Result` giữ một `Error`; `Error` mang một `ErrorType`):

```mermaid
classDiagram
    class ErrorType {
        <<enum>>
        Failure
        Validation
        NotFound
        Conflict
        Unauthorized
    }
    class Error {
        <<record>>
        +string Code
        +string Description
        +ErrorType Type
        +Error None$
    }
    class Result {
        +bool IsSuccess
        +bool IsFailure
        +Error Error
        +Success() Result$
        +Failure(Error) Result$
    }
    class Result~T~ {
        +T Value
        +Success(T) Result~T~$
        +Failure(Error) Result~T~$
    }
    Result <|-- Result~T~ : kế thừa
    Result --> Error : giữ
    Error --> ErrorType : phân loại
```

## 1.2. Vì sao

**Vì sao tách `Error` thành record riêng, không chỉ để một `string message`:** một `string` mất thông tin phân loại. Endpoint cần biết lỗi này nên ra 409 hay 404 — thông tin đó nằm ở `Error.Type`, không suy được từ một câu mô tả. `Code` (vd `"Identity.DuplicateEmail"`) cho client/log đối chiếu ổn định kể cả khi `Description` đổi lời. Ba field phục vụ ba người đọc: `Type` cho tầng web chọn status, `Code` cho máy, `Description` cho người.

**Vì sao `Result` bất biến và tự mâu thuẫn thì ném ngay:** một `Result` "thành công nhưng mang `Error`" hoặc "thất bại nhưng `Error` rỗng" là **trạng thái vô nghĩa** — nếu để dựng được, bug sẽ trốn tới tận chỗ đọc. Ép bất biến trong constructor (`success == true` **buộc** `error == Error.None`, và ngược lại; sai thì `throw`) khiến sai lệch **nổ ngay lúc tạo**, ngay dòng gây ra nó, không phải ba tầng sau. Đây là nguyên tắc "make illegal states unrepresentable".

**Vì sao `Result<T>` mà đọc `Value` lúc failure thì nên chặn:** khi thất bại, `Value` không có nghĩa. Cho `Value` ném (hoặc ít nhất tài liệu hóa là không được đọc) khi `IsFailure` giúp bắt lỗi caller quên check `IsSuccess`. Mentor khuyến nghị **ném `InvalidOperationException`** khi đọc `Value` của một failure.

**Vì sao implicit conversion:** cho phép viết `return user.Id;` (T → `Result<T>` thành công) và `return someError;` (`Error` → `Result` thất bại) thay vì `Result<Guid>.Success(user.Id)` dài dòng ở mọi chỗ. Đây là đường tắt cú pháp phổ biến trong Result pattern, làm code service đọc tự nhiên. Nhưng nó là **tùy chọn** — nếu thấy rối, cứ dùng factory tường minh.

## 1.3. Dữ kiện đã xác minh

- `EventHub.SharedKernel.csproj` hiện là plain `<Project Sdk="Microsoft.NET.Sdk">`, **không** `FrameworkReference`. Đây là chủ ý: SharedKernel là primitive miền dùng chung, phải không lệ thuộc framework web. Bước này **giữ nguyên** điều đó — không thêm reference nào.
- ROADMAP mục 3 liệt kê `Result<T>` là nội dung của `EventHub.SharedKernel` (cùng `DomainEvent base`, guards). Đặt đúng chỗ đã định.
- Result pattern chuẩn ngành .NET: `Error(Code, Description, Type)` + enum loại lỗi + `Result`/`Result<T>` với factory và implicit conversion. Nguồn: [Milan Jovanović — Result pattern](https://milanjovanovic.tech/blog/functional-error-handling-in-dotnet-with-the-result-pattern).

## 1.4. Điểm xuất phát (code đang có)

- `EventHub.SharedKernel` hiện **rỗng** (chỉ có csproj + thư mục obj sinh khi build). Không có file `.cs` thật nào. Bước này thêm bốn type mới, không sửa gì có sẵn.
- Các project khác chưa reference type nào của SharedKernel (vì chưa có gì để reference). Sau Bước 5, `Identity.Application`/`Infrastructure` sẽ dùng `Result`; reference project tới `SharedKernel` cần được thêm khi tới đó (kiểm ở Bước 5). Bước 1 chỉ lo dựng type + build xanh nội bộ SharedKernel.

## 1.5. Bản đồ thi công

### Thứ tự build (từ trong ra ngoài)

Build theo phụ thuộc: `ErrorType` (không phụ thuộc ai) → `Error` (dùng `ErrorType`) → `Result` (dùng `Error`) → `Result<T>` (kế thừa/ghép `Result`). **Build xanh sau mỗi mảnh** mới sang mảnh sau.

### Mảnh 1 — `enum ErrorType`

- Chữ ký: `enum ErrorType { Failure, Validation, NotFound, Conflict, Unauthorized }`.
- File: `ErrorType.cs` ở gốc project `EventHub.SharedKernel` (gợi ý thư mục `Results/` để gom nhóm). Namespace gợi ý `EventHub.SharedKernel.Results`.
- **`Failure` để đầu (giá trị 0)** làm mặc định trung tính: một lỗi không rõ loại rơi về `Failure` (Bước 2 map sang 500). Đừng để `Validation` ở vị trí 0 kẻo lỗi vô loại thành 400 sai.
- **Quyết định của bạn:** có thêm `Forbidden` (403, "đã đăng nhập nhưng thiếu quyền") không? Mentor khuyến nghị **năm giá trị trên là đủ cho toàn project**; thêm `Forbidden` khi Ticketing thật sự cần phân biệt 401 vs 403 ở tầng nghiệp vụ. Đừng thêm giá trị "cho đủ bộ" rồi không dùng.

### Mảnh 2 — `record Error`

- Chữ ký: `record Error(string Code, string Description, ErrorType Type)`. Dùng **positional record** cho gọn và bất biến sẵn.
- Thêm hai `static readonly Error` tiện dụng:
  - `Error.None` = `new("", "", ErrorType.Failure)` — đại diện "không có lỗi", dùng cho `Result` thành công (Mảnh 3 cần một `Error` để gán khi success, `Error.None` là nó). Vì sao cần: `Result.Error` không nên là `null` (bắt caller null-check), một `Error.None` sentinel sạch hơn.
  - (Tùy chọn) `Error.NullValue` cho trường hợp trả `Result<T>` thành công nhưng `T` là null — biên hiếm, có thể bỏ qua ở Day 5.
- File: `Error.cs` cùng thư mục `Results/`.
- **Micro-gotcha:** `Error.None` **không** phải "lỗi loại None" — enum không có giá trị `None`. Nó là một `Error` rỗng dùng làm chỗ giữ khi *không* có lỗi. Đừng nhầm thêm một `ErrorType.None`.

### Mảnh 3 — `class Result`

- File: `Result.cs` cùng thư mục.
- **Constructor `protected`** nhận `(bool isSuccess, Error error)` và **ép bất biến** trong đó:
  - Nếu `isSuccess == true` **và** `error != Error.None` → `throw new InvalidOperationException(...)` ("success không được mang Error").
  - Nếu `isSuccess == false` **và** `error == Error.None` → `throw new InvalidOperationException(...)` ("failure phải có Error").
  - Hợp lệ thì gán `IsSuccess = isSuccess`, `Error = error`.
  - Để `protected` (không `public`) vì caller phải đi qua factory `Success`/`Failure`, không tự `new Result(true, someError)` phá bất biến.
- Property: `bool IsSuccess { get; }`, `bool IsFailure => !IsSuccess`, `Error Error { get; }`.
- Factory tĩnh:
  - `static Result Success()` → `new(true, Error.None)`.
  - `static Result Failure(Error error)` → `new(false, error)`.
  - (Chuẩn bị cho `Result<T>`) `static Result<T> Success<T>(T value)` và `static Result<T> Failure<T>(Error error)` — hoặc để `Result<T>` tự có factory riêng (Mảnh 4). Chọn một, đừng làm cả hai kẻo trùng.

### Mảnh 4 — `class Result<T> : Result`

- File: `Result{T}.cs` (đặt tên file `ResultT.cs` hoặc `Result.Generic.cs` cho khỏi vướng ký tự `<>` trên một số hệ thống; nội dung là `Result<T>`).
- Kế thừa `Result`. Constructor `private`/`protected` nhận `(T? value, bool isSuccess, Error error)`, gọi `base(isSuccess, error)`, lưu `value` vào field.
- Property `T Value` với getter: nếu `IsFailure` → `throw new InvalidOperationException("Không đọc được Value của một Result thất bại")`; ngược lại trả field.
- Factory:
  - `static Result<T> Success(T value)` → `new(value, true, Error.None)`.
  - `static Result<T> Failure(Error error)` → `new(default, false, error)`.
- **Implicit conversion (tùy chọn, khuyến nghị):**
  - `public static implicit operator Result<T>(T value)` → `Success(value)` — cho `return user.Id;`.
  - `public static implicit operator Result<T>(Error error)` → `Failure(error)` — cho `return someError;`.
  - **Micro-gotcha:** nếu `T` chính là `Error` (hiếm), hai operator đâm nhau nhập nhằng. Trong project này `T` là `Guid`/DTO nên an toàn; cứ biết để không ngạc nhiên.

### DI

Bước này **không** đăng ký DI gì. `Result`/`Error` là **value/kiểu dữ liệu**, không phải service — không vào container. Đừng `AddScoped<Result>` (vô nghĩa).

### Naming rationale

- `ErrorType` (không `ResultType`/`ErrorKind`): nó phân loại `Error`, tên gắn với `Error`. `Type` là property của `Error` đọc tự nhiên `error.Type`.
- `IsFailure` là property tính từ `IsSuccess` (`=> !IsSuccess`), không phải field riêng — một nguồn sự thật, không thể lệch nhau.

## 1.6. Ba bẫy dễ dính nhất

1. **Vô tình kéo ASP.NET vào SharedKernel.** Nếu bạn thấy mình cần `using Microsoft.AspNetCore...` trong file nào ở đây → **dừng lại**, bạn đang đặt sai tầng. `Result` chỉ biết `Error`/`ErrorType`, không biết status code. Mọi thứ HTTP để Bước 2.
2. **Constructor `public` + không ép bất biến.** Nếu để `new Result(true, someError)` dựng được, một "success mang lỗi" sẽ trốn tới chỗ đọc. Constructor `protected` + ném khi mâu thuẫn là cả điểm của type này.
3. **`Error` để `null` thay vì `Error.None`.** `Result` thành công gán `Error = null` khiến mọi chỗ đọc `result.Error.Type` phải null-check hoặc nổ `NullReferenceException`. Dùng sentinel `Error.None`.

## 1.7. Kiểm chứng

```bash
dotnet build EventHub.slnx
```

- Build xanh.
- Kiểm bằng mắt: `EventHub.SharedKernel.csproj` **vẫn** không có `FrameworkReference`/`PackageReference` ASP.NET nào. Nếu IDE tự thêm `using` lạ, gỡ.
- (Tùy chọn, khuyến khích) viết nhanh một đoạn thử trong một test/scratch: `Result.Success().IsSuccess` là `true`; `Result.Failure(new Error("x","y",ErrorType.Conflict)).IsFailure` là `true`; thử `new`/factory tạo một "success mang Error" phải thấy nó **ném** — chứng minh bất biến hoạt động. (Chưa có project test riêng cho SharedKernel ở Day 5 thì để ý tưởng này lại cho ngày viết test.)

## 1.8. Cạm bẫy thường gặp

- **Trộn `Result` (không giá trị) và `Result<T>` (có giá trị) tùy tiện.** Hàm không trả gì (vd `LogoutAsync`) dùng `Result`; hàm trả giá trị (vd `RegisterUserAsync` trả `userId`) dùng `Result<Guid>`. Đừng ép mọi thứ thành `Result<T>` với `T = bool`.
- **`Error.Code` đặt tùy hứng, không nhất quán.** Chọn một quy ước ngay: gợi ý `"<Module>.<Lý do>"`, vd `"Identity.DuplicateEmail"`. Ổn định `Code` để log/monitoring đối chiếu được; đừng đổi liên tục.
- **So `Error` bằng tham chiếu.** `record` so bằng **giá trị** (mọi field bằng nhau) — đây là lý do dùng `record` cho `Error`, nên `error == Error.None` so đúng. Nếu lỡ dùng `class` thường cho `Error`, `==` thành so tham chiếu, bất biến ở Mảnh 3 sẽ sai. Giữ `Error` là `record`.
- **Quên `T Value` chặn khi failure.** Nếu getter cứ trả field kể cả lúc failure, caller quên check `IsSuccess` sẽ nhận `default(T)` (null/`Guid.Empty`) âm thầm thay vì lỗi rõ. Cho nó ném.

## 1.9. Góc kể khi phỏng vấn

*"Tôi tách lỗi nghiệp vụ khỏi exception bằng Result pattern. `Error` là record gồm `Code`, `Description`, và một `ErrorType` trung tính miền — chính `Type` cho tầng web sau này chọn status, còn bản thân Result mù HTTP nên tôi đặt nó ở SharedKernel không lệ thuộc ASP.NET. `Result` ép bất biến trong constructor: một 'success mang Error' hay 'failure không Error' ném ngay lúc dựng, nên trạng thái vô nghĩa không tồn tại được. Với `Result<T>`, đọc `Value` khi thất bại cũng ném, bắt caller quên check `IsSuccess`."*

## 1.10. Link tài liệu chính thức

- [Milan Jovanović — Functional Error Handling With the Result Pattern](https://milanjovanovic.tech/blog/functional-error-handling-in-dotnet-with-the-result-pattern)
- [The Result Pattern in ASP.NET Core Minimal APIs (Simple Talk)](https://www.red-gate.com/simple-talk/development/dotnet-development/the-result-pattern-in-asp-net-core-minimal-apis/)
- [record types (C# language reference)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)
- [User-defined conversion operators (implicit/explicit)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/user-defined-conversion-operators)

## 1.11. Xong bước này khi

- [x] `ErrorType`, `Error`, `Result`, `Result<T>` nằm trong `EventHub.SharedKernel`; csproj vẫn không ASP.NET.
- [x] Dựng "success mang Error" hoặc "failure không Error" → **ném** lúc tạo.
- [x] `Result<T>.Value` đọc khi `IsFailure` → ném.
- [x] `Error.None` là sentinel cho success (không dùng `null`); `Error` là `record` (so bằng giá trị).
- [x] `dotnet build` xanh.

→ Sang [Bước 2. Map `Result` → ProblemDetails trong Modularity](02-map-problem-details.md).
