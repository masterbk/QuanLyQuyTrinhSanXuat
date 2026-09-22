using Finbuckle.MultiTenant.Abstractions;
using HCP.Domain.Entities.Business;
using HCP.Domain.Entities.Common;
using HCP.Domain.Entities.Infrastructure;
using HCP.Domain.Enums;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.TraCuu;

public enum LoaiTraCuu
{
    DonHang,
    Lo,

    /// <summary>Mã QR của lệnh sản xuất - không phải trang tra cứu, app quét để tham gia các khâu.</summary>
    LenhSanXuat
}

/// <summary>
/// Nội dung mã QR: <see cref="LinkNgoai"/> (trang truy xuất HanoiCheck) nếu có, ngược lại trang tra cứu công khai của
/// chính hệ thống theo <see cref="MaTraCuu"/>. <see cref="DongChuThem"/>: các dòng chữ in thêm dưới dòng mã - đơn
/// hàng: tên sản phẩm/số lượng từng dòng, ngày hẹn giao (nếu có), địa chỉ giao; lệnh sản xuất: ngày sản xuất, tên
/// sản phẩm/số lượng/mã lô từng dòng.
/// </summary>
public record QrTraCuu(string Ma, string? LinkNgoai, string? MaTraCuu, string MoTa,
                       IReadOnlyList<string>? DongChuThem = null);

public record TraCuuCoSo(string? Ten, string? DiaChi, string? DienThoai);

public record TraCuuLoTomTat(string MaLo, DateOnly? NgaySanXuat, DateOnly? HanSuDung, decimal SoLuong, string? MaTraCuu);

public record TraCuuDongDon(string MaSanPham, string TenSanPham, string? DonViTinh, decimal SoLuong,
                            IReadOnlyList<TraCuuLoTomTat> Lo);

public record TraCuuDonHang(TraCuuCoSo CoSo, string MaDonHang, string TenKhachHang, DateOnly NgayDat,
                            DateOnly? NgayGiao, string TrangThai, DateTime? ThoiGianGiaoUtc,
                            IReadOnlyList<TraCuuDongDon> Dong);

public record TraCuuKhau(int ThuTu, string TenKhau, DateTime? ThoiGian, string? TenCoSo, string? MaLoNguyenLieu);

public record TraCuuLo(TraCuuCoSo CoSo, string MaLo, string TenLo, string MaSanPham, string TenSanPham,
                       DateOnly NgayNhap, DateOnly? NgaySanXuat, DateOnly? HanSuDung,
                       IReadOnlyList<string> Anh, IReadOnlyList<TraCuuKhau> Khau);

/// <summary>
/// Mã QR và trang tra cứu công khai của đơn hàng bán và lô sản xuất.
///  - <see cref="LayQrDonHangAsync"/>/<see cref="LayQrLoAsync"/>: gọi trong phiên đăng nhập của cơ sở.
///  - <see cref="TraCuuDonHangAsync"/>/<see cref="TraCuuLoAsync"/>: gọi từ trang công khai KHÔNG có tenant - tìm theo mã tra
///    cứu ngẫu nhiên, bỏ bộ lọc tenant rồi lọc tường minh theo TenantId của bản ghi tìm được. Chỉ đọc, không trả giá
///    tiền, số điện thoại/địa chỉ khách hay nhân sự.
/// </summary>
public interface ITraCuuCongKhaiService
{
    Task<QrTraCuu?> LayQrDonHangAsync(int id, CancellationToken ct = default);

    Task<QrTraCuu?> LayQrLoAsync(int id, CancellationToken ct = default);

    /// <summary>QR của lệnh sản xuất: nội dung "LSX:&lt;mã lệnh&gt;" cho app của nhân viên sản xuất quét.</summary>
    Task<QrTraCuu?> LayQrLenhSanXuatAsync(int id, CancellationToken ct = default);

    Task<TraCuuDonHang?> TraCuuDonHangAsync(string maTraCuu, CancellationToken ct = default);

    Task<TraCuuLo?> TraCuuLoAsync(string maTraCuu, CancellationToken ct = default);
}

/// <inheritdoc cref="ITraCuuCongKhaiService"/>
public sealed class TraCuuCongKhaiService : ITraCuuCongKhaiService
{
    private readonly AppDbContext _db;
    private readonly IMultiTenantStore<Tenant>? _tenantStore;

