# Bước 3 — Build settings dùng chung (`Directory.Build.props`)

> Mục tiêu: khai báo **một lần** các thiết lập build (target framework, nullable, ngôn ngữ C#, coi warning là error...) cho **mọi** project, thay vì lặp lại trong từng `.csproj`.
>
> File này là XML — mình mô tả từng phần tử bằng lời, bạn tự gõ.

---

## 3.1. Vấn đề (vì sao làm bước này)

Mở `src/Modules/Identity/EventHub.Identity.Domain/EventHub.Identity.Domain.csproj` — bạn thấy template `classlib` tự khai `TargetFramework` là `net10.0`, `Nullable` là `enable`, `ImplicitUsings` là `enable`. Bây giờ mở các `.csproj` khác (host, các project còn lại): y hệt, lặp đi lặp lại. Khi có chục project, đổi một thiết lập (vd bật "coi warning là lỗi") phải sửa chục file — dễ sót.

`Directory.Build.props` giải quyết bằng cơ chế của MSBuild: nó **tự động nạp** file này từ thư mục gốc và áp xuống mọi project bên dưới. MSBuild đi ngược cây thư mục từ mỗi project lên, gặp `Directory.Build.props` đầu tiên thì nạp và **dừng**. File được nạp *rất sớm*, trước cả khi SDK của project được xử lý.

> **Góc kể phỏng vấn:** "Tôi tập trung build settings vào Directory.Build.props để đảm bảo mọi project nhất quán — cùng target framework, cùng mức nghiêm về nullable và warning — và để bật/tắt một chính sách build ở một chỗ duy nhất."

## 3.2. Tạo file

Tạo file tên `Directory.Build.props` ở **gốc repo** (ngang `EventHub.slnx` và `Directory.Packages.props`). Không có `dotnet new` riêng cho nó — tạo file trống rồi điền theo mục 3.3.

## 3.3. Nội dung nên đưa vào (tự gõ XML)

Cấu trúc: thẻ gốc `Project` (không kèm `Sdk`), bên trong một `PropertyGroup` chứa các thuộc tính dùng chung. Các thuộc tính nên cân nhắc, kèm lý do:

| Thuộc tính | Giá trị | Vì sao |
|------------|---------|--------|
| `TargetFramework` | `net10.0` | Cả solution dùng chung một runtime; khai một chỗ. |
| `Nullable` | `enable` | Bật nullable reference types — trình biên dịch cảnh báo nguy cơ null, giảm `NullReferenceException`. |
| `ImplicitUsings` | `enable` | Tự thêm các `using` phổ biến, code gọn hơn. |
| `LangVersion` | `latest` (hoặc `14`) | Dùng được cú pháp C# 14 mà ROADMAP nhắc tới. |
| `TreatWarningsAsErrors` | `true` | Coi mọi warning là lỗi build → ép code sạch. Với một CV piece, "không warning" là điểm cộng kỷ luật. |

> **Cân nhắc về `TreatWarningsAsErrors`:** bật nó làm build *khắt khe hơn* — một warning nhỏ cũng làm đỏ build. Đó là chủ đích (ép bạn xử lý ngay), nhưng nếu thấy vướng lúc mới học, bạn có thể tạm để `false` rồi bật lại sau. Nếu cần coi *hầu hết* warning là lỗi nhưng tha vài mã cụ thể, có thuộc tính `WarningsNotAsErrors` nhận danh sách mã warning.

*Tham khảo (có ví dụ XML đầy đủ):* [Customize the build by folder — MSBuild, Microsoft Learn](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory).

## 3.4. Dọn trùng lặp trong các `.csproj` (đừng quên bước này)

Sau khi đưa các thuộc tính trên vào `Directory.Build.props`, hãy **mở từng `.csproj`** (host + 4 project Identity + 2 project Shared = 7 file) và **xóa các thuộc tính đã trùng** (`TargetFramework`, `Nullable`, `ImplicitUsings`). Vì chúng đã nằm ở file gốc, để lại là khai báo hai nơi.

> **Cạm bẫy thứ tự ưu tiên:** thuộc tính khai trong `.csproj` **đè** giá trị từ `Directory.Build.props`. Nên nếu một project *cần* giá trị khác (hiếm), cứ khai riêng trong `.csproj` đó; còn lại để file gốc lo. Nếu sau khi xóa mà một project "mất" target framework, nghĩa là file gốc chưa khai đủ — bổ sung vào `Directory.Build.props`.

Sau khi dọn, các `.csproj` của bạn sẽ ngắn hơn hẳn (gần như rỗng) — **đó là dấu hiệu đúng**.

## 3.5. Kiểm chứng

```bash
dotnet build EventHub.slnx
```

- Phải `Build succeeded`. Các template tối giản (`web` Hello World, `classlib` rỗng) không sinh warning, nên dù bật `TreatWarningsAsErrors` build vẫn nên xanh. Nếu đỏ, đọc kỹ thông báo lỗi để xử lý (thường là một thuộc tính gõ sai trong `Directory.Build.props`).
- Kiểm tra nhanh một project vẫn target đúng: mở project bất kỳ trong IDE, xác nhận nó vẫn là `net10.0` dù `.csproj` không còn dòng `TargetFramework`. Điều này chứng minh `Directory.Build.props` đang phát huy tác dụng.

## 3.6. Xong bước này khi

- [ ] `Directory.Build.props` ở gốc repo, chứa các thuộc tính build dùng chung.
- [ ] Cả 7 `.csproj` đã bỏ thuộc tính trùng, gọn lại.
- [ ] `dotnet build EventHub.slnx` xanh.

→ Sang [Bước 4 — Dọn template](04-don-template.md).
