using HCP.Domain.Entities.Common;
using HCP.Domain.Enums;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Thực phẩm / SKU của cơ sở - danh mục ổn định, khai một lần rồi dùng lại cho nhiều lô nhập.
/// Đồng bộ qua POST /supplier/foods/merge (bất đồng bộ, trả 202).
/// </summary>
public class Product : TenantEntity
{
    public int Id { get; set; }

    /// <summary>ma_san_pham - mã SKU, khoá nghiệp vụ.</summary>
    public string MaSanPham { get; set; } = string.Empty;

    /// <summary>ten_san_pham.</summary>
    public string TenSanPham { get; set; } = string.Empty;

    /// <summary>ma_loai_sp - mã danh mục thực phẩm chuẩn của HnC (vd "THIT"). Bắt buộc.</summary>
    public string MaLoaiSp { get; set; } = string.Empty;

    /// <summary>ma_thuc_pham_chuan - mã thực phẩm chuẩn đối chiếu danh mục quốc gia (nếu có).</summary>
    public string? MaThucPhamChuan { get; set; }

    /// <summary>gtin - mã vạch GTIN/barcode.</summary>
    public string? Gtin { get; set; }

    /// <summary>quoc_gia - quốc gia xuất xứ.</summary>
    public string? QuocGia { get; set; }

    /// <summary>mo_ta.</summary>
    public string? MoTa { get; set; }

    /// <summary>ma_quy_trinh - mã quy trình sản xuất áp dụng (nếu có).</summary>
    public string? MaQuyTrinh { get; set; }

    // --- Phục vụ quản lý kho + sản xuất nội bộ (KHÔNG gửi sang HanoiCheck) ---

    /// <summary>Phân loại nguyên liệu / thành phẩm.</summary>
    public LoaiSanPham LoaiSanPham { get; set; } = LoaiSanPham.ThanhPham;

    /// <summary>Đơn vị tính dùng cho tồn kho (vd kg, cái, quả).</summary>
    public string? DonViTinh { get; set; }

    /// <summary>Mức tồn tối thiểu để cảnh báo "sắp hết" trên dashboard (null = không cảnh báo).</summary>
    public decimal? TonToiThieu { get; set; }

    /// <summary>Định mức (công thức) - chỉ có ý nghĩa với thành phẩm.</summary>
    public List<DinhMucNguyenLieu> DanhSachDinhMuc { get; set; } = new();
}
