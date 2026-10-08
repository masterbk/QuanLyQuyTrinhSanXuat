using HCP.Domain.Constants;
using System.Text.Json;
using HCP.Domain;
using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using BieuMauEntity = HCP.Domain.Entities.Business.BieuMau;

namespace HCP.Infrastructure.Services.BieuMau;

/// <inheritdoc cref="IPhieuGhiNhanService"/>
public sealed class PhieuGhiNhanService : IPhieuGhiNhanService
{
    private readonly AppDbContext _db;
    public PhieuGhiNhanService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<BieuMauEntity>> LayBieuMauChoNhapAsync(IEnumerable<string> vaiTro, string? maNhanSu,
                                                                           CancellationToken ct = default)
    {
        var duocXem = await LayMauDuocXemAsync(vaiTro, maNhanSu, ct);
        if (duocXem is { Count: 0 }) return Array.Empty<BieuMauEntity>();
        var mau = await _db.BieuMaus.AsNoTracking().AsSplitQuery()
            .Where(b => b.KichHoat)
            .Include(b => b.Truong.OrderBy(t => t.ThuTu))
            .Include(b => b.HangMuc.OrderBy(h => h.ThuTu))
            .OrderBy(b => b.ThuTu).ThenBy(b => b.Ten)
            .ToListAsync(ct);
        return duocXem is null ? mau : mau.Where(b => duocXem.Contains(b.Id)).ToList();
    }

    public async Task<IReadOnlySet<int>?> LayMauDuocXemAsync(IEnumerable<string> vaiTro, string? maNhanSu,
                                                            CancellationToken ct = default)
    {
        var vt = vaiTro.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (vt.Contains(AppRoles.TenantAdmin) || vt.Contains(AppRoles.TenantStaff)) return null;
        if (!vt.Contains(AppRoles.TenantBieuMau) || string.IsNullOrWhiteSpace(maNhanSu)) return new HashSet<int>();

        var nhanSuId = await _db.Staff.AsNoTracking().Where(s => s.MaNhanSu == maNhanSu)
            .Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        var pq = nhanSuId is null ? null
            : await _db.PhanQuyenBieuMaus.AsNoTracking().FirstOrDefaultAsync(p => p.NhanSuId == nhanSuId, ct);
        return pq is null || pq.TatCa ? null : pq.BieuMauIds.ToHashSet();   // chưa cấu hình = mặc định tất cả
    }

    public async Task<(IReadOnlyList<PhieuGhiNhan> DuLieu, int TongSo)> LayLichSuAsync(IReadOnlySet<int>? mauIds,
        DateOnly? tuNgay, DateOnly? denNgay, int? bieuMauId, (string? MaNhanSu, string? UserId)? cuaToi,
        int trang, int soDong, CancellationToken ct = default)
    {
        var q = _db.PhieuGhiNhans.AsNoTracking().AsQueryable();
        if (mauIds is not null)
        {
            var ids = mauIds.ToList();
            q = q.Where(p => ids.Contains(p.BieuMauId));
        }
        if (tuNgay is { } tu) q = q.Where(p => p.Ngay >= tu);
        if (denNgay is { } den) q = q.Where(p => p.Ngay <= den);
        if (bieuMauId is { } bm) q = q.Where(p => p.BieuMauId == bm);
        if (cuaToi is { } toi)
        {
            var ma = string.IsNullOrWhiteSpace(toi.MaNhanSu) ? null : toi.MaNhanSu;
            var uid = string.IsNullOrWhiteSpace(toi.UserId) ? null : toi.UserId;
            q = q.Where(p => (ma != null && p.NguoiLap == ma) || (uid != null && p.NguoiLapUserId == uid));
        }

        var tong = await q.CountAsync(ct);
        var ds = await q.Include(p => p.Dong).AsSplitQuery()
            .OrderByDescending(p => p.Ngay).ThenByDescending(p => p.Id)
            .Skip((Math.Max(1, trang) - 1) * soDong).Take(soDong)
            .ToListAsync(ct);
        return (ds, tong);
    }

    public Task<BieuMauEntity?> LayBieuMauAsync(int bieuMauId, CancellationToken ct = default) =>
        _db.BieuMaus.AsNoTracking().AsSplitQuery()
            .Include(b => b.Truong.OrderBy(t => t.ThuTu))
            .Include(b => b.HangMuc.OrderBy(h => h.ThuTu))
            .FirstOrDefaultAsync(b => b.Id == bieuMauId, ct);

