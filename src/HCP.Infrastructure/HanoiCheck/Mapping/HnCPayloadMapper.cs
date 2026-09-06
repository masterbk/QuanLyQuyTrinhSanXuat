using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using HCP.Domain.Entities.Business;

namespace HCP.Infrastructure.HanoiCheck.Mapping;

/// <summary>
/// Dựng payload JSON đúng định dạng các endpoint merge của HanoiCheck từ entity nội bộ.
///
/// Mỗi endpoint merge nhận một MẢNG bản ghi; ở đây ta gửi từng bản ghi một (mảng 1 phần tử)
/// ngay khi cơ sở lưu thay đổi. Tên trường theo đúng đặc tả (snake_case), trường không bắt buộc
/// mà rỗng thì BỎ HẲN khỏi JSON (nhờ JsonIgnoreCondition.WhenWritingNull) thay vì gửi null.
/// </summary>
public static class HnCPayloadMapper
{
    /// <summary>
    /// Tùy chọn tuần tự hoá dùng CHUNG cho toàn bộ payload gửi HnC và cho phần enqueue outbox:
    /// giữ nguyên tên trường snake_case do DTO tự đặt, bỏ trường null, để tiếng Việt nguyên văn.
    /// </summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static object Kho(Warehouse w) => new[]
    {
        new { ma_kho = w.MaKho, ten_kho = w.TenKho, dia_chi = w.DiaChi, dien_tich = w.DienTich }
    };

    public static object CoSo(Facility f) => new[]
    {
        new { ma_co_so = f.MaCoSo, ten_co_so = f.TenCoSo, dia_chi = f.DiaChi }
    };

    public static object Khau(ProductionStep s) => new[]
    {
        new { ma_khau = s.MaKhau, ten_khau = s.TenKhau, ghi_chu = s.GhiChu }
    };

    public static object QuyTrinh(ProductionProcess p) => new[]
    {
        new
        {
            ma_quy_trinh = p.MaQuyTrinh,
            ten_quy_trinh = p.TenQuyTrinh,
            ma_danh_muc_thuc_pham = p.MaDanhMucThucPham, // int? - bỏ nếu null (xem điểm cần hỏi HnC)
            danh_sach_khau = p.DanhSachKhau
                .OrderBy(l => l.ThuTu)
                .Select(l => new { ma_khau = l.MaKhau, thu_tu = l.ThuTu })
                .ToArray()
        }
    };

    public static object NccDauVao(SubSupplier s) => new[]
    {
        new
        {
            ma_ncc_dau_vao = s.MaNccDauVao,
            ten = s.Ten,
            ma_so_thue = s.MaSoThue,
            dia_chi = s.DiaChi,
            dien_thoai = s.DienThoai,
            nhom_thuc_pham = s.NhomThucPham.Select(g => g.MaNhom).ToArray(),

            // Chỉ gửi object giấy ATTP / hợp đồng khi có khai; nếu không thì bỏ hẳn khỏi JSON.
            giay_chung_nhan_attp = s.CoGiayChungNhanAttp
                ? new
                {
                    so_giay = s.AttpSoGiay,
                    ngay_cap = Ngay(s.AttpNgayCap),
                    ngay_het_han = Ngay(s.AttpNgayHetHan)
                }
                : null,
            hop_dong = s.CoHopDong
                ? new
                {
                    so_hop_dong = s.HopDongSo,
                    ngay_ky = Ngay(s.HopDongNgayKy),
                    ngay_het_han = Ngay(s.HopDongNgayHetHan)
                }
                : null
        }
    };

    public static object ThucPham(Product p) => new[]
    {
        new
        {
            ma_san_pham = p.MaSanPham,
            ten_san_pham = p.TenSanPham,
            ma_loai_sp = MaLoaiSp(p.MaLoaiSp),
            ma_thuc_pham_chuan = p.MaThucPhamChuan,
            gtin = p.Gtin,
            quoc_gia = p.QuocGia,
            mo_ta = p.MoTa,
            ma_quy_trinh = p.MaQuyTrinh
        }
    };

