using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.Security;

/// <summary>
/// Cài đặt dựa trên ASP.NET Core Data Protection - khoá do framework quản lý và tự xoay vòng,
/// không hardcode khoá trong source hay config.
///
/// LƯU Ý TRIỂN KHAI: mặc định khoá lưu trong thư mục cục bộ của ứng dụng. Khi chạy nhiều
/// instance hoặc deploy lại bằng container, phải cấu hình PersistKeysTo... dùng chung,
/// nếu không sẽ không giải mã được secret đã lưu.
/// </summary>
public class DataProtectionSecretProtector : ISecretProtector
{
    private const string Purpose = "HCP.TenantSecrets.v1";

    private readonly IDataProtector _protector;
    private readonly ILogger<DataProtectionSecretProtector> _logger;

    public DataProtectionSecretProtector(IDataProtectionProvider provider,
                                         ILogger<DataProtectionSecretProtector> logger)
    {
        _protector = provider.CreateProtector(Purpose);
        _logger = logger;
    }

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string? TryUnprotect(string protectedText)
    {
        try
        {
            return _protector.Unprotect(protectedText);
        }
        catch (Exception ex)
        {
            // Không log nội dung secret, chỉ log sự kiện.
            _logger.LogError(ex, "Không giải mã được secret đã lưu - có thể khoá Data Protection đã thay đổi.");
            return null;
        }
    }
}
