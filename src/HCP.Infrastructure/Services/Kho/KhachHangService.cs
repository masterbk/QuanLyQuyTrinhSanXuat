using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.Kho;

/// <inheritdoc cref="IKhachHangService"/>
public sealed class KhachHangService : IKhachHangService
{
    private readonly AppDbContext _db;

    public KhachHangService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<KhachHang>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.KhachHangs.AsNoTracking().OrderBy(k => k.TenKhachHang).ToListAsync(ct);

    public Task<KhachHang?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        _db.KhachHangs.FirstOrDefaultAsync(k => k.Id == id, ct);

    public async Task<KetQuaThaoTac> LuuAsync(KhachHang kh, CancellationToken ct = default)
    {
        kh.MaKhachHang = kh.MaKhachHang?.Trim() ?? "";
        kh.TenKhachHang = kh.TenKhachHang?.Trim() ?? "";
        kh.DienThoai = kh.DienThoai?.Trim();
        kh.DiaChi = kh.DiaChi?.Trim();
        kh.GhiChu = kh.GhiChu?.Trim();
        kh.MaTruongHnC = kh.Loai == LoaiKhachHang.TruongHoc && !string.IsNullOrWhiteSpace(kh.MaTruongHnC)
            ? kh.MaTruongHnC.Trim()
            : null;

        if (string.IsNullOrWhiteSpace(kh.MaKhachHang)) return KetQuaThaoTac.Loi("Vui lòng nhập mã khách hàng.");
        if (string.IsNullOrWhiteSpace(kh.TenKhachHang)) return KetQuaThaoTac.Loi("Vui lòng nhập tên khách hàng.");

        var trung = await _db.KhachHangs
            .AnyAsync(k => k.MaKhachHang == kh.MaKhachHang && k.Id != kh.Id, ct);
        if (trung) return KetQuaThaoTac.Loi($"Mã khách hàng \"{kh.MaKhachHang}\" đã tồn tại.");

        if (kh.Id == 0)
        {
            _db.KhachHangs.Add(kh);
        }
        else
        {
            var db = await _db.KhachHangs.FirstOrDefaultAsync(k => k.Id == kh.Id, ct);
            if (db is null) return KetQuaThaoTac.Loi("Không tìm thấy khách hàng cần cập nhật.");
            db.MaKhachHang = kh.MaKhachHang;
            db.TenKhachHang = kh.TenKhachHang;
            db.Loai = kh.Loai;
            db.DienThoai = kh.DienThoai;
            db.DiaChi = kh.DiaChi;
            db.GhiChu = kh.GhiChu;
            db.MaTruongHnC = kh.MaTruongHnC;
        }

        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã lưu khách hàng \"{kh.TenKhachHang}\".");
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var kh = await _db.KhachHangs.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kh is null) return KetQuaThaoTac.Loi("Không tìm thấy khách hàng.");

        // Chặn xoá khi đã có đơn hàng tham chiếu (giữ toàn vẹn lịch sử bán hàng).
        if (await _db.DonHangBans.AnyAsync(d => d.MaKhachHang == kh.MaKhachHang, ct))
            return KetQuaThaoTac.Loi("Không xoá được: khách hàng đã có đơn hàng.");

        _db.KhachHangs.Remove(kh);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã xoá khách hàng \"{kh.TenKhachHang}\".");
    }
}