    public static object LoSanXuat(Batch b) => new[]
    {
        new
        {
            ma_san_pham = b.MaSanPham,
            ma_lo = b.MaLo,
            ten_lo = b.TenLo,
            ngay_nhap = Ngay(b.NgayNhap),
            ngay_san_xuat = Ngay(b.NgaySanXuat),
            han_su_dung = Ngay(b.HanSuDung),
            dia_chi_thu_mua = b.DiaChiThuMua,
            ma_co_so = b.MaCoSo,
            ma_ncc_dau_vao = b.MaNccDauVao,
            ghi_chu = b.GhiChu,

            // danh_sach_kho bắt buộc ≥1 (service đã kiểm tra), luôn gửi.
            danh_sach_kho = b.DanhSachKho.Select(w => new { ma_kho = w.MaKho }).ToArray(),

            // Các mảng tuỳ chọn: rỗng thì bỏ hẳn thay vì gửi [].
            danh_sach_khau = b.DanhSachKhau.Count == 0 ? null : b.DanhSachKhau
                .OrderBy(s => s.ThuTu)
                .Select(s => new
                {
                    ma_buoc_sx = s.MaBuocSx,
                    ma_khau = s.MaKhau,
                    thu_tu = s.ThuTu,
                    ma_lo_nhap = s.MaLoNhap,
                    ma_lo_nguyen_lieu = s.MaLoNguyenLieu,
                    ma_lo_san_xuat = s.MaLoSanXuat,
                    thoi_gian = s.ThoiGian?.ToString("yyyy-MM-dd HH:mm:ss"),
                    danh_sach_nguoi_thuc_hien = s.NguoiThucHien.Count == 0 ? null : s.NguoiThucHien.ToArray(),
                    dia_chi = s.DiaChi,
                    trang_thai = s.TrangThai,
                    ma_qr_truy_vet = s.MaQrTruyVet,
                    ghi_chu = s.GhiChu,
                    ma_co_so = s.MaCoSo,
                    ma_ncc_dau_vao = s.MaNccDauVao
                })
                .ToArray(),

            danh_sach_file = b.DanhSachFile.Count == 0 ? null : b.DanhSachFile
                .Select(f => new
                {
                    ma_file = f.MaFile,
                    ma_khau = f.MaKhau,
                    ma_buoc_sx = f.MaBuocSx,
                    ten_file = f.TenFile,
                    duong_dan = f.DuongDan,
                    loai = f.Loai
                })
                .ToArray()
        }
    };

    public static object DonHang(Order o) => new[]
    {
        new
        {
            ma_don_hang = o.MaDonHang,
            loai_don_hang = o.LoaiDonHang,
            ma_truong = o.MaTruong,
            dia_chi_nhan = o.DiaChiNhan,
            diem_giao = o.DiemGiao,
            ma_nguoi_giao = o.MaNguoiGiao,
            trang_thai = o.TrangThai,
            ngay_don_hang = Ngay(o.NgayDonHang),
            ghi_chu = o.GhiChu,

            images = o.Images.Count == 0 ? null : o.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => new { path_file = i.PathFile, sort_order = i.SortOrder })
                .ToArray(),

            // chi_tiet bắt buộc ≥1 (service đã kiểm tra). Đơn food gửi ma_loai_sp, đơn dish gửi
            // ma_mon_an; trường null bị bỏ nên mỗi dòng chỉ mang đúng khoá theo loại đơn.
            chi_tiet = o.ChiTiet.Select(l => new
            {
                ma_loai_sp = l.MaLoaiSp,
                ma_mon_an = l.MaMonAn,
                so_luong = l.SoLuong,
                path_file = l.PathFile
            }).ToArray(),

