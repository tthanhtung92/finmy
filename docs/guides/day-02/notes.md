# Ghi chú & đính chính sau review Day 2

> Ba khái niệm dưới đây là phần dễ hiểu *mơ hồ* nhất Day 2 (lộ ra khi quiz + review code). Bản ghi này chốt cách hiểu **đúng cơ chế** — chính chỗ này mới là điểm "kể được khi phỏng vấn", không phải chỉ "làm xong".

---

## Ghi chú 1 — Migration rỗng vẫn chứng minh pipeline: `database update` làm gì?

**Hiểu sai thường gặp:** "migration `Up`/`Down` rỗng thì `dotnet ef database update` không làm gì cả."

**Đúng cơ chế:** ngay cả khi `Up()` rỗng (không `CREATE TABLE` nghiệp vụ nào), lần `database update` đầu tiên vẫn:

1. Kết nối Postgres bằng connection string → **tạo bảng hệ thống `__EFMigrationsHistory`** (nếu chưa có).
2. **Insert một dòng** ghi id migration (vd `20260702052136_InitialCreate`) vào bảng đó — đánh dấu "đã áp", để lần sau không áp trùng.

Vậy **căn cứ** khẳng định "pipeline EF chạy thông" **không** phải "có bảng nghiệp vụ", mà là: kết nối được DB + EF có quyền tạo bảng + cơ chế version migration hoạt động. Cả ba chứng minh qua chính `__EFMigrationsHistory`.

- **Kiểm chứng:** `SELECT * FROM "__EFMigrationsHistory";` ra đúng một dòng `InitialCreate`. Có bảng + có dòng = pipeline thông.

**Câu chốt khi phỏng vấn:** *"Migration rỗng vẫn chứng minh pipeline vì `database update` tạo `__EFMigrationsHistory` và ghi nhận migration đã áp — tức EF kết nối, xác thực quyền, và version hóa được schema. Entity nghiệp vụ để Day sau."*

---

## Ghi chú 2 — `IModule`: explicit list vs reflection, và nó "Open/Closed" tới đâu

**Vế đúng:** pattern `IModule` + `AddModules`/`UseModules` đóng gói "module đăng ký service gì + map endpoint gì" vào **chính module**, thay vì rải lời gọi `AddX()`/`MapX()` khắp `Program.cs` của host. Gần với **Open/Closed** (mở rộng bằng thêm module) + **Single Responsibility** (mỗi module tự lo phần mình).

**Đính chính nuance (dễ nói quá lời):** mức "không sửa gì khi thêm module" **tùy cách host tìm module**:

- **Explicit list (đang dùng):** host giữ một registry tường minh (`[new IdentityModule(), ...]`). Thêm module = **thêm đúng 1 dòng** vào registry — không phải "zero sửa", mà là *sửa một chỗ tập trung, nhỏ, đã biết trước*.
- **Reflection scan:** quét assembly tìm mọi `IModule` → thêm module = **0 sửa** ở host (Open/Closed "thuần"). Đổi lại: khó trace, và dính lỗ **assembly chưa được nạp thì quét ra rỗng** (chính bug gặp trong ngày — module không map được endpoint, 404 âm thầm, không lỗi).

**Trade-off đã chọn:** explicit — chấp nhận thêm 1 dòng mỗi module để lấy tính **traceable** và tránh lỗi assembly chưa nạp.

**Câu chốt khi phỏng vấn:** *"Mỗi module hiện thực `IModule` tự khai service + endpoint; host chỉ thêm module vào một registry tập trung. Tôi chọn explicit thay vì reflection để trace được và tránh lỗi assembly chưa nạp — chấp nhận thêm 1 dòng khi có module mới."*

> Bẫy đã gặp: `IEndpointRouteBuilder` **không phải service DI** → không `GetRequiredService` được. `WebApplication` *tự nó* implement `IEndpointRouteBuilder`, nên truyền thẳng `app` vào `MapEndpoints`.

---

## Ghi chú 3 — Connection string: runtime vs design-time là hai đường khác nhau

DbContext cần connection string ở **hai thời điểm tách biệt**, đọc từ **hai nơi**:

- **Runtime (host chạy):** DI của `EventHub.Api` đọc `ConnectionStrings:IdentityDb` từ config (User Secrets ở dev) → `AddDbContext(... UseNpgsql(...))`.
- **Design-time (`dotnet ef`):** chạy *ngoài* host, không có DI runtime → `IDesignTimeDbContextFactory` tự dựng config (đọc cùng User Secrets qua `UserSecretsId`) để `dotnet ef` khởi tạo được context.

Cùng đọc một kho User Secrets vì factory hardcode đúng `UserSecretsId` của host. `appsettings.json` để `IdentityDb=""` (không commit secret).

**Nợ kỹ thuật còn treo (chưa chặn Day 2):** guard hiện dùng `?? throw`. Nhưng `GetConnectionString` trả `""` (không null) khi key có mà rỗng → `?? throw` **không bắn**, `""` lọt xuống `UseNpgsql("")` chết muộn với lỗi khó đọc. Nên đổi sang kiểm `string.IsNullOrWhiteSpace(...)` (cả DI runtime lẫn factory) để fail sớm, rõ — quan trọng khi clone máy mới chưa set secret.

**Câu chốt khi phỏng vấn:** *"Runtime lấy connection string qua DI của host; design-time `dotnet ef` không có DI nên cần `IDesignTimeDbContextFactory` tự dựng config — hai đường khác nhau, cùng trỏ một kho secret."*
