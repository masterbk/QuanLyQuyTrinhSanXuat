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

    public async Task<IReadOnlyList<BieuMauEntity>> LayBieuMauChoNhapAsync(IEnumerable<string> vaiTro,
                                                                           CancellationToken ct = default)
    {
        var mau = await _db.BieuMaus.AsNoTracking().AsSplitQuery()
            .Where(b => b.KichHoat)
            .Include(b => b.Truong.OrderBy(t => t.ThuTu))
            .Include(b => b.HangMuc.OrderBy(h => h.ThuTu))
            .OrderBy(b => b.ThuTu).ThenBy(b => b.Ten)
            .ToListAsync(ct);

        // NhomQuyen là chuỗi role (phẩy = OR); giữ mẫu nếu người dùng có ÍT NHẤT một vai trò trong đó.
        var cuaToi = vaiTro.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return mau.Where(b => b.NhomQuyen.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                        .Any(cuaToi.Contains)).ToList();
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

    public async Task<KetQuaThaoTac> TaoPhieuAsync(PhieuGhiNhan phieu, CancellationToken ct = default)
    {
        var mau = await _db.BieuMaus.AsNoTracking().AsSplitQuery()
            .Include(b => b.Truong).Include(b => b.HangMuc)
            .FirstOrDefaultAsync(b => b.Id == phieu.BieuMauId, ct);
        if (mau is null) return KetQuaThaoTac.Loi("Không tìm thấy biểu mẫu.");
        if (!mau.KichHoat) return KetQuaThaoTac.Loi("Biểu mẫu đã ngừng kích hoạt.");
        if (phieu.Ngay == default) phieu.Ngay = GioVietNam.HomNay;

        var dong = phieu.Dong ?? new List<DongGhiNhan>();
        if (dong.Count == 0) return KetQuaThaoTac.Loi("Phiếu phải có ít nhất một dòng dữ liệu.");

        var truongBatBuoc = mau.Truong.Where(t => t.BatBuoc).ToList();
        var hangMucHopLe = mau.HangMuc.Select(h => h.Id).ToHashSet();
        var thuTu = 0;
        foreach (var d in dong)
        {
            d.Id = 0;
            d.ThuTu = thuTu++;
            // Chuẩn hoá JSON giá trị (chặn JSON hỏng).
            Dictionary<string, string?> giaTri;
            try
            {
                giaTri = string.IsNullOrWhiteSpace(d.GiaTriJson)
                    ? new()
                    : JsonSerializer.Deserialize<Dictionary<string, string?>>(d.GiaTriJson) ?? new();
            }
            catch (JsonException)
            {
                return KetQuaThaoTac.Loi("Dữ liệu dòng không hợp lệ.");
            }
            d.GiaTriJson = JsonSerializer.Serialize(giaTri);

            if (mau.BoCuc == BoCucBieuMau.Checklist)
            {
                if (d.HangMucBieuMauId is not { } hm || !hangMucHopLe.Contains(hm))
                    return KetQuaThaoTac.Loi("Dòng checklist không gắn đúng hạng mục của biểu mẫu.");
            }
            else d.HangMucBieuMauId = null;

            foreach (var t in truongBatBuoc)
            {
                if (!giaTri.TryGetValue(t.Ma, out var v) || string.IsNullOrWhiteSpace(v))
                    return KetQuaThaoTac.Loi($"Thiếu giá trị bắt buộc \"{t.Ten}\".");
            }
        }

        phieu.TrangThai = TrangThaiPhieu.DaGhiNhan;
        phieu.ThoiGianUtc = DateTime.UtcNow;
        phieu.NguoiThamTra = null;
        phieu.ThoiGianThamTraUtc = null;
        phieu.KetQuaThamTra = null;
        _db.PhieuGhiNhans.Add(phieu);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã lưu phiếu \"{mau.Ten}\" ngày {phieu.Ngay:dd/MM/yyyy}.");
    }
}
