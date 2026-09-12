using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Services.DonHangNhan;

namespace HCP.Web.Api;

/// <summary>
/// API đơn hàng cho ứng dụng di động:
///   - /don-hang-nhan: đơn các trường đặt, kéo về từ HanoiCheck (CHỈ ĐỌC).
///   - /don-hang: đơn do cơ sở tự lập để đẩy lên HanoiCheck.
/// </summary>
public static class DonHangApi
{
    public static void MapDonHangApi(this IEndpointRouteBuilder app)
    {
        // ---------- Đơn từ trường (chiều kéo) ----------
        var nhan = app.MapGroup("/api/v1/don-hang-nhan")
            .RequireAuthorization(ApiAuth.ChinhSach)
            .WithTags("Đơn hàng từ trường");

        nhan.MapGet("", async (IDonHangNhanService svc, string? trangThai, string? tuKhoa,
                               int trang = 1, int soDong = 20) =>
        {
            IEnumerable<DonHangNhan> ds = await svc.LayTatCaAsync();
            if (!string.IsNullOrWhiteSpace(trangThai))
                ds = ds.Where(d => string.Equals(d.TrangThai, trangThai, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                var k = tuKhoa.Trim();
                ds = ds.Where(d => (d.MaDonHang ?? "").Contains(k, StringComparison.OrdinalIgnoreCase)
                                   || (d.TenTruong ?? "").Contains(k, StringComparison.OrdinalIgnoreCase));
            }

            var tatCa = ds.ToList();
            var (t, n) = LenhSanXuatApi.ChuanHoaTrang(trang, soDong);
            return Results.Ok(new TrangDuLieu<DonHangNhanDto>(
                tatCa.Skip((t - 1) * n).Take(n).Select(Map).ToList(), t, n, tatCa.Count));
        });

        nhan.MapGet("/{maDon}", async (string maDon, IDonHangNhanService svc) =>
        {
            var don = (await svc.LayTatCaAsync())
                .FirstOrDefault(d => string.Equals(d.MaDonHang, maDon, StringComparison.OrdinalIgnoreCase));
            return don is null
                ? Results.NotFound(new LoiDto($"Không tìm thấy đơn \"{maDon}\"."))
                : Results.Ok(Map(don));
        });

        nhan.MapPost("/dong-bo-ngay", async (IDonHangNhanService svc, CancellationToken ct) =>
        {
            var kq = await svc.DongBoNgayAsync(ct);
            if (kq.ChuaCauHinh)
                return Results.BadRequest(new LoiDto(kq.ThongBao ?? "Cơ sở chưa cấu hình kết nối HanoiCheck."));
            return kq.ThanhCong
                ? Results.Ok(new KetQuaDto(true, $"Đã đồng bộ {kq.SoDon} đơn từ HanoiCheck."))
                : Results.BadRequest(new LoiDto(kq.ThongBao ?? "Không đồng bộ được đơn hàng."));
        });

        // ---------- Đơn cơ sở tự lập (chiều đẩy) ----------
        var day = app.MapGroup("/api/v1/don-hang")
            .RequireAuthorization(ApiAuth.ChinhSach)
            .WithTags("Đơn hàng & xuất kho");

        day.MapGet("", async (IDanhMucService<Order> svc, int trang = 1, int soDong = 20) =>
        {
            var tatCa = (await svc.LayTatCaAsync()).ToList();
            var (t, n) = LenhSanXuatApi.ChuanHoaTrang(trang, soDong);
            return Results.Ok(new TrangDuLieu<DonHangDto>(
                tatCa.Skip((t - 1) * n).Take(n).Select(MapDon).ToList(), t, n, tatCa.Count));
        });

        day.MapGet("/{id:int}", async (int id, IDanhMucService<Order> svc) =>
        {
            var don = await svc.LayTheoIdAsync(id);
            return don is null
                ? Results.NotFound(new LoiDto("Không tìm thấy đơn hàng."))
                : Results.Ok(MapDon(don));
        });
    }

    private static DonHangNhanDto Map(DonHangNhan d) => new(
        d.Id, d.MaDonHang, d.TenTruong, d.TrangThai, TenTrangThai(d.TrangThai), d.NgayGiao,
        d.DiemTruong, d.LoaiDon, d.KhoXuat, d.MaNguoiGiao, d.TenNguoiGiao, d.SdtNguoiGiao,
        d.PhuongTienGiao, d.BienSoXe, d.DiaChiGiao, d.GhiChu, d.LinkTruyXuat, d.NgayTaoTrenHnC,
        d.LanDongBoUtc, d.DaLayChiTiet,
        d.Dong.Select(l => new DonHangNhanDongDto(
            l.MaSanPham, l.TenSanPham, l.SoLuong, l.DonViTinh, l.MaTruyVet, l.MaThucDon,
            l.PhanBo.Select(p => new PhanBoDto(p.MaPhieuXuat, p.MaThucPhamNcc, p.MaLo, p.TenLo,
                                               p.MaKho, p.TenKho, p.SoLuong)).ToList())).ToList());

    /// <summary>Nhãn tiếng Việt của trạng thái đơn HanoiCheck - để app khỏi phải tự map.</summary>
    private static string TenTrangThai(string? s) => s switch
    {
        "CHO_XAC_NHAN" => "Chờ xác nhận",
        "TU_CHOI" => "Từ chối",
        "DANG_CHUAN_BI" => "Đang chuẩn bị",
        "DANG_GIAO" => "Đang giao",
        "DA_GIAO" => "Đã giao",
        "GIAO_HANG_THANH_CONG" => "Giao thành công",
        "TRA_HANG" => "Trả hàng",
        "HUY" => "Huỷ",
        _ => s ?? "-"
    };

    private static DonHangDto MapDon(Order o) => new(
        o.Id, o.MaDonHang, o.LoaiDonHang, o.MaTruong, o.NgayDonHang, o.DiemGiao, o.DiaChiNhan,
        o.MaNguoiGiao, o.TrangThai, TenTrangThai(o.TrangThai), o.GhiChu,
        o.ChiTiet.Select(l => new DonHangDongDto(
            l.MaSanPham, l.MaLoaiSp, l.MaMonAn, l.SoLuong)).ToList());
}
