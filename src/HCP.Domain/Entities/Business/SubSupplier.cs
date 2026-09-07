using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Nhà cung ứng đầu vào (nguồn nguyên liệu của cơ sở).
/// Đồng bộ qua POST /supplier/sub-suppliers/merge (đồng bộ, trả 200).
/// </summary>
public class SubSupplier : TenantEntity
{
    public int Id { get; set; }

    /// <summary>ma_ncc_dau_vao - khoá nghiệp vụ.</summary>
    public string MaNccDauVao { get; set; } = string.Empty;

    /// <summary>ten.</summary>
    public string Ten { get; set; } = string.Empty;

    /// <summary>ma_so_thue - tối đa 20 ký tự.</summary>
    public string? MaSoThue { get; set; }

    public string? DiaChi { get; set; }

    /// <summary>dien_thoai - tối đa 20 ký tự.</summary>
    public string? DienThoai { get; set; }

    /// <summary>nhom_thuc_pham - danh sách mã nhóm thực phẩm cung ứng, bắt buộc tối thiểu 1.</summary>
    public List<SubSupplierFoodGroup> NhomThucPham { get; set; } = new();

    // --- giay_chung_nhan_attp (HnC nhận dạng MẢNG, mỗi phần tử cần ten_giay_chung_nhan) ---
    /// <summary>ten_giay_chung_nhan - tên giấy chứng nhận ATTP (HnC bắt buộc khi có khai ATTP).</summary>
    public string? AttpTenGiay { get; set; }
    public string? AttpSoGiay { get; set; }
    public DateOnly? AttpNgayCap { get; set; }
    public DateOnly? AttpNgayHetHan { get; set; }

    // --- hop_dong: gửi thì cả 3 trường đều bắt buộc ---
    public string? HopDongSo { get; set; }
    public DateOnly? HopDongNgayKy { get; set; }
    public DateOnly? HopDongNgayHetHan { get; set; }

    /// <summary>Có khai báo giấy chứng nhận ATTP hay không (quyết định có gửi mảng sang HnC).</summary>
    public bool CoGiayChungNhanAttp =>
        !string.IsNullOrWhiteSpace(AttpTenGiay) || !string.IsNullOrWhiteSpace(AttpSoGiay)
        || AttpNgayCap.HasValue || AttpNgayHetHan.HasValue;

    public bool CoHopDong =>
        !string.IsNullOrWhiteSpace(HopDongSo) || HopDongNgayKy.HasValue || HopDongNgayHetHan.HasValue;
}
