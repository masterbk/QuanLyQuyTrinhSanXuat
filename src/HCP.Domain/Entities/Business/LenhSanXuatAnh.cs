using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Ảnh chứng từ của lô thành phẩm, bắt buộc chụp khi hoàn thành lệnh sản xuất.
/// Nếu lệnh có sinh Lô sản xuất đồng bộ HanoiCheck thì mỗi ảnh cũng được đưa vào
/// danh sách file của lô (<see cref="BatchFile"/>, loại HINH_ANH).
/// </summary>
public class LenhSanXuatAnh : TenantEntity
{
    public int Id { get; set; }

    public int LenhSanXuatSanPhamId { get; set; }
    public LenhSanXuatSanPham? LenhSanXuatSanPham { get; set; }

    /// <summary>Mã file (khoá nghiệp vụ gửi sang HanoiCheck), vd LSX-001-A1.</summary>
    public string MaFile { get; set; } = string.Empty;

    /// <summary>Tên file gốc người dùng tải lên.</summary>
    public string TenFile { get; set; } = string.Empty;

    /// <summary>Đường dẫn phục vụ ảnh, vd /uploads/{cơ sở}/2026/09/{guid}.jpg</summary>
    public string DuongDan { get; set; } = string.Empty;

    public DateTime ThoiGianUtc { get; set; } = DateTime.UtcNow;
}
