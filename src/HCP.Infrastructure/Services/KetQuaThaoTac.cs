namespace HCP.Infrastructure.Services;

/// <summary>Kết quả một thao tác nghiệp vụ, kèm thông báo hiển thị cho người dùng.</summary>
public record KetQuaThaoTac(bool ThanhCong, string ThongBao)
{
    public static KetQuaThaoTac Ok(string thongBao) => new(true, thongBao);
    public static KetQuaThaoTac Loi(string thongBao) => new(false, thongBao);

    /// <summary>true = lỗi do dữ liệu vừa bị người khác cập nhật (API trả 409 để client tải lại).</summary>
    public bool XungDot { get; init; }

    public static KetQuaThaoTac LoiXungDot(string thongBao) => new(false, thongBao) { XungDot = true };

    /// <summary>Nối thêm một ghi chú (vd "đã tự bật đồng bộ kèm...") vào thông báo, nếu có.</summary>
    public KetQuaThaoTac KemGhiChu(string? ghiChu) =>
        string.IsNullOrWhiteSpace(ghiChu) ? this : this with { ThongBao = ThongBao + " " + ghiChu };
}
