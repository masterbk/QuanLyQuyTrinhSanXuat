# Bàn giao dự án HanoiCheckPlatform — để Claude (phiên/tài khoản khác) làm tiếp

> Tài liệu này gói đủ ngữ cảnh để một phiên Claude mới tiếp tục phát triển mà không cần trí nhớ cũ.
> Cập nhật lần cuối: 08/10/2026.

## 1. Tổng quan
- **Sản phẩm:** phần mềm quản lý quy trình sản xuất cho **tiệm bánh Tuấn Nghĩa**: quy trình sản xuất,
  kho (theo lô/FEFO), đơn hàng bán, biểu mẫu kiểm soát ATTP (GMP/ISO), đồng bộ HanoiCheck (HnC) tuỳ chọn.
- **Repo:** github.com/masterbk/QuanLyQuyTrinhSanXuat (nhánh chính `master`).
- **Bàn giao:** **bán đứt toàn bộ** cho khách hàng (không phải dùng nội bộ) → tránh commit thông tin hạ tầng/secret.

## 2. Công nghệ & cấu trúc
- **Web/API:** .NET 8, Blazor Server + MudBlazor, EF Core (SqlServer), Finbuckle MultiTenant, QuestPDF (PDF).
  - `src/HCP.Domain` (entity + enum + helper), `src/HCP.Infrastructure` (service + EF + migration),
    `src/HCP.Web` (Blazor pages `Pages/CoSo/`, minimal API `Api/`, `Program.cs` DI ~dòng 189, `Shared/NavMenu.razor`).
  - `tests/HCP.Tests` (xUnit, EF InMemory). Hiện **250 test** pass.
- **App mobile:** Flutter ở `mobile/` (package `hcp_mobile`), Riverpod + dio (`lib/loi/api.dart`).
  Tính năng theo `lib/tinh_nang/<ten>/`. **33 test** pass.

## 3. Quy ước BẮT BUỘC (hay sai)
- **Đa cơ sở (multitenant):** entity nghiệp vụ kế thừa `TenantEntity : AuditableEntity`, cấu hình
  `.IsMultiTenant()` + `.AdjustUniqueIndexes()` trong `AppDbContext.OnModelCreating`. Namespace entity
  `HCP.Domain.Entities.Business`, enum `HCP.Domain.Enums`.
- **Giờ Việt Nam:** DB lưu **UTC**. Mọi "hôm nay/hiển thị/gửi đi" dùng `HCP.Domain.GioVietNam` (`.Nay`, `.HomNay`).
  **CẤM** `DateTime.Now` / `ToLocalTime`. App có `lib/loi/gio_viet_nam.dart`.
- **Service trả `KetQuaThaoTac`** (record Ok/Loi, `.ThanhCong`, `.ThongBao`). DI đăng ký ở `Program.cs`.
- **Migration:** `dotnet ef migrations add <Tên> --project src/HCP.Infrastructure --startup-project src/HCP.Web --context AppDbContext --output-dir Persistence/Migrations` (ĐỪNG dùng `--no-build`: dễ sinh migration rỗng/xoá nhầm migration cũ). Migration **tự áp khi app khởi động** (`Database.MigrateAsync`).
- **Quyền (AppRoles):** `TenantAdmin, TenantStaff, TenantSanXuat, TenantGiaoHang`. Chuỗi gộp:
  `QuyenNhapLieu`=Admin+Staff, `QuyenSanXuat`=+SanXuat, `QuyenGiaoHang`=+GiaoHang, `MoiNguoiDungCoSo`=cả 4.
  Nhân viên đăng nhập bằng **số điện thoại** (hoặc email).
- **Namespace bẫy:** namespace `HCP.*.Services.BieuMau` TRÙNG tên type `BieuMau` → dùng alias
  `using BieuMauEntity = HCP.Domain.Entities.Business.BieuMau;`.
- **Ngôn ngữ:** làm việc bằng **tiếng Việt**, tài liệu gọn, **kiểm chứng bằng chạy thật** (build/test/deploy), không nói suông.

