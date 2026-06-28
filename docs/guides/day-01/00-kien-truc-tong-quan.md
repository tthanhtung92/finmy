# Bước A — Hiểu kiến trúc bạn sắp dựng

> Bước này **chỉ đọc và hiểu**, chưa gõ gì. Mục tiêu: trước khi gõ một lệnh nào, bạn đã hình dung được mình sắp tạo ra cái gì và *vì sao*. Bỏ qua bước này, các bước sau bạn sẽ làm như con vẹt.

Vừa đọc, vừa hình dung cây thư mục đích trong [ROADMAP mục 3](../../ROADMAP.md).

---

## A1. "Modular Monolith" là gì và vì sao chọn nó?

Tưởng tượng 3 cách tổ chức một backend:

- **Monolith truyền thống (layered):** chia theo *tầng kỹ thuật* — tất cả Controller một chỗ, tất cả Service một chỗ, tất cả Repository một chỗ. Dễ bắt đầu, nhưng càng lớn càng rối: sửa tính năng vé lại lỡ tay đụng tính năng user, vì mọi thứ nằm chung. Người ta gọi đây là "big ball of mud".
- **Microservices:** mỗi nghiệp vụ là một service riêng, deploy riêng, database riêng, gọi nhau qua mạng. Ranh giới cực rõ, nhưng **trả giá đắt**: phải lo network, lỗi mạng, transaction phân tán, deploy chục service. Với một người làm project CV, đây là "dùng dao mổ trâu giết gà".
- **Modular Monolith (cái project này chọn):** vẫn deploy **một process duy nhất** (đơn giản như monolith), nhưng mã nguồn chia theo *nghiệp vụ* thành các **module** (Identity, Events, Ticketing) có ranh giới rõ ràng, và ranh giới đó được **ép buộc bằng test tự động**.

Điểm mấu chốt: nó cho bạn cái lợi "ranh giới rõ" của microservices mà **không** gánh chi phí vận hành phân tán. Nếu sau này một module thực sự cần tách thành microservice, ranh giới sẵn có giúp việc tách dễ hơn nhiều.

> **Góc kể khi phỏng vấn:** *"Tôi chọn modular monolith vì nó giữ ranh giới nghiệp vụ rõ như microservices nhưng tránh chi phí vận hành phân tán; và tôi ép ranh giới đó bằng architecture test để nó không trôi theo thời gian."* Đây là tư duy của middle, không phải junior.

## A2. Vì sao file solution là `.slnx` chứ không phải `.sln`?

`.sln` (định dạng cũ) là text với cú pháp GUID lằng nhằng, rất khó đọc và hay xung đột khi merge git. `.slnx` là **định dạng mới, dạng XML, người đọc được**, và **từ .NET 10 nó là mặc định** khi bạn chạy `dotnet new sln` (bạn sẽ thấy ở [Bước 1](01-tao-scaffold.md): chạy `dotnet new sln` ra ngay file `.slnx`).

Khi mở `EventHub.slnx` (sau khi tạo) ra xem, nó chỉ là vài thẻ `<Folder>` và `<Project>` — chính là cây bạn thấy trong Solution Explorer. Tin tốt: bạn **không bao giờ phải sửa file XML này bằng tay** — lệnh `dotnet sln add` lo việc đó.

*Tham khảo:* [Giới thiệu .slnx trong .NET CLI](https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli/) · [dotnet new sln mặc định .slnx (.NET 10)](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/dotnet-new-sln-slnx-default).

## A3. Vì sao mỗi module có tới 4 project? Và vì sao project `Api` là *library*?

Mỗi module (vd Identity) gồm 4 project: `EventHub.Identity.Domain`, `.Application`, `.Infrastructure`, `.Api`. Đây là **Clean Architecture thu nhỏ**. Quy tắc vàng: **mũi tên phụ thuộc chỉ đi vào trong**.

```
Api  ──►  Application  ──►  Domain
              ▲
              │
        Infrastructure
```