    public TraCuuCongKhaiService(AppDbContext db, IMultiTenantStore<Tenant>? tenantStore = null)
    {
        _db = db;
        _tenantStore = tenantStore;
    }

    public async Task<QrTraCuu?> LayQrDonHangAsync(int id, CancellationToken ct = default)
    {
        var don = await _db.DonHangBans.Include(d => d.Dong).ThenInclude(l => l.XuatLo)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (don is null) return null;

        // Đơn từ trường thì hiển thị/QR theo mã trên HanoiCheck (trường chỉ biết mã đó), đơn nội bộ thì
        // theo mã nội bộ.
        var laHanoiCheck = don.Nguon == NguonDonHang.HanoiCheck && !string.IsNullOrWhiteSpace(don.MaDonHnC);
        var maHienThi = laHanoiCheck ? don.MaDonHnC! : don.MaDonHang;
        var dongChuThem = await DongChuThemDonHangAsync(don, ct);

        // Đơn từ HanoiCheck: QR là trang truy xuất (traceability_url) HanoiCheck cấp cho đơn.
        if (laHanoiCheck)
        {
            var tenantId = don.TenantId;
            var maDonHnC = don.MaDonHnC;
            var link = await _db.DonHangNhans.AsNoTracking()
                .Where(n => n.TenantId == tenantId && n.MaDonHang == maDonHnC)
                .Select(n => n.LinkTruyXuat).FirstOrDefaultAsync(ct);
            if (LaLinkWeb(link))
                return new QrTraCuu(maHienThi, link!.Trim(), null, "Trang truy xuất của đơn trên HanoiCheck.", dongChuThem);
        }

        // Đơn nội bộ (hoặc đơn HanoiCheck chưa có link): trang tra cứu của hệ thống. Sinh luôn mã tra cứu cho các lô đã
        // xuất để trang đơn dẫn sang được trang từng lô.
        GanMa(don);
        var maLo = don.Dong.SelectMany(l => l.XuatLo).Select(x => x.MaLo).Distinct().ToList();
        if (maLo.Count > 0)
        {
            foreach (var lo in await _db.Batches.Where(b => maLo.Contains(b.MaLo) && b.MaTraCuu == null).ToListAsync(ct))
                GanMa(lo);
        }
        await _db.SaveChangesAsync(ct);

        return new QrTraCuu(maHienThi, null, don.MaTraCuu,
            don.Nguon == NguonDonHang.HanoiCheck
                ? "Đơn HanoiCheck chưa có link truy xuất - dùng trang tra cứu của hệ thống."
                : "Trang tra cứu đơn hàng trên hệ thống.",
            dongChuThem);
    }

    /// <summary>Dòng "Tên sản phẩm × số lượng đơn vị tính" cho từng dòng hàng, rồi dòng ngày hẹn giao (nếu có)
    /// và địa chỉ giao (nếu có) - in thêm dưới mã QR đơn hàng để nhân viên giao hàng nhìn tem là biết giao gì,
    /// giao khi nào, giao đâu.</summary>
    private async Task<List<string>> DongChuThemDonHangAsync(DonHangBan don, CancellationToken ct)
    {
        var maSp = don.Dong.Select(l => l.MaThanhPham).Distinct().ToList();
        var sanPham = maSp.Count == 0
            ? new Dictionary<string, Product>()
            : await _db.Products.AsNoTracking().Where(p => maSp.Contains(p.MaSanPham)).ToDictionaryAsync(p => p.MaSanPham, ct);

        var dong = don.Dong.OrderBy(l => l.Id).Select(l =>
        {
            sanPham.TryGetValue(l.MaThanhPham, out var sp);
            var dvt = sp?.DonViTinh;
            return $"{sp?.TenSanPham ?? l.MaThanhPham} × {l.SoLuong.ToString("#,0.###")}{(string.IsNullOrWhiteSpace(dvt) ? "" : " " + dvt)}";
        }).ToList();

        if (don.NgayGiao is { } ngayGiao) dong.Add($"Hẹn giao: {ngayGiao:dd/MM/yyyy}");
        if (!string.IsNullOrWhiteSpace(don.DiaChiGiao)) dong.Add($"Giao: {don.DiaChiGiao.Trim()}");
        return dong;
    }

    public async Task<QrTraCuu?> LayQrLoAsync(int id, CancellationToken ct = default)
    {
        var lo = await _db.Batches.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (lo is null) return null;

        if (GanMa(lo)) await _db.SaveChangesAsync(ct);
        return new QrTraCuu(lo.MaLo, null, lo.MaTraCuu, "Trang tra cứu lô sản xuất trên hệ thống.");
    }

