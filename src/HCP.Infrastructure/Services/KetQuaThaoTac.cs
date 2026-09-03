namespace HCP.Infrastructure.Services;

/// <summary>Kết quả một thao tác nghiệp vụ, kèm thông báo hiển thị cho người dùng.</summary>
public record KetQuaThaoTac(bool ThanhCong, string ThongBao)
{
    public static KetQuaThaoTac Ok(string thongBao) => new(true, thongBao);
    public static KetQuaThaoTac Loi(string thongBao) => new(false, thongBao);
}
