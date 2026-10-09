using HCP.Domain.Entities.Common;
using HCP.Domain.Enums;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Một phiếu ghi nhận theo một biểu mẫu: nhân viên điền dữ liệu cho một ngày/ca/khu vực. Gồm nhiều dòng
/// (DongGhiNhan). Các cột thẩm tra để trống sẵn cho bước QC sau này.
/// </summary>
public class PhieuGhiNhan : TenantEntity
{
    public int Id { get; set; }

    public int BieuMauId { get; set; }
    public BieuMau? BieuMau { get; set; }

    public DateOnly Ngay { get; set; }

    /// <summary>Giá trị các trường ĐẦU PHIẾU (khu vực, biển số, tổ...): JSON {ma_truong: giá trị}.</summary>
    public string GiaTriDauJson { get; set; } = "{}";

    /// <summary>Mã nhân sự người lập phiếu (null nếu tài khoản không gắn nhân sự, vd chủ cơ sở).</summary>
    public string? NguoiLap { get; set; }

    /// <summary>Tài khoản đăng nhập đã lập phiếu - để lọc "Do tôi lập" kể cả khi tài khoản không gắn nhân sự.</summary>
    public string? NguoiLapUserId { get; set; }

    /// <summary>Tên người lập lúc tạo phiếu (họ tên nhân sự hoặc tên tài khoản) - để hiển thị.</summary>
    public string? TenNguoiLap { get; set; }

    /// <summary>Tên người lưu phiếu gần nhất - để báo "phiếu vừa được X cập nhật" khi hai người cùng sửa.</summary>
    public string? TenNguoiCapNhat { get; set; }

    public TrangThaiPhieu TrangThai { get; set; } = TrangThaiPhieu.DaGhiNhan;

    public string? GhiChu { get; set; }

    // ----- Chữ ký khi Hoàn thành (ký tay trên điện thoại / chuột trên máy tính) -----
    /// <summary>Đường dẫn ảnh chữ ký tay (PNG) đã tải lên.</summary>
    public string? ChuKyAnh { get; set; }
    public string? TenNguoiKy { get; set; }
    public string? NguoiKyUserId { get; set; }
    public DateTime? KyLucUtc { get; set; }

    /// <summary>SHA-256 (hex) nội dung phiếu lúc ký - đối chiếu khi quét QR để phát hiện sửa sau khi ký.</summary>
    public string? MaBamNoiDung { get; set; }

    /// <summary>Chữ ký số của máy chủ (base64url) trên (MaTraCuu, MaBamNoiDung) - khoá nằm ngoài CSDL, xem KyPhieuMayChu.</summary>
    public string? ChuKyMayChu { get; set; }

    /// <summary>Mã tra cứu công khai (QR trên PDF) - chuỗi ngẫu nhiên 32 ký tự, sinh khi ký.</summary>
    public string? MaTraCuu { get; set; }

    // ----- Để dành cho bước QC thẩm tra (chưa dùng) -----
    public string? NguoiThamTra { get; set; }
    public DateTime? ThoiGianThamTraUtc { get; set; }
    public bool? KetQuaThamTra { get; set; }

    public DateTime ThoiGianUtc { get; set; } = DateTime.UtcNow;

    public List<DongGhiNhan> Dong { get; set; } = new();
}
