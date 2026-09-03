namespace HCP.Domain.Entities.Infrastructure;

/// <summary>
/// Nhật ký hệ thống, gồm toàn bộ request/response gửi sang HanoiCheck.
///
/// Tài liệu hướng dẫn HnC yêu cầu nhà cung cấp "lưu trữ nhật ký hệ thống (log file)
/// trực tuyến tối thiểu 02 năm kể từ thời điểm phát sinh giao dịch kết nối".
/// </summary>
public class SystemLogEntry
{
    public long Id { get; set; }

    /// <summary>Null = log cấp nền tảng (không thuộc cơ sở nào).</summary>
    public string? TenantId { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "Information";
    public string Message { get; set; } = string.Empty;

    public string? HttpMethod { get; set; }
    public string? RequestUrl { get; set; }
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public int? HttpStatusCode { get; set; }

    /// <summary>Người dùng thực hiện thao tác (nếu phát sinh từ UI).</summary>
    public string? UserId { get; set; }
}
