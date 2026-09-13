namespace HCP.Domain.Entities.Common;

/// <summary>
/// Bản ghi có trang tra cứu công khai (quét QR không cần đăng nhập).
/// <see cref="MaTraCuu"/> là chuỗi ngẫu nhiên 32 ký tự hex - không dùng Id/mã nghiệp vụ để người ngoài không dò được
/// dữ liệu của bản ghi khác. Sinh lần đầu khi có người mở mã QR.
/// </summary>
public interface ICoMaTraCuu
{
    string? MaTraCuu { get; set; }
}