    public async Task<IReadOnlyList<PhieuGhiNhan>> LayPhieuAsync(DateOnly? ngay, string? nguoiLap, int? bieuMauId,
                                                                 CancellationToken ct = default)
    {
        var q = _db.PhieuGhiNhans.AsNoTracking().Include(p => p.Dong).AsSplitQuery().AsQueryable();
        if (ngay is { } n) q = q.Where(p => p.Ngay == n);
        if (!string.IsNullOrWhiteSpace(nguoiLap)) q = q.Where(p => p.NguoiLap == nguoiLap);
        if (bieuMauId is { } bm) q = q.Where(p => p.BieuMauId == bm);
        return await q.OrderByDescending(p => p.Ngay).ThenByDescending(p => p.Id).ToListAsync(ct);
    }

    public Task<PhieuGhiNhan?> LayPhieuTheoIdAsync(int id, CancellationToken ct = default) =>
        _db.PhieuGhiNhans.AsNoTracking().Include(p => p.Dong).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<PhieuGhiNhan>> LayPhieuThangAsync(int bieuMauId, int nam, int thang,
                                                                     CancellationToken ct = default)
    {
        var dau = new DateOnly(nam, thang, 1);
        var cuoi = dau.AddMonths(1);
        return await _db.PhieuGhiNhans.AsNoTracking().Include(p => p.Dong).AsSplitQuery()
            .Where(p => p.BieuMauId == bieuMauId && p.Ngay >= dau && p.Ngay < cuoi)
            .OrderBy(p => p.Ngay).ThenBy(p => p.Id)
            .ToListAsync(ct);
    }

    public Task<PhieuGhiNhan?> LayPhieuNhapAsync(int bieuMauId, DateOnly ngay, string? nguoiLap,
                                                 CancellationToken ct = default) =>
        _db.PhieuGhiNhans.AsNoTracking().Include(p => p.Dong).OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(p => p.BieuMauId == bieuMauId && p.Ngay == ngay
                                      && p.TrangThai == TrangThaiPhieu.Nhap
                                      && (nguoiLap == null || p.NguoiLap == nguoiLap), ct);

    public Task<PhieuGhiNhan?> LayPhieuTheoNgayAsync(int bieuMauId, DateOnly ngay, CancellationToken ct = default) =>
        _db.PhieuGhiNhans.AsNoTracking().Include(p => p.Dong).OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(p => p.BieuMauId == bieuMauId && p.Ngay == ngay, ct);

    public async Task<KetQuaThaoTac> TaoPhieuAsync(PhieuGhiNhan phieu, bool hoanThanh = true,
                                                   CancellationToken ct = default)
    {
        var mau = await LayMauKiemTraAsync(phieu.BieuMauId, ct);
        if (mau is null) return KetQuaThaoTac.Loi("Không tìm thấy biểu mẫu.");
        if (!mau.KichHoat) return KetQuaThaoTac.Loi("Biểu mẫu đã ngừng kích hoạt.");
        if (phieu.Ngay == default) phieu.Ngay = GioVietNam.HomNay;

        // Biểu mẫu "1 phiếu/ngày": khoá theo ngày - không cho tạo phiếu thứ 2 cho cùng ngày
        // (mở phiếu đã có để sửa nếu còn nháp, hoặc xem nếu đã hoàn thành).
        if (mau.MotPhieuMoiNgay
            && await _db.PhieuGhiNhans.AnyAsync(p => p.BieuMauId == phieu.BieuMauId && p.Ngay == phieu.Ngay, ct))
            return KetQuaThaoTac.Loi($"Ngày {phieu.Ngay:dd/MM/yyyy} đã có phiếu \"{mau.Ten}\". " +
                                     "Hãy mở phiếu của ngày này để sửa (nếu còn nháp) hoặc xem (nếu đã hoàn thành).");

        var loi = KiemTraChuanHoaDong(mau, phieu.Dong ?? new(), hoanThanh)
                  ?? ChuanHoaDauPhieu(mau, phieu, hoanThanh);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        phieu.TrangThai = hoanThanh ? TrangThaiPhieu.DaGhiNhan : TrangThaiPhieu.Nhap;
        phieu.ThoiGianUtc = DateTime.UtcNow;
        phieu.NguoiThamTra = null;
        phieu.ThoiGianThamTraUtc = null;
        phieu.KetQuaThamTra = null;
        _db.PhieuGhiNhans.Add(phieu);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok(ThongBaoLuu(mau.Ten, phieu.Ngay, hoanThanh));
    }

