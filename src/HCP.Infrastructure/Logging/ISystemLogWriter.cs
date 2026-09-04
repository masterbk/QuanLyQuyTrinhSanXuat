using HCP.Domain.Entities.Infrastructure;

namespace HCP.Infrastructure.Logging;

/// <summary>
/// Ghi nhật ký hệ thống vào bảng SystemLogs. Tài liệu HnC yêu cầu lưu tối thiểu 2 năm
/// toàn bộ giao dịch kết nối, nên mỗi lần gửi request đều phải ghi lại request/response.
/// </summary>
public interface ISystemLogWriter
{
    Task GhiAsync(SystemLogEntry entry, CancellationToken ct = default);
}
