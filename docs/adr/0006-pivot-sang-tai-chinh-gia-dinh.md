# ADR-0006: Chuyển domain sang Family/Shared Personal Finance (envelope budgeting)

## Trạng thái

Accepted — 2026-07-16

## Bối cảnh

Finmy là project học tập, một người làm, mục tiêu là mỗi khái niệm backend cốt lõi (auth, caching, CDN, realtime, messaging, concurrency) có một vertical slice mỏng nhưng chạy thật và *giải thích được*. Bài toán "đinh" xuyên suốt là chống tranh chấp khi nhiều người cùng ghi lên một tài nguyên khan hiếm — trong domain gốc là chống overselling vé.

Domain gốc là event-ticketing. Vấn đề: đây là một domain tổng hợp mà tác giả không bao giờ vận hành thật. Không có sự kiện nào để bán vé, nên project chỉ dừng ở mức practice dùng một lần rồi bỏ — thiếu cái driver "có người thật vận hành nó" vốn là thứ ép một hệ thống phải đúng và sống lâu. Ngay cả khi dựng thêm UI, sẽ không có ai mở nó ra dùng.

Ràng buộc mới đặt ra cho lần chọn lại domain:

1. Domain phải là thứ tác giả **thật sự vận hành** (self-host, dùng cho gia đình), để có động lực giữ nó đúng và hoàn thiện dần.
2. **Không được đánh mất bề mặt kỹ thuật đang có**: vẫn phải phủ đủ 8 khái niệm và giữ được slice concurrency chống tranh chấp — phần cốt lõi của project.
3. **Tránh làm bản clone kém hơn của một incumbent đã trưởng thành.** Nếu domain đã có sẵn tool miễn phí và mạnh, tác giả sẽ dùng tool đó thay vì bản tự viết, và driver "dùng thật" biến mất.

## Các phương án đã cân nhắc

- **Giữ event-ticketing** — có sẵn slice Ticketing/anti-oversell, nhưng tác giả không vận hành sự kiện nên vẫn là practice, không có ai dùng hằng ngày kể cả khi có UI.

- **Booking/reservation tài nguyên giới hạn** — giữ nguyên bài toán chống double-booking (ánh xạ 1-1 với anti-oversell). Nhưng "dùng thật hằng ngày" yếu, trừ khi tác giả đang quản lý một tài nguyên chia sẻ cụ thể.

- **Dev-tools** (webhook inspector, request runner, synthetic monitor, secret share…) — hữu ích với một BE, nhưng mọi ý đều có incumbent miễn phí đã trưởng thành (webhook.site, Postman, Uptime Kuma, PrivateBin). Bản tự host chỉ ra bản kém hơn → vi phạm ràng buộc 3, và tần suất chạm thực tế thấp.

- **Personal notification / watcher hub** — có lõi async thật (scheduled poll + dedup + outbox), nhưng đụng Uptime Kuma / Google Alerts, và nhiều nguồn cần scraping HTML bẩn, dễ vướng chống bot.

- **Family/Shared Personal Finance theo envelope budgeting** — chi tiêu chung của gia đình, chia tiền vào các "phong bì" ngân sách. Có nhu cầu dùng thật hằng ngày; thoát ràng buộc 3 nhờ ba lý do: dữ liệu tiền bạc nên tự host vì privacy, nhu cầu nội địa (VND, ngân hàng và e-wallet VN) không được YNAB/Actual phục vụ tốt, và bản tự viết may đo được cho đúng gia đình mình. Firefly III / Actual / Money Lover tồn tại nhưng lực kéo "thôi dùng luôn cái có sẵn" ở đây yếu hẳn.

Trong nhánh finance còn hai lựa chọn con:

- **Solo tracking vs family/shared.** Solo tracking về bản chất là CRUD thuần, bài toán concurrency phải gắn vào một cách gượng ép. Family/shared với ngân sách chung thì concurrency và realtime có ý nghĩa thật (nhiều thành viên cùng chi một phong bì).

