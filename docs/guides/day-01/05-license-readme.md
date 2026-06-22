# Bước 5 — LICENSE & README

> Mục tiêu: xác nhận `LICENSE` đúng, và sửa các chỗ placeholder trong `README` để link trỏ đúng repo thật của bạn.
>
> Đây là sửa văn bản, không phải code — bạn tự sửa, mình chỉ vào đâu cần sửa.

---

## 5.1. Vì sao bận tâm LICENSE & README ở Day 1?

ROADMAP coi đây là một phần của "nền móng" và là tiêu chí Definition of Done của cả project. Quan trọng hơn: **README là thứ nhà tuyển dụng đọc đầu tiên**, trước cả code. Một repo có LICENSE rõ ràng + README chỉn chu tạo ấn tượng "người này làm việc nghiêm túc" ngay từ giây đầu.

## 5.2. Kiểm tra LICENSE

Mở [LICENSE](../../../LICENSE). Xác nhận:

- Đúng loại **MIT** (ROADMAP và README đều cam kết MIT).
- Dòng bản quyền có **đúng năm** và **đúng tên** bạn (chủ sở hữu). Nếu template để tên/năm sai, sửa lại.

> *Vì sao MIT:* đây là license cho phép người khác dùng lại tự do, phổ biến nhất cho project mã nguồn mở/CV. ROADMAP chọn MIT có chủ đích.

## 5.3. Sửa placeholder trong README

Mở [README.md](../../../README.md). Có vài chỗ còn để giữ chỗ `<user>/<repo>` — phải thay bằng đường dẫn GitHub thật của bạn:

1. **Badge CI** (gần đầu file): URL chứa `github.com/<user>/<repo>/actions/...`. Thay `<user>/<repo>` bằng `tên-tài-khoản/tên-repo` thật. (Badge sẽ chưa xanh cho tới khi có GitHub Actions ở Tuần 4 — bình thường.)
2. **Lệnh clone** (mục "Bắt đầu nhanh"): `git clone https://github.com/<user>/<repo>.git` — thay cho khớp repo thật.

Quét cả file một lượt: nếu còn chuỗi `<user>` hoặc `<repo>` nào sót, thay hết.

> **Chưa cần làm hôm nay:** các phần README có ghi chú `<!-- TODO -->` (ảnh demo, sơ đồ kiến trúc, thông tin tài khoản seed) là cho các tuần sau. Cứ để nguyên, đừng xóa ghi chú TODO — chúng là lời nhắc.

## 5.4. Kiểm chứng

- Tìm trong README: không còn chuỗi `<user>` hay `<repo>`.
- Mở thử link clone/badge bằng mắt xem đã trỏ đúng tài khoản/repo của bạn chưa.
- (Chưa push nên badge/CI chưa hoạt động — đó là việc của [Bước 6](06-commit-push.md).)

## 5.5. Xong bước này khi

- [ ] LICENSE là MIT, đúng tên & năm.
- [ ] README không còn placeholder `<user>/<repo>`; badge và lệnh clone trỏ repo thật.

→ Sang [Bước 6 — Commit & Push](06-commit-push.md).
