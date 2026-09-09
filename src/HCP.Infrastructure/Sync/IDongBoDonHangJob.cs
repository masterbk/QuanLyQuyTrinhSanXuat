namespace HCP.Infrastructure.Sync;

/// <summary>Kết quả đồng bộ đơn hàng của MỘT cơ sở.</summary>
public sealed record KetQuaDongBoDon(bool ThanhCong, bool ChuaCauHinh, int SoDon, string? ThongBao)
{
    public static KetQuaDongBoDon Ok(int soDon) => new(true, false, soDon, null);
    public static KetQuaDongBoDon ChuaKetNoi() => new(false, true, 0, "Chưa cấu hình kết nối HanoiCheck.");
    public static KetQuaDongBoDon Loi(string thongBao) => new(false, false, 0, thongBao);
}

/// <summary>
/// Kéo đơn hàng từ HanoiCheck về (GET /api/supplier/orders) và lưu vào DonHangNhan.
/// Chạy định kỳ cho mọi cơ sở, hoặc theo yêu cầu cho một cơ sở.
/// </summary>
public interface IDongBoDonHangJob
{
    /// <summary>Đồng bộ cho TẤT CẢ cơ sở có cấu hình kết nối (dùng cho job nền định kỳ).</summary>
    Task DongBoTatCaAsync(CancellationToken ct = default);

    /// <summary>Đồng bộ cho MỘT cơ sở (dùng cho nút "Đồng bộ ngay").</summary>
    Task<KetQuaDongBoDon> DongBoMotCoSoAsync(string tenantId, CancellationToken ct = default);
}
