# Bàn giao dự án HanoiCheckPlatform — để Claude (phiên/tài khoản khác) làm tiếp

> Tài liệu này gói đủ ngữ cảnh để một phiên Claude mới tiếp tục phát triển mà không cần trí nhớ cũ.
> Cập nhật lần cuối: 08/10/2026 (tối - thêm tự đổi mật khẩu, ký tay + QR đối chiếu, lọc khoảng ngày + xuất zip PDF).

## 1. Tổng quan
- **Sản phẩm:** phần mềm quản lý quy trình sản xuất cho **tiệm bánh Tuấn Nghĩa**: quy trình sản xuất,
  kho (theo lô/FEFO), đơn hàng bán, biểu mẫu kiểm soát ATTP (GMP/ISO), đồng bộ HanoiCheck (HnC) tuỳ chọn.
- **Repo:** github.com/masterbk/QuanLyQuyTrinhSanXuat (nhánh chính `master`).
- **Bàn giao:** **bán đứt toàn bộ** cho khách hàng (không phải dùng nội bộ) → tránh commit thông tin hạ tầng/secret.

## 2. Công nghệ & cấu trúc
- **Web/API:** .NET 8, Blazor Server + MudBlazor, EF Core (SqlServer), Finbuckle MultiTenant, QuestPDF (PDF).
  - `src/HCP.Domain` (entity + enum + helper), `src/HCP.Infrastructure` (service + EF + migration),
    `src/HCP.Web` (Blazor pages `Pages/CoSo/`, minimal API `Api/`, `Program.cs` DI ~dòng 189, `Shared/NavMenu.razor`).
  - `tests/HCP.Tests` (xUnit, EF InMemory). Hiện **259 test** pass.
- **App mobile:** Flutter ở `mobile/` (package `hcp_mobile`), Riverpod + dio (`lib/loi/api.dart`).
  Tính năng theo `lib/tinh_nang/<ten>/`. **46 test** pass.

## 3. Quy ước BẮT BUỘC (hay sai)
- **Đa cơ sở (multitenant):** entity nghiệp vụ kế thừa `TenantEntity : AuditableEntity`, cấu hình
  `.IsMultiTenant()` + `.AdjustUniqueIndexes()` trong `AppDbContext.OnModelCreating`. Namespace entity
  `HCP.Domain.Entities.Business`, enum `HCP.Domain.Enums`.
- **Giờ Việt Nam:** DB lưu **UTC**. Mọi "hôm nay/hiển thị/gửi đi" dùng `HCP.Domain.GioVietNam` (`.Nay`, `.HomNay`).
  **CẤM** `DateTime.Now` / `ToLocalTime`. App có `lib/loi/gio_viet_nam.dart`.
- **Service trả `KetQuaThaoTac`** (record Ok/Loi, `.ThanhCong`, `.ThongBao`; `.XungDot` = dữ liệu vừa bị người
  khác sửa → API trả 409). DI đăng ký ở `Program.cs`.
- **Migration:** `dotnet ef migrations add <Tên> --project src/HCP.Infrastructure --startup-project src/HCP.Web --context AppDbContext --output-dir Persistence/Migrations` (ĐỪNG dùng `--no-build`: dễ sinh migration rỗng/xoá nhầm migration cũ). Migration **tự áp khi app khởi động** (`Database.MigrateAsync`).
- **Quyền (AppRoles):** `TenantAdmin, TenantStaff, TenantSanXuat, TenantGiaoHang, TenantBieuMau`. Chuỗi gộp:
  `QuyenNhapLieu`=Admin+Staff, `QuyenSanXuat`=+SanXuat, `QuyenGiaoHang`=+GiaoHang, `QuyenBieuMau`=+BieuMau,
  `MoiNguoiDungCoSo`=cả 5. Nhân viên đăng nhập bằng **số điện thoại** (hoặc email).
- **Nhân sự của tài khoản:** dùng `LenhSanXuatApi.MaNhanSuTaiKhoanAsync`/`MaNhanSuHienTaiAsync` (đừng tự query).
  Tài khoản quản trị cơ sở KHÔNG gắn hồ sơ nhân sự → được hiểu là nhân sự `LaChuCoSo` (khi có đúng 1 người).
  **Đừng gắn NhanSuId cho tài khoản quản trị** (màn Nhân sự sẽ đổi UserName sang SĐT).
