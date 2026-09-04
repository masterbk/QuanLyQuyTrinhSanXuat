using System.Text.RegularExpressions;
using HCP.Domain.Entities.Infrastructure;
using HCP.Domain.Enums;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Logging;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.Sync;

/// <inheritdoc cref="ISyncOutboxProcessor"/>
public sealed class SyncOutboxProcessor : ISyncOutboxProcessor
{
    /// <summary>Số lần thử tối đa trước khi chuyển sang chờ người dùng xử lý thủ công.</summary>
    public const int SoLanThuToiDa = 6;

    /// <summary>Khoảng chờ trước khi thử lại cơ sở chưa cấu hình xong credential.</summary>
    private static readonly TimeSpan ChoCauHinh = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly IHanoiCheckSyncClient _syncClient;
    private readonly ISystemLogWriter _logWriter;
    private readonly TimeProvider _clock;
    private readonly ILogger<SyncOutboxProcessor> _logger;

    public SyncOutboxProcessor(AppDbContext db,
                               IHanoiCheckSyncClient syncClient,
                               ISystemLogWriter logWriter,
                               TimeProvider clock,
                               ILogger<SyncOutboxProcessor> logger)
    {
        _db = db;
        _syncClient = syncClient;
        _logWriter = logWriter;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> XuLyCacBanGhiDenHanAsync(int gioiHan = 50, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        // Đến hạn = đang chờ/đang lỗi còn hạn retry/đang chờ cấu hình, và đã tới NextRetryAt.
        // Sắp theo TenantId để các bản ghi cùng cơ sở đi liền nhau (token đã cache theo cơ sở).
        var denHan = await _db.SyncOutboxItems
            .Where(o => (o.Status == SyncOutboxStatus.Pending
                         || o.Status == SyncOutboxStatus.Failed
                         || o.Status == SyncOutboxStatus.AwaitingCredential)
                        && (o.NextRetryAtUtc == null || o.NextRetryAtUtc <= now))
            .OrderBy(o => o.TenantId).ThenBy(o => o.Id)
            .Take(gioiHan)
            .ToListAsync(ct);

        var daXuLy = 0;
        foreach (var item in denHan)
        {
            if (ct.IsCancellationRequested) break;

            var ketQua = await _syncClient.GuiMergeAsync(item.TenantId, item.EntityType, item.PayloadJson, ct);
            ApDungKetQua(item, ketQua, now);

            await _db.SaveChangesAsync(ct);

            // Ghi nhật ký cho các lần THỰC SỰ gửi (bỏ qua trường hợp chưa cấu hình để đỡ nhiễu log).
            if (ketQua.TrangThai != SyncTrangThai.ChuaCauHinh)
            {
                await GhiNhatKyAsync(item, ketQua, now, ct);
            }

            daXuLy++;
        }

        if (daXuLy > 0)
            _logger.LogInformation("Engine đồng bộ đã xử lý {SoLuong} bản ghi outbox.", daXuLy);

        return daXuLy;
    }

    private void ApDungKetQua(SyncOutboxItem item, SyncSendResult ketQua, DateTime now)
    {
        item.LastHttpStatusCode = ketQua.HttpStatusCode;

        switch (ketQua.TrangThai)
        {
            case SyncTrangThai.ThanhCong:
                item.Status = SyncOutboxStatus.Success;
                item.CompletedAtUtc = now;
                item.NextRetryAtUtc = null;
                item.LastError = null;
                break;

            case SyncTrangThai.ChuaCauHinh:
                // Không phải lỗi: chờ cơ sở nhập xong credential rồi tự thử lại. Không tăng Attempts.
                item.Status = SyncOutboxStatus.AwaitingCredential;
                item.LastError = ketQua.ThongBao;
                item.NextRetryAtUtc = now.Add(ChoCauHinh);
                break;

            case SyncTrangThai.LoiDuLieu:
                // 422: retry vô ích, cần người dùng sửa dữ liệu rồi gửi lại thủ công.
                item.Attempts++;
                item.Status = SyncOutboxStatus.NeedsManualReview;
                item.NextRetryAtUtc = null;
                item.LastError = GhepLoi(ketQua);
                break;

            case SyncTrangThai.LoiTamThoi:
                item.Attempts++;
                item.LastError = ketQua.ThongBao;
                if (item.Attempts >= SoLanThuToiDa)
                {
                    item.Status = SyncOutboxStatus.NeedsManualReview;
                    item.NextRetryAtUtc = null;
                }
                else
                {
                    item.Status = SyncOutboxStatus.Failed;
                    item.NextRetryAtUtc = now.Add(TinhGianCach(item.Attempts));
                }
                break;
        }
    }

    /// <summary>Giãn cách tăng dần (exponential backoff), chặn trần 60 phút.</summary>
    public static TimeSpan TinhGianCach(int soLanDaThu)
    {
        var phut = Math.Min(60, Math.Pow(3, soLanDaThu)); // 3, 9, 27, 60, 60...
        return TimeSpan.FromMinutes(phut);
    }

    private static string GhepLoi(SyncSendResult ketQua)
    {
        var body = ketQua.ResponseBody;
        var thongBao = ketQua.ThongBao ?? "Lỗi dữ liệu.";
        return string.IsNullOrWhiteSpace(body) ? thongBao : $"{thongBao} | {body}";
    }

    private Task GhiNhatKyAsync(SyncOutboxItem item, SyncSendResult ketQua, DateTime now, CancellationToken ct)
    {
        var thanhCong = ketQua.TrangThai == SyncTrangThai.ThanhCong;
        return _logWriter.GhiAsync(new SystemLogEntry
        {
            TenantId = item.TenantId,
            TimestampUtc = now,
            Level = thanhCong ? "Information" : "Warning",
            Message = $"Đồng bộ {item.EntityType} [{item.EntityKey}]: "
                      + (thanhCong ? "thành công" : ketQua.ThongBao),
            HttpMethod = "POST",
            RequestUrl = ketQua.RequestPath,
            RequestPayload = AnDuLieuNhayCam(item.PayloadJson),
            ResponsePayload = ketQua.ResponseBody,
            HttpStatusCode = ketQua.HttpStatusCode
        }, ct);
    }

    // Ẩn CCCD trong payload trước khi ghi nhật ký: log giữ tối thiểu 2 năm, không được lưu
    // số định danh cá nhân ở dạng đọc được. (Payload thật gửi HnC vẫn nguyên - HnC cần trường này.)
    private static readonly Regex RegexCccd =
        new("(\"cccd\"\\s*:\\s*\")[^\"]*(\")", RegexOptions.Compiled);

    private static string AnDuLieuNhayCam(string json) =>
        RegexCccd.Replace(json, "$1***$2");
}
