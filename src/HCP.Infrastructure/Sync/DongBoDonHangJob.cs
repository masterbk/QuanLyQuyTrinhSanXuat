using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.Sync;

/// <inheritdoc cref="IDongBoDonHangJob"/>
public sealed class DongBoDonHangJob : IDongBoDonHangJob
{
    /// <summary>Cửa sổ kéo về: chỉ lấy đơn có ngày giao trong khoảng gần đây (bao đủ, gọn tải).</summary>
    private static readonly int SoNgayCuaSo = 60;

    private readonly AppDbContext _db;
    private readonly IHanoiCheckOrderQueryClient _client;
    private readonly TimeProvider _clock;
    private readonly ILogger<DongBoDonHangJob> _logger;

    public DongBoDonHangJob(AppDbContext db,
                            IHanoiCheckOrderQueryClient client,
                            TimeProvider clock,
                            ILogger<DongBoDonHangJob> logger)
    {
        _db = db;
        _client = client;
        _clock = clock;
        _logger = logger;
    }

    public async Task DongBoTatCaAsync(CancellationToken ct = default)
    {
        // TenantHnCCredentials là bảng hạ tầng (không lọc theo tenant) -> lấy được mọi cơ sở.
        var tenantIds = await _db.TenantHnCCredentials.Select(c => c.TenantId).ToListAsync(ct);
        foreach (var tenantId in tenantIds)
        {
            if (ct.IsCancellationRequested) break;
            var kq = await DongBoMotCoSoAsync(tenantId, ct);
            if (!kq.ThanhCong && !kq.ChuaCauHinh)
                _logger.LogWarning("Đồng bộ đơn hàng cơ sở {TenantId} lỗi: {ThongBao}", tenantId, kq.ThongBao);
        }
    }

    public async Task<KetQuaDongBoDon> DongBoMotCoSoAsync(string tenantId, CancellationToken ct = default)
    {
        var homNay = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var filter = new OrderQueryFilter { OrderDateFrom = homNay.AddDays(-SoNgayCuaSo) };

        var kq = await _client.LayDanhSachAsync(tenantId, filter, ct);
        if (kq.ChuaCauHinh) return KetQuaDongBoDon.ChuaKetNoi();
        if (!kq.ThanhCong) return KetQuaDongBoDon.Loi(kq.ThongBao ?? "Không tra cứu được đơn hàng.");

        var now = _clock.GetUtcNow().UtcDateTime;
        var maList = kq.Items.Where(i => !string.IsNullOrWhiteSpace(i.Code)).Select(i => i.Code!).ToList();

        // Nạp sẵn các đơn đã có của cơ sở này (theo mã) để upsert.
        var daCo = await _db.DonHangNhans
            .Include(d => d.Dong)
            .Where(d => d.TenantId == tenantId && maList.Contains(d.MaDonHang))
            .ToListAsync(ct);
        var theoMa = daCo.GroupBy(d => d.MaDonHang).ToDictionary(g => g.Key, g => g.First());

        var soDon = 0;
        foreach (var item in kq.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Code)) continue;

            var don = theoMa.GetValueOrDefault(item.Code);
            if (don is null)
            {
                don = new DonHangNhan { TenantId = tenantId, MaDonHang = item.Code };
                _db.DonHangNhans.Add(don);
            }
            else
            {
                // Thay toàn bộ dòng cũ để phản ánh đúng dữ liệu mới nhất.
                _db.DonHangNhanDongs.RemoveRange(don.Dong);
                don.Dong.Clear();
                don.UpdatedAtUtc = now;
            }

            don.TenTruong = item.School?.Name;
            don.TrangThai = item.Status;
            don.NgayGiao = DateOnly.TryParse(item.OrderDate, out var ngay) ? ngay : null;
            don.LanDongBoUtc = now;
            don.Dong = (item.Products ?? new List<ProductInfo>())
                .Where(p => !string.IsNullOrWhiteSpace(p.Code))
                .Select(p => new DonHangNhanDong { TenantId = tenantId, MaSanPham = p.Code!, TenSanPham = p.Name })
                .ToList();
            soDon++;
        }

        await _db.SaveChangesAsync(ct);
        return KetQuaDongBoDon.Ok(soDon);
    }
}
