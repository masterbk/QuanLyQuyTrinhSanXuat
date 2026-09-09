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
            var laMoi = don is null;
            var trangThaiCu = don?.TrangThai;   // bắt trước khi ghi đè để phát hiện đổi trạng thái
            if (don is null)
            {
                don = new DonHangNhan { TenantId = tenantId, MaDonHang = item.Code };
                _db.DonHangNhans.Add(don);
            }
            else
            {
                don.UpdatedAtUtc = now;
            }

            don.TenTruong = item.School?.Name;
            don.TrangThai = item.Status;
            don.NgayGiao = DateOnly.TryParse(item.OrderDate, out var ngay) ? ngay : null;
            don.LanDongBoUtc = now;

            // Kéo chi tiết khi: đơn mới, chưa lấy chi tiết, trạng thái vừa đổi, hoặc còn đang biến động.
            var canChiTiet = laMoi || !don.DaLayChiTiet
                             || trangThaiCu != item.Status || !LaTrangThaiCuoi(item.Status);
            var xongChiTiet = false;
            if (canChiTiet)
            {
                var ct2 = await _client.LayChiTietAsync(tenantId, item.Code, ct);
                if (ct2.ThanhCong && ct2.Detail is { } detail)
                {
                    _db.DonHangNhanDongs.RemoveRange(don.Dong); // cascade xoá cả phân bổ cũ
                    don.Dong = (detail.Items ?? new List<OrderDetailItem>())
                        .Where(i => !string.IsNullOrWhiteSpace(i.Code))
                        .Select(i => new DonHangNhanDong
                        {
                            TenantId = tenantId,
                            MaSanPham = i.Code!,
                            TenSanPham = i.Name,
                            SoLuong = i.SoLuongCuoi,
                            MaTruyVet = i.TraceCode,
                            MaThucDon = i.MenuCode,
                            PhanBo = (i.Allocations ?? new List<OrderAllocation>())
                                .Select(a => new DonHangNhanPhanBo
                                {
                                    TenantId = tenantId,
                                    MaThucPhamNcc = a.SupplierFoodCode,
                                    MaLo = a.MaLo,
                                    MaKho = a.MaKho,
                                    SoLuong = a.SoLuongCuoi
                                }).ToList()
                        }).ToList();
                    don.MaNguoiGiao = LayChuoi(detail.Extra, "transporter_code", "deliverer_code", "ma_nguoi_giao");
                    don.DiaChiGiao = LayChuoi(detail.Extra, "delivery_address", "address", "dia_chi", "diem_giao");
                    don.DaLayChiTiet = true;
                    xongChiTiet = true;
                }
            }

            // Không lấy được chi tiết: với đơn mới, tạm dùng products[] của danh sách làm dòng.
            if (!xongChiTiet && laMoi)
            {
                don.Dong = (item.Products ?? new List<ProductInfo>())
                    .Where(p => !string.IsNullOrWhiteSpace(p.Code))
                    .Select(p => new DonHangNhanDong { TenantId = tenantId, MaSanPham = p.Code!, TenSanPham = p.Name })
                    .ToList();
            }
            soDon++;
        }

        await _db.SaveChangesAsync(ct);
        return KetQuaDongBoDon.Ok(soDon);
    }

    /// <summary>Trạng thái đã "chốt", không cần kéo lại chi tiết mỗi lần.</summary>
    private static bool LaTrangThaiCuoi(string? status) => status is
        "DA_GIAO" or "GIAO_HANG_THANH_CONG" or "HUY" or "TU_CHOI" or "TRA_HANG";

    /// <summary>Lấy giá trị chuỗi đầu tiên tìm được trong Extra theo danh sách khoá ứng viên (dò tên trường chưa chắc).</summary>
    private static string? LayChuoi(Dictionary<string, System.Text.Json.JsonElement>? extra, params string[] keys)
    {
        if (extra is null) return null;
        foreach (var k in keys)
            if (extra.TryGetValue(k, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String)
                return v.GetString();
        return null;
    }
}
