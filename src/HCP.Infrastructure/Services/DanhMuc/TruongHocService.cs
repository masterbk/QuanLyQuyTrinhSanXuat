using Finbuckle.MultiTenant.Abstractions;
using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>Danh sách trường học cơ sở đang đăng nhập được HanoiCheck phân công phục vụ.</summary>
public interface ITruongHocService
{
    Task<IReadOnlyList<TruongHocHnC>> LayTatCaAsync(CancellationToken ct = default);

    /// <summary>Tải lại từ GET /supplier/schools và thay toàn bộ danh sách đang lưu của cơ sở.</summary>
    Task<KetQuaThaoTac> CapNhatTuHnCAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="ITruongHocService"/>
public class TruongHocService : ITruongHocService
{
    private readonly AppDbContext _db;
    private readonly IHanoiCheckTruongHocClient _client;
    private readonly IMultiTenantContextAccessor _tenantAccessor;

    public TruongHocService(AppDbContext db, IHanoiCheckTruongHocClient client, IMultiTenantContextAccessor tenantAccessor)
    {
        _db = db;
        _client = client;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<IReadOnlyList<TruongHocHnC>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.TruongHocHnCs.AsNoTracking().OrderBy(t => t.Ten).ToListAsync(ct);

    public async Task<KetQuaThaoTac> CapNhatTuHnCAsync(CancellationToken ct = default)
    {
        var tenantId = _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId)) return KetQuaThaoTac.Loi("Không xác định được cơ sở đang đăng nhập.");

        var kq = await _client.LayTruongAsync(tenantId, ct);
        if (!kq.ThanhCong) return KetQuaThaoTac.Loi(kq.ThongBao ?? "Không lấy được danh sách trường.");

        var dsMoi = kq.Items.Where(i => i.Id is > 0).GroupBy(i => i.Id!.Value).Select(g => g.First()).ToList();

        // Đơn vị chưa được phân công trường nào thì HnC trả mảng rỗng - đây là dữ liệu thật (khác danh mục chung),
        // nên vẫn ghi đè thành rỗng để không giữ trường đã bị thu hồi.
        var hienCo = await _db.TruongHocHnCs.ToDictionaryAsync(t => t.TruongId, ct);
        var now = DateTime.UtcNow;
        foreach (var moi in dsMoi)
        {
            var ten = (moi.Name ?? "").Trim();
            if (hienCo.Remove(moi.Id!.Value, out var cu)) { cu.Ten = ten; cu.CapNhatLucUtc = now; }
            else _db.TruongHocHnCs.Add(new TruongHocHnC { TruongId = moi.Id!.Value, Ten = ten, CapNhatLucUtc = now });
        }
        _db.TruongHocHnCs.RemoveRange(hienCo.Values);
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok(dsMoi.Count == 0
            ? "HanoiCheck chưa phân công trường nào cho cơ sở - thực đơn chỉ dùng chung được."
            : $"Đã cập nhật {dsMoi.Count} trường được phân công.");
    }
}