Đọc sơ đồ (mũi tên = "phụ thuộc vào"): Api phụ thuộc Application, Application phụ thuộc Domain. Infrastructure phụ thuộc **Application** (vì nó hiện thực các interface mà Application định nghĩa), nên gián tiếp cũng phụ thuộc Domain. Điểm mấu chốt: **mọi mũi tên đều hướng *vào trong* phía Domain** — Domain không phụ thuộc gì cả.

- **Domain** — trái tim nghiệp vụ: entity (vd `User`, `RefreshToken`), value object, luật nghiệp vụ thuần. **Không tham chiếu gì cả** — không EF Core, không ASP.NET. Nhờ vậy nó dễ test nhất.
- **Application** — điều phối *use case* (vd handler "Đăng nhập", "Đăng ký"). Nó *khai báo* các interface mà nó cần (vd "tôi cần một thứ lưu được user"), nhưng không tự hiện thực.
- **Infrastructure** — hiện thực các interface đó bằng công nghệ cụ thể: EF Core, `DbContext`, dịch vụ JWT, Redis. Đây là nơi "bẩn tay" với hạ tầng.
- **Api** — định nghĩa các endpoint Minimal API của module.

### Điểm CỰC KỲ quan trọng: `Api` của module là *class library*, KHÔNG phải web host

Đây là chỗ rất dễ làm sai. Trong Modular Monolith này có **đúng một host / một điểm khởi chạy**: `src/Bootstrap/EventHub.Api`. Đó là **composition root** — nơi duy nhất có `Program.cs`, `appsettings.json`, và lệnh `app.Run()`.

Project `Api` của *module* (vd `EventHub.Identity.Api`) **không** phải là một web app chạy độc lập. Nó là một **class library** chỉ chứa các *định nghĩa endpoint* (sau này phơi ra qua một extension method kiểu `MapIdentityEndpoints(...)`), để host Bootstrap **gọi vào** lúc khởi động. Vì vậy:

- Module Api **được tạo bằng `dotnet new classlib`**, KHÔNG phải `dotnet new web`.
- Nó **không có** `Program.cs`, `appsettings.json`, hay `launchSettings.json` riêng.

**Vì sao bắt buộc vậy?** Nếu mỗi module Api là một host riêng, bạn sẽ có nhiều `Program.cs`/`app.Run()` — tức nhiều ứng dụng, không còn là "một process duy nhất". Điều đó phá vỡ định nghĩa Modular Monolith và mâu thuẫn với mục tiêu "composition root duy nhất". Việc nạp endpoint của từng module vào host chung sẽ làm ở [Day 2](../README.md) (pattern `AddModules()`/`UseModules()`). Hôm nay chỉ cần tạo đúng *kiểu* project.

> **Việc cần ghi nhớ:** khi tạo project ở Bước 1, chỉ host `Bootstrap/EventHub.Api` dùng `dotnet new web`; **tất cả** project còn lại (kể cả `*.Api` của module) dùng `dotnet new classlib`.

**Vì sao bận tâm chiều mũi tên?** Để **logic nghiệp vụ không bị công nghệ kéo theo**. Mai mốt đổi PostgreSQL sang SQL Server, chỉ Infrastructure đổi — Domain không đụng một dòng. Khái niệm đứng sau là **Dependency Inversion** (chữ D trong SOLID) — phần đáng giá nhất của kiến trúc này.

## A4. Hai project Shared: `SharedKernel` và `Contracts` khác nhau thế nào?

Hai cái tên nghe giống nhau nhưng mục đích **khác hẳn** — đừng nhầm, đây là chỗ junior hay sai:

- **`EventHub.SharedKernel`** — các "viên gạch" dùng chung *bên trong* mọi module: kiểu `Result<T>` (biểu diễn thành công/thất bại mà không ném exception cho luồng nghiệp vụ), lớp cơ sở cho domain event, các guard clause (hàm kiểm tra đầu vào kiểu "không được null"). Coi như thư viện tiện ích nội bộ.
- **`EventHub.Contracts`** — **bề mặt công khai DUY NHẤT giữa các module**: các **integration event** như `TicketSoldEvent`, `EventCreatedEvent`. Khi module Ticketing muốn báo "vừa bán 1 vé", nó publish một event định nghĩa *ở đây* qua message bus Wolverine. Module khác lắng nghe event đó — chứ **không** được "thò tay" vào Domain của Ticketing.

