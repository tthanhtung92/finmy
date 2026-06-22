# Bước 4 — Dọn rác template

> Mục tiêu: xóa hết code mẫu mà `dotnet new` sinh ra (`Class1.cs`, demo `weatherforecast`), để commit "nền móng" sạch sẽ, đúng nghề.
>
> Phần sửa `Program.cs` là **code C# — mình KHÔNG viết hộ**. Mình mô tả host tối giản cần đạt được; bạn tự gõ.

---

## 4.1. Xóa các file `Class1.cs`

`Class1.cs` là lớp rỗng vô nghĩa do template `classlib` sinh ra. Tìm và xóa **tất cả**:

- `src/Modules/Identity/EventHub.Identity.Domain/Class1.cs`
- `src/Modules/Identity/EventHub.Identity.Application/Class1.cs`
- `src/Modules/Identity/EventHub.Identity.Infrastructure/Class1.cs`
- `src/Shared/EventHub.SharedKernel/Class1.cs` (vừa tạo ở Bước 3)
- `src/Shared/EventHub.Contracts/Class1.cs` (vừa tạo ở Bước 3)

Xóa bằng cách click chuột phải → Delete trong IDE, hoặc dùng lệnh. Để chắc không sót, tìm toàn repo: trong IDE bấm tìm file tên `Class1.cs`, hoặc dùng tìm kiếm của editor. Không file nào được còn lại.

> *Vì sao không bỏ qua:* để rác mẫu trong repo public khiến người xem (nhà tuyển dụng) nghĩ bạn không để ý chi tiết. Sạch sẽ là một phần của "đúng chuẩn".

## 4.2. Thay demo `weatherforecast` trong host

Mở [src/Bootstrap/EventHub.Api/Program.cs](../../../src/Bootstrap/EventHub.Api/Program.cs). Hiện tại nó chứa toàn bộ demo: mảng `summaries`, endpoint `/weatherforecast`, và `record WeatherForecast` ở cuối file. Tất cả phải đi.

**Host tối giản cần đạt được** (bạn tự viết code, đây là *mô tả mục tiêu*, không phải code):

1. Tạo `WebApplicationBuilder` từ `args` (dòng `CreateBuilder` đã có sẵn — giữ).
2. Giữ phần đăng ký OpenAPI nếu muốn (tùy bạn) — không bắt buộc cho Day 1.
3. Build ra `WebApplication`.
4. **Bỏ hoàn toàn**: mảng `summaries`, endpoint `/weatherforecast`, và `record WeatherForecast`.
5. **Thêm một endpoint sức khỏe đơn giản**: một route GET (vd đường dẫn `/` hoặc `/health`) trả về một chuỗi/đối tượng ngắn báo app sống (vd trả `"OK"` hoặc tên app). Mục đích chỉ để xác nhận app chạy.
6. Gọi `Run()` để khởi động.

> **Chưa làm hôm nay:** việc nạp các module qua `AddModules()`/`UseModules()` là của **[Day 2](../README.md)**. Đừng ôm sớm — hôm nay host chỉ cần **sạch và chạy được**.

*Gợi ý tra cứu nếu bí cú pháp Minimal API:* [Minimal APIs overview — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview). (Đọc để hiểu rồi tự viết — đừng copy nguyên si.)

## 4.3. Kiểm chứng

Build trước:

```bash
dotnet build EventHub.slnx
```

Nếu ở [Bước 2](02-directory-build-props.md) bạn bật `TreatWarningsAsErrors` và trước đó build đỏ vì code template, giờ dọn xong nó nên **xanh trở lại**.

Chạy host và thử endpoint:

```bash
dotnet run --project src/Bootstrap/EventHub.Api
```

Mở trình duyệt (hoặc dùng `curl`) gọi vào địa chỉ host in ra trong terminal (vd `http://localhost:5xxx/` hoặc `/health`). Phải nhận phản hồi "sống" của bạn. Gọi `/weatherforecast` phải **404** — vì đã xóa. Nhấn `Ctrl+C` để dừng.

## 4.4. Xong bước này khi

- [ ] Không còn `Class1.cs` nào trong toàn repo.
- [ ] `Program.cs` không còn dấu vết `weatherforecast`/`WeatherForecast`.
- [ ] Có một endpoint sức khỏe trả về phản hồi.
- [ ] `dotnet build` xanh, `dotnet run` chạy được.

→ Sang [Bước 5 — LICENSE & README](05-license-readme.md).
