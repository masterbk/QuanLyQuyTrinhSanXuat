using HCP.Domain.Constants;
using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Đơn hàng (kèm xuất kho). Đồng bộ qua POST /supplier/orders/merge (bất đồng bộ, trả 202).
///
/// Hai loại: "food" (dòng dùng ma_loai_sp, được xuất kho) và "dish" (dòng dùng ma_mon_an,
/// KHÔNG được gửi xuat_kho - HnC trả 422 nếu gửi).
/// </summary>
public class Order : TenantEntity
{
    public int Id { get; set; }

    /// <summary>ma_don_hang - khoá nghiệp vụ.</summary>
    public string MaDonHang { get; set; } = string.Empty;

    /// <summary>loai_don_hang - "food" (mặc định) hoặc "dish".</summary>
    public string LoaiDonHang { get; set; } = Constants.LoaiDonHang.Food;

    /// <summary>ma_truong - mã trường học.</summary>
    public string MaTruong { get; set; } = string.Empty;

    public string? DiaChiNhan { get; set; }
    public string? DiemGiao { get; set; }

    /// <summary>ma_nguoi_giao - mã nhân sự giao hàng.</summary>
    public string? MaNguoiGiao { get; set; }

    /// <summary>trang_thai - một trong các giá trị của <see cref="TrangThaiDonHang"/>.</summary>
    public string TrangThai { get; set; } = "CHO_XAC_NHAN";

    /// <summary>ngay_don_hang.</summary>
    public DateOnly? NgayDonHang { get; set; }

    public string? GhiChu { get; set; }

    /// <summary>images - ảnh tổng quan đơn, tối đa 3.</summary>
    public List<OrderImage> Images { get; set; } = new();

    /// <summary>chi_tiet - dòng đặt hàng, bắt buộc ≥1.</summary>
    public List<OrderLine> ChiTiet { get; set; } = new();

    /// <summary>xuat_kho - chỉ áp dụng đơn food.</summary>
    public List<OrderExport> XuatKho { get; set; } = new();
}
