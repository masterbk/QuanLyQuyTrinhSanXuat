using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.Services.ThongBao;

/// <inheritdoc cref="IPushNotificationService"/>
public sealed class PushNotificationService : IPushNotificationService
{
    /// <summary>Giới hạn của FCM cho một lần gửi multicast.</summary>
    private const int SoTokenMoiLo = 500;

    private readonly AppDbContext _db;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(AppDbContext db, ILogger<PushNotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task GuiTheoQuyenAsync(string tenantId, IReadOnlyList<string> vaiTro, string tieuDe, string noiDung,
                                        IReadOnlyDictionary<string, string>? duLieu = null, CancellationToken ct = default)
    {
        // Chưa cấu hình Firebase (xem Program.cs) -> FirebaseApp.DefaultInstance null, bỏ qua âm thầm.
        if (FirebaseApp.DefaultInstance is null)
        {
            _logger.LogInformation("Bỏ qua gửi thông báo đẩy (chưa cấu hình Firebase): {TieuDe}", tieuDe);
            return;
        }

        try
        {
            var vaiTroIds = await _db.Roles.Where(r => vaiTro.Contains(r.Name!)).Select(r => r.Id).ToListAsync(ct);
            if (vaiTroIds.Count == 0)
            {
                _logger.LogWarning("Bỏ qua gửi thông báo đẩy: không tìm thấy vai trò {VaiTro} trong hệ thống.",
                    string.Join(",", vaiTro));
                return;
            }

            var userIds = await _db.UserRoles.Where(ur => vaiTroIds.Contains(ur.RoleId))
                .Join(_db.Users.Where(u => u.TenantId == tenantId), ur => ur.UserId, u => u.Id, (ur, u) => u.Id)
                .Distinct().ToListAsync(ct);
            if (userIds.Count == 0)
            {
                _logger.LogInformation(
                    "Bỏ qua gửi thông báo đẩy: cơ sở {TenantId} chưa có tài khoản nào giữ vai trò {VaiTro}.",
                    tenantId, string.Join(",", vaiTro));
                return;
            }

            var tokens = await _db.PushDeviceTokens.Where(t => userIds.Contains(t.UserId))
                .Select(t => t.Token).Distinct().ToListAsync(ct);
            if (tokens.Count == 0)
            {
                _logger.LogInformation(
                    "Bỏ qua gửi thông báo đẩy: {SoNguoi} tài khoản đúng vai trò nhưng chưa ai đăng ký thiết bị "
                    + "nhận thông báo (chưa đăng nhập app mobile bản có push, hoặc từ chối quyền thông báo).",
                    userIds.Count);
                return;
            }

            var hetHan = new List<string>();
            for (var i = 0; i < tokens.Count; i += SoTokenMoiLo)
            {
                var lo = tokens.Skip(i).Take(SoTokenMoiLo).ToList();
                var message = new MulticastMessage
                {
                    // Tokens (registration token cổ điển) bị đánh Obsolete nhưng vẫn là API đúng cho việc
                    // này - Fids (Firebase Installation ID) là khái niệm khác, không dùng để gửi tới token
                    // do FirebaseMessaging.instance.getToken() phía app trả về.
#pragma warning disable CS0618
                    Tokens = lo,
#pragma warning restore CS0618
                    Notification = new Notification { Title = tieuDe, Body = noiDung },
                    Data = duLieu?.ToDictionary(x => x.Key, x => x.Value),
                };

                var ket = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message, ct);
                if (ket.FailureCount > 0)
                {
                    // Log MỘT lần cho cả lô kèm lý do lỗi đầu tiên - đủ để phát hiện các kiểu lỗi thầm lặng
                    // hay gặp: google-services.json trên app khác project với service account trên máy chủ
                    // (mọi token đều lỗi InvalidArgument/SenderIdMismatch, bị xoá khỏi PushDeviceTokens mà
                    // không ai biết vì sao).
                    var lyDoDau = ket.Responses.FirstOrDefault(r => !r.IsSuccess)?.Exception?.Message;
                    _logger.LogWarning(
                        "Gửi thông báo đẩy: {SoLoi}/{SoTong} token trong lô thất bại. Lý do (mẫu đầu): {LyDo}",
                        ket.FailureCount, lo.Count, lyDoDau);
                }
                for (var j = 0; j < ket.Responses.Count; j++)
                {
                    var phanHoi = ket.Responses[j];
                    if (!phanHoi.IsSuccess && phanHoi.Exception is FirebaseMessagingException lex
                        && lex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                        hetHan.Add(lo[j]);
                }
            }

            if (hetHan.Count > 0)
            {
                await _db.PushDeviceTokens.Where(t => hetHan.Contains(t.Token)).ExecuteDeleteAsync(ct);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: thông báo hỏng không được chặn nghiệp vụ đơn hàng đang xử lý.
            _logger.LogWarning(ex, "Gửi thông báo đẩy thất bại: {TieuDe}", tieuDe);
        }
    }
}