- **Tự đổi mật khẩu** (mọi tài khoản): `IDoiMatKhauService` (Infrastructure/Identity) - đổi xong thu hồi mọi phiên
  app + đổi security stamp (phiên web khác hết hiệu lực ~1 phút). Web: Razor Page `/doi-mat-khau` (Razor Page để gọi
  `RefreshSignInAsync` giữ phiên đang dùng) + nút 🔑 trên thanh trên. API `POST /api/v1/auth/doi-mat-khau` trả phiên
  mới cho máy đang dùng. App: menu tài khoản → `xac_thuc/man_doi_mat_khau.dart`. Lỗi Identity tiếng Việt: `LoiIdentity.MoTa`.
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
- **SQL production:** chạy được qua SSH: `ssh -i ~/.ssh/tuannghia_deploy <user>@<VPS> "sqlcmd -E -S <instance>
  -d HanoiCheckPlatform -W -s \"|\" -Q \"...\""` (thông tin VPS/instance ở `deploy/deploy.local.ps1` + memory).
  Luôn SELECT xem trước, UPDATE/DELETE kèm `Id` + `TenantId`, chỉ ghi khi người dùng yêu cầu.
- **An toàn:** production là hệ thật của khách → **xác nhận trước khi ghi/deploy**. Không bỏ private key/mật khẩu
  vào chat hay repo. Người dùng cũng **tự deploy thủ công** đôi khi, và **đôi khi xử lý đơn thẳng trên cổng
  HanoiCheck** → trạng thái đơn nội bộ lệch là BÌNH THƯỜNG.

