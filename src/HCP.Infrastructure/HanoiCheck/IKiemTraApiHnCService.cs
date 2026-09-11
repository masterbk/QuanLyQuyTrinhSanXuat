namespace HCP.Infrastructure.HanoiCheck;

/// <summary>Một tham số của API tra cứu: nằm trong đường dẫn ({...}) hoặc trên query string.</summary>
public sealed record ThamSoApiHnC(
    string Ten,
    string MoTa,
    bool TrongDuongDan = false,
    bool BatBuoc = false,
    string? MacDinh = null,
    IReadOnlyList<string>? LuaChon = null);

/// <summary>
/// Định nghĩa một API GET của HanoiCheck được phép gọi thử. Danh sách này CỐ ĐỊNH trong code -
/// màn kiểm tra không cho gõ đường dẫn tự do, để không thể dùng credential của cơ sở gọi
/// endpoint ghi dữ liệu (merge) hay đường dẫn ngoài /api/supplier.
/// </summary>
public sealed record ApiHnCDinhNghia(
    string Ma,
    string Ten,
    string MauDuongDan,
    bool CanKy,
    string MoTa,
    IReadOnlyList<ThamSoApiHnC> ThamSo);

/// <summary>Kết quả một lần gọi thử - giữ nguyên nội dung HnC trả về để hiển thị.</summary>
public sealed record KetQuaGoiApiHnC(
    bool DaGui,
    int? HttpStatus,
    string? Url,
    bool CoKy,
    long ThoiGianMs,
    string? NoiDung,
    bool LaJson,
    string? Loi)
{
    public static KetQuaGoiApiHnC ChuaGui(string loi) => new(false, null, null, false, 0, null, false, loi);
}

/// <summary>
/// Gọi thử các API GET của HanoiCheck bằng chính credential của cơ sở đang đăng nhập và trả
/// nguyên văn phản hồi - phục vụ kiểm tra kết nối, đối chiếu dữ liệu, dò tên trường khi đặc
/// tả thiếu JSON mẫu. Mỗi lần gọi đều ghi SystemLog như các giao dịch kết nối khác.
/// </summary>
public interface IKiemTraApiHnCService
{
    IReadOnlyList<ApiHnCDinhNghia> DanhSachApi { get; }

    Task<KetQuaGoiApiHnC> GoiAsync(
        string maApi, IReadOnlyDictionary<string, string?> thamSo,
        string? userId = null, CancellationToken ct = default);
}
