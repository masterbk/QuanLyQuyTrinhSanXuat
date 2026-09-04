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

    /// <summary>Định dạng ngày theo ISO yyyy-MM-dd đúng như ví dụ trong đặc tả.</summary>
    private static string? Ngay(DateOnly? d) => d?.ToString("yyyy-MM-dd");
}
