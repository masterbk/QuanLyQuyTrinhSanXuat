using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Lô sản xuất/nhập hàng của một thực phẩm. Đồng bộ qua POST /supplier/batches/merge
/// (bất đồng bộ, trả 202). Đây là một trong hai entity phức tạp nhất: gồm thông tin lô,
/// danh sách kho chứa, danh sách khâu (bước sản xuất) và file minh chứng.
/// </summary>
public class Batch : TenantEntity
{
    public int Id { get; set; }

    /// <summary>ma_san_pham - thực phẩm sở hữu lô.</summary>
    public string MaSanPham { get; set; } = string.Empty;

    /// <summary>ma_lo - mã lô, khoá nghiệp vụ.</summary>
    public string MaLo { get; set; } = string.Empty;

    /// <summary>ten_lo.</summary>
    public string TenLo { get; set; } = string.Empty;

    /// <summary>ngay_nhap - bắt buộc.</summary>
    public DateOnly NgayNhap { get; set; }

    /// <summary>ngay_san_xuat.</summary>
    public DateOnly? NgaySanXuat { get; set; }

    /// <summary>han_su_dung.</summary>
    public DateOnly? HanSuDung { get; set; }

    /// <summary>dia_chi_thu_mua - tối đa 2000 ký tự.</summary>
    public string? DiaChiThuMua { get; set; }

    /// <summary>ma_co_so - cơ sở sản xuất ở cấp lô.</summary>
    public string? MaCoSo { get; set; }

    /// <summary>ma_ncc_dau_vao.</summary>
    public string? MaNccDauVao { get; set; }

    /// <summary>ghi_chu.</summary>
    public string? GhiChu { get; set; }

    /// <summary>danh_sach_kho - lô phải thuộc ít nhất một kho.</summary>
    public List<BatchWarehouse> DanhSachKho { get; set; } = new();

    /// <summary>danh_sach_khau - các bước sản xuất đã thực hiện cho lô.</summary>
    public List<BatchStep> DanhSachKhau { get; set; } = new();

    /// <summary>danh_sach_file - file minh chứng gắn theo khâu.</summary>
    public List<BatchFile> DanhSachFile { get; set; } = new();
}
