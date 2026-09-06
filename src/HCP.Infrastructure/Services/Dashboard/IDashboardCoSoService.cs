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

/// <summary>Số liệu tổng quan cho dashboard của một cơ sở.</summary>
public sealed record DashboardCoSo(
    IReadOnlyDictionary<SyncOutboxStatus, int> OutboxTheoTrangThai,
    int TongBanGhi,
    int SoDangCho,
    int SoLoi,
    int SoThanhCong,
    IReadOnlyList<CanhBaoGiayTo> CanhBaoGiayTo,
    IReadOnlyList<CanhBaoTonKho> CanhBaoTonKho);

/// <summary>Tổng hợp số liệu hiển thị trên dashboard của cơ sở đang đăng nhập.</summary>
public interface IDashboardCoSoService
{
    Task<DashboardCoSo> LayAsync(int soNgayCanhBao = 30, CancellationToken ct = default);
}
