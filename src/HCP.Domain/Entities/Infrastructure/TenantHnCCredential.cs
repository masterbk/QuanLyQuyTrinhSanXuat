namespace HCP.Domain.Entities.Infrastructure;

/// <summary>
/// Bộ credential RIÊNG của từng cơ sở để gọi API HanoiCheck.
/// Mỗi cơ sở tự xin từ HanoiCheck (quy trình đăng ký 5 bước với cơ quan quản lý,
/// nằm NGOÀI phạm vi nền tảng này) rồi tự nhập vào màn "Cài đặt kết nối".
///
/// client_secret và hmac_secret là dữ liệu nhạy cảm - PHẢI mã hoá trước khi lưu.
/// Tuyệt đối không dùng credential của tenant này để ký request của tenant khác.
/// </summary>
public class TenantHnCCredential
{
    public string TenantId { get; set; } = string.Empty;
    public Tenant? Tenant { get; set; }

    /// <summary>Base URL môi trường HanoiCheck (sandbox hoặc production).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    /// <summary>client_secret đã mã hoá (dùng cho OAuth2 client_credentials).</summary>
    public string ClientSecretEncrypted { get; set; } = string.Empty;

    /// <summary>hmac_secret đã mã hoá - KHÁC client_secret, dùng để ký X-Signature.</summary>
    public string HmacSecretEncrypted { get; set; } = string.Empty;

    /// <summary>Đã bấm "Kiểm tra kết nối" và lấy token thành công ít nhất 1 lần.</summary>
    public bool DaXacThuc { get; set; }
    public DateTime? NgayXacThucUtc { get; set; }
    public string? LoiXacThucGanNhat { get; set; }
}
