namespace HCP.Infrastructure.HanoiCheck;

/// <summary>Một dòng ảnh gửi kèm process (đơn giản hoá còn mỗi đường dẫn - loại/tên không cần với HnC).</summary>
public sealed record ProcessOrderAllocation(string MaLo, string MaKho, decimal SoLuong);

/// <summary>
/// Một dòng hàng cần khai nguồn hàng của đơn thực phẩm. Định danh bằng <see cref="TraceCode"/>; đơn cũ chưa lấy được
/// mã truy vết thì dùng <see cref="MaThucPham"/> (đặc tả cho phép, với điều kiện mã đó chỉ xuất hiện một lần trong đơn).
/// </summary>
public sealed class ProcessOrderLine
{
    public string? TraceCode { get; set; }

    public string? MaThucPham { get; set; }

    public List<ProcessOrderAllocation> PhanBo { get; set; } = new();
}

/// <summary>Nội dung gửi lên POST /api/supplier/orders/{code}/process.</summary>
public sealed class ProcessOrderRequest
{
    /// <summary>ma_nguoi_giao - null nghĩa là không đổi (không gửi khoá này lên).</summary>
    public string? MaNguoiGiao { get; set; }
    public string? GhiChu { get; set; }
    /// <summary>Đường dẫn ảnh tổng quan (≤3) - null nghĩa là không đổi ảnh hiện có.</summary>
    public List<string>? DanhSachAnh { get; set; }
    public List<ProcessOrderLine> ChiTiet { get; set; } = new();
}

/// <summary>Kết quả gọi 1 trong 2 dịch vụ ghi của mục "Đơn hàng" (process/status) - đồng bộ, có kết quả ngay.</summary>
public sealed record OrderCommandResult(bool ThanhCong, bool ChuaCauHinh, string? ThongBao)
{
    public static OrderCommandResult Ok() => new(true, false, null);
    public static OrderCommandResult ChuaCauHinhKq(string lyDo) => new(false, true, lyDo);
    public static OrderCommandResult Loi(string thongBao) => new(false, false, thongBao);
}

/// <summary>
/// Đẩy ngược xử lý đơn hàng HanoiCheck (chiều GHI): POST orders/{code}/process và orders/{code}/status,
/// có ký HMAC. Khác dịch vụ đồng bộ (merge) - đây là request/response ĐỒNG BỘ, trả 200 ngay, không qua outbox.
/// </summary>
public interface IHanoiCheckOrderCommandClient
{
    /// <summary>Gán người giao, ghi chú, ảnh tổng quan và khai nguồn hàng từng dòng của đơn.</summary>
    Task<OrderCommandResult> XuLyDonAsync(string tenantId, string maDon, ProcessOrderRequest noiDung,
                                          CancellationToken ct = default);

    /// <summary>Đổi trạng thái đơn (không nhận GIAO_HANG_THANH_CONG - chỉ trường xác nhận mới đặt được).</summary>
    Task<OrderCommandResult> DoiTrangThaiAsync(string tenantId, string maDon, string trangThai, string? ghiChu,
                                               CancellationToken ct = default);
}
