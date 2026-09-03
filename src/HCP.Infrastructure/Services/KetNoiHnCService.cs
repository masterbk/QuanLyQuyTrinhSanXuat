using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services;

public class KetNoiHnCService : IKetNoiHnCService
{
    private readonly AppDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly IHanoiCheckTokenClient _tokenClient;

    public KetNoiHnCService(AppDbContext db,
                            ISecretProtector protector,
                            IHanoiCheckTokenClient tokenClient)
    {
        _db = db;
        _protector = protector;
        _tokenClient = tokenClient;
    }

    public Task<TenantHnCCredential?> LayCauHinhAsync(string tenantId, CancellationToken ct = default) =>
        _db.TenantHnCCredentials.FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);

    public async Task<KetQuaThaoTac> LuuCauHinhAsync(string tenantId, CauHinhKetNoiRequest request,
                                                     CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return KetQuaThaoTac.Loi("Không xác định được cơ sở đang đăng nhập.");
        }

        var cauHinh = await LayCauHinhAsync(tenantId, ct);
        var laTaoMoi = cauHinh is null;

        if (laTaoMoi)
        {
            // Lần đầu cấu hình thì bắt buộc phải có đủ cả hai secret.
            if (string.IsNullOrWhiteSpace(request.ClientSecret) || string.IsNullOrWhiteSpace(request.HmacSecret))
            {
                return KetQuaThaoTac.Loi("Lần đầu cấu hình phải nhập đầy đủ client_secret và hmac_secret.");
            }

            cauHinh = new TenantHnCCredential { TenantId = tenantId };
            _db.TenantHnCCredentials.Add(cauHinh);
        }

        var doiClientId = !string.Equals(cauHinh!.ClientId, request.ClientId.Trim(), StringComparison.Ordinal);
        var doiBaseUrl = !string.Equals(cauHinh.BaseUrl, request.BaseUrl.Trim(), StringComparison.Ordinal);

        cauHinh.BaseUrl = request.BaseUrl.Trim().TrimEnd('/');
        cauHinh.ClientId = request.ClientId.Trim();

        var doiSecret = false;

        // Để trống nghĩa là giữ nguyên secret cũ - tránh bắt người dùng gõ lại secret dài
        // mỗi lần chỉnh sửa thông tin khác.
        if (!string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            cauHinh.ClientSecretEncrypted = _protector.Protect(request.ClientSecret.Trim());
            doiSecret = true;
        }

        if (!string.IsNullOrWhiteSpace(request.HmacSecret))
        {
            cauHinh.HmacSecretEncrypted = _protector.Protect(request.HmacSecret.Trim());
            doiSecret = true;
        }

        // Đổi bất kỳ thành phần nào thì trạng thái xác thực cũ không còn giá trị,
        // buộc cơ sở kiểm tra lại trước khi tin là kết nối vẫn tốt.
        if (doiClientId || doiBaseUrl || doiSecret)
        {
            cauHinh.DaXacThuc = false;
            cauHinh.NgayXacThucUtc = null;
            cauHinh.LoiXacThucGanNhat = null;

            // Token cũ cấp theo credential cũ - phải bỏ đi.
            var token = await _db.TenantOAuthTokens.FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
            if (token is not null) _db.TenantOAuthTokens.Remove(token);
        }

        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok("Đã lưu cấu hình kết nối. Hãy bấm \"Kiểm tra kết nối\" để xác nhận.");
    }

    public async Task<ConnectionTestResult> KiemTraKetNoiAsync(string tenantId, CancellationToken ct = default)
    {
        var cauHinh = await LayCauHinhAsync(tenantId, ct);

        if (cauHinh is null)
        {
            return ConnectionTestResult.Loi("Cơ sở chưa cấu hình thông tin kết nối HanoiCheck.");
        }

        var clientSecret = _protector.TryUnprotect(cauHinh.ClientSecretEncrypted);

        if (clientSecret is null)
        {
            return ConnectionTestResult.Loi(
                "Không giải mã được client_secret đã lưu. Vui lòng nhập lại secret rồi thử lại.");
        }

        var ketQua = await _tokenClient.KiemTraKetNoiAsync(
            cauHinh.BaseUrl, cauHinh.ClientId, clientSecret, ct);

        cauHinh.DaXacThuc = ketQua.ThanhCong;
        cauHinh.NgayXacThucUtc = ketQua.ThanhCong ? DateTime.UtcNow : null;
        cauHinh.LoiXacThucGanNhat = ketQua.ThanhCong ? null : ketQua.ThongBao;
        await _db.SaveChangesAsync(ct);

        return ketQua;
    }
}