## 4. Môi trường & deploy
- **Dev (máy này):** .NET 8.0.425, Flutter 3.44.1. SQL Server local; `appsettings.Development.json`,
  `appsettings.Production.json`, `firebase-adminsdk.json` đều **gitignore**.
  - ⚠️ **Ổ C: hay đầy** → `flutter build/test/analyze` hay treo. Luôn đặt
    `TEMP=/e/flutter-tmp TMP=/e/flutter-tmp` (bash) hoặc `$env:TEMP="E:\flutter-tmp"` (pwsh) khi chạy Flutter.
- **Production:** `https://quanly.tuannghiabakery.vn` (IIS/Windows Server). Cấu hình prod (chuỗi kết nối SQL,
  tenant, khoá HnC) nằm **trong `appsettings.json` TRÊN SERVER** (không có file Production riêng) →
  deploy **tuyệt đối không ghi đè** file này, `firebase-adminsdk.json`, `web.config`, `wwwroot/uploads`.
- **Deploy tự động:** `pwsh -File deploy\deploy.ps1` (thêm `-SkipBuild` nếu đã publish). Chi tiết + cách
  khôi phục ở `deploy/README.md`. Thông tin VPS (IP/user/đường dẫn) nằm ở `deploy/deploy.local.ps1`
  (**gitignore**, chỉ trên máy này; mẫu: `deploy/deploy.local.ps1.example`). SSH key (không mật khẩu) ở
  `~/.ssh/tuannghia_deploy`. Script: publish → **loại file secret khỏi gói** → scp → `app_offline.htm` nhả
  khoá → **robocopy KHÔNG /PURGE** (giữ config+uploads) → gỡ app_offline; có backup DLL ở `C:\deploy\backup`.
- **App mobile deploy:** build **APK trên GitHub Actions CI** (`.github/workflows/mobile-android.yml`,
  tự chạy khi push `master` đổi `mobile/**`). KHÔNG qua `deploy.ps1`. Thay đổi app chỉ tới điện thoại khi cài APK mới.
- **An toàn:** production là hệ thật của khách → **xác nhận trước khi ghi/deploy**. Không bỏ private key/mật khẩu
  vào chat hay repo. Người dùng cũng **tự deploy thủ công** đôi khi, và **đôi khi xử lý đơn thẳng trên cổng
  HanoiCheck** → trạng thái đơn nội bộ lệch là BÌNH THƯỜNG.

## 5. Lệnh hay dùng
```bash
# Test .NET
dotnet test tests/HCP.Tests/HCP.Tests.csproj --nologo -v q
# Build web
dotnet build src/HCP.Web/HCP.Web.csproj -c Debug --nologo -clp:ErrorsOnly
# Flutter (đặt TEMP sang ổ E vì ổ C hay đầy)
cd mobile && TEMP=/e/flutter-tmp TMP=/e/flutter-tmp flutter test
cd mobile && TEMP=/e/flutter-tmp TMP=/e/flutter-tmp flutter analyze
# Deploy production
pwsh -File deploy/deploy.ps1
```

## 6. Trạng thái các module

### Biểu mẫu kiểm soát (GMP/ISO) — engine động, đã deploy
Cho nhân viên nhập dữ liệu các biểu mẫu ATTP thay giấy. KHÔNG đồng bộ HnC (hồ sơ nội bộ).
- **Mô hình:** `BieuMau`(MaHieu, BoCuc enum TheoNgay/Checklist/NhieuDongTuDo, NhomQuyen, **MotPhieuMoiNgay**)
  → `TruongBieuMau`(Ma khoá, Kieu enum Text/So/Gio/Ngay/DatKhongDat/ChonSanPham/ChonNhanSu/ChonNcc/ChonCoSo/
  **Anh**/LuaChon, LaDauPhieu, BatBuoc, TuyChonCsv, **LaHanNhac**) + `HangMucBieuMau`(chỉ Checklist).
  `PhieuGhiNhan`(Ngay, GiaTriDauJson, TrangThai enum **Nhap/DaGhiNhan**[+ChoThamTra/DaThamTra để dành],
  cột thẩm tra để trống sẵn) → `DongGhiNhan`(HangMucBieuMauId?, **GiaTriJson** {ma_truong: giá trị}).
