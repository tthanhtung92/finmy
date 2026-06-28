# Bước 4 — Dọn rác template

> Mục tiêu: xóa hết code mẫu mà `dotnet new` sinh ra (`Class1.cs` trong mọi library, endpoint mẫu `"Hello World!"` trong host), để commit "nền móng" sạch sẽ, đúng nghề.
>
> Phần sửa `Program.cs` là **code C# — mình KHÔNG viết hộ**. Mình mô tả host tối giản cần đạt được; bạn tự gõ.

---

## 4.1. Xóa các file `Class1.cs`

`Class1.cs` là lớp rỗng vô nghĩa do template `classlib` sinh ra. Mỗi project class library bạn tạo ở [Bước 1](01-tao-scaffold.md) đều có một file này. Tìm và xóa **tất cả 6 file**:

- `src/Modules/Identity/EventHub.Identity.Domain/Class1.cs`
- `src/Modules/Identity/EventHub.Identity.Application/Class1.cs`
- `src/Modules/Identity/EventHub.Identity.Infrastructure/Class1.cs`
- `src/Modules/Identity/EventHub.Identity.Api/Class1.cs`
- `src/Shared/EventHub.SharedKernel/Class1.cs`
- `src/Shared/EventHub.Contracts/Class1.cs`

Xóa bằng cách click chuột phải → Delete trong IDE, hoặc dùng lệnh. Để chắc không sót, tìm toàn repo: trong IDE bấm tìm file tên `Class1.cs`, hoặc dùng tìm kiếm của editor. Không file nào được còn lại.

> *Vì sao không bỏ qua:* để rác mẫu trong repo public khiến người xem (nhà tuyển dụng) nghĩ bạn không để ý chi tiết. Sạch sẽ là một phần của "đúng chuẩn".

## 4.2. Chỉnh endpoint mẫu trong host thành endpoint sức khỏe

Chỉ **một** project có `Program.cs`: host `src/Bootstrap/EventHub.Api` (các module Api là class library nên không có `Program.cs`). Vì bạn tạo host bằng `dotnet new web` (Empty), `Program.cs` đã rất gọn — chỉ có một endpoint mẫu `/` trả `"Hello World!"`. Không có `weatherforecast` hay `record` thừa để xóa (đó là phần thưởng của việc chọn `web` thay vì `webapi`).

**Host tối giản cần đạt được** (bạn tự viết code, đây là *mô tả mục tiêu*, không phải code):

1. Tạo `WebApplicationBuilder` từ `args` (dòng `CreateBuilder` đã có sẵn — giữ).
2. Build ra `WebApplication`.
3. **Chỉnh endpoint mẫu `/`**: thay chuỗi `"Hello World!"` thành một phản hồi sức khỏe có ý nghĩa hơn (vd trả `"OK"` hoặc tên app), hoặc đổi đường dẫn thành `/health` nếu bạn thích. Mục đích chỉ để xác nhận app sống.
4. Gọi `Run()` để khởi động.
5. *(Tùy chọn, không bắt buộc Day 1)* đăng ký OpenAPI nếu muốn — nhưng bản `web` không kèm sẵn, nên cứ để dành tới khi có endpoint thật.

> **Chưa làm hôm nay:** việc nạp các module qua `AddModules()`/`UseModules()` là của **[Day 2](../README.md)**. Đừng ôm sớm — hôm nay host chỉ cần **sạch và chạy được**.

*Gợi ý tra cứu nếu bí cú pháp Minimal API:* [Minimal APIs overview — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview). (Đọc để hiểu rồi tự viết — đừng copy nguyên si.)

## 4.3. Kiểm chứng

Build trước:

```bash
dotnet build EventHub.slnx
```

Phải `Build succeeded` — việc xóa `Class1.cs` và chỉnh endpoint `/` không được làm hỏng build.

Chạy host và thử endpoint:

```bash
dotnet run --project src/Bootstrap/EventHub.Api
```

Mở trình duyệt (hoặc dùng `curl`) gọi vào địa chỉ host in ra trong terminal (vd `http://localhost:5xxx/` hoặc `/health`). Phải nhận phản hồi "sống" của bạn. Nhấn `Ctrl+C` để dừng.

## 4.4. Xong bước này khi

- [ ] Không còn `Class1.cs` nào trong toàn repo (đã xóa cả 6).
- [ ] Host chỉ có một endpoint sức khỏe (không còn `"Hello World!"` mặc định, không có sample thừa).
- [ ] `dotnet build` xanh, `dotnet run` chạy được.

→ Sang [Bước 5 — LICENSE & README](05-license-readme.md).
