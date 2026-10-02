using HCP.Domain.Enums;

namespace HCP.Infrastructure.Services.Dashboard;

/// <summary>Một cảnh báo giấy tờ sắp/đã hết hạn của cơ sở.</summary>
public sealed record CanhBaoGiayTo(
    string NhomDoiTuong,   // "Nhà cung ứng" hoặc "Nhân sự"
    string TenDoiTuong,
    string Ma,
    string LoaiGiayTo,     // vd "Giấy chứng nhận ATTP", "Giấy khám sức khoẻ"
    DateOnly NgayHetHan,
    bool DaHetHan);

/// <summary>Một cảnh báo về tồn kho: sắp/đã hết hạn theo lô, hoặc tồn thấp dưới mức tối thiểu.</summary>
public sealed record CanhBaoTonKho(
    string Loai,            // "Đã hết hạn" | "Sắp hết hạn" | "Tồn thấp"
    string MaSanPham,
    string TenSanPham,
    string MaKho,
    string? MaLo,           // null với cảnh báo tồn thấp (gộp mọi lô)
    DateOnly? HanSuDung,
    decimal SoLuongTon,
    string? DonViTinh);

/// <summary>
/// Đếm đơn hàng bán theo trạng thái, tính từ <paramref name="TuNgay"/> (theo NGÀY ĐẶT của đơn).
/// </summary>
/// <param name="TuNgay">Mốc bắt đầu tính - hiển thị lên màn hình để người xem biết phạm vi số liệu.</param>
/// <param name="ChoXacNhan">Đơn mới nhận, chờ cơ sở xác nhận.</param>
/// <param name="ChoXuatKho">Đã xác nhận nhưng chưa xuất kho.</param>
/// <param name="DangGiao">Đã xuất kho mà chưa giao xong - gồm cả đơn chờ nhân viên giao hàng nhận.</param>
/// <param name="DaGiao">Cơ sở đã xác nhận giao xong.</param>
/// <param name="TruongXacNhanGiaoThanhCong">Đơn từ HanoiCheck được nhà trường xác nhận "Giao thành công".</param>
public sealed record ThongKeDonHang(
    DateOnly TuNgay,
    int ChoXacNhan,
    int ChoXuatKho,
    int DangGiao,
    int DaGiao,
    int TruongXacNhanGiaoThanhCong);

/// <summary>Số liệu tổng quan cho dashboard của một cơ sở.</summary>
public sealed record DashboardCoSo(
    IReadOnlyDictionary<SyncOutboxStatus, int> OutboxTheoTrangThai,
    int TongBanGhi,
    int SoDangCho,
    int SoLoi,
    int SoThanhCong,
    IReadOnlyList<CanhBaoGiayTo> CanhBaoGiayTo,
    IReadOnlyList<CanhBaoTonKho> CanhBaoTonKho,
    ThongKeDonHang DonHang);

/// <summary>Tổng hợp số liệu hiển thị trên dashboard của cơ sở đang đăng nhập.</summary>
public interface IDashboardCoSoService
{
    /// <param name="tuNgay">Mốc bắt đầu đếm đơn hàng; bỏ trống thì dùng
    /// <see cref="DashboardCoSoService.MocDemDonHang"/>.</param>
    Task<DashboardCoSo> LayAsync(int soNgayCanhBao = 30, DateOnly? tuNgay = null,
                                 CancellationToken ct = default);
}