- **Giá trị phiếu lưu theo khoá `Ma`** của trường (không theo định nghĩa hiện tại) → sửa mẫu không mất dữ liệu.
  Hạng mục Checklist liên kết bằng Id thật → `LuuAsync` **upsert giữ Id** (đừng xoá-tạo lại).
- **ĐÃ XONG:** định nghĩa mẫu (web `QuanLyBieuMau.razor`), seed 9 mẫu mặc định (nhóm A/B/C/D) idempotent theo
  MaHieu (`NapMauMacDinhAsync`, KHÔNG seed ở DbSeeder vì multitenant), app nhập phiếu form động
  (`mobile/lib/tinh_nang/bieu_mau/`), lưu nháp/nhập tiếp, trường đầu phiếu động, sắp xếp trường (nút ▲▼),
  **khóa phiếu theo ngày** (MotPhieuMoiNgay: 1 phiếu/ngày → sửa nháp/xem khi hoàn thành/chặn trùng;
  nhiều phiếu/ngày → danh sách+tạo mới), xác nhận trước khi Hoàn thành, **trường Ảnh** (API
  `/api/v1/phieu-ghi-nhan/anh`, app chụp/tải/xem, PDF nhúng ảnh qua `IDocAnhPhieu`), in **PDF** phiếu +
  báo cáo tháng nhóm A (QuestPDF), **nhắc hạn** thiết bị (LaHanNhac → `LayNhacHanAsync` + API
  `/api/v1/bieu-mau/nhac-han` + web `/app/nhac-han` + app màn Nhắc hạn & banner).
- **CÒN LẠI (chỉ 1):** **QC thẩm tra** (duyệt/ký phiếu) — đã để sẵn cột/trạng thái, **người dùng chủ động
  tạm BỎ**, làm sau nếu cần.
- **Việc thủ công trên prod sau deploy:** bấm "Nạp biểu mẫu mẫu" để thêm 2 mẫu nhóm D mới; (tuỳ) đổi mẫu
  KPH sang "Nhiều phiếu/ngày".

### Các module khác (xem memory nếu cần chi tiết)
- **Kho + sản xuất nội bộ:** tồn theo lô, lệnh SX trừ định mức, khách hàng. GĐ1 xong.
- **Bán hàng NCC / đơn hàng:** đơn bán thống nhất; đơn HnC tự thành đơn bán; công tắc HanoiCheck tổng & từng
  bản ghi; nhân viên giao hàng trên app; QR in được (web+app). Đồng bộ đơn kéo về từ cổng NCC (ký HMAC).
- **App mobile:** màn Lệnh SX (tham gia khâu), Đơn hàng (giao hàng, quét QR, chụp ảnh), Kho, Biểu mẫu. Còn:
  chạy thử máy thật đầy đủ.
- **Thông báo đẩy FCM**, **giờ VN**, **audit form Sửa có tải lại dữ liệu** (còn vài màn) — xem memory.

## 7. Trí nhớ chi tiết (máy này)
Thư mục `C:\Users\thuyb\.claude\projects\E--TuanNghia\memory\` có các file .md theo chủ đề (chỉ mục ở
`MEMORY.md`). Nếu phiên mới trên **cùng máy này**, Claude tự nạp. Nếu **máy/tài khoản khác**, tài liệu này
(trong repo) là nguồn bàn giao chính; các điểm quan trọng đã tóm ở trên.

## 8. Bẫy đã gặp (đừng vấp lại)
- `dotnet ef migrations ... --no-build` → migration rỗng, và `migrations remove --no-build` **xoá nhầm**
  migration trước đó. Luôn để nó build.
- EF "another instance with same key": khi sửa entity có con mang Id cũ, ép `Id=0` cho con mới trước khi gán,
  hoặc upsert theo Id (xem `BieuMauService.CapNhatHangMuc`, `LenhSanXuatService.CapNhatAsync`).
- Blazor: `MudDialogInstance` (không phải `IMudDialogInstance`). Build MSB3027 file bị khoá → tắt app/preview đang chạy.
- Flutter: `TextFormField/DropdownButtonFormField.initialValue` chỉ áp lần đầu → đổi dữ liệu phải đổi `Key`
  để dựng lại ô (xem `_napLan`/`_khoaO` trong `man_nhap_phieu.dart`). Riverpod 3 dùng `asyncValue.value`.
