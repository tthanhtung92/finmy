# Bước 3 — Tạo 2 project Shared

> Mục tiêu: tạo `EventHub.SharedKernel` và `EventHub.Contracts` dưới `src/Shared/`, rồi đưa chúng vào solution.
>
> Bước này chủ yếu là **lệnh CLI** — cứ chạy theo. Hôm nay chỉ dựng khung rỗng; nội dung viết ở các ngày sau.

---

## 3.1. Nhắc lại vai trò (đọc kỹ kẻo nhầm)

- **`EventHub.SharedKernel`** — viên gạch dùng chung *nội bộ* mọi module: `Result<T>`, base type cho domain event, guard clause.
- **`EventHub.Contracts`** — bề mặt công khai *duy nhất giữa các module*: các integration event (vd `TicketSoldEvent`). Chỉ chứa "hợp đồng" message, **không** chứa entity/logic.

(Chi tiết vì sao ở [Bước A, mục A4](00-hieu-scaffold.md).)

## 3.2. Tạo 2 project class library

Tại thư mục gốc repo, chạy lần lượt hai lệnh:

```bash
dotnet new classlib --output src/Shared/EventHub.SharedKernel
dotnet new classlib --output src/Shared/EventHub.Contracts
```

Giải thích từng phần:
- `dotnet new classlib` — tạo một project **thư viện lớp** (class library), tức project không chạy độc lập mà để project khác tham chiếu. Đúng bản chất của Shared.
- `--output <đường-dẫn>` — thư mục đích. Đặt dưới `src/Shared/` cho khớp cấu trúc trong [ROADMAP mục 3](../../ROADMAP.md).

Sau lệnh, bạn có `src/Shared/EventHub.SharedKernel/EventHub.SharedKernel.csproj` và tương tự cho Contracts.

> **Lưu ý:** tên project mặc định lấy theo tên thư mục (`EventHub.SharedKernel`), nên không cần `--name` riêng. Mỗi project mới sẽ kèm một file `Class1.cs` rỗng — ta xóa ở [Bước 4](04-don-template.md).

## 3.3. Đưa 2 project vào solution

Vẫn ở thư mục gốc, chạy:

```bash
dotnet sln EventHub.slnx add src/Shared/EventHub.SharedKernel/EventHub.SharedKernel.csproj src/Shared/EventHub.Contracts/EventHub.Contracts.csproj
```

Giải thích:
- `dotnet sln EventHub.slnx add <csproj...>` — thêm một hoặc nhiều project vào file solution.
- Khi đường dẫn project chứa các thư mục cha (ở đây là `src/Shared`), `dotnet sln` **tự tạo solution folder tương ứng** trong `.slnx`. Nhờ vậy hai project mới sẽ nằm gọn trong nhóm `src/Shared` — khớp với cách các project hiện có đang được nhóm. Bạn **không phải sửa XML bằng tay**.

*Tham khảo cú pháp đầy đủ:* [dotnet sln command — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln).

## 3.4. Kiểm chứng

Liệt kê project trong solution:

```bash
dotnet sln EventHub.slnx list
```

Phải thấy `EventHub.SharedKernel` và `EventHub.Contracts` xuất hiện trong danh sách. Sau đó build:

```bash
dotnet build EventHub.slnx
```

Phải `Build succeeded`. Mở solution trong IDE, xác nhận hai project mới nằm dưới nhóm Shared.

> **Cạm bẫy:** nếu bạn `dotnet new classlib` mà **quên** bước `dotnet sln ... add`, project tồn tại trên đĩa nhưng **không nằm trong solution** → IDE không hiện, và `dotnet build EventHub.slnx` không build nó. Bạn sẽ tưởng ổn cho tới khi project khác cần tham chiếu nó mà không thấy. Luôn chạy `dotnet sln list` để xác nhận.

## 3.5. Về việc tham chiếu (chưa làm hôm nay)

Sau này, khi một project (vd `EventHub.Identity.Domain`) cần dùng `Result<T>` từ SharedKernel, bạn sẽ thêm **project reference** bằng `dotnet add <project> reference <project khác>`. **Chưa cần hôm nay** — hôm nay chỉ dựng khung. Mình nhắc để bạn biết bức tranh.

> **Ranh giới cứng cần nhớ:** module được phép tham chiếu `SharedKernel` và `Contracts`, nhưng **tuyệt đối không** tham chiếu `Domain`/`Infrastructure` của module *khác*. Giữ đúng từ ngày đầu để Tuần 4 (architecture test) không bắt lỗi.

## 3.6. Xong bước này khi

- [ ] `src/Shared/EventHub.SharedKernel` và `src/Shared/EventHub.Contracts` tồn tại.
- [ ] `dotnet sln EventHub.slnx list` hiện cả hai.
- [ ] `dotnet build EventHub.slnx` xanh.

→ Sang [Bước 4 — Dọn template](04-don-template.md).
