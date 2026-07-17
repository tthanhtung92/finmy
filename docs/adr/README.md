# Architecture Decision Records

Thư mục này là hồ sơ "vì sao" của Finmy: mỗi ADR ghi một quyết định kiến trúc tại thời điểm chốt, kèm bối cảnh và trade-off. ADR là immutable — quyết định đổi thì viết ADR mới và đánh dấu ADR cũ "Superseded", không sửa đè.

| Số | Tiêu đề | Trạng thái | Ngày |
|----|---------|------------|------|
| [0001](0001-modular-monolith.md) | Dùng Modular Monolith thay vì microservices | Accepted | 2026-07-12 |
| [0002](0002-wolverine.md) | Dùng Wolverine làm mediator + message bus + transactional outbox | Accepted | 2026-07-12 |
| [0003](0003-tranh-thu-vien-thuong-mai.md) | Tránh thư viện đã thương mại hóa, chọn Mapster / NSubstitute / Shouldly | Accepted | 2026-07-12 |
| [0004](0004-identity-option-a.md) | Ranh giới module Identity theo Option A (Dependency Inversion / IIdentityService) | Accepted | 2026-07-12 |
| [0005](0005-jwt-short-name-claim.md) | Phát JWT bằng short-name claim với IdentityClaimTypes làm source of truth | Accepted | 2026-07-12 |
| [0006](0006-pivot-sang-tai-chinh-gia-dinh.md) | Chuyển domain sang Family/Shared Personal Finance (envelope budgeting) | Accepted | 2026-07-16 |
