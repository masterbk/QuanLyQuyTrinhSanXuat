using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.BanHang;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;
using DonHangNhanEntity = HCP.Domain.Entities.Business.DonHangNhan;

namespace HCP.Infrastructure.Services.DonHangNhan;

/// <inheritdoc cref="IDonHangNhanService"/>
public sealed class DonHangNhanService : IDonHangNhanService
{
    private readonly AppDbContext _db;
    private readonly IDongBoDonHangJob _job;
    private readonly IDonHangHnCService _donBan;

    public DonHangNhanService(AppDbContext db, IDongBoDonHangJob job, IDonHangHnCService donBan)
    {
        _db = db;
        _job = job;
        _donBan = donBan;
    }

    private string TenantId => _db.TenantInfo?.Id
        ?? throw new InvalidOperationException("Không xác định được cơ sở đang đăng nhập.");

    public async Task<IReadOnlyList<DonHangNhanEntity>> LayTatCaAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        return await _db.DonHangNhans.AsNoTracking()
            .Include(d => d.Dong).ThenInclude(l => l.PhanBo)
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.NgayGiao).ThenByDescending(d => d.LanDongBoUtc)
            .ToListAsync(ct);
    }

    public async Task<KetQuaDongBoDon> DongBoNgayAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        if (!await _db.TenantHnCCredentials.AnyAsync(c => c.TenantId == tenantId && c.BatDongBo, ct))
            return KetQuaDongBoDon.Loi("Đồng bộ HanoiCheck đang tắt - bật ở màn Cài đặt kết nối.");

        var kq = await _job.DongBoMotCoSoAsync(tenantId, ct);
        // Kéo về xong thì chuyển ngay thành Đơn hàng bán (người dùng đang đăng nhập -> đã có ngữ cảnh cơ sở).
        if (kq.ThanhCong) await _donBan.DongBoVaoDonHangBanAsync(ct);
        return kq;
    }
}
