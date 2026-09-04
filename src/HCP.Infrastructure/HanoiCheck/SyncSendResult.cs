namespace HCP.Infrastructure.HanoiCheck;

/// <summary>Phân loại kết quả gửi một bản ghi merge sang HanoiCheck, quyết định cách retry.</summary>
public enum SyncTrangThai
{
    /// <summary>HnC nhận thành công (HTTP 2xx).</summary>
    ThanhCong,

    /// <summary>Cơ sở chưa cấu hình đủ credential - BỎ QUA, không tính là lỗi.</summary>
    ChuaCauHinh,

    /// <summary>Lỗi tạm thời (mạng, 5xx, 401, hết token...) - nên gửi lại sau.</summary>
    LoiTamThoi,

    /// <summary>Lỗi dữ liệu (HTTP 422): retry cũng vô ích, cần người dùng sửa dữ liệu rồi gửi lại.</summary>
    LoiDuLieu
}

/// <summary>Kết quả gửi một bản ghi merge sang HanoiCheck.</summary>
public sealed record SyncSendResult(
    SyncTrangThai TrangThai,
    int? HttpStatusCode,
    string? ThongBao,
    string? RequestPath,
    string? ResponseBody)
{
    public static SyncSendResult ThanhCong(int httpStatusCode, string? path, string? body) =>
        new(SyncTrangThai.ThanhCong, httpStatusCode, null, path, body);

    public static SyncSendResult ChuaCauHinh(string lyDo) =>
        new(SyncTrangThai.ChuaCauHinh, null, lyDo, null, null);

    public static SyncSendResult LoiTamThoi(string thongBao, int? httpStatusCode, string? path = null, string? body = null) =>
        new(SyncTrangThai.LoiTamThoi, httpStatusCode, thongBao, path, body);

    public static SyncSendResult LoiDuLieu(string thongBao, int? httpStatusCode, string? path, string? body) =>
        new(SyncTrangThai.LoiDuLieu, httpStatusCode, thongBao, path, body);
}
