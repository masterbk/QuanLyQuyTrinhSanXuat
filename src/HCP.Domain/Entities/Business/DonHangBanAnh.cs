using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>Ảnh tổng quan của đơn hàng bán, gắn khi xuất kho - gửi kèm sang HanoiCheck với đơn nguồn HanoiCheck
/// (danh_sach_anh của POST orders/{code}/process). Danh sách THAY THẾ TOÀN BỘ mỗi lần xuất kho lại.</summary>
public class DonHangBanAnh : TenantEntity
{
    public int Id { get; set; }

    public int DonHangBanId { get; set; }
    public DonHangBan? DonHangBan { get; set; }

    public string TenAnh { get; set; } = string.Empty;

    public string DuongDan { get; set; } = string.Empty;
}
