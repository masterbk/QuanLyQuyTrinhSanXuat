using Finbuckle.MultiTenant.Abstractions;
using HCP.Domain.Enums;

namespace HCP.Domain.Entities.Infrastructure;

/// <summary>
/// Một cơ sở sản xuất đăng ký sử dụng nền tảng.
///
/// Về phía HanoiCheck, mỗi cơ sở là MỘT NHÀ CUNG CẤP (NCC) ĐỘC LẬP, có tài khoản
/// và bộ credential OAuth/HMAC riêng do HanoiCheck cấp - xem <see cref="TenantHnCCredential"/>.
///
/// Lớp này đồng thời là ITenantInfo của Finbuckle: Id chính là giá trị TenantId
/// gắn trên mọi bản ghi nghiệp vụ.
/// </summary>
public class Tenant : ITenantInfo
{
    // --- ITenantInfo ---
    public string? Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Định danh ngắn, duy nhất, dùng để tra cứu (vd "bep-abc"). </summary>
    public string? Identifier { get; set; }

    public string? Name { get; set; }

    // --- Hồ sơ đăng ký ---
    public string? MaSoThue { get; set; }
    public string? DiaChi { get; set; }
    public string? NguoiDaiDien { get; set; }
    public string EmailLienHe { get; set; } = string.Empty;
    public string? SoDienThoai { get; set; }

    /// <summary>Đường dẫn file giấy chứng nhận ATTP đính kèm khi đăng ký (nếu có).</summary>
    public string? GiayChungNhanAttpPath { get; set; }

    // --- Trạng thái duyệt ---
    public TenantStatus TrangThai { get; set; } = TenantStatus.PendingApproval;
    public DateTime NgayDangKyUtc { get; set; } = DateTime.UtcNow;
    public DateTime? NgayDuyetUtc { get; set; }
    public string? NguoiDuyet { get; set; }
    public string? LyDoTuChoi { get; set; }

    // --- Quan hệ ---
    public TenantHnCCredential? HnCCredential { get; set; }
    public TenantOAuthToken? OAuthToken { get; set; }
}
