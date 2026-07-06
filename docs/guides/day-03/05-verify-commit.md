# Bước 5. Verify end-to-end & commit

> Mục tiêu: rà lại toàn bộ Day 3 chạy thông (build + migration + bảng trong DB), rồi commit theo Conventional Commits tiếng Việt.
>
> Lưu ý: chỉ có lệnh CLI, cứ chạy theo.

---

## 5.1. Verify end-to-end

Chạy tuần tự, mỗi lệnh phải xanh trước khi sang lệnh sau:

```bash
dotnet build EventHub.slnx
dotnet ef migrations list --project src/Modules/Identity/EventHub.Identity.Infrastructure --startup-project src/Bootstrap/EventHub.Api
docker compose --env-file .env -f docker/docker-compose.yml exec postgres psql -U <user> -d <db> -c "\dt"
```

- Build **xanh, không warning**.
- `migrations list` liệt kê migration schema Identity (Hướng A: `InitialCreate` + `AddIdentitySchema`; Hướng B: một `InitialCreate`).
- `\dt` thấy đủ **7 bảng `AspNet*`** + **`RefreshTokens`** + `__EFMigrationsHistory`.

Tuỳ chọn: chạy thử host để chắc DI Identity không nổ lúc khởi động:

```bash
dotnet run --project src/Bootstrap/EventHub.Api
```

Host lên được (không exception về `UserManager`/store thiếu đăng ký) rồi tắt (`Ctrl+C`).

## 5.2. Định nghĩa "hoàn thành" Day 3

Đối chiếu checklist ở [README Day 3](README.md#định-nghĩa-hoàn-thành-day-3). Điểm cốt lõi:

- [x] Package Identity EF trong CPM, version căn khớp EF Core.
- [x] `ApplicationUser`/`ApplicationRole`/`RefreshToken` mô hình hóa xong; quan hệ 1-n rõ.
- [x] `IdentityModuleDbContext` kế thừa base Identity; `base.OnModelCreating` gọi trước.
- [x] Migration schema Identity áp được; DB thấy 8 bảng.
- [x] Build xanh, không warning.
- [x] Bạn tự giải thích được 4–5 điểm ở checklist README (không chỉ "làm xong").

## 5.3. Commit

Conventional Commits, tiếng Việt, imperative (xem [docs/conventional-commits.md](../../conventional-commits.md)). Scope `identity`:

```bash
git add -A
git status
git commit
```

**Khuyến nghị message** (một commit gộp cho cả ngày):

```text
feat(identity): mô hình hóa User/Role/RefreshToken + schema Identity
```

Nếu bạn thích commit nhỏ, tách theo bước cho lịch sử dễ đọc (đây là **lựa chọn phong cách của bạn**, mentor khuyến nghị gộp một commit vì cả ngày là một đơn vị logic "dựng mô hình Identity"):

```text
build(identity): thêm package Identity EF + căn version EF Core
feat(identity): thêm ApplicationUser/Role (Guid) + RefreshToken
feat(identity): IdentityModuleDbContext kế thừa Identity + AddIdentityCore
feat(identity): migration schema Identity (InitialCreate, PK uuid)
```

Đẩy lên remote nếu muốn:

```bash
git push
```

## 5.4. Cạm bẫy thường gặp

- **Commit file migration nhưng quên snapshot:** `IdentityModuleDbContextModelSnapshot.cs` phải commit cùng migration, thiếu nó máy khác diff sai. `git add -A` rồi kiểm `git status` để chắc.
- **Lỡ commit secret:** connection string vẫn ở User Secrets (Day 2). Kiểm tra `appsettings.json` không chứa mật khẩu thật trước khi push.

## 5.5. Xong Day 3 khi

- [x] Ba lệnh verify ở 5.1 đều xanh.
- [ ] Đã commit (và push nếu muốn) với message Conventional Commits tiếng Việt.
- [ ] Nhắn mentor **"review Day 3"**.

→ Quay lại [README Day 3](README.md) hoặc xem trước [Day 4](../README.md).