            // xuat_kho chỉ gửi khi có (chỉ đơn food mới có - service chặn đơn dish).
            xuat_kho = o.XuatKho.Count == 0 ? null : o.XuatKho
                .Select(x => new
                {
                    ma_xuat_kho = x.MaXuatKho,
                    ma_loai_sp = x.MaLoaiSp,
                    ma_san_pham = x.MaSanPham,
                    ma_kho = x.MaKho,
                    ma_lo = x.MaLo,
                    so_luong = x.SoLuong
                })
                .ToArray()
        }
    };

    public static object MonAn(Dish d) => new[]
    {
        new
        {
            ma_mon_an = d.MaMonAn,
            ten_mon_an = d.TenMonAn,
            nhom_tuoi_id = d.NhomTuoiId,
            mo_ta = d.MoTa,
            ma_co_so = d.MaCoSo,
            ma_quy_trinh = d.MaQuyTrinh,

            // Công thức bắt buộc ≥1 (service đã kiểm tra), luôn gửi.
            danh_sach_nguyen_lieu = d.DanhSachNguyenLieu.Select(i => new
            {
                ma_nguyen_lieu = i.MaNguyenLieu,
                dinh_luong = i.DinhLuong,
                don_vi_tinh_id = i.DonViTinhId
            }).ToArray(),

            danh_sach_khau = d.DanhSachKhau.Count == 0 ? null : d.DanhSachKhau
                .OrderBy(s => s.ThuTu)
                .Select(s => new
                {
                    ma_buoc_sx = s.MaBuocSx,
                    ma_khau = s.MaKhau,
                    thu_tu = s.ThuTu,
                    thoi_gian = s.ThoiGian?.ToString("yyyy-MM-dd HH:mm:ss"),
                    danh_sach_nguoi_thuc_hien = s.NguoiThucHien.Count == 0 ? null : s.NguoiThucHien.ToArray(),
                    dia_chi = s.DiaChi,
                    trang_thai = s.TrangThai,
                    ma_qr_truy_vet = s.MaQrTruyVet,
                    ghi_chu = s.GhiChu,
                    ma_co_so = s.MaCoSo,
                    ma_ncc_dau_vao = s.MaNccDauVao
                })
                .ToArray(),

            danh_sach_file = d.DanhSachFile.Count == 0 ? null : d.DanhSachFile
                .Select(f => new
                {
                    ma_file = f.MaFile,
                    ma_khau = f.MaKhau,
                    ma_buoc_sx = f.MaBuocSx,
                    ten_file = f.TenFile,
                    duong_dan = f.DuongDan,
                    loai = f.Loai
                })
                .ToArray()
        }
    };

    public static object NhanSu(Staff s) => new[]
    {
        new
        {
            ma_nhan_su = s.MaNhanSu,
            ho_ten = s.HoTen,
            vi_tri = s.ViTri,
            ngay_sinh = Ngay(s.NgaySinh),
            dia_chi = s.DiaChi,
            dien_thoai = s.DienThoai,
            cccd = string.IsNullOrWhiteSpace(s.Cccd) ? null : s.Cccd,
            la_chu_co_so = s.LaChuCoSo,
            la_nguoi_che_bien = s.LaNguoiCheBien,
            la_nguoi_giao_hang = s.LaNguoiGiaoHang,
            phuong_tien = s.PhuongTien,
            bien_so = s.BienSo,
            trang_thai = s.TrangThai,

            giay_kham_suc_khoe = s.CoGiayKhamSucKhoe
                ? new
                {
                    so_giay = s.KskSoGiay,
                    ngay_kham = Ngay(s.KskNgayKham),
                    ngay_het_han = Ngay(s.KskNgayHetHan),
                    noi_kham = s.KskNoiKham
                }
                : null,
            chung_nhan_tap_huan_attp = s.CoChungNhanAttp
                ? new
                {
                    so_chung_nhan = s.AttpSoChungNhan,
                    ngay_cap = Ngay(s.AttpNgayCap),
                    co_quan_cap = s.AttpCoQuanCap
                }
                : null
        }
    };

    /// <summary>
    /// ma_loai_sp = id (số) của danh mục thực phẩm chuẩn HnC. Gửi dạng SỐ nếu là id; nếu vì lý do
    /// nào đó là chuỗi (dữ liệu cũ) thì gửi nguyên chuỗi. Rỗng thì bỏ (WhenWritingNull).
    /// </summary>
    private static object? MaLoaiSp(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null
        : int.TryParse(value, out var id) ? id : value;

    /// <summary>Định dạng ngày theo ISO yyyy-MM-dd đúng như ví dụ trong đặc tả.</summary>
    private static string? Ngay(DateOnly? d) => d?.ToString("yyyy-MM-dd");
}