    public async Task<KetQuaThaoTac> CapNhatPhieuAsync(PhieuGhiNhan phieu, bool hoanThanh = true,
                                                       CancellationToken ct = default)
    {
        var goc = await _db.PhieuGhiNhans.Include(p => p.Dong).FirstOrDefaultAsync(p => p.Id == phieu.Id, ct);
        if (goc is null) return KetQuaThaoTac.Loi("Không tìm thấy phiếu.");
        if (goc.TrangThai != TrangThaiPhieu.Nhap)
            return KetQuaThaoTac.Loi("Phiếu đã hoàn thành, không sửa tiếp được.");

        var mau = await LayMauKiemTraAsync(goc.BieuMauId, ct);
        if (mau is null) return KetQuaThaoTac.Loi("Không tìm thấy biểu mẫu.");

        var dongMoi = phieu.Dong ?? new List<DongGhiNhan>();
        var loi = KiemTraChuanHoaDong(mau, dongMoi, hoanThanh)
                  ?? ChuanHoaDauPhieu(mau, phieu, hoanThanh);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        goc.GiaTriDauJson = phieu.GiaTriDauJson;
        goc.GhiChu = phieu.GhiChu;
        goc.TrangThai = hoanThanh ? TrangThaiPhieu.DaGhiNhan : TrangThaiPhieu.Nhap;
        goc.ThoiGianUtc = DateTime.UtcNow;
        _db.DongGhiNhans.RemoveRange(goc.Dong);
        goc.Dong = dongMoi;

        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok(ThongBaoLuu(mau.Ten, goc.Ngay, hoanThanh));
    }

    private Task<BieuMauEntity?> LayMauKiemTraAsync(int bieuMauId, CancellationToken ct) =>
        _db.BieuMaus.AsNoTracking().AsSplitQuery()
            .Include(b => b.Truong).Include(b => b.HangMuc)
            .FirstOrDefaultAsync(b => b.Id == bieuMauId, ct);

    private static string ThongBaoLuu(string tenMau, DateOnly ngay, bool hoanThanh) =>
        hoanThanh ? $"Đã hoàn thành phiếu \"{tenMau}\" ngày {ngay:dd/MM/yyyy}."
                  : $"Đã lưu nháp phiếu \"{tenMau}\" ngày {ngay:dd/MM/yyyy} - có thể nhập tiếp đến khi Hoàn thành.";

    /// <summary>Chuẩn hoá (Id=0, ThuTu, JSON) + kiểm tra danh sách dòng. Trường bắt buộc chỉ bắt khi hoàn thành.
    /// Trả thông báo lỗi, hoặc null nếu hợp lệ.</summary>
    private static string? KiemTraChuanHoaDong(BieuMauEntity mau, IList<DongGhiNhan> dong, bool hoanThanh)
    {
        if (dong.Count == 0) return "Phiếu phải có ít nhất một dòng dữ liệu.";
        var truongBatBuoc = mau.Truong.Where(t => t.BatBuoc && !t.LaDauPhieu).ToList();
        var hangMucHopLe = mau.HangMuc.Select(h => h.Id).ToHashSet();
        var thuTu = 0;
        foreach (var d in dong)
        {
            d.Id = 0;
            d.ThuTu = thuTu++;
            Dictionary<string, string?> giaTri;
            try
            {
                giaTri = string.IsNullOrWhiteSpace(d.GiaTriJson)
                    ? new()
                    : JsonSerializer.Deserialize<Dictionary<string, string?>>(d.GiaTriJson) ?? new();
            }
            catch (JsonException)
            {
                return "Dữ liệu dòng không hợp lệ.";
            }
            d.GiaTriJson = JsonSerializer.Serialize(giaTri);

            if (mau.BoCuc == BoCucBieuMau.Checklist)
            {
                if (d.HangMucBieuMauId is not { } hm || !hangMucHopLe.Contains(hm))
                    return "Dòng checklist không gắn đúng hạng mục của biểu mẫu.";
            }
            else d.HangMucBieuMauId = null;

            if (hoanThanh)
                foreach (var t in truongBatBuoc)
                    if (!giaTri.TryGetValue(t.Ma, out var v) || string.IsNullOrWhiteSpace(v))
                        return $"Thiếu giá trị bắt buộc \"{t.Ten}\".";
        }
        return null;
    }

    /// <summary>Chuẩn hoá JSON đầu phiếu + kiểm tra trường đầu phiếu bắt buộc (chỉ khi hoàn thành).</summary>
    private static string? ChuanHoaDauPhieu(BieuMauEntity mau, PhieuGhiNhan phieu, bool hoanThanh)
    {
        Dictionary<string, string?> giaTri;
        try
        {
            giaTri = string.IsNullOrWhiteSpace(phieu.GiaTriDauJson)
                ? new()
                : JsonSerializer.Deserialize<Dictionary<string, string?>>(phieu.GiaTriDauJson) ?? new();
        }
        catch (JsonException)
        {
            return "Dữ liệu đầu phiếu không hợp lệ.";
        }
        phieu.GiaTriDauJson = JsonSerializer.Serialize(giaTri);

        if (hoanThanh)
            foreach (var t in mau.Truong.Where(t => t.LaDauPhieu && t.BatBuoc))
                if (!giaTri.TryGetValue(t.Ma, out var v) || string.IsNullOrWhiteSpace(v))
                    return $"Thiếu thông tin đầu phiếu bắt buộc \"{t.Ten}\".";
        return null;
    }
}
