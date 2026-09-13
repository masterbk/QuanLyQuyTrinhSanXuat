using System.Text.Json;
using Finbuckle.MultiTenant.Abstractions;
using HCP.Domain.Entities.Business;
using HCP.Domain.Entities.Common;
using HCP.Domain.Entities.Infrastructure;
using HCP.Domain.Enums;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Sync;

/// <inheritdoc cref="ISyncOutboxWriter"/>
public sealed class SyncOutboxWriter : ISyncOutboxWriter
{
    private readonly AppDbContext _db;
    private readonly IMultiTenantContextAccessor _tenantAccessor;
    private readonly ISecretProtector? _protector;

    public SyncOutboxWriter(AppDbContext db, IMultiTenantContextAccessor tenantAccessor,
                            ISecretProtector? protector = null)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _protector = protector;
    }

    private string TenantIdBatBuoc()
    {
        var tenantId = _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            // Enqueue phải nằm trong ngữ cảnh cơ sở đã đăng nhập. Thiếu là lỗi lập trình,
            // không được âm thầm ghi bản ghi thiếu TenantId (job sẽ không biết gửi bằng credential nào).
            throw new InvalidOperationException(
                "Không xác định được cơ sở khi ghi hàng đợi đồng bộ (thiếu tenant context).");
        }
        return tenantId;
    }

    public async Task<bool> DangBatAsync(CancellationToken ct = default)
    {
        var tenantId = _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId)) return false;
        // Không cache: công tắc có thể vừa đổi trong cùng phiên làm việc.
        return await _db.TenantHnCCredentials.AsNoTracking()
            .AnyAsync(c => c.TenantId == tenantId && c.BatDongBo, ct);
    }

    public async Task ThemAsync(string entityType, string entityKey, object payload, CancellationToken ct = default)
    {
        var tenantId = TenantIdBatBuoc();

        // Công tắc tổng tắt: cơ sở dùng phần mềm như hệ thống quản lý nội bộ, không gửi HanoiCheck.
        if (!await DangBatAsync(ct)) return;

        _db.SyncOutboxItems.Add(new SyncOutboxItem
        {
            TenantId = tenantId,
            EntityType = entityType,
            EntityKey = entityKey,
            PayloadJson = JsonSerializer.Serialize(payload, HnCPayloadMapper.Json),
            Status = SyncOutboxStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> GuiAsync(object banGhi, CancellationToken ct = default)
    {
        TenantIdBatBuoc();
        if (banGhi is not ICoDongBoHnC coCo || MoTa(banGhi) is not { } mo) return null;
        if (!await DangBatAsync(ct)) return null;

        if (!coCo.DongBoHnC)
        {
            // Bản ghi nội bộ: không gửi, và gỡ những lần gửi đang chờ (vd vừa bỏ tick).
            await GoHangDoiAsync(mo.Loai, mo.Khoa, ct);
            return null;
        }

        var tuBat = new List<string>();
        await BaoDamPhuThuocAsync(banGhi, tuBat, ct);
        await ThemAsync(mo.Loai, mo.Khoa, mo.Payload, ct);

        return tuBat.Count == 0
            ? null
            : "Đã tự bật đồng bộ HanoiCheck kèm theo: " + string.Join(", ", tuBat) + ".";
    }

    /// <summary>Loại endpoint, khoá nghiệp vụ và payload của một bản ghi; null nếu loại này không gửi HanoiCheck.</summary>
    private static (string Loai, string Khoa, object Payload)? MoTa(object e) => e switch
    {
        Warehouse w => ("Warehouse", w.MaKho, HnCPayloadMapper.Kho(w)),
        Facility f => ("Facility", f.MaCoSo, HnCPayloadMapper.CoSo(f)),
        ProductionStep s => ("ProductionStep", s.MaKhau, HnCPayloadMapper.Khau(s)),
        ProductionProcess p => ("ProductionProcess", p.MaQuyTrinh, HnCPayloadMapper.QuyTrinh(p)),
        SubSupplier n => ("SubSupplier", n.MaNccDauVao, HnCPayloadMapper.NccDauVao(n)),
        Staff s => ("Staff", s.MaNhanSu, HnCPayloadMapper.NhanSu(s)),
        // Chỉ THÀNH PHẨM gửi HanoiCheck; nguyên liệu quản lý nội bộ.
        Product p when p.LoaiSanPham == LoaiSanPham.ThanhPham => ("Product", p.MaSanPham, HnCPayloadMapper.ThucPham(p)),
        Batch b => ("Batch", b.MaLo, HnCPayloadMapper.LoSanXuat(b)),
        Dish d => ("Dish", d.MaMonAn, HnCPayloadMapper.MonAn(d)),
        _ => null
    };

    /// <summary>Tự bật + gửi các bản ghi mà <paramref name="e"/> tham chiếu nhưng đang không đồng bộ.</summary>
    private async Task BaoDamPhuThuocAsync(object e, List<string> tuBat, CancellationToken ct)
    {
        switch (e)
        {
            case ProductionProcess p:
                foreach (var ma in p.DanhSachKhau.Select(k => k.MaKhau).Distinct())
                    await BatKhauAsync(ma, tuBat, ct);
                break;

            case Product p when !string.IsNullOrWhiteSpace(p.MaQuyTrinh):
                await BatQuyTrinhAsync(p.MaQuyTrinh!, tuBat, ct);
                break;

            case Batch b:
                await BatSanPhamAsync(b.MaSanPham, tuBat, ct);
                foreach (var k in b.DanhSachKho) await BatKhoAsync(k.MaKho, tuBat, ct);
                await BatNccAsync(b.MaNccDauVao, tuBat, ct);
                foreach (var s in b.DanhSachKhau)
                {
                    await BatKhauAsync(s.MaKhau, tuBat, ct);
                    await BatCoSoAsync(s.MaCoSo, tuBat, ct);
                    await BatNccAsync(s.MaNccDauVao, tuBat, ct);
                    foreach (var ns in s.NguoiThucHien) await BatNhanSuAsync(ns, tuBat, ct);
                }
                break;

            case Dish d:
                foreach (var nl in d.DanhSachNguyenLieu) await BatSanPhamAsync(nl.MaNguyenLieu, tuBat, ct);
                if (!string.IsNullOrWhiteSpace(d.MaQuyTrinh)) await BatQuyTrinhAsync(d.MaQuyTrinh!, tuBat, ct);
                foreach (var s in d.DanhSachKhau)
                {
                    await BatKhauAsync(s.MaKhau, tuBat, ct);
                    await BatCoSoAsync(s.MaCoSo, tuBat, ct);
                    await BatNccAsync(s.MaNccDauVao, tuBat, ct);
                    foreach (var ns in s.NguoiThucHien) await BatNhanSuAsync(ns, tuBat, ct);
                }
                break;
        }
    }

    private Task BatKhauAsync(string? ma, List<string> tuBat, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(ma) ? Task.CompletedTask
            : BatAsync(_db.ProductionSteps.Where(x => x.MaKhau == ma), $"khâu {ma}", tuBat, ct);

    private Task BatKhoAsync(string? ma, List<string> tuBat, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(ma) ? Task.CompletedTask
            : BatAsync(_db.Warehouses.Where(x => x.MaKho == ma), $"kho {ma}", tuBat, ct);

    private Task BatCoSoAsync(string? ma, List<string> tuBat, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(ma) ? Task.CompletedTask
            : BatAsync(_db.Facilities.Where(x => x.MaCoSo == ma), $"cơ sở {ma}", tuBat, ct);

    private Task BatNccAsync(string? ma, List<string> tuBat, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(ma) ? Task.CompletedTask
            : BatAsync(_db.SubSuppliers.Include(x => x.NhomThucPham).Where(x => x.MaNccDauVao == ma), $"NCC {ma}", tuBat, ct);

    private Task BatNhanSuAsync(string? ma, List<string> tuBat, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(ma) ? Task.CompletedTask
            : BatAsync(_db.Staff.Where(x => x.MaNhanSu == ma), $"nhân sự {ma}", tuBat, ct);

    private Task BatSanPhamAsync(string? ma, List<string> tuBat, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(ma) ? Task.CompletedTask
            : BatAsync(_db.Products.Where(x => x.MaSanPham == ma && x.LoaiSanPham == LoaiSanPham.ThanhPham),
                       $"thực phẩm {ma}", tuBat, ct);

    private Task BatQuyTrinhAsync(string ma, List<string> tuBat, CancellationToken ct) =>
        BatAsync(_db.ProductionProcesses.Include(x => x.DanhSachKhau).Where(x => x.MaQuyTrinh == ma),
                 $"quy trình {ma}", tuBat, ct);

    private async Task BatAsync<T>(IQueryable<T> truyVan, string moTa, List<string> tuBat, CancellationToken ct)
        where T : class, ICoDongBoHnC
    {
        var e = await truyVan.FirstOrDefaultAsync(ct);
        // Không có (kiểm tra dữ liệu ở service lo) hoặc đã đồng bộ sẵn thì thôi.
        if (e is null || e.DongBoHnC) return;

        e.DongBoHnC = true;
        await _db.SaveChangesAsync(ct);

        // Nhân sự nạp từ DB chỉ có CCCD đã mã hoá - giải mã để payload đủ trường.
        if (e is Staff s && s.Cccd is null && _protector is not null && !string.IsNullOrEmpty(s.CccdEncrypted))
            s.Cccd = _protector.TryUnprotect(s.CccdEncrypted);

        await BaoDamPhuThuocAsync(e, tuBat, ct);
        if (MoTa(e) is { } mo) await ThemAsync(mo.Loai, mo.Khoa, mo.Payload, ct);
        tuBat.Add(moTa);
    }

    private async Task GoHangDoiAsync(string loai, string khoa, CancellationToken ct)
    {
        var tenantId = TenantIdBatBuoc();
        var dangCho = await _db.SyncOutboxItems
            .Where(o => o.TenantId == tenantId && o.EntityType == loai && o.EntityKey == khoa
                        && o.Status != SyncOutboxStatus.Success && o.Status != SyncOutboxStatus.Processing)
            .ToListAsync(ct);
        if (dangCho.Count == 0) return;

        _db.SyncOutboxItems.RemoveRange(dangCho);
        await _db.SaveChangesAsync(ct);
    }
}
