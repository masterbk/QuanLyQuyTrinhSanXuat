using HCP.Domain.Enums;
using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Ảnh của đơn hàng bán: ảnh tổng quan chụp lúc xuất kho và ảnh chứng minh đã giao (phân biệt bằng <see cref="Loai"/>).
/// Với đơn nguồn HanoiCheck, ảnh được gửi sang HnC ở danh_sach_anh của POST orders/{code}/process - danh sách bên đó
/// THAY THẾ TOÀN BỘ mỗi lần gửi, và HnC bắt buộc đơn có 1-3 ảnh trước khi chuyển "Đã giao".
/// </summary>
public class DonHangBanAnh : TenantEntity
{
    public int Id { get; set; }

    public int DonHangBanId { get; set; }
    public DonHangBan? DonHangBan { get; set; }

    public string TenAnh { get; set; } = string.Empty;

    public string DuongDan { get; set; } = string.Empty;

    public LoaiAnhDonHang Loai { get; set; } = LoaiAnhDonHang.TongQuan;
}
