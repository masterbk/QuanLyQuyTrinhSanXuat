using HCP.Domain.Entities.Common;
using HCP.Domain.Enums;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Đơn hàng bán thành phẩm của nhà cung cấp cho MỌI loại khách hàng (cửa hàng, đại lý, trường học...).
/// Thay cho "Phiếu xuất bán" và "Đơn hàng &amp; xuất kho" cũ.
///
/// Vòng đời: Chờ xác nhận → Đã xác nhận → Đang giao (xuất kho: trừ tồn theo lô) → Đã giao; hoặc Đã huỷ.
/// Đơn đến từ HanoiCheck (<see cref="NguonDonHang.HanoiCheck"/>) dùng chung mô hình này; chỉ các đơn đó mới
/// liên quan tới đồng bộ.
/// </summary>
public class DonHangBan : TenantEntity, ICoMaTraCuu
{
    public int Id { get; set; }

    /// <summary>Mã tra cứu công khai (QR) - chuỗi ngẫu nhiên, sinh khi mở mã QR lần đầu.</summary>
    public string? MaTraCuu { get; set; }

    /// <summary>Mã đơn (tự sinh DH-yyyyMMdd-001).</summary>
    public string MaDonHang { get; set; } = string.Empty;

    public string MaKhachHang { get; set; } = string.Empty;

    /// <summary>Kho xuất hàng cho đơn.</summary>
    public string MaKho { get; set; } = string.Empty;

    public DateOnly NgayDat { get; set; }

    /// <summary>Ngày hẹn giao.</summary>
    public DateOnly? NgayGiao { get; set; }

    public string? DiaChiGiao { get; set; }

    /// <summary>Nhân sự giao hàng (ma_nhan_su).</summary>
    public string? MaNguoiGiao { get; set; }

    public TrangThaiDonHangBan TrangThai { get; set; } = TrangThaiDonHangBan.ChoXacNhan;

    public NguonDonHang Nguon { get; set; } = NguonDonHang.NoiBo;

    /// <summary>Mã đơn trên HanoiCheck với đơn nguồn HanoiCheck.</summary>
    public string? MaDonHnC { get; set; }

    /// <summary>Trạng thái đơn trên HanoiCheck (CHO_XAC_NHAN, DANG_GIAO...) - chỉ để đối chiếu, app không chạy theo.</summary>
    public string? TrangThaiHnC { get; set; }

    /// <summary>
    /// HanoiCheck đã đổi hoặc huỷ đơn sau khi NCC xác nhận/xuất kho nên app KHÔNG tự áp - NCC cần xem lại.
    /// Tắt khi NCC lưu lại đơn.
    /// </summary>
    public bool HnCCoThayDoi { get; set; }

    public string? GhiChu { get; set; }

    public string? LyDoHuy { get; set; }

    /// <summary>Lúc xuất kho (trừ tồn). Có giá trị = đơn đã phát sinh giao dịch kho.</summary>
    public DateTime? ThoiGianXuatKhoUtc { get; set; }

    public DateTime? ThoiGianGiaoUtc { get; set; }

    public DateTime? ThoiGianHuyUtc { get; set; }

    public List<DonHangBanDong> Dong { get; set; } = new();

    /// <summary>Ảnh tổng quan chụp lúc xuất kho - gửi kèm sang HanoiCheck với đơn nguồn HanoiCheck.</summary>
    public List<DonHangBanAnh> AnhTongQuan { get; set; } = new();

    public decimal TongTien => Dong.Sum(d => d.ThanhTien);
}
