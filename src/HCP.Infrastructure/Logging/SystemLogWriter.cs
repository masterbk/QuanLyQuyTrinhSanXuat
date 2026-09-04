using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.Logging;

/// <inheritdoc cref="ISystemLogWriter"/>
public sealed class SystemLogWriter : ISystemLogWriter
{
    private readonly AppDbContext _db;
    private readonly ILogger<SystemLogWriter> _logger;

    public SystemLogWriter(AppDbContext db, ILogger<SystemLogWriter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task GhiAsync(SystemLogEntry entry, CancellationToken ct = default)
    {
        // Cắt bớt để vừa giới hạn cột, tránh SaveChanges ném lỗi và làm hỏng cả vòng xử lý.
        entry.Message = CatBot(entry.Message, 4000) ?? string.Empty;
        entry.RequestPayload = CatBot(entry.RequestPayload, 8000);
        entry.ResponsePayload = CatBot(entry.ResponsePayload, 8000);
        entry.RequestUrl = CatBot(entry.RequestUrl, 1000);

        try
        {
            _db.SystemLogs.Add(entry);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Ghi nhật ký là phụ trợ - nếu lỗi thì không được kéo đổ luồng đồng bộ chính.
            _logger.LogError(ex, "Không ghi được SystemLog cho cơ sở {TenantId}.", entry.TenantId);
        }
    }

    private static string? CatBot(string? s, int max) =>
        s is not null && s.Length > max ? s[..max] : s;
}
