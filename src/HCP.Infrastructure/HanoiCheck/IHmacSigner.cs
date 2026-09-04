namespace HCP.Infrastructure.HanoiCheck;

/// <summary>Ba header ký HMAC phải đính kèm mỗi request nghiệp vụ gửi HanoiCheck.</summary>
public sealed record HmacHeaders(string Timestamp, string Nonce, string Signature);

/// <summary>
/// Ký request theo Lớp 2 của đặc tả HanoiCheck (mục 1.2).
///
/// Canonical string nối bằng ký tự '\n':
///     METHOD
///     PATH
///     TIMESTAMP
///     NONCE
///     SHA256_HEX(raw_body)
/// X-Signature = Base64(HMAC_SHA256(canonical_string, hmac_secret)).
///
/// LƯU Ý: chữ ký là Base64, còn hash của body là hex thường - hai kiểu mã hoá khác nhau,
/// rất dễ nhầm. Dùng ĐÚNG hmac_secret của CƠ SỞ đang gửi, tuyệt đối không dùng nhầm secret
/// giữa các cơ sở (đây là rủi ro nghiêm trọng của kiến trúc multi-tenant).
/// </summary>
public interface IHmacSigner
{
    /// <summary>
    /// Sinh đủ 3 header cho một request thực tế: tự lấy timestamp hiện tại và nonce ngẫu nhiên.
    /// </summary>
    /// <param name="hmacSecret">hmac_secret ở dạng ĐÃ GIẢI MÃ của đúng cơ sở đang gửi.</param>
    /// <param name="method">Phương thức HTTP viết hoa, vd "POST".</param>
    /// <param name="path">Đường dẫn tuyệt đối, vd "/api/supplier/warehouses/merge" (không kèm host).</param>
    /// <param name="rawBody">Thân request đúng từng byte sẽ gửi đi (rỗng nếu không có body).</param>
    HmacHeaders Ky(string hmacSecret, string method, string path, string rawBody);

    /// <summary>
    /// Nhân tất định: tính chữ ký từ timestamp và nonce cho trước. Tách riêng để kiểm thử
    /// đối chiếu với một vector chuẩn (golden vector) tính độc lập bên ngoài.
    /// </summary>
    string TinhChuKy(string hmacSecret, string method, string path,
                     string timestamp, string nonce, string rawBody);
}