- **Single-entry vs double-entry ledger.** Slice concurrency nằm ở số dư Envelope, không phụ thuộc mô hình ledger. Single-entry đủ và đơn giản; double-entry cho rigor kế toán cao hơn nhưng phức tạp hơn mà không thêm concurrency.

## Quyết định

Chúng tôi chuyển domain của Finmy sang **Family/Shared Personal Finance theo mô hình envelope budgeting**, với **ledger single-entry**, giữ trọn 8 khái niệm và phạm vi MVP full 8-concept.

- Aggregate trung tâm là **Household** (root cho việc chia sẻ), sở hữu Account, Category, Envelope và Transaction. **Member** gắn một user vào Household kèm role (Owner / Member / Viewer); ranh giới authorization là user chỉ chạm dữ liệu của Household mình.
- Bài toán tranh chấp thay cho anti-oversell vé: **chống chi vượt hạn mức một Envelope khi nhiều thành viên chi đồng thời**, dùng optimistic concurrency (rowversion) trên số dư Envelope. Đây là ánh xạ 1-1 của slice concurrency cũ.
- Bản đồ module (đổi theme, giữ kiến trúc): Identity giữ nguyên; Events → **Accounts / Categories / Budgets** (giữ pattern CRUD + HybridCache + repository port); Ticketing → **Transactions / Ledger** (giữ Wolverine outbox + concurrency + idempotency + SignalR). SharedKernel, Contracts, hạ tầng và Central Package Management giữ nguyên.
- Ánh xạ các slice khái niệm: caching là cache report/aggregate kèm invalidation; CDN / object-storage là MinIO lưu ảnh hóa đơn; realtime là SignalR đẩy số dư mới cho Household; messaging là post transaction async cộng recurring transaction qua Wolverine scheduled cộng transactional outbox; concurrency là rowversion trên Envelope; idempotency là dedup khi import CSV sao kê.
- Phạm vi MVP: **full 8-concept**, bao gồm cả MinIO và recurring. Ledger **single-entry**.

## Hệ quả

- Domain nay có driver "tác giả vận hành thật" (tự host tài chính gia đình), khác hẳn domain dùng một lần — áp lực giữ nó đúng đến từ chính việc mình phải xài.
- Bề mặt kỹ thuật giữ nguyên: đủ 8 khái niệm và slice concurrency vẫn còn. Phần lớn công sức Identity, hạ tầng và kiến trúc tái dùng; chi phí pivot khu trú ở việc đổi theme domain của hai module.
- Bài toán concurrency trở thành tình huống có thật: nhiều người cùng tiêu một ngân sách chung, chứ không phải một giả định dựng lên cho có.
- Các quyết định trong ADR-0001, 0002, 0004, 0005 vẫn đứng nguyên; chỉ tên hai module đổi theme. Đây là đổi tên, không phải đảo quyết định, nên không ADR nào bị superseded.
- Đánh đổi cần quản chủ động: phần lớn khối lượng một app finance là CRUD và báo cáo. Nếu không cố ý đặt hai slice envelope-concurrency và import-idempotency làm trung tâm, project dễ trượt thành một CRUD app tầm thường và mất chiều sâu.
- Single-entry bỏ qua tính cân đối của double-entry; nếu sau này cần bút toán chuyển khoản chặt hoặc đối soát, phải nâng cấp ledger — một quyết định riêng, chưa chốt.
- Auto-import từ ngân hàng VN không khả thi vì thiếu hạ tầng open-banking phổ biến, nên nguồn cho slice import là upload CSV / sao kê hoặc nhập tay.
- Tiền phải xử lý đúng: dùng decimal và lưu theo đơn vị nhỏ nhất, cẩn thận làm tròn. Đây là việc phát sinh so với domain vé.
- Việc dời phát sinh: guide day-06/07 (domain Event/Venue) viết lại theo Account/Envelope; ROADMAP và README cập nhật theme finance; các memory quyết định Events Day 6 đánh dấu superseded, riêng phần pattern kỹ thuật giữ lại.
- Để mở, chưa quyết và sẽ có ADR riêng khi chốt: double-entry ledger, UI frontend (Blazor/htmx), và mức vận hành thật đầy đủ (email, e-wallet VN, đa tiền tệ, deploy).
