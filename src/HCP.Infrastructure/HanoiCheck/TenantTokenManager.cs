using System.Collections.Concurrent;
using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.HanoiCheck;

/// <inheritdoc cref="ITenantTokenManager"/>
public sealed class TenantTokenManager : ITenantTokenManager
{
    /// <summary>Coi token là hết hạn khi còn dưới ngưỡng này, tránh dùng token chết giữa chừng.</summary>
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(60);

    /// <summary>Đặc tả không trả hạn refresh_token; mặc định 14 ngày (mục 1.6).</summary>
    private static readonly TimeSpan HanRefreshMacDinh = TimeSpan.FromDays(14);

    // Khoá theo từng cơ sở để hai luồng job của CÙNG một cơ sở không đồng thời xin token
    // (gây tốn request và ghi đè token của nhau). Process-wide, keyed theo tenantId.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    private readonly AppDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly IHanoiCheckTokenClient _tokenClient;
    private readonly TimeProvider _clock;
    private readonly ILogger<TenantTokenManager> _logger;

    public TenantTokenManager(AppDbContext db,
                              ISecretProtector protector,
                              IHanoiCheckTokenClient tokenClient,
                              TimeProvider clock,
                              ILogger<TenantTokenManager> logger)
    {
        _db = db;
        _protector = protector;
        _tokenClient = tokenClient;
        _clock = clock;
        _logger = logger;
    }

    public async Task<TokenKetQua> LayAccessTokenAsync(string tenantId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return TokenKetQua.Loi("Không xác định được cơ sở khi lấy token.");

        var cauHinh = await _db.TenantHnCCredentials
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);

        if (cauHinh is null
            || string.IsNullOrWhiteSpace(cauHinh.BaseUrl)
            || string.IsNullOrWhiteSpace(cauHinh.ClientId)
            || string.IsNullOrWhiteSpace(cauHinh.ClientSecretEncrypted))
        {
            return TokenKetQua.ChuaCauHinh("Cơ sở chưa nhập đủ thông tin kết nối HanoiCheck.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;

        var lockCoSo = Locks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
        await lockCoSo.WaitAsync(ct);
        try
        {
            var tokenLuu = await _db.TenantOAuthTokens
                .FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);

            // Token còn hạn (trừ margin) thì dùng lại, không gọi HnC.
            if (tokenLuu is not null
                && !string.IsNullOrEmpty(tokenLuu.AccessToken)
                && tokenLuu.AccessTokenExpiresAtUtc - Margin > now)
            {
                return TokenKetQua.Co(tokenLuu.AccessToken);
            }

            var clientSecret = _protector.TryUnprotect(cauHinh.ClientSecretEncrypted);
            if (clientSecret is null)
            {
                return TokenKetQua.Loi(
                    "Không giải mã được client_secret đã lưu (khoá Data Protection có thể đã đổi). "
                    + "Cơ sở cần nhập lại secret ở màn Cài đặt kết nối.");
            }

            // Ưu tiên refresh_token nếu còn hạn - đỡ một vòng client_credentials.
            TokenFetchResult ketQua;
            if (tokenLuu is not null
                && !string.IsNullOrEmpty(tokenLuu.RefreshToken)
                && tokenLuu.RefreshTokenExpiresAtUtc > now)
            {
                ketQua = await _tokenClient.LamMoiTokenAsync(
                    cauHinh.BaseUrl, cauHinh.ClientId, clientSecret, tokenLuu.RefreshToken, ct);

                // Refresh thất bại (refresh_token hết hạn/không dùng được) -> lấy token mới hẳn.
                if (!ketQua.ThanhCong)
                {
                    _logger.LogInformation(
                        "Refresh token của cơ sở {TenantId} không dùng được, lấy token mới bằng client_credentials.",
                        tenantId);
                    ketQua = await _tokenClient.LayTokenAsync(
                        cauHinh.BaseUrl, cauHinh.ClientId, clientSecret, ct);
                }
            }
            else
            {
                ketQua = await _tokenClient.LayTokenAsync(
                    cauHinh.BaseUrl, cauHinh.ClientId, clientSecret, ct);
            }

            if (!ketQua.ThanhCong)
            {
                return TokenKetQua.Loi(ketQua.ThongBao ?? "Lấy token HanoiCheck thất bại.", ketQua.HttpStatusCode);
            }

            var token = ketQua.Token!;
            await LuuTokenAsync(tenantId, tokenLuu, token, now, ct);
            return TokenKetQua.Co(token.AccessToken!);
        }
        finally
        {
            lockCoSo.Release();
        }
    }

    private async Task LuuTokenAsync(string tenantId, TenantOAuthToken? hienCo,
                                     HanoiCheckTokenResponse token, DateTime now, CancellationToken ct)
    {
        var expiresIn = token.ExpiresIn > 0 ? token.ExpiresIn : 3600;

        if (hienCo is null)
        {
            hienCo = new TenantOAuthToken { TenantId = tenantId };
            _db.TenantOAuthTokens.Add(hienCo);
        }

        hienCo.AccessToken = token.AccessToken ?? string.Empty;
        // Nếu HnC không trả refresh_token mới (vd khi refresh), giữ lại cái cũ để còn dùng.
        if (!string.IsNullOrEmpty(token.RefreshToken))
        {
            hienCo.RefreshToken = token.RefreshToken;
            hienCo.RefreshTokenExpiresAtUtc = now.Add(HanRefreshMacDinh);
        }
        hienCo.AccessTokenExpiresAtUtc = now.AddSeconds(expiresIn);
        hienCo.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);
    }
}
