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

            // Danh sách là nguồn chuẩn cho phần đầu đơn: ghi đè cả giá trị null (vd bỏ phân công người giao).
            ApDungPhanDau(don, item, ghiDeKhiNull: true);
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
                            SoLuong = i.RequestedAmount,
                            DonViTinh = i.Unit,
                            FileUrl = i.FileUrl,
                            MaTruyVet = i.TraceCode,
                            MaThucDon = i.MenuCode,
                            PhanBo = (i.Allocations ?? new List<OrderAllocation>())
                                .Select(a => new DonHangNhanPhanBo
                                {
                                    TenantId = tenantId,
                                    MaPhieuXuat = a.StockOutCode,
                                    MaThucPhamNcc = a.SupplierFoodCode,
                                    MaLo = a.Batch?.Code,
                                    TenLo = a.Batch?.Name,
                                    MaKho = a.Warehouse?.Code,
                                    TenKho = a.Warehouse?.Name,
                                    SoLuong = a.Amount
                                }).ToList()
                        }).ToList();
                    // Chi tiết chỉ bổ sung, không xoá thông tin danh sách đã có nếu chi tiết thiếu trường.
                    ApDungPhanDau(don, detail, ghiDeKhiNull: false);
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

            // Tên sản phẩm: products[] của danh sách luôn có name - bù cho dòng chưa có tên (kể cả
            // đơn cũ đã chốt không kéo lại chi tiết).
            var tenTheoMa = (item.Products ?? new List<ProductInfo>())
                .Where(p => !string.IsNullOrWhiteSpace(p.Code) && !string.IsNullOrWhiteSpace(p.Name))
                .GroupBy(p => p.Code!).ToDictionary(g => g.Key, g => g.First().Name);
            foreach (var dong in don.Dong.Where(d => string.IsNullOrWhiteSpace(d.TenSanPham)))
                dong.TenSanPham = tenTheoMa.GetValueOrDefault(dong.MaSanPham);
            soDon++;
        }

        await _db.SaveChangesAsync(ct);
        return KetQuaDongBoDon.Ok(soDon);
    }

    /// <summary>Trạng thái đã "chốt", không cần kéo lại chi tiết mỗi lần.</summary>
    private static bool LaTrangThaiCuoi(string? status) => status is
        "DA_GIAO" or "GIAO_HANG_THANH_CONG" or "HUY" or "TU_CHOI" or "TRA_HANG";

    /// <summary>
    /// Chép phần đầu đơn (trường, trạng thái, người giao, kho, điểm trường...) vào entity.
    /// ghiDeKhiNull=false: chỉ ghi các giá trị có mặt - dùng khi bổ sung từ chi tiết đơn.
    /// </summary>
    private static void ApDungPhanDau(DonHangNhan don, OrderHeader h, bool ghiDeKhiNull)
    {
        void Gan(string? giaTri, Action<string?> gan)
        {
            var v = string.IsNullOrWhiteSpace(giaTri) ? null : giaTri.Trim();
            if (v is not null || ghiDeKhiNull) gan(v);
        }

        Gan(h.School?.Name, v => don.TenTruong = v);
        Gan(h.Status, v => don.TrangThai = v);
        if (DateOnly.TryParse(h.OrderDate, out var ngay)) don.NgayGiao = ngay;
        else if (ghiDeKhiNull) don.NgayGiao = null;

        if (h.Transporter is not null || ghiDeKhiNull)
        {
            var t = h.Transporter;
            don.MaNguoiGiao = t?.Code;
            don.TenNguoiGiao = t?.Name;
            don.SdtNguoiGiao = t?.Phone;
            don.PhuongTienGiao = t?.TransportMean;
            don.BienSoXe = t?.LicensePlate;
        }

        var kho = (h.Warehouses ?? new List<WarehouseInfo>())
            .Where(k => !string.IsNullOrWhiteSpace(k.Code) || !string.IsNullOrWhiteSpace(k.Name))
            .Select(k => string.IsNullOrWhiteSpace(k.Name) ? k.Code : $"{k.Code} - {k.Name}")
            .ToList();
        if (kho.Count > 0 || ghiDeKhiNull) don.KhoXuat = kho.Count == 0 ? null : string.Join("; ", kho);

        Gan(h.DeliveryAddress, v => don.DiaChiGiao = v);
        Gan(h.SchoolPoint, v => don.DiemTruong = v);
        Gan(h.ProductTypeLabel ?? h.ProductType, v => don.LoaiDon = v);
        Gan(h.Note, v => don.GhiChu = v);
        Gan(h.TraceabilityUrl, v => don.LinkTruyXuat = v);

        if (DateTime.TryParseExact(h.CreatedAt, "yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var tao))
            don.NgayTaoTrenHnC = tao;
        else if (ghiDeKhiNull) don.NgayTaoTrenHnC = null;
    }
}
