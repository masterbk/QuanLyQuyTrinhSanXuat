using HCP.Domain.Entities.Common;
using HCP.Domain.Enums;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Thực phẩm / SKU của cơ sở - danh mục ổn định, khai một lần rồi dùng lại cho nhiều lô nhập.
/// Đồng bộ qua POST /supplier/foods/merge (bất đồng bộ, trả 202).
/// </summary>
public class Product : TenantEntity, ICoDongBoHnC
{
    public int Id { get; set; }

    /// <summary>Có gửi bản ghi này sang HanoiCheck không (khi cơ sở bật đồng bộ). Mặc định có.</summary>
    public bool DongBoHnC { get; set; } = true;

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

    // ----- Cấu hình đơn vị tính gửi HanoiCheck (food_config, đặc tả v2.6). Chỉ gửi khi đã chọn đơn vị. -----

    /// <summary>food_config.measure_id - id đơn vị tính trong danh mục GET /measures (bảng DonViTinhHnC).</summary>
    public int? DonViTinhHnCId { get; set; }

    /// <summary>food_config.gam_exchange - 1 đơn vị tính quy đổi ra bao nhiêu gam.</summary>
    public decimal? QuyDoiGam { get; set; }

    /// <summary>food_config.extrude_factor - hệ số thải bỏ, tính theo phần trăm.</summary>
    public decimal? HeSoThaiBoPhanTram { get; set; }

    /// <summary>food_config.extrude_factor_g - hệ số thải bỏ, tính theo gam.</summary>
    public decimal? HeSoThaiBoGam { get; set; }

    /// <summary>Định mức (công thức) - chỉ có ý nghĩa với thành phẩm.</summary>
    public List<DinhMucNguyenLieu> DanhSachDinhMuc { get; set; } = new();
}
