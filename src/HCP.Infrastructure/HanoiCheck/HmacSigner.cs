using System.Security.Cryptography;
using System.Text;

namespace HCP.Infrastructure.HanoiCheck;

/// <inheritdoc cref="IHmacSigner"/>
public sealed class HmacSigner : IHmacSigner
{
    public HmacHeaders Ky(string hmacSecret, string method, string path, string rawBody)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = TaoNonce();
        var signature = TinhChuKy(hmacSecret, method, path, timestamp, nonce, rawBody);
        return new HmacHeaders(timestamp, nonce, signature);
    }

    public string TinhChuKy(string hmacSecret, string method, string path,
                            string timestamp, string nonce, string rawBody)
    {
        if (string.IsNullOrEmpty(hmacSecret))
            throw new ArgumentException("Thiếu hmac_secret để ký request.", nameof(hmacSecret));

        // Body được băm bằng SHA-256 rồi biểu diễn hex THƯỜNG (khác với chữ ký ở dưới dùng Base64).
        var bodyHashHex = Sha256Hex(rawBody ?? string.Empty);

        // Nối đúng thứ tự đặc tả, phân tách bằng '\n'. Không thêm '\n' ở cuối.
        var canonical = string.Join('\n', method, path, timestamp, nonce, bodyHashHex);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hmacSecret));
        var chuKy = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));

        // X-Signature = Base64(...) - đúng đặc tả mục 1.2.
        return Convert.ToBase64String(chuKy);
    }

    private static string Sha256Hex(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Nonce ngẫu nhiên an toàn mã hoá, duy nhất cho mỗi request (chống replay).</summary>
    private static string TaoNonce()
    {
        Span<byte> buffer = stackalloc byte[16];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }
}
