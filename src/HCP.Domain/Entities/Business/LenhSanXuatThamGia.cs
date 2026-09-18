using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Nhân viên sản xuất tự tham gia lệnh bằng cách quét mã QR của lệnh trên app: ghi nhận ai tham gia, lúc nào.
/// Khâu cụ thể người đó làm vẫn nằm ở <see cref="LenhSanXuatKhau.NguoiThucHienCsv"/>.
/// </summary>
public class LenhSanXuatThamGia : TenantEntity
{
    public int Id { get; set; }

    public int LenhSanXuatId { get; set; }
    public LenhSanXuat? LenhSanXuat { get; set; }

    public string MaNhanSu { get; set; } = string.Empty;

    /// <summary>Họ tên lúc tham gia - để hiển thị khỏi phải tra lại danh mục nhân sự.</summary>
    public string? HoTen { get; set; }

    /// <summary>Lần đầu tham gia.</summary>
    public DateTime ThoiGianUtc { get; set; }
}
