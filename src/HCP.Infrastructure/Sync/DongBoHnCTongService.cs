using HCP.Domain.Entities.Common;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using HCP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Sync;

/// <summary>
/// Công tắc tổng đồng bộ HanoiCheck của cơ sở đang đăng nhập.
/// Tắt: phần mềm chạy như hệ thống quản lý nội bộ (không gửi, không kéo đơn, ẩn phần HanoiCheck) và gỡ các bản ghi
/// chưa gửi khỏi hàng đợi. Bật: chỉ gửi thay đổi từ lúc bật; muốn gửi dữ liệu đã có thì bấm "Đẩy toàn bộ danh mục".
/// </summary>
public interface IDongBoHnCTongService
{
    Task<bool> DangBatAsync(CancellationToken ct = default);

    Task<KetQuaThaoTac> BatTatAsync(bool bat, CancellationToken ct = default);

    /// <summary>Xếp hàng gửi mọi bản ghi đang tick đồng bộ, theo thứ tự phụ thuộc của HanoiCheck.</summary>
    Task<KetQuaThaoTac> DayToanBoAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IDongBoHnCTongService"/>
public sealed class DongBoHnCTongService : IDongBoHnCTongService
{
    private readonly AppDbContext _db;
    private readonly ISyncOutboxWriter _outbox;
    private readonly ISecretProtector? _protector;

    public DongBoHnCTongService(AppDbContext db, ISyncOutboxWriter outbox, ISecretProtector? protector = null)
    {
        _db = db;
        _outbox = outbox;
        _protector = protector;
    }

    public Task<bool> DangBatAsync(CancellationToken ct = default) => _outbox.DangBatAsync(ct);

    public async Task<KetQuaThaoTac> BatTatAsync(bool bat, CancellationToken ct = default)
    {
        var tenantId = _db.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId)) return KetQuaThaoTac.Loi("Không xác định được cơ sở đang đăng nhập.");

        var cauHinh = await _db.TenantHnCCredentials.FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        if (cauHinh is null)
            return KetQuaThaoTac.Loi("Cần lưu cấu hình kết nối HanoiCheck trước khi bật đồng bộ.");
        if (cauHinh.BatDongBo == bat)
            return KetQuaThaoTac.Ok(bat ? "Đồng bộ HanoiCheck đang bật." : "Đồng bộ HanoiCheck đang tắt.");

        cauHinh.BatDongBo = bat;
        var soGo = 0;
        if (!bat)
        {
            // Bản ghi đã gửi thành công giữ lại làm lịch sử; phần chưa gửi thì bỏ (bật lại sẽ gửi dữ liệu mới nhất).
            var chuaGui = await _db.SyncOutboxItems
                .Where(o => o.TenantId == tenantId
                            && o.Status != SyncOutboxStatus.Success && o.Status != SyncOutboxStatus.Processing)
                .ToListAsync(ct);
            soGo = chuaGui.Count;
            _db.SyncOutboxItems.RemoveRange(chuaGui);
        }
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok(bat
            ? "Đã bật đồng bộ HanoiCheck. Từ giờ các thay đổi sẽ được gửi; bấm \"Đẩy toàn bộ danh mục\" nếu cần gửi dữ liệu đã có."
            : $"Đã tắt đồng bộ HanoiCheck, gỡ {soGo} bản ghi chưa gửi khỏi hàng đợi.");
    }

    public async Task<KetQuaThaoTac> DayToanBoAsync(CancellationToken ct = default)
    {
        if (!await DangBatAsync(ct))
            return KetQuaThaoTac.Loi("Đồng bộ HanoiCheck đang tắt - bật lên trước khi đẩy danh mục.");

        var soBanGhi = 0;
        async Task GuiAsync<T>(IEnumerable<T> danhSach) where T : class, ICoDongBoHnC
        {
            foreach (var e in danhSach.Where(x => x.DongBoHnC))
            {
                await _outbox.GuiAsync(e, ct);
                soBanGhi++;
            }
        }

        // Thứ tự theo phụ thuộc của HanoiCheck: kho/cơ sở/khâu → quy trình → NCC/nhân sự → thực phẩm → món ăn → lô.
        await GuiAsync(await _db.Warehouses.ToListAsync(ct));
        await GuiAsync(await _db.Facilities.ToListAsync(ct));
        await GuiAsync(await _db.ProductionSteps.ToListAsync(ct));
        await GuiAsync(await _db.ProductionProcesses.Include(p => p.DanhSachKhau).ToListAsync(ct));
        await GuiAsync(await _db.SubSuppliers.Include(s => s.NhomThucPham).ToListAsync(ct));

        var nhanSu = await _db.Staff.ToListAsync(ct);
        foreach (var s in nhanSu.Where(s => s.Cccd is null && !string.IsNullOrEmpty(s.CccdEncrypted)))
            s.Cccd = _protector?.TryUnprotect(s.CccdEncrypted);
        await GuiAsync(nhanSu);

        await GuiAsync(await _db.Products.Where(p => p.LoaiSanPham == LoaiSanPham.ThanhPham).ToListAsync(ct));
        await GuiAsync(await _db.Dishes.Include(d => d.DanhSachNguyenLieu).Include(d => d.DanhSachKhau)
            .Include(d => d.DanhSachFile).ToListAsync(ct));
        await GuiAsync(await _db.Batches.Include(b => b.DanhSachKho).Include(b => b.DanhSachKhau)
            .Include(b => b.DanhSachFile).ToListAsync(ct));

        return KetQuaThaoTac.Ok($"Đã đưa {soBanGhi} bản ghi vào hàng đợi đồng bộ HanoiCheck.");
    }
}