> **Cạm bẫy chết người:** đừng nhét entity domain hay logic nghiệp vụ vào `Contracts`. Nó chỉ chứa các "hợp đồng" message — thường là `record` đơn giản, bất biến (immutable). Trộn lẫn = bạn làm rò rỉ nội bộ module ra ngoài, phá đúng cái ranh giới mà project muốn khoe.

Ở Day 1 ta chỉ **tạo khung rỗng** hai project này. Nội dung thật: `Result<T>` viết ở [Day 5](../README.md), các event viết ở Tuần 3.

## A5. `Class1.cs` và endpoint mẫu là gì? (rác sẽ cần dọn)

Khi chạy `dotnet new classlib`, .NET tự sinh một file `Class1.cs` rỗng làm ví dụ — vô giá trị, chỉ là rác mẫu. Sau khi tạo scaffold ở Bước 1, bạn sẽ thấy `Class1.cs` trong **mọi** class library.

Host (tạo bằng `dotnet new web`) thì sinh một `Program.cs` tối giản với một endpoint mẫu `/` trả `"Hello World!"` — không phải rác nặng, chỉ cần chỉnh thành endpoint sức khỏe của bạn. (Nếu lỡ dùng `dotnet new webapi`, host sẽ kèm thêm demo `weatherforecast` — nhiều rác hơn; đó là một lý do nữa để dùng `dotnet new web`.) Ta dọn tất cả ở [Bước 4](04-don-template.md).

---

## Tự kiểm tra trước khi đi tiếp

Nhắm mắt trả lời được 5 câu này thì sang [Bước 1](01-tao-scaffold.md):

1. Modular monolith khác microservices ở điểm nào, lợi gì?
2. Trong một module, `Domain` được phép tham chiếu `Infrastructure` không? Vì sao?
3. Project `Api` của *module* nên tạo bằng `dotnet new web` hay `dotnet new classlib`? Vì sao?
4. `SharedKernel` và `Contracts` — cái nào là bề mặt giao tiếp giữa các module?
5. `Class1.cs` để làm gì?

<details>
<summary>Đáp án (tự trả lời trước rồi mới mở)</summary>

1. **Khác:** cả hai đều có ranh giới nghiệp vụ rõ, nhưng modular monolith chạy trong **một process / một lần deploy**, còn microservices là **nhiều service deploy riêng, gọi nhau qua mạng**. **Lợi:** có được sự rõ ràng về ranh giới như microservices mà **không gánh chi phí phân tán** (network, lỗi mạng, transaction phân tán, deploy phức tạp); khi cần, ranh giới sẵn có giúp tách ra microservice dễ hơn.
2. **Không.** Chiều phụ thuộc chỉ đi vào trong (Api → Application → Domain; Infrastructure → Application/Domain). Domain là lõi nghiệp vụ thuần, không được biết công nghệ cụ thể (EF, Redis...). Giữ vậy để **logic nghiệp vụ không bị công nghệ kéo theo** — đổi DB/hạ tầng thì Domain không phải sửa (Dependency Inversion).
3. **`dotnet new classlib`.** Module Api chỉ là thư viện chứa định nghĩa endpoint, được host **duy nhất** (`Bootstrap/EventHub.Api`) nạp vào lúc khởi động. Nếu tạo bằng `dotnet new web` thì module trở thành một host riêng (có `Program.cs`/`app.Run()`) → có nhiều ứng dụng, phá vỡ "một process duy nhất" của Modular Monolith.
4. **`Contracts`** — chứa integration event (vd `TicketSoldEvent`), là bề mặt công khai *duy nhất* giữa các module qua message bus. `SharedKernel` chỉ là viên gạch dùng chung *nội bộ* (`Result<T>`, base types, guards), **không** phải kênh giao tiếp cross-module.
5. Không để làm gì cả — là **file rỗng do template `dotnet new classlib` tự sinh**. Chỉ là rác mẫu, cần xóa.

</details>
