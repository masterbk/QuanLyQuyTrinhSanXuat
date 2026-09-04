using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Nhân sự của cơ sở (quản lý, người chế biến, người giao hàng...).
/// Đồng bộ qua POST /supplier/users/merge (đồng bộ, trả 200).
///
/// CCCD là dữ liệu cá nhân nhạy cảm nên KHÔNG lưu thẳng: cột <see cref="CccdEncrypted"/> giữ
/// bản đã mã hoá, còn <see cref="Cccd"/> là plaintext [không map DB] chỉ tồn tại trong bộ nhớ
/// để nhập/hiển thị và dựng payload gửi HnC. Tầng service chịu trách nhiệm mã hoá/giải mã.
/// </summary>
public class Staff : TenantEntity
{
    public int Id { get; set; }

    /// <summary>ma_nhan_su - khoá nghiệp vụ.</summary>
    public string MaNhanSu { get; set; } = string.Empty;

    /// <summary>ho_ten.</summary>
    public string HoTen { get; set; } = string.Empty;

    /// <summary>vi_tri - vị trí công việc.</summary>
    public string? ViTri { get; set; }

    /// <summary>ngay_sinh.</summary>
    public DateOnly? NgaySinh { get; set; }

    /// <summary>dia_chi.</summary>
    public string? DiaChi { get; set; }

    /// <summary>dien_thoai - tối đa 20 ký tự.</summary>
    public string? DienThoai { get; set; }

    /// <summary>cccd đã mã hoá (rỗng nếu không khai). KHÔNG hiển thị/gửi trực tiếp cột này.</summary>
    public string CccdEncrypted { get; set; } = string.Empty;

    /// <summary>cccd plaintext - KHÔNG map vào DB (bảng .Ignore). Chỉ dùng ở bộ nhớ.</summary>
    public string? Cccd { get; set; }

    /// <summary>la_chu_co_so.</summary>
    public bool LaChuCoSo { get; set; }

    /// <summary>la_nguoi_che_bien.</summary>
    public bool LaNguoiCheBien { get; set; }

    /// <summary>la_nguoi_giao_hang (shipper).</summary>
    public bool LaNguoiGiaoHang { get; set; }

    /// <summary>phuong_tien - loại phương tiện giao hàng (nếu là shipper).</summary>
    public string? PhuongTien { get; set; }

    /// <summary>bien_so - biển số xe (nếu là shipper).</summary>
    public string? BienSo { get; set; }

    /// <summary>trang_thai - đang hoạt động hay không.</summary>
    public bool TrangThai { get; set; } = true;

    // --- giay_kham_suc_khoe: gửi object thì so_giay/ngay_kham/ngay_het_han bắt buộc, noi_kham tuỳ ---
    public string? KskSoGiay { get; set; }
    public DateOnly? KskNgayKham { get; set; }
    public DateOnly? KskNgayHetHan { get; set; }
    public string? KskNoiKham { get; set; }

    // --- chung_nhan_tap_huan_attp: gửi object thì so_chung_nhan/ngay_cap bắt buộc, co_quan_cap tuỳ ---
    public string? AttpSoChungNhan { get; set; }
    public DateOnly? AttpNgayCap { get; set; }
    public string? AttpCoQuanCap { get; set; }

    /// <summary>Có khai giấy khám sức khoẻ hay không (quyết định có gửi object sang HnC).</summary>
    public bool CoGiayKhamSucKhoe =>
        !string.IsNullOrWhiteSpace(KskSoGiay) || KskNgayKham.HasValue
        || KskNgayHetHan.HasValue || !string.IsNullOrWhiteSpace(KskNoiKham);

    public bool CoChungNhanAttp =>
        !string.IsNullOrWhiteSpace(AttpSoChungNhan) || AttpNgayCap.HasValue
        || !string.IsNullOrWhiteSpace(AttpCoQuanCap);
}
