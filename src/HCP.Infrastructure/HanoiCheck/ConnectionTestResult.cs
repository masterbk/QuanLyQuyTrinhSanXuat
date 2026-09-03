namespace HCP.Infrastructure.HanoiCheck;

/// <summary>
/// Kết quả bấm "Kiểm tra kết nối" ở màn cài đặt của cơ sở.
/// Thông báo lỗi phải nói rõ nguyên nhân để người dùng tự sửa được,
/// thay vì để họ phát hiện qua hàng loạt bản ghi lỗi về sau.
/// </summary>
public record ConnectionTestResult(bool ThanhCong, string ThongBao)
{
    public static ConnectionTestResult Ok(int expiresIn) =>
        new(true, $"Kết nối thành công. Token có hiệu lực {expiresIn} giây.");

    public static ConnectionTestResult Loi(string thongBao) => new(false, thongBao);
}
