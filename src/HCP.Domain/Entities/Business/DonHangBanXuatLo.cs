using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>Số lượng lấy từ một lô thành phẩm cho một dòng đơn bán khi xuất kho.</summary>
public class DonHangBanXuatLo : TenantEntity
{
    public int Id { get; set; }

    public int DonHangBanDongId { get; set; }
    public DonHangBanDong? DonHangBanDong { get; set; }

    public string MaLo { get; set; } = string.Empty;

    /// <summary>Hạn dùng của lô tại lúc xuất - dùng khi ghi bút toán đảo lúc huỷ đơn.</summary>
    public DateOnly? HanSuDung { get; set; }

    public decimal SoLuong { get; set; }
}
