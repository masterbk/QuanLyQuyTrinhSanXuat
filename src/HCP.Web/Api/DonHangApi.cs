using HCP.Web.Services;
using HCP.Infrastructure.Persistence;
using System.Security.Claims;
using HCP.Domain.Constants;
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
            .RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { Roles = AppRoles.QuyenGiaoHang })
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
            .RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { Roles = AppRoles.QuyenGiaoHang })
            .WithTags("Đơn hàng bán");

        // canGiao: việc của nhân viên giao hàng (đơn đang giao); cuaToi: đơn mình đã nhận.
        ban.MapGet("", async (IDonHangBanService svc, IKhachHangService kh, IDanhMucService<Product> sp,
                              IDanhMucService<Staff> ns, ClaimsPrincipal user, AppDbContext db,
                              string? trangThai, bool canGiao = false, bool cuaToi = false,
                              int trang = 1, int soDong = 20) =>
        {
            IEnumerable<DonHangBan> ds = await svc.LayTatCaAsync();
            if (!string.IsNullOrWhiteSpace(trangThai) && Enum.TryParse<TrangThaiDonHangBan>(trangThai, true, out var tt))
                ds = ds.Where(d => d.TrangThai == tt);
            if (canGiao) ds = ds.Where(d => d.TrangThai == TrangThaiDonHangBan.DangGiao);
            if (cuaToi)
            {
                var maToi = await LenhSanXuatApi.MaNhanSuHienTaiAsync(user, db);
                ds = maToi is null
                    ? Enumerable.Empty<DonHangBan>()
                    : ds.Where(d => string.Equals(d.MaNguoiGiao, maToi, StringComparison.OrdinalIgnoreCase));
            }

            var tatCa = ds.ToList();
            var ten = await LayTenAsync(kh, sp, ns);
            var (t, n) = LenhSanXuatApi.ChuanHoaTrang(trang, soDong);
            return Results.Ok(new TrangDuLieu<DonHangBanDto>(
                tatCa.Skip((t - 1) * n).Take(n).Select(d => MapDonBan(d, ten)).ToList(), t, n, tatCa.Count));
        });

        ban.MapGet("/{id:int}", async (int id, IDonHangBanService svc, IKhachHangService kh,
                                       IDanhMucService<Product> sp, IDanhMucService<Staff> ns) =>
        {
            var don = await svc.LayTheoIdAsync(id);
            if (don is null) return Results.NotFound(new LoiDto("Không tìm thấy đơn hàng."));
            return Results.Ok(MapDonBan(don, await LayTenAsync(kh, sp, ns)));
        });

        // Quét mã QR trên phiếu giao (QR tra cứu của hệ thống hoặc link truy xuất HanoiCheck) -> mở đúng đơn.
        ban.MapGet("/quet", async (string? noiDung, IDonHangBanService svc, IKhachHangService kh,
                                   IDanhMucService<Product> sp, IDanhMucService<Staff> ns) =>
        {
            var don = await svc.TimTheoQrAsync(noiDung ?? "");
            return don is null
                ? Results.NotFound(new LoiDto("Mã QR này không phải của đơn hàng nào trong cơ sở."))
                : Results.Ok(MapDonBan(don, await LayTenAsync(kh, sp, ns)));
        });

        // Xác nhận đơn mới (Chờ xác nhận -> Đã xác nhận) - việc của quản lý/nhập liệu, KHÔNG phải shipper.
        ban.MapPost("/{id:int}/xac-nhan", async (int id, IDonHangBanService svc, CancellationToken ct) =>
        {
            var kq = await svc.XacNhanAsync(id, ct);
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao))
                                : Results.BadRequest(new LoiDto(kq.ThongBao));
        }).RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { Roles = AppRoles.QuyenNhapLieu });

        ban.MapPost("/{id:int}/nhan-don", async (int id, ClaimsPrincipal user, AppDbContext db,
                                                 IDonHangBanService svc) =>
        {
            var maNhanSu = await LenhSanXuatApi.MaNhanSuHienTaiAsync(user, db);
            if (maNhanSu is null)
                return Results.BadRequest(new LoiDto(
                    "Tài khoản chưa gắn với hồ sơ nhân sự đang làm việc - liên hệ quản trị cơ sở."));
            var kq = await svc.NhanDonAsync(id, maNhanSu);
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao))
                                : Results.BadRequest(new LoiDto(kq.ThongBao));
        });

        // Xác nhận đã giao: multipart/form-data kèm ảnh chụp tại chỗ (bắt buộc).
        ban.MapPost("/{id:int}/da-giao", async (int id, HttpRequest http, IDonHangBanService svc,
                                                ILuuTruAnhService luuAnh, CancellationToken ct) =>
        {
            if (!http.HasFormContentType)
                return Results.BadRequest(new LoiDto("Cần gửi dạng multipart/form-data kèm ảnh giao hàng."));

            IFormCollection form;
            try
            {
                form = await http.ReadFormAsync(ct);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                return Results.BadRequest(new LoiDto("Dữ liệu ảnh gửi lên không đọc được. Vui lòng thử lại."));
            }

            var anh = new List<AnhDauVao>();
            foreach (var f in form.Files.Where(f => f.Length > 0).Take(DonHangBanService.SoAnhToiDa))
            {
                try
                {
                    await using var luong = f.OpenReadStream();
                    var daLuu = await luuAnh.LuuAsync(luong, f.FileName, f.ContentType, f.Length, ct);
                    anh.Add(new AnhDauVao(daLuu.TenFile, daLuu.DuongDan));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new LoiDto(ex.Message));
                }
            }

            var kq = await svc.HoanTatGiaoAsync(id, anh, ct);
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao))
                                : Results.BadRequest(new LoiDto(kq.ThongBao));
        }).DisableAntiforgery();
    }

    private sealed record BangTenDon(IReadOnlyDictionary<string, string> Khach,
                                     IReadOnlyDictionary<string, string> SanPham,
                                     IReadOnlyDictionary<string, string> NhanSu);

    private static async Task<BangTenDon> LayTenAsync(IKhachHangService kh, IDanhMucService<Product> sp,
                                                      IDanhMucService<Staff> ns) => new(
        (await kh.LayTatCaAsync()).GroupBy(k => k.MaKhachHang).ToDictionary(g => g.Key, g => g.First().TenKhachHang),
        (await sp.LayTatCaAsync()).GroupBy(p => p.MaSanPham).ToDictionary(g => g.Key, g => g.First().TenSanPham),
        (await ns.LayTatCaAsync()).GroupBy(n => n.MaNhanSu).ToDictionary(g => g.Key, g => g.First().HoTen));

    private static DonHangBanDto MapDonBan(DonHangBan d, BangTenDon ten) => new(
        d.Id, d.MaDonHang, d.MaKhachHang, ten.Khach.GetValueOrDefault(d.MaKhachHang), d.MaKho, d.NgayDat, d.NgayGiao,
        d.DiaChiGiao, d.MaNguoiGiao, d.MaNguoiGiao is null ? null : ten.NhanSu.GetValueOrDefault(d.MaNguoiGiao),
        d.TrangThai.ToString(), TenTrangThaiBan(d.TrangThai), d.Nguon.ToString(),
        d.MaDonHnC, d.GhiChu, d.LyDoHuy, d.TongTien, d.ThoiGianXuatKhoUtc, d.ThoiGianGiaoUtc,
        d.Dong.OrderBy(l => l.Id).Select(l => new DonHangBanDongDto(
            l.Id, l.MaThanhPham, ten.SanPham.GetValueOrDefault(l.MaThanhPham), l.SoLuong, l.DonGia, l.ThanhTien,
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
