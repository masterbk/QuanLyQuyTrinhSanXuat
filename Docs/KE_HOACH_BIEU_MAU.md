# Kế hoạch: Chức năng Biểu mẫu kiểm soát (GMP/ISO) cho nhân viên nhập liệu

> Cho phép nhân viên nhập dữ liệu các biểu mẫu kiểm soát ATTP (nhiệt độ tủ, đèn UV, vệ sinh xe,
> check list vệ sinh, tiếp nhận nguyên liệu, công đoạn nướng/trộn, sản phẩm không phù hợp, thiết bị/
> bảo dưỡng…) trên **web** và **app**, thay cho biểu mẫu giấy.

## 1. Bối cảnh — 10 biểu mẫu mẫu (phân loại theo CẤU TRÚC)

| Nhóm | Bố cục | Biểu mẫu |
|---|---|---|
| **A** | Theo ngày (mỗi ngày 1 phiếu, ít trường) | Kiểm soát nhiệt độ tủ đông/mát (sáng & chiều); Nhật ký vận hành đèn UV; Giám sát vệ sinh xe chở hàng |
| **B** | Checklist hạng mục cố định (tick Đạt/KĐ theo ca) | Check list vệ sinh hàng ngày (20 hạng mục) |
| **C** | Phiếu nhiều dòng tự do (mỗi lần = 1 phiếu) | Tiếp nhận nguyên liệu NCC; Công đoạn nướng; Trộn/nặn bánh; Theo dõi sản phẩm không phù hợp (KPH) |
| **D** | Danh mục + sổ theo dõi định kỳ | Danh mục thiết bị đo + hiệu chuẩn; Sổ theo dõi bảo dưỡng TTB |

Đặc điểm chung: header (công ty, mã hiệu biểu mẫu, ngày/lần ban hành) · ngữ cảnh kỳ (ngày/tháng, bộ phận/
khu vực/tổ/biển số xe) · bảng dữ liệu nhiều cột · footer (quy chuẩn/ghi chú + QC thẩm tra ký). Tham chiếu
danh mục sẵn có: Thành phẩm, Nhân sự, NCC đầu vào, Cơ sở. **Không đồng bộ HanoiCheck** (hồ sơ nội bộ).

## 2. Quyết định đã chốt (07/10/2026)

- **Engine biểu mẫu động** (không code cứng từng mẫu) — cấu hình được, khách tự thêm/sửa mẫu không cần lập trình.
- Phủ cả 4 nhóm A/B/C/D.
- **Chỉ ghi nhận** (chưa có QC thẩm tra) — nhưng thiết kế trạng thái + cột nullable để **bật bước QC duyệt/ký sau** mà không phải sửa dữ liệu cũ.
- **Có in PDF** theo đúng dáng biểu mẫu gốc.
- Seed dần cả 10 mẫu.

## 3. Mô hình dữ liệu (lõi engine)

- **`BieuMau`** (mẫu): `MaHieu`, `Ten`, `BoCuc` (TheoNgay | Checklist | NhieuDongTuDo), `TanSuat`,
  `NhomQuyen` (vai trò được điền), `GhiChuChan` (quy chuẩn/chú thích), `KichHoat`, `ThuTu`.
- **`TruongBieuMau`** (cột/trường): `Ten`, `Ma` (khóa), `Kieu` (Text | So | Gio | Ngay | DatKhongDat |
  ChonSanPham | ChonNhanSu | ChonNcc | ChonCoSo | Anh | LuaChon), `BatBuoc`, `DonVi`, `GiaTriChuan`,
  `TuyChonCsv` (cho LuaChon), `Nhom` (gộp cột), `ThuTu`.
- **`HangMucBieuMau`** (chỉ Checklist B): `Ten`, `DienGiai`, `TanSuat`, `ThuTu` (các dòng cố định).
- **`PhieuGhiNhan`** (phiếu): `BieuMauId`, `Ngay`, `Ca`, `KhuVuc`, `NguoiLap`, `TrangThai`
  (Nhap | DaGhiNhan; để dành ChoThamTra | DaThamTra), `GhiChu`, + cột thẩm tra để trống sẵn
  (`NguoiThamTra`, `ThoiGianThamTraUtc`, `KetQuaThamTra`), `ThoiGianUtc`.
- **`DongGhiNhan`** (dòng): `PhieuId`, `HangMucId?` (Checklist), `ThuTu`, `GiaTriJson` (dict khóa→giá trị), `GhiChu`.
- **Ảnh**: dùng lại `ILuuTruAnhService`; gắn phiếu hoặc dòng.

## 4. Luồng

- **Web (quản trị):** màn "Biểu mẫu" định nghĩa mẫu/trường/hạng mục (seed sẵn 10 mẫu); màn "Phiếu ghi nhận"
  xem/lọc theo mẫu/ngày/người.
- **App (nhân viên):** mục "Biểu mẫu" → chọn mẫu được phép → tạo phiếu (ngày/ca/khu vực) → form động
  tự sinh theo kiểu trường → chụp ảnh → lưu; lọc "Hôm nay/Của tôi", nhắc theo tần suất.
- **In PDF theo mẫu:** sinh PDF giống dáng giấy (header + bảng + ô ký). Dùng thư viện PDF .NET **đa nền tảng**
  (vd QuestPDF) — tránh System.Drawing, tiện cả khi chạy Linux. Nhóm A có báo cáo tháng (bảng ~30 dòng ngày).

## 5. Giai đoạn

1. **GĐ1 — Lõi engine**: entities + migration + màn web định nghĩa mẫu + **seed nhóm A/B** + API. *(đang làm)*
2. **GĐ2 — App nhập liệu** (form động) cho nhóm A + B.
3. **GĐ3 — Nhóm C** (phiếu nhiều dòng tự do, gắn Thành phẩm/NCC) + seed.
4. **GĐ4 — In PDF** theo mẫu + báo cáo tháng.
5. **GĐ5 — Nhóm D** (thiết bị đo/hiệu chuẩn + bảo dưỡng) + nhắc hạn kiểm định/bảo dưỡng.
6. **Về sau** — bật bước QC thẩm tra (đã để sẵn chỗ).

## 6. Lưu ý

- Module lớn (tầm cỡ module kho/sản xuất).
- Mỗi mẫu gắn nhóm quyền được điền; có thể thêm vai trò QC/KCS riêng ở GĐ sau.
- Tài liệu gốc 10 biểu mẫu: thư mục upload của người dùng (không commit — có dữ liệu công ty).
