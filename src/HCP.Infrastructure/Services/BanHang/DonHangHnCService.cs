using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using HCP.Domain.Constants;
using HCP.Domain.Entities.Business;
using HCP.Domain.Entities.Infrastructure;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.MaTuSinh;
using HCP.Infrastructure.Services.ThongBao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DonHangNhanEntity = HCP.Domain.Entities.Business.DonHangNhan;

namespace HCP.Infrastructure.Services.BanHang;

/// <summary>
/// Biến đơn trường đặt trên HanoiCheck (bảng DonHangNhan, do job kéo về) thành Đơn hàng bán nguồn HanoiCheck.
///
/// Quy tắc (người dùng chốt 13/09/2026):
///  - Khách hàng: khớp khách loại Trường học theo TÊN trường; chưa có thì tự tạo (mã KH-0001...).
///  - App quản lý trạng thái + kho; trạng thái HanoiCheck chỉ lưu để đối chiếu. HnC huỷ/từ chối mà đơn chưa xuất kho
///    thì tự huỷ; đã xuất kho thì chỉ gắn cờ để NCC xử lý.
///  - HnC đổi nội dung đơn: cập nhật (giữ đơn giá NCC đã nhập) khi đơn CHƯA XUẤT KHO (Chờ xác nhận hoặc
///    Đã xác nhận - trường được sửa đơn tới sát lúc xác nhận nhận hàng theo đặc tả); đã xuất kho thì chỉ gắn cờ.
///  - Đơn giá: HnC không có giá -> 0, NCC tự nhập.
/// Chạy trong ngữ cảnh MỘT cơ sở (tenant hiện hành).
/// </summary>
public interface IDonHangHnCService
{
    /// <summary>Đồng bộ mọi đơn HanoiCheck của cơ sở hiện hành vào Đơn hàng bán. Trả số đơn được tạo/cập nhật.</summary>
    Task<int> DongBoVaoDonHangBanAsync(CancellationToken ct = default);
}

/// <summary>Chạy <see cref="IDonHangHnCService"/> cho một cơ sở từ job nền (vốn không có ngữ cảnh cơ sở).</summary>
public interface IDonHangHnCTheoCoSo
{
    Task<int> DongBoAsync(string tenantId, CancellationToken ct = default);
}

/// <inheritdoc cref="IDonHangHnCService"/>
public sealed class DonHangHnCService : IDonHangHnCService
{
    private static readonly HashSet<string> TrangThaiHuyHnC = new(StringComparer.OrdinalIgnoreCase) { "HUY", "TU_CHOI" };

    private readonly AppDbContext _db;
    private readonly IMaTuSinhService _maTuSinh;
    private readonly IPushNotificationService _push;

    public DonHangHnCService(AppDbContext db, IMaTuSinhService maTuSinh, IPushNotificationService push)
    {
        _db = db;
        _maTuSinh = maTuSinh;
        _push = push;
    }

