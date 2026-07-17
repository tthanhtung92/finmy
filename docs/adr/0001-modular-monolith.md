# ADR-0001: Dùng Modular Monolith thay vì microservices

## Trạng thái

Accepted — 2026-07-12

## Bối cảnh

Finmy là một project học tập, không phải sản phẩm đầy đủ tính năng. Nó do một người làm, quỹ thời gian 1–4 giờ mỗi ngày, mục tiêu là mỗi khái niệm backend cốt lõi (auth, caching, CDN, realtime, messaging, concurrency) có một vertical slice mỏng nhưng chạy thật và *giải thích được*.

Câu hỏi kiến trúc nền: chia hệ thống thế nào để vừa giữ được kỷ luật ranh giới rõ ràng, vừa không chết chìm trong chi phí vận hành của một dev đơn lẻ. Ràng buộc: phải `docker compose up` là chạy trong một lệnh, và ranh giới phải kiểm chứng được bằng máy chứ không dựa vào lời hứa.

## Các phương án đã cân nhắc

- **Microservices** — mỗi module một service, một database, giao tiếp qua network. Mạnh về scale độc lập và tách deploy, nhưng kéo theo phân tán từ sớm: nhiều repo/pipeline, discovery, distributed tracing, eventual consistency, chi phí ops nặng. Quá tầm một dev học tập, và phần lớn công sức đổ vào hạ tầng chứ không vào chính các khái niệm cần học.

- **Monolith một khối (layered thuần)** — nhanh và đơn giản, nhưng không có ranh giới nội bộ. Không có gì ngăn code Ticketing gọi thẳng repository của Identity. Không có ranh giới module để dựa vào — đúng thứ project muốn làm rõ thì lại thiếu.

- **Modular Monolith** — một process, một solution, tách thành các module tự chứa, mỗi module là vertical slice bốn lớp (Domain → Application → Infrastructure → Api). Ranh giới có thật nhưng chi phí vận hành vẫn ở mức một tiến trình.

## Quyết định

Chúng tôi chọn **Modular Monolith**. Source chia thành các module tự chứa dưới `src/Modules/` (Identity, Events, Ticketing), mỗi module bốn project. `src/Bootstrap/Finmy.Api` là composition root **duy nhất** — nơi duy nhất nạp service và endpoint của mọi module (pattern `AddModules()` / `UseModules()`). Vì thế các project `*.Api` của module là class library, không phải host; chúng chỉ *khai báo* endpoint, host mới *nạp* chúng.

Ranh giới cứng: một module **không** reference trực tiếp `Domain`/`Infrastructure` của module khác. Giao tiếp cross-module đi **chỉ qua** `src/Shared/Finmy.Contracts` (integration events) publish trên Wolverine bus. Luật này được ép bằng project reference — thêm reference sai chiều là gãy build — và sẽ được NetArchTest kiểm để *fail CI* khi có ai vi phạm (Tuần 4).

## Hệ quả

- Ranh giới trở thành thứ máy kiểm được, không phải quy ước dễ trôi: reference sai chiều gãy build ngay, NetArchTest bắt phần còn lại.
- Nếu sau này thật sự cần tách một module ra microservice, ranh giới cứng đã dựng sẵn khiến đường tách ngắn — module đã không rò rỉ internal.
- Đánh đổi chấp nhận: không có scale độc lập từng module, không tách deploy — nhưng đó không phải mục tiêu của project này.
- Việc phát sinh: mọi trao đổi cross-module phải nắn qua integration event trong `Finmy.Contracts`, kể cả khi gọi thẳng sẽ tiện hơn. Chi phí này là cố ý — nó chính là thứ giữ cho monolith "modular" chứ không rối.
- Một process nhưng ranh giới ép bằng compiler và test kiến trúc chứ không bằng kỷ luật con người, nên không phụ thuộc vào việc ai đó nhớ giữ chúng.