    /// <summary>Tiền tố nội dung QR của lệnh sản xuất - app nhận ra để mở màn tham gia khâu.</summary>
    public const string TienToQrLenhSanXuat = "LSX:";

    public async Task<QrTraCuu?> LayQrLenhSanXuatAsync(int id, CancellationToken ct = default)
    {
        var lenh = await _db.LenhSanXuats.AsNoTracking().Include(l => l.SanPham)
            .FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lenh is null) return null;

        var dongChuThem = await DongChuThemLenhSanXuatAsync(lenh, ct);
        return new QrTraCuu(lenh.MaLenh, TienToQrLenhSanXuat + lenh.MaLenh, null,
            "Nhân viên sản xuất mở ứng dụng, bấm \"Quét mã lệnh\" để tham gia các khâu của lệnh.", dongChuThem);
    }

    /// <summary>Dòng ngày sản xuất rồi "Tên sản phẩm × số lượng đơn vị tính - Lô mã lô" cho từng dòng thành
    /// phẩm - in thêm dưới mã QR lệnh sản xuất để nhân viên nhìn tem là biết lệnh làm gì, làm lô nào.</summary>
    private async Task<List<string>> DongChuThemLenhSanXuatAsync(LenhSanXuat lenh, CancellationToken ct)
    {
        var dong = new List<string> { $"Ngày SX: {lenh.NgaySanXuat:dd/MM/yyyy}" };

        var maSp = lenh.SanPham.Select(s => s.MaThanhPham).Distinct().ToList();
        var sanPham = maSp.Count == 0
            ? new Dictionary<string, Product>()
            : await _db.Products.AsNoTracking().Where(p => maSp.Contains(p.MaSanPham)).ToDictionaryAsync(p => p.MaSanPham, ct);

        dong.AddRange(lenh.SanPham.OrderBy(s => s.Id).Select(s =>
        {
            sanPham.TryGetValue(s.MaThanhPham, out var sp);
            var dvt = sp?.DonViTinh;
            var ten = $"{sp?.TenSanPham ?? s.MaThanhPham} × {s.SoLuong.ToString("#,0.###")}{(string.IsNullOrWhiteSpace(dvt) ? "" : " " + dvt)}";
            return string.IsNullOrWhiteSpace(s.MaLoThanhPham) ? ten : $"{ten} - Lô {s.MaLoThanhPham}";
        }));

        return dong;
    }

    public async Task<TraCuuDonHang?> TraCuuDonHangAsync(string maTraCuu, CancellationToken ct = default)
    {
        if (!LaMaHopLe(maTraCuu)) return null;

        var don = await _db.DonHangBans.IgnoreQueryFilters().AsNoTracking()
            .Include(d => d.Dong).ThenInclude(l => l.XuatLo)
            .FirstOrDefaultAsync(d => d.MaTraCuu == maTraCuu, ct);
        if (don is null) return null;

        var tenantId = don.TenantId;
        var maSp = don.Dong.Select(l => l.MaThanhPham).Distinct().ToList();
        var sanPham = await _db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.TenantId == tenantId && maSp.Contains(p.MaSanPham))
            .ToDictionaryAsync(p => p.MaSanPham, ct);

        var maLo = don.Dong.SelectMany(l => l.XuatLo).Select(x => x.MaLo).Distinct().ToList();
        var lo = await _db.Batches.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.TenantId == tenantId && maLo.Contains(b.MaLo))
            .ToDictionaryAsync(b => b.MaLo, ct);

        var maKhach = don.MaKhachHang;
        var tenKhach = await _db.KhachHangs.IgnoreQueryFilters().AsNoTracking()
            .Where(k => k.TenantId == tenantId && k.MaKhachHang == maKhach)
            .Select(k => k.TenKhachHang).FirstOrDefaultAsync(ct);

        var dong = don.Dong.OrderBy(l => l.Id).Select(l =>
        {
            sanPham.TryGetValue(l.MaThanhPham, out var sp);
            var cacLo = l.XuatLo.OrderBy(x => x.Id).Select(x =>
            {
                lo.TryGetValue(x.MaLo, out var b);
                return new TraCuuLoTomTat(x.MaLo, b?.NgaySanXuat, x.HanSuDung ?? b?.HanSuDung, x.SoLuong, b?.MaTraCuu);
            }).ToList();
            return new TraCuuDongDon(l.MaThanhPham, sp?.TenSanPham ?? l.MaThanhPham, sp?.DonViTinh, l.SoLuong, cacLo);
        }).ToList();

        return new TraCuuDonHang(await CoSoAsync(tenantId), don.MaDonHang, tenKhach ?? don.MaKhachHang, don.NgayDat,
            don.NgayGiao, TenTrangThai(don.TrangThai), don.ThoiGianGiaoUtc, dong);
    }

    public async Task<TraCuuLo?> TraCuuLoAsync(string maTraCuu, CancellationToken ct = default)
    {
        if (!LaMaHopLe(maTraCuu)) return null;

        var lo = await _db.Batches.IgnoreQueryFilters().AsNoTracking()
            .Include(b => b.DanhSachKhau).Include(b => b.DanhSachFile)
            .FirstOrDefaultAsync(b => b.MaTraCuu == maTraCuu, ct);
        if (lo is null) return null;

        var tenantId = lo.TenantId;
        var maSp = lo.MaSanPham;
        var tenSp = await _db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.MaSanPham == maSp)
            .Select(p => p.TenSanPham).FirstOrDefaultAsync(ct);

        var maKhau = lo.DanhSachKhau.Select(s => s.MaKhau).Distinct().ToList();
        var tenKhau = await _db.ProductionSteps.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.TenantId == tenantId && maKhau.Contains(s.MaKhau))
            .ToDictionaryAsync(s => s.MaKhau, s => s.TenKhau, ct);

        var maCoSo = lo.DanhSachKhau.Where(s => s.MaCoSo != null).Select(s => s.MaCoSo!).Distinct().ToList();
        var tenCoSo = await _db.Facilities.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.TenantId == tenantId && maCoSo.Contains(f.MaCoSo))
            .ToDictionaryAsync(f => f.MaCoSo, f => f.TenCoSo, ct);

        var khau = lo.DanhSachKhau.OrderBy(s => s.ThuTu).Select(s => new TraCuuKhau(
            s.ThuTu,
            tenKhau.GetValueOrDefault(s.MaKhau) ?? s.MaKhau,
            s.ThoiGian,
            s.MaCoSo is null ? null : tenCoSo.GetValueOrDefault(s.MaCoSo) ?? s.MaCoSo,
            s.MaLoNguyenLieu)).ToList();

        var anh = lo.DanhSachFile.Where(f => HnCPayloadMapper.LaAnhLo(f) && HnCPayloadMapper.LaDuongDanAnhLo(f.DuongDan))
            .Select(f => f.DuongDan).ToList();

        return new TraCuuLo(await CoSoAsync(tenantId), lo.MaLo, lo.TenLo, lo.MaSanPham, tenSp ?? lo.MaSanPham,
            lo.NgayNhap, lo.NgaySanXuat, lo.HanSuDung, anh, khau);
    }

    private async Task<TraCuuCoSo> CoSoAsync(string tenantId)
    {
        var t = _tenantStore is null ? null : await _tenantStore.TryGetAsync(tenantId);
        return new TraCuuCoSo(t?.Name, t?.DiaChi, t?.SoDienThoai);
    }

    /// <summary>Sinh mã tra cứu nếu bản ghi chưa có; trả true khi vừa sinh.</summary>
    private static bool GanMa(ICoMaTraCuu banGhi)
    {
        if (!string.IsNullOrEmpty(banGhi.MaTraCuu)) return false;
        banGhi.MaTraCuu = Guid.NewGuid().ToString("N");
        return true;
    }

    private static bool LaMaHopLe(string? ma) =>
        ma is { Length: 32 } && ma.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool LaLinkWeb(string? link) =>
        !string.IsNullOrWhiteSpace(link)
        && (link.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || link.Trim().StartsWith("http://", StringComparison.OrdinalIgnoreCase));

    private static string TenTrangThai(TrangThaiDonHangBan t) => t switch
    {
        TrangThaiDonHangBan.ChoXacNhan => "Chờ xác nhận",
        TrangThaiDonHangBan.DaXacNhan => "Đã xác nhận",
        TrangThaiDonHangBan.ChoGiaoHang => "Chờ giao hàng",
        TrangThaiDonHangBan.DangGiao => "Đang giao",
        TrangThaiDonHangBan.DaGiao => "Đã giao",
        TrangThaiDonHangBan.DaHuy => "Đã huỷ",
        _ => t.ToString()
    };
}