### Chuyển máy chủ
- **Chép sang là chạy tiếp:** CSDL (backup/restore), `appsettings.json` (chuỗi kết nối, `Jwt:Key`, cấu hình
  HanoiCheck), file Firebase, `wwwroot\uploads`, và **khoá ký phiếu** - cả 2 file trong
  `C:\ProgramData\HanoiCheckPlatform\KhoaKyPhieu\` (`khoa-ky-phieu.pem` + `khoa-ky-phieu.pem.da-ky-bu`; thiếu file
  dấu thì máy mới ký bù lại phiếu chưa có chữ ký). Cho app pool quyền đọc. Chép bằng USB/SSH, không qua email/chat.
- **Chép khoá ký phiếu TRƯỚC khi chạy app lần đầu** - không thì app tự sinh khoá mới, mọi phiếu cũ báo đỏ (chữa:
  chép đè khoá cũ, khởi động lại; phiếu nào ký bằng khoá mới trong lúc đó phải ký lại).
- **KHÔNG chép được:** khoá Data Protection (`C:\ProgramData\HanoiCheckPlatform\DataProtectionKeys`) mã hoá DPAPI
  theo máy (`ProtectKeysWithDpapi(protectToLocalMachine: true)`). Trên máy mới phải **nhập lại**:
  1. Client secret + 2. HMAC secret kết nối HanoiCheck của từng cơ sở (`TenantHnCCredential`) - thiếu thì mọi đồng
     bộ/kéo đơn/đẩy trạng thái HanoiCheck lỗi.
  3. **CCCD của từng nhân sự** (`Staff.CccdEncrypted`) - thiếu thì ô CCCD trống, đồng bộ nhân sự gửi CCCD rỗng.
  Ngoài ra mọi người phải đăng nhập lại web (cookie cũng dùng khoá này). Mật khẩu tài khoản không ảnh hưởng.
- **Muốn khỏi nhập lại (chưa làm, ~nửa ngày):** trên máy cũ giải mã cả 3 loại rồi mã hoá lại bằng khoá Data
  Protection bảo vệ bằng chứng chỉ `.pfx` (`ProtectKeysWithCertificate`) → chỉ cần chép thư mục khoá + `.pfx`.

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
- **ĐÃ XONG:** định nghĩa mẫu (web `QuanLyBieuMau.razor`, có Ngày/Lần ban hành), seed **10 mẫu** mặc định (nhóm
  A/B/C/D + bẫy côn trùng `BM-GMP-ISO-02-04`) idempotent theo MaHieu (`NapMauMacDinhAsync`, KHÔNG seed ở DbSeeder;
  mẫu đã có KHÔNG bị cập nhật khi nạp lại), app nhập phiếu form động (`mobile/lib/tinh_nang/bieu_mau/`), lưu nháp/
  nhập tiếp, trường đầu phiếu, **khóa phiếu theo ngày** (MotPhieuMoiNgay), **trường Ảnh**, **nhắc hạn** thiết bị.
- **PDF (QuestPDF, `PhieuPdfService`):** header giống bản giấy `KhungHeader` = [logo | TÊN CÔNG TY + Đ/c | Mã hiệu /
  Ngày BH / Lần BH]; logo+tên+địa chỉ ở entity `CaiDatInBieuMau` (1 dòng/cơ sở, sửa ở màn Biểu mẫu kiểm soát →
  "Thông tin in"). **Báo cáo tháng mọi mẫu**: mẫu thường = bảng phẳng; Checklist = **ma trận hạng mục × ngày**
  (mỗi trường Đạt/KĐ là 1 cột con X/O, >62 cột con thì tách nửa tháng) + bảng ghi chú/không đạt.
- **Quyền nhập biểu mẫu:** CHỈ role `TenantBieuMau` (+ quản trị, nhập liệu) được nhập; giao mẫu theo người ở màn
  Nhân sự (bảng `PhanQuyenBieuMau`: TatCa / danh sách Id). Lọc ở `PhieuGhiNhanService.LayBieuMauChoNhapAsync` /
  `LayMauDuocXemAsync`; API + PDF trả 403 với mẫu không được giao. `BieuMau.NhomQuyen` ("Ai được điền") KHÔNG còn dùng.
- **Xem lại phiếu:** app tab "Phiếu đã nhập" (`man_lich_su.dart`, `man_chi_tiet_phieu.dart`; API
  `GET /api/v1/phieu-ghi-nhan/lich-su` phân trang + tóm tắt Đạt/KĐ, `/{id}/pdf`); web `/app/phieu-ghi-nhan`.
- **Nhập phiếu trên web** `/app/nhap-phieu` (`NhapPhieu.razor`): checklist/nhiều dòng dạng bảng, "Đạt các ô trống".
- **Ô chọn có tìm kiếm không dấu** (gõ "nghia" ra "Nghĩa"): web `MudAutocomplete`, app `o_chon_tim_kiem.dart`
  (`OChonTimKiem`, nên dùng lại cho màn mới).
- **Ngày/giờ hiển thị cố định:** trường Ngày luôn dd/MM/yyyy, Giờ 24h HH:mm (web MudDatePicker Culture vi-VN +
  MudTimePicker; app `hienThiNgay`). Dữ liệu lưu vẫn yyyy-MM-dd / HH:mm. KHÔNG dùng ô ngày/giờ gốc của trình duyệt.
- **Tự điền người:** nhập dữ liệu ô nào thì ô "Chọn nhân sự" CÒN TRỐNG cùng dòng + cùng `Nhom` tự = người đăng nhập
  (cố ý KHÔNG điền sẵn lúc mở phiếu để không lẫn ca sáng/chiều) - app `_dat`, web `Dat`.
- **Người lập:** phiếu lưu `NguoiLap` (mã NS), `NguoiLapUserId`, `TenNguoiLap`, `TenNguoiCapNhat`; "Do tôi lập"
  khớp mã NS HOẶC tài khoản.
- **Chống ghi đè:** `CapNhatPhieuAsync(phieu, hoanThanh, mocLuuLucMo)` so `ThoiGianUtc` lúc mở (lệch >1ms) → XungDot
  (API 409; app/web hộp thoại "Tải lại"). App gửi `thoiGianUtcGoc` khi PUT.
- **Ký tay khi Hoàn thành + QR đối chiếu bản gốc** (khách cần hồ sơ có giá trị khi gửi kiểm tra): app ký bằng ngón tay
  (`bieu_mau/man_ky_ten.dart`), web ký bằng chuột/cảm ứng trên canvas (`wwwroot/js/chu-ky.js`, nạp trong `_Host.cshtml`).
  Ảnh PNG tải qua `/phieu-ghi-nhan/anh` rồi gửi `chuKy`. Phiếu lưu `ChuKyAnh, TenNguoiKy, NguoiKyUserId, KyLucUtc,
  MaBamNoiDung` (SHA-256 nội dung chuẩn hoá - `PhieuGhiNhanService.TinhMaBam`), `MaTraCuu`. PDF có khối "Người lập & ký"
  + mã QR (QRCoder, URL = `Uploads:BaseUrl` + `/tra-cuu/phieu/{ma}`). Trang công khai `Pages/TraCuu/Phieu.cshtml`
  tính lại mã băm từ dữ liệu trong CSDL + xác minh **chữ ký số máy chủ** `ChuKyMayChu` (ECDSA P-256 trên
  `HCP-PHIEU-v1|MaTraCuu|MaBam`, `Security/KyPhieuMayChu.cs`). Khoá bí mật là file PEM ngoài CSDL: `KyPhieu:KhoaPath`,
  mặc định `C:\ProgramData\HanoiCheckPlatform\KhoaKyPhieu\khoa-ky-phieu.pem`, tự sinh lần đầu - **PHẢI SAO LƯU**, mất là
  mọi phiếu báo đỏ. Lần đầu có khoá thì ký bù phiếu cũ (`KyBuPhieuCuAsync`) rồi ghi file dấu `*.da-ky-bu` (dấu ở file,
  không ở CSDL, để người sửa CSDL không kích lại ký bù được). Phiếu thiếu chữ ký = không hợp lệ. QR mang `?h=<mã băm>`
  → trang tự báo bản PDF đang quét có phải bản hiện hành (PDF in trước 09/10/2026 không có h: người kiểm tra tự so 12
  ký tự in đậm). Trang KHÔNG đọc được chữ trên tờ PDF. Không chống được người có quyền quản trị máy chủ (đọc được file
  khoá) - muốn thế phải ký số PKI. Server chưa bắt buộc chữ ký
  (app cũ vẫn hoàn thành được). Đây là chữ ký điện tử nội bộ, KHÔNG phải chữ ký số PKI (đã tư vấn khách: file gửi
  cơ quan thì ký thêm bằng USB token của công ty).
- **Lọc + xuất hàng loạt:** web Phiếu ghi nhận lọc Từ ngày – Đến ngày; nút "Xuất PDF (.zip)" →
  `GET /app/phieu/xuat-zip?bieuMauId&tuNgay&denNgay` (`PhieuPdfApi`): mỗi phiếu 1 PDF `yyyy-MM-dd_MaHieu_Id.pdf`,
  tối đa `ToiDaXuatZip` = 500 phiếu/lần, chỉ mẫu người dùng được xem.
- **Hướng dẫn sử dụng cho khách:** `Docs/Huong-dan-su-dung-Bieu-mau-kiem-soat.docx` (11 mục: vai trò, thiết lập,
  cấu hình mẫu, giao quyền, nhập app, nhập web, xem/PDF/báo cáo/nhắc hạn, ký tay + QR, xuất zip, đổi mật khẩu,
  tình huống thường gặp). Bản gốc là Claude Doc, xuất Word rồi sửa dòng byline thành "Cập nhật ngày dd/MM/yyyy".
- **Dữ liệu prod:** phiếu nhập thử đã xoá hết (08/10/2026) - prod chưa có phiếu thật. Cơ sở thử "Hoa Sen" đã xoá
  hẳn (có backup DB trên server trước khi xoá); prod chỉ còn 1 cơ sở.
- **CÒN LẠI:** **QC thẩm tra** (duyệt/ký phiếu) — đã để sẵn cột/trạng thái, **người dùng chủ động tạm BỎ**.
- **Việc thủ công trên prod:** gán role "Nhân viên nhập biểu mẫu" cho nhân viên (màn Nhân sự); tải logo + kiểm tên/
  địa chỉ ở "Thông tin in"; nhập Ngày/Lần ban hành cho mẫu cũ (cột "Ban hành" báo "Chưa khai"); cài APK mới.

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
- **Blazor giữ 1 DbContext suốt phiên** → EF trả bản entity CŨ đang tracked thay vì đọc DB (không thấy người khác
  vừa sửa, ghi thì `DbUpdateConcurrencyException` làm treo màn). Trước khi đọc-để-cập-nhật phải Detach bản cũ
  (xem `PhieuGhiNhanService.CapNhatPhieuAsync`).
- Razor: không đặt biến tên `helper` (`@helper` là directive). Entity `PhieuGhiNhan.ThoiGianUtc` có mặc định = now,
  đừng dùng nó làm "không có giá trị".
- Razor `.cshtml`: ký tự `@` trong chữ phải viết `@@` (vd "(vd @@, #, !)").
- Flutter test có xuất ảnh (`toImage`) phải chạy trong `tester.runAsync`; SnackBar ở đáy có thể che nút → đợi nó ẩn.
- Kiểm UI ô có mặt nạ (Mask) bằng Playwright: gõ `press_sequentially(..., delay=80)`; gõ quá nhanh ra ngày sai.
- `sqlcmd` chạy file .sql: thêm `-I` (QUOTED_IDENTIFIER, bảng có filtered index) và `-f 65001` (chữ tiếng Việt).
- Kiểm UI web thật: Playwright cài ở `E:\pwlib` (`PYTHONPATH=/e/pwlib`), `chromium.launch(channel='msedge')` dùng
  Edge sẵn có (không tải trình duyệt vì ổ C gần đầy). Tài khoản dev thử: `coso-sx@example.vn` (mật khẩu hỏi người dùng/xem memory máy này).
- Flutter: `TextFormField/DropdownButtonFormField.initialValue` chỉ áp lần đầu → đổi dữ liệu phải đổi `Key`
  để dựng lại ô (xem `_napLan`/`_khoaO` trong `man_nhap_phieu.dart`). Riverpod 3 dùng `asyncValue.value`.
