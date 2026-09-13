using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Domain.Enums;
using HCP.Infrastructure.Services.BanHang;
using HCP.Infrastructure.Services.DonHangNhan;
using HCP.Infrastructure.Services.Kho;

namespace HCP.Web.Api;

/// <summary>
/// API đơn hàng cho ứng dụng di động:
///   - /don-hang-nhan: đơn các trường đặt, kéo về từ HanoiCheck (CHỈ ĐỌC).
///   - /don-hang-ban: đơn hàng bán của nhà cung cấp cho mọi khách hàng (chỉ đọc trên app).
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

        // ---------- Đơn hàng bán của nhà cung cấp (mọi khách hàng) ----------
        var ban = app.MapGroup("/api/v1/don-hang-ban")
            .RequireAuthorization(ApiAuth.ChinhSach)
            .WithTags("Đơn hàng bán");

        ban.MapGet("", async (IDonHangBanService svc, IKhachHangService kh, IDanhMucService<Product> sp,
                              string? trangThai, int trang = 1, int soDong = 20) =>
        {
            IEnumerable<DonHangBan> ds = await svc.LayTatCaAsync();
            if (!string.IsNullOrWhiteSpace(trangThai) && Enum.TryParse<TrangThaiDonHangBan>(trangThai, true, out var tt))
                ds = ds.Where(d => d.TrangThai == tt);
            var tatCa = ds.ToList();
            var (tenKhach, tenSp) = await LayTenAsync(kh, sp);
            var (t, n) = LenhSanXuatApi.ChuanHoaTrang(trang, soDong);
            return Results.Ok(new TrangDuLieu<DonHangBanDto>(
                tatCa.Skip((t - 1) * n).Take(n).Select(d => MapDonBan(d, tenKhach, tenSp)).ToList(), t, n, tatCa.Count));
        });

        ban.MapGet("/{id:int}", async (int id, IDonHangBanService svc, IKhachHangService kh, IDanhMucService<Product> sp) =>
        {
            var don = await svc.LayTheoIdAsync(id);
            if (don is null) return Results.NotFound(new LoiDto("Không tìm thấy đơn hàng."));
            var (tenKhach, tenSp) = await LayTenAsync(kh, sp);
            return Results.Ok(MapDonBan(don, tenKhach, tenSp));
        });
    }

    private static async Task<(Dictionary<string, string> Khach, Dictionary<string, string> SanPham)> LayTenAsync(
        IKhachHangService kh, IDanhMucService<Product> sp) =>
        ((await kh.LayTatCaAsync()).GroupBy(k => k.MaKhachHang).ToDictionary(g => g.Key, g => g.First().TenKhachHang),
         (await sp.LayTatCaAsync()).GroupBy(p => p.MaSanPham).ToDictionary(g => g.Key, g => g.First().TenSanPham));

    private static DonHangBanDto MapDonBan(DonHangBan d, IReadOnlyDictionary<string, string> tenKhach,
                                           IReadOnlyDictionary<string, string> tenSp) => new(
        d.Id, d.MaDonHang, d.MaKhachHang, tenKhach.GetValueOrDefault(d.MaKhachHang), d.MaKho, d.NgayDat, d.NgayGiao,
        d.DiaChiGiao, d.MaNguoiGiao, d.TrangThai.ToString(), TenTrangThaiBan(d.TrangThai), d.Nguon.ToString(),
        d.MaDonHnC, d.GhiChu, d.LyDoHuy, d.TongTien, d.ThoiGianXuatKhoUtc, d.ThoiGianGiaoUtc,
        d.Dong.OrderBy(l => l.Id).Select(l => new DonHangBanDongDto(
            l.Id, l.MaThanhPham, tenSp.GetValueOrDefault(l.MaThanhPham), l.SoLuong, l.DonGia, l.ThanhTien,
            l.XuatLo.Select(x => new XuatLoDto(x.MaLo, x.HanSuDung, x.SoLuong)).ToList())).ToList());

    private static string TenTrangThaiBan(TrangThaiDonHangBan t) => t switch
    {
        TrangThaiDonHangBan.ChoXacNhan => "Chờ xác nhận",
        TrangThaiDonHangBan.DaXacNhan => "Đã xác nhận",
        TrangThaiDonHangBan.DangGiao => "Đang giao",
        TrangThaiDonHangBan.DaGiao => "Đã giao",
        TrangThaiDonHangBan.DaHuy => "Đã huỷ",
        _ => t.ToString()
    };

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

}