    public async Task<int> DongBoVaoDonHangBanAsync(CancellationToken ct = default)
    {
        var tenantId = _db.TenantInfo?.Id
            ?? throw new InvalidOperationException("Không xác định được cơ sở để đồng bộ đơn hàng bán.");

        // DonHangNhan là bảng job-ghi, không có global filter -> lọc tường minh theo cơ sở.
        var donNhan = await _db.DonHangNhans.AsNoTracking()
            .Include(d => d.Dong).ThenInclude(l => l.PhanBo)
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(ct);
        if (donNhan.Count == 0) return 0;

        var thanhPham = (await _db.Products.AsNoTracking().Where(p => p.LoaiSanPham == LoaiSanPham.ThanhPham)
            .Select(p => p.MaSanPham).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var kho = await _db.Warehouses.AsNoTracking().OrderBy(k => k.MaKho).Select(k => k.MaKho).ToListAsync(ct);
        var nhanSu = (await _db.Staff.AsNoTracking().Select(s => s.MaNhanSu).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        var maHnC = donNhan.Select(d => d.MaDonHang).ToList();
        var daCo = (await _db.DonHangBans.Include(d => d.Dong)
                .Where(d => d.Nguon == NguonDonHang.HanoiCheck && d.MaDonHnC != null && maHnC.Contains(d.MaDonHnC))
                .ToListAsync(ct))
            .GroupBy(d => d.MaDonHnC!).ToDictionary(g => g.Key, g => g.First());

        var now = DateTime.UtcNow;
        var soDon = 0;
        foreach (var n in donNhan.OrderBy(d => d.NgayTaoTrenHnC ?? DateTime.MaxValue).ThenBy(d => d.Id))
        {
            // KHÔNG gộp theo mã sản phẩm: mỗi dòng HnC (kèm trace_code riêng) phải giữ nguyên 1-1
            // để sau này đẩy ngược xử lý đơn (chi_tiet[].trace_code) gọi đúng dòng - một sản phẩm có
            // thể xuất hiện nhiều lần trong cùng đơn với trace_code khác nhau.
            var dong = n.Dong.Where(l => l.SoLuong is > 0 && thanhPham.Contains(l.MaSanPham))
                .Select(l => (Ma: l.MaSanPham, SoLuong: l.SoLuong!.Value, TraceCode: l.MaTruyVet))
                .OrderBy(x => x.Ma, StringComparer.Ordinal).ThenBy(x => x.TraceCode, StringComparer.Ordinal)
                .ToList();
            var biHuyTrenHnC = n.TrangThai is not null && TrangThaiHuyHnC.Contains(n.TrangThai);

            if (!daCo.TryGetValue(n.MaDonHang, out var don))
            {
                // Đơn đã huỷ từ đầu, hoặc không có thành phẩm nào khớp danh mục -> không tạo.
                if (biHuyTrenHnC || dong.Count == 0 || kho.Count == 0) continue;
                don = await TaoDonAsync(n, dong, kho, nhanSu, ct);
                daCo[n.MaDonHang] = don;
                soDon++;
                continue;
            }

            if (CapNhatDon(don, n, dong, biHuyTrenHnC, nhanSu, now)) soDon++;
        }

        await _db.SaveChangesAsync(ct);
        return soDon;
    }

    private async Task<DonHangBan> TaoDonAsync(DonHangNhanEntity n, List<(string Ma, decimal SoLuong, string? TraceCode)> dong,
                                                List<string> kho, HashSet<string> nhanSu, CancellationToken ct)
    {
        var khach = await TimHoacTaoKhachTruongAsync(n.TenTruong, ct);

        var ngayGiao = n.NgayGiao;
        var ngayDat = n.NgayTaoTrenHnC is { } tao ? DateOnly.FromDateTime(tao) : ngayGiao ?? MaTuSinhService.HomNay;
        if (ngayGiao is { } g && ngayDat > g) ngayDat = g;

        // Kho: ưu tiên kho HanoiCheck đã phân bổ (nếu có trong danh mục app), không thì kho đầu tiên.
        var maKho = n.Dong.SelectMany(l => l.PhanBo).Select(p => p.MaKho)
            .FirstOrDefault(m => m is not null && kho.Contains(m)) ?? kho[0];

        var khongKhop = n.Dong.Where(l => !dong.Any(x => x.Ma == l.MaSanPham))
            .Select(l => string.IsNullOrWhiteSpace(l.TenSanPham) ? l.MaSanPham : $"{l.MaSanPham} ({l.TenSanPham})")
            .Distinct().ToList();
        var ghiChu = string.Join(" | ", new[]
        {
            n.GhiChu,
            khongKhop.Count > 0 ? "Dòng HanoiCheck không khớp thành phẩm, chưa đưa vào đơn: " + string.Join(", ", khongKhop) : null
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var don = new DonHangBan
        {
            MaKhachHang = khach.MaKhachHang,
            MaKho = maKho,
            NgayDat = ngayDat,
            NgayGiao = ngayGiao,
            DiaChiGiao = n.DiaChiGiao ?? khach.DiaChi,
            MaNguoiGiao = n.MaNguoiGiao is { } ng && nhanSu.Contains(ng) ? ng : null,
            TrangThai = TrangThaiDonHangBan.ChoXacNhan,
            Nguon = NguonDonHang.HanoiCheck,
            MaDonHnC = n.MaDonHang,
            TrangThaiHnC = n.TrangThai,
            GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu[..Math.Min(ghiChu.Length, 1000)],
            Dong = dong.Select(x => new DonHangBanDong
            {
                MaThanhPham = x.Ma, SoLuong = x.SoLuong, DonGia = 0, MaTruyVetHnC = x.TraceCode
            }).ToList()
        };

        // Hàm sinh mã tự SaveChanges bộ đếm -> gọi TRƯỚC khi Add.
        don.MaDonHang = await _maTuSinh.SinhAsync(LoaiMaTuSinh.DonHangBan, ngayDat, ct);
        _db.DonHangBans.Add(don);
        await _db.SaveChangesAsync(ct);

        if (_db.TenantInfo?.Id is { } tenantId)
            await _push.GuiTheoQuyenAsync(tenantId, AppRoles.QuyenNhapLieu.Split(','), "Đơn hàng mới",
                $"Đơn mới từ {khach.TenKhachHang}: {don.MaDonHang}",
                new Dictionary<string, string> { ["loaiThongBao"] = "don_hang", ["donHangId"] = don.Id.ToString() }, ct);

        return don;
    }

    /// <summary>Áp thay đổi từ HanoiCheck vào đơn đã có. Trả true nếu có thay đổi.</summary>
    private bool CapNhatDon(DonHangBan don, DonHangNhanEntity n, List<(string Ma, decimal SoLuong, string? TraceCode)> dong,
                            bool biHuyTrenHnC, HashSet<string> nhanSu, DateTime now)
    {
        var coDoi = false;
        if (don.TrangThaiHnC != n.TrangThai)
        {
            don.TrangThaiHnC = n.TrangThai;
            coDoi = true;
        }

        var chuaXuatKho = don.TrangThai is TrangThaiDonHangBan.ChoXacNhan or TrangThaiDonHangBan.DaXacNhan;

        if (don.TrangThai == TrangThaiDonHangBan.DaHuy)
        {
            // NCC hoặc lần đồng bộ trước đã huỷ: không "hồi sinh" đơn.
        }
        else if (biHuyTrenHnC)
        {
            if (chuaXuatKho)
            {
                don.TrangThai = TrangThaiDonHangBan.DaHuy;
                don.LyDoHuy = n.TrangThai == "TU_CHOI" ? "HanoiCheck: đơn bị từ chối." : "HanoiCheck: trường đã huỷ đơn.";
                don.ThoiGianHuyUtc = now;
                coDoi = true;
            }
            else if (!don.HnCCoThayDoi)
            {
                // Đã xuất kho: không tự trả hàng về kho - để NCC quyết định.
                don.HnCCoThayDoi = true;
                coDoi = true;
            }
        }
        else if (NoiDungKhac(don, dong, n.NgayGiao))
        {
            // Trường được sửa đơn (thêm/đổi dòng, đổi số lượng) tới sát lúc xác nhận nhận hàng
            // (theo đặc tả HnC v2.5 mục 11.a/11.c) - nên NCC vẫn đồng bộ lại được miễn ĐƠN CHƯA
            // XUẤT KHO (chưa "Đang giao"), dù đã "Đã xác nhận" nội bộ. Trừ tồn/số dòng chỉ chốt
            // thật khi xuất kho, nên sửa dòng lúc này không ảnh hưởng gì đã ghi trước đó.
            if (chuaXuatKho && dong.Count > 0)
            {
                // Giữ đơn giá NCC đã nhập: khớp theo (mã, trace_code) trước, mã đơn thuần là dự phòng
                // (đơn cũ trước khi có trace_code, hoặc trace_code đổi nhẹ giữa hai lần đồng bộ).
                var giaCuTheoTrace = don.Dong.Where(l => l.MaTruyVetHnC is not null)
                    .GroupBy(l => (l.MaThanhPham, l.MaTruyVetHnC))
                    .ToDictionary(g => g.Key, g => g.First().DonGia);
                var giaCuTheoMa = don.Dong.GroupBy(l => l.MaThanhPham).ToDictionary(g => g.Key, g => g.First().DonGia);
                _db.DonHangBanDongs.RemoveRange(don.Dong);
                don.Dong = dong.Select(x => new DonHangBanDong
                {
                    MaThanhPham = x.Ma, SoLuong = x.SoLuong, MaTruyVetHnC = x.TraceCode,
                    DonGia = x.TraceCode is not null && giaCuTheoTrace.TryGetValue((x.Ma, x.TraceCode), out var giaTrace)
                        ? giaTrace : giaCuTheoMa.GetValueOrDefault(x.Ma)
                }).ToList();
                don.NgayGiao = n.NgayGiao;
                if (don.NgayGiao is { } g && don.NgayDat > g) don.NgayDat = g;
                coDoi = true;
            }
            else if (!don.HnCCoThayDoi)
            {
                don.HnCCoThayDoi = true;
                coDoi = true;
            }
        }

        // Người giao do HnC phân công: chỉ điền khi NCC chưa chọn và đơn chưa xuất kho.
        if (chuaXuatKho && don.TrangThai != TrangThaiDonHangBan.DaHuy && don.MaNguoiGiao is null
            && n.MaNguoiGiao is { } ng && nhanSu.Contains(ng))
        {
            don.MaNguoiGiao = ng;
            coDoi = true;
        }

        if (coDoi) don.UpdatedAtUtc = now;
        return coDoi;
    }

    private static bool NoiDungKhac(DonHangBan don, List<(string Ma, decimal SoLuong, string? TraceCode)> dong, DateOnly? ngayGiao)
    {
        if (don.NgayGiao != ngayGiao) return true;
        var hienTai = don.Dong
            .Select(l => (Ma: l.MaThanhPham, SoLuong: l.SoLuong, TraceCode: l.MaTruyVetHnC))
            .OrderBy(x => x.Ma, StringComparer.Ordinal).ThenBy(x => x.TraceCode, StringComparer.Ordinal).ToList();
        var moi = dong.OrderBy(x => x.Ma, StringComparer.Ordinal).ThenBy(x => x.TraceCode, StringComparer.Ordinal).ToList();
        return !hienTai.SequenceEqual(moi);
    }

    private async Task<KhachHang> TimHoacTaoKhachTruongAsync(string? tenTruong, CancellationToken ct)
    {
        var ten = string.IsNullOrWhiteSpace(tenTruong) ? "Trường (HanoiCheck, chưa rõ tên)" : tenTruong.Trim();
        var khach = await _db.KhachHangs.FirstOrDefaultAsync(
            k => k.Loai == LoaiKhachHang.TruongHoc && k.TenKhachHang == ten, ct);
        if (khach is not null) return khach;

        khach = new KhachHang
        {
            TenKhachHang = ten,
            Loai = LoaiKhachHang.TruongHoc,
            GhiChu = "Tự tạo từ đơn hàng HanoiCheck."
        };
        khach.MaKhachHang = await _maTuSinh.SinhAsync(LoaiMaTuSinh.KhachHang, ct: ct);
        _db.KhachHangs.Add(khach);
        await _db.SaveChangesAsync(ct);
        return khach;
    }
}

/// <inheritdoc cref="IDonHangHnCTheoCoSo"/>
public sealed class DonHangHnCTheoCoSo : IDonHangHnCTheoCoSo
{
    private readonly IServiceScopeFactory _scopes;

    public DonHangHnCTheoCoSo(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task<int> DongBoAsync(string tenantId, CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var sp = scope.ServiceProvider;

        var tenant = await sp.GetRequiredService<IMultiTenantStore<Tenant>>().TryGetAsync(tenantId);
        if (tenant is null) return 0;

        // Gán cơ sở cho luồng này TRƯỚC khi lấy DbContext, để bộ lọc đa cơ sở và TenantId ghi đúng.
        sp.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<Tenant> { TenantInfo = tenant };

        return await sp.GetRequiredService<IDonHangHnCService>().DongBoVaoDonHangBanAsync(ct);
    }
}
