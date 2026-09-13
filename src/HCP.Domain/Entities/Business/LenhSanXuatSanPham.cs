using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Một thành phẩm trong lệnh sản xuất. Mỗi dòng là MỘT LÔ riêng: có mã lô, hạn dùng,
/// quy trình sản xuất, các khâu (kèm người thực hiện và cơ sở) và ảnh chứng từ của chính lô đó.
///
/// Khi đồng bộ HanoiCheck, mỗi dòng này sinh ra một <see cref="Batch"/> độc lập.
/// </summary>
public class LenhSanXuatSanPham : TenantEntity
{
    public int Id { get; set; }

    public int LenhSanXuatId { get; set; }
    public LenhSanXuat? LenhSanXuat { get; set; }

    /// <summary>Mã thành phẩm (SKU) cần làm.</summary>
    public string MaThanhPham { get; set; } = string.Empty;

    public decimal SoLuong { get; set; }

    /// <summary>Mã lô thành phẩm tạo ra - khoá truy xuất nguồn gốc.</summary>
    public string MaLoThanhPham { get; set; } = string.Empty;

    public DateOnly? HanSuDung { get; set; }

    /// <summary>
    /// Quy trình sản xuất áp dụng cho lô này. Mặc định lấy theo khai báo ở danh mục thành phẩm,
    /// người dùng đổi được khi lập lệnh.
    /// </summary>
    public string MaQuyTrinh { get; set; } = string.Empty;

    /// <summary>Mã lô (Batch) đã sinh để đồng bộ HanoiCheck, nếu có.</summary>
    public string? MaLoDaTao { get; set; }

    /// <summary>Các khâu của quy trình, mỗi khâu ghi ai làm và làm ở cơ sở nào.</summary>
    public List<LenhSanXuatKhau> Khau { get; set; } = new();

    /// <summary>Nguyên liệu (theo lô) đã tiêu hao cho riêng lô thành phẩm này.</summary>
    public List<LenhSanXuatTieuHao> TieuHao { get; set; } = new();

    /// <summary>Ảnh chụp lô thành phẩm (bắt buộc ít nhất 1 khi hoàn thành lệnh).</summary>
    public List<LenhSanXuatAnh> Anh { get; set; } = new();
}
