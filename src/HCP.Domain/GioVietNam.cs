namespace HCP.Domain;

/// <summary>
/// Giờ Việt Nam (UTC+7, không có giờ mùa hè) dùng cho MỌI chỗ hiển thị, "ngày hôm nay" và dữ liệu gửi đi.
/// Cơ sở dữ liệu vẫn lưu mốc thời gian theo UTC (các cột tên *Utc); KHÔNG dùng DateTime.Now/Today/ToLocalTime vì
/// chúng phụ thuộc múi giờ đặt trên máy chủ (IIS đặt UTC thì giờ hiển thị lệch 7 tiếng).
/// </summary>
public static class GioVietNam
{
    public static readonly TimeSpan Lech = TimeSpan.FromHours(7);

    /// <summary>Thời điểm hiện tại theo giờ Việt Nam.</summary>
    public static DateTime Nay => TuUtc(DateTime.UtcNow);

    /// <summary>Ngày hôm nay theo giờ Việt Nam.</summary>
    public static DateOnly HomNay => DateOnly.FromDateTime(Nay);

    /// <summary>Đổi mốc UTC (kể cả giá trị EF đọc ra có Kind=Unspecified) sang giờ Việt Nam.</summary>
    public static DateTime TuUtc(DateTime utc) =>
        DateTime.SpecifyKind((utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc) + Lech,
                             DateTimeKind.Unspecified);

    public static DateTime? TuUtc(DateTime? utc) => utc is { } u ? TuUtc(u) : null;

    /// <summary>Múi giờ Việt Nam cho thư viện cần TimeZoneInfo (lịch chạy Hangfire).</summary>
    public static TimeZoneInfo MuiGio { get; } = TimMuiGio();

    private static TimeZoneInfo TimMuiGio()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh", "Asia/Bangkok" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }
}
