# EventHub: Sổ tay Mentor (Hướng dẫn tự implement)

> Đây là khu vực **tài liệu học tập** đi kèm project EventHub. Mỗi ngày trong [ROADMAP](../ROADMAP.md) có một file hướng dẫn riêng, viết theo giọng **mentor**: giải thích khái niệm + lý do + việc cần làm + cách kiểm chứng + cạm bẫy, **không kèm code**. Bạn tự viết toàn bộ code.

## Cách làm việc theo Mentor mode

Quy tắc bất di bất dịch:

1. **Mentor không viết code.** Mọi dòng code là của bạn. Mentor giải thích, định hướng, và review.
2. **Hiểu rồi mới gõ.** Mỗi mục đều trả lời "cái gì / vì sao / làm thế nào kiểm chứng" trước khi bạn chạm bàn phím.
3. **Học được phải kể được.** Mỗi kỹ thuật đều có một "góc kể khi phỏng vấn": nếu chưa giải thích được cho người khác, coi như chưa xong.

Vòng lặp mỗi ngày:

```mermaid
flowchart LR
    A[Đọc guide ngày] --> B[Hiểu khái niệm]
    B --> C[Tự code]
    C --> D[Tự verify]
    D --> E[Nhờ mentor review]
    E --> F[Commit]
```

```text
Đọc guide ngày → hiểu khái niệm → tự code → tự verify → nhờ mentor review → commit
```

Khi cần mentor review, hãy mô tả bạn đã làm gì (hoặc để mentor đọc code trong repo) và hỏi thẳng điểm bạn nghi ngờ.

## Chỉ mục hướng dẫn

| Ngày | Chủ đề | Guide |
|------|--------|-------|
| 1 | Nền móng: solution, CPM, build props, Shared projects, dọn template | [day-01/](day-01/README.md) |
| 2 | Docker Compose hạ tầng + EF Core first migration + pattern `AddModules/UseModules` | [day-02/](day-02/README.md) |
| 3 | Identity: ApplicationUser/Role (Infra) + RefreshToken POCO (Domain) + DbContext | [day-03/](day-03/README.md) |
| 4 | JWT + refresh token + role-based authorization | [day-04/](day-04/README.md) |
| 5 | Global exception handling + `Result<T>` + FluentValidation | [day-05/](day-05/README.md) |
| 6–7 | Module Events: CRUD + pagination + validation + unit test | *(sẽ thêm)* |

## Ghi chú cho các ngày tới (Tuần 1)

Đọc lướt trước để có bức tranh tổng thể. Chi tiết sẽ nằm trong guide từng ngày.

### Day 2: Hạ tầng + nền tảng module

- **Mục tiêu:** `docker compose up` dựng được Postgres + Redis + MinIO; EF Core 10 tạo migration đầu tiên; định hình pattern `AddModules()` / `UseModules()` để host nạp module.
- **Cần đọc trước:** Docker Compose cơ bản (service, volume, port, healthcheck); EF Core `DbContext` + `dotnet ef migrations`; ý tưởng "mỗi module tự đăng ký service/endpoint của mình qua một interface chung".

### Day 3: Identity Domain

- **Mục tiêu:** mô hình hóa `User`, `Role`, `RefreshToken`; dựng `DbContext` cho Identity; chạy được migration của module Identity.
- **Cần đọc trước:** ASP.NET Core Identity (`IdentityUser`, `IdentityRole`) vs domain model thuần; quan hệ 1-n giữa User và RefreshToken; vì sao tách DbContext theo module.

### Day 4: JWT + Refresh token + Authorization

- **Mục tiêu:** Login/Register trả JWT thật; cơ chế refresh token; phân quyền theo role.
- **Cần đọc trước:** cấu trúc JWT (header/payload/signature), claim, `JwtBearer` authentication; vòng đời access token ngắn + refresh token dài; rotation & thu hồi refresh token.

### Day 5: Xử lý lỗi & Validation chuẩn

- **Mục tiêu:** middleware bắt exception toàn cục trả `ProblemDetails`; mẫu `Result<T>` để biểu diễn thành công/thất bại không dùng exception cho luồng nghiệp vụ; FluentValidation cho Register.
- **Cần đọc trước:** RFC 7807 ProblemDetails; khác biệt giữa lỗi nghiệp vụ (dùng `Result<T>`) và lỗi ngoại lệ thật; pipeline validation.

### Day 6–7: Module Events (CRUD)

- **Mục tiêu:** CRUD `Event`/`Venue` đầy đủ, pagination, validation; vài unit test domain (xUnit + Shouldly).
- **Cần đọc trước:** thiết kế aggregate `Event`; pagination kiểu keyset vs offset; viết unit test cho logic domain thuần (không chạm DB).

---

> Nhắc lại ranh giới cứng của project: module **không** reference trực tiếp `Domain`/`Infrastructure` của module khác, giao tiếp cross-module **chỉ** qua `EventHub.Contracts` (integration events) trên Wolverine. Giữ đúng từ ngày đầu để Tuần 4 không phải gỡ rối.
