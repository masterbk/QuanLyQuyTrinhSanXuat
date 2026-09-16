using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.MaTuSinh;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.BanHang;

/// <summary>Một lô có thể xuất cho một dòng đơn: tồn hiện tại của lô và số lượng hệ thống gợi ý (FEFO).</summary>
public sealed record LoCoTheXuatDto(string MaLo, DateOnly? HanSuDung, decimal Ton, decimal GoiY);

/// <summary>Bảng phân bổ lô cho một dòng đơn khi xuất kho.</summary>
public sealed record DongXuatKhoDto(int DongId, string MaThanhPham, string TenThanhPham, string? DonViTinh,
                                    decimal SoLuong, IReadOnlyList<LoCoTheXuatDto> Lo);

/// <summary>Người dùng chốt: lấy bao nhiêu từ lô nào cho dòng nào.</summary>
public sealed record PhanBoLoRequest(int DongId, string MaLo, decimal SoLuong);

/// <summary>Một ảnh tổng quan đã tải lên (tên gốc + đường dẫn công khai) gắn cho đơn lúc xuất kho.</summary>
public sealed record AnhDauVao(string TenAnh, string DuongDan);

/// <summary>
/// Đơn hàng bán: lập đơn → xác nhận → xuất kho (trừ tồn theo lô, gợi ý FEFO, cho sửa) → đã giao; huỷ khi
/// đang giao thì trả hàng về đúng lô bằng bút toán đảo.
/// </summary>
public interface IDonHangBanService
{
    Task<IReadOnlyList<DonHangBan>> LayTatCaAsync(CancellationToken ct = default);
    Task<DonHangBan?> LayTheoIdAsync(int id, CancellationToken ct = default);

    Task<KetQuaThaoTac> TaoAsync(DonHangBan don, CancellationToken ct = default);

    /// <summary>Sửa đơn chưa xuất kho (thay toàn bộ dòng hàng). Mã đơn không đổi.</summary>
    Task<KetQuaThaoTac> CapNhatAsync(DonHangBan don, CancellationToken ct = default);

    Task<KetQuaThaoTac> XacNhanAsync(int id, CancellationToken ct = default);

    /// <summary>Các lô còn tồn cho từng dòng, kèm số lượng gợi ý theo FEFO (cộng dồn các dòng cùng thành phẩm).</summary>
    Task<IReadOnlyList<DongXuatKhoDto>> GoiYXuatKhoAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Xuất kho: trừ tồn theo phân bổ lô rồi chuyển "Đang giao". <paramref name="phanBo"/> rỗng = dùng gợi ý FEFO.
    /// Với đơn nguồn HanoiCheck: trước khi trừ tồn, gọi đẩy ngược "process" (người giao/ghi chú/ảnh/nguồn hàng)
    /// rồi "status" (Đang giao) sang HnC - lỗi thật thì KHÔNG trừ tồn/đổi trạng thái nội bộ để 2 bên luôn khớp.
    /// </summary>
    Task<KetQuaThaoTac> XuatKhoAsync(int id, IReadOnlyList<PhanBoLoRequest>? phanBo, string? maNguoiGiao,
                                     string? ghiChu = null, IReadOnlyList<AnhDauVao>? anhTongQuan = null,
                                     CancellationToken ct = default);

    Task<KetQuaThaoTac> HoanTatGiaoAsync(int id, CancellationToken ct = default);

    Task<KetQuaThaoTac> HuyAsync(int id, string? lyDo, CancellationToken ct = default);

    /// <summary>Xoá đơn chưa từng xuất kho.</summary>
    Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default);
}

/// <inheritdoc cref="IDonHangBanService"/>
public sealed class DonHangBanService : IDonHangBanService
{
    private readonly AppDbContext _db;
    private readonly IMaTuSinhService _maTuSinh;
    private readonly IHanoiCheckOrderCommandClient _orderCommandClient;

    public DonHangBanService(AppDbContext db, IMaTuSinhService maTuSinh, IHanoiCheckOrderCommandClient orderCommandClient)
    {
        _db = db;
        _maTuSinh = maTuSinh;
        _orderCommandClient = orderCommandClient;
    }

    private IQueryable<DonHangBan> QueryDayDu() =>
        _db.DonHangBans.Include(d => d.Dong).ThenInclude(l => l.XuatLo).Include(d => d.AnhTongQuan);

    public async Task<IReadOnlyList<DonHangBan>> LayTatCaAsync(CancellationToken ct = default) =>
        await QueryDayDu().AsNoTracking()
            .OrderByDescending(d => d.NgayDat).ThenByDescending(d => d.Id)
            .ToListAsync(ct);

    public Task<DonHangBan?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        QueryDayDu().FirstOrDefaultAsync(d => d.Id == id, ct);

    // ==================== Lập / sửa ====================

    public async Task<KetQuaThaoTac> TaoAsync(DonHangBan don, CancellationToken ct = default)
    {
        if (don.NgayDat == default) don.NgayDat = MaTuSinhService.HomNay;
        var loi = await KiemTraAsync(don, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        // Sinh mã SAU khi hợp lệ (không phí số), TRƯỚC khi Add (hàm sinh tự SaveChanges bộ đếm).
        don.MaDonHang = await _maTuSinh.SinhAsync(LoaiMaTuSinh.DonHangBan, don.NgayDat, ct);
        don.TrangThai = TrangThaiDonHangBan.ChoXacNhan;
        don.ThoiGianXuatKhoUtc = don.ThoiGianGiaoUtc = don.ThoiGianHuyUtc = null;
        foreach (var l in don.Dong) l.XuatLo.Clear();

        _db.DonHangBans.Add(don);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã tạo đơn hàng \"{don.MaDonHang}\" - tổng {don.TongTien:#,0} đ.");
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(DonHangBan don, CancellationToken ct = default)
    {
        var goc = await QueryDayDu().FirstOrDefaultAsync(d => d.Id == don.Id, ct);
        if (goc is null) return KetQuaThaoTac.Loi("Không tìm thấy đơn hàng.");
        if (goc.ThoiGianXuatKhoUtc is not null || goc.TrangThai is not (TrangThaiDonHangBan.ChoXacNhan or TrangThaiDonHangBan.DaXacNhan))
            return KetQuaThaoTac.Loi("Chỉ sửa được đơn chưa xuất kho.");

        if (don.NgayDat == default) don.NgayDat = goc.NgayDat;
        var loi = await KiemTraAsync(don, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        goc.MaKhachHang = don.MaKhachHang;
        goc.MaKho = don.MaKho;
        goc.NgayDat = don.NgayDat;
        goc.NgayGiao = don.NgayGiao;
        goc.DiaChiGiao = don.DiaChiGiao;
        goc.MaNguoiGiao = don.MaNguoiGiao;
        goc.GhiChu = don.GhiChu;
        goc.HnCCoThayDoi = false;   // NCC đã xem lại đơn
        goc.UpdatedAtUtc = DateTime.UtcNow;

        _db.DonHangBanDongs.RemoveRange(goc.Dong);
        goc.Dong = don.Dong.Select(l => new DonHangBanDong
        {
            MaThanhPham = l.MaThanhPham, SoLuong = l.SoLuong, DonGia = l.DonGia, GhiChu = l.GhiChu
        }).ToList();

        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã cập nhật đơn hàng \"{goc.MaDonHang}\".");
    }

    /// <summary>Chuẩn hoá + kiểm tra phần đầu đơn và dòng hàng. Trả thông báo lỗi hoặc null.</summary>
    private async Task<string?> KiemTraAsync(DonHangBan don, CancellationToken ct)
    {
        don.MaKhachHang = don.MaKhachHang?.Trim() ?? "";
        don.MaKho = don.MaKho?.Trim() ?? "";
        don.DiaChiGiao = string.IsNullOrWhiteSpace(don.DiaChiGiao) ? null : don.DiaChiGiao.Trim();
        don.MaNguoiGiao = string.IsNullOrWhiteSpace(don.MaNguoiGiao) ? null : don.MaNguoiGiao.Trim();
        don.GhiChu = string.IsNullOrWhiteSpace(don.GhiChu) ? null : don.GhiChu.Trim();

        var khach = await _db.KhachHangs.AsNoTracking().FirstOrDefaultAsync(k => k.MaKhachHang == don.MaKhachHang, ct);
        if (khach is null) return "Vui lòng chọn khách hàng hợp lệ.";
        if (!await _db.Warehouses.AnyAsync(k => k.MaKho == don.MaKho, ct)) return "Vui lòng chọn kho hợp lệ.";
        if (don.NgayGiao is { } ngayGiao && ngayGiao < don.NgayDat) return "Ngày giao không được trước ngày đặt.";
        if (don.MaNguoiGiao is not null && !await _db.Staff.AnyAsync(s => s.MaNhanSu == don.MaNguoiGiao, ct))
            return $"Không có nhân sự \"{don.MaNguoiGiao}\".";
        don.DiaChiGiao ??= khach.DiaChi;

        don.Dong = (don.Dong ?? new()).Where(l => !string.IsNullOrWhiteSpace(l.MaThanhPham)).ToList();
        if (don.Dong.Count == 0) return "Đơn phải có ít nhất một dòng hàng.";

        var ma = don.Dong.Select(l => l.MaThanhPham.Trim()).Distinct().ToList();
        var sanPham = await _db.Products.AsNoTracking().Where(p => ma.Contains(p.MaSanPham))
            .ToDictionaryAsync(p => p.MaSanPham, ct);
        foreach (var l in don.Dong)
        {
            l.MaThanhPham = l.MaThanhPham.Trim();
            l.GhiChu = string.IsNullOrWhiteSpace(l.GhiChu) ? null : l.GhiChu.Trim();
            if (!sanPham.TryGetValue(l.MaThanhPham, out var sp) || sp.LoaiSanPham != LoaiSanPham.ThanhPham)
                return $"\"{l.MaThanhPham}\" không phải thành phẩm hợp lệ.";
            if (l.SoLuong <= 0) return $"Số lượng của \"{sp.TenSanPham}\" phải lớn hơn 0.";
            if (l.DonGia < 0) return $"Đơn giá của \"{sp.TenSanPham}\" không được âm.";
        }
        return null;
    }

    // ==================== Chuyển trạng thái ====================

    public async Task<KetQuaThaoTac> XacNhanAsync(int id, CancellationToken ct = default)
    {
        var don = await _db.DonHangBans.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (don is null) return KetQuaThaoTac.Loi("Không tìm thấy đơn hàng.");
        if (don.TrangThai != TrangThaiDonHangBan.ChoXacNhan)
            return KetQuaThaoTac.Loi("Chỉ xác nhận được đơn đang chờ xác nhận.");

        // Đơn nguồn HanoiCheck: đẩy ngược "Đang chuẩn bị" TRƯỚC khi lưu nội bộ, để 2 bên luôn khớp -
        // công tắc HnC tắt/chưa cấu hình thì bỏ qua (ChuaCauHinh), lỗi thật thì chặn xác nhận.
        if (don.Nguon == NguonDonHang.HanoiCheck && don.MaDonHnC is not null && _db.TenantInfo?.Id is { } tenantId)
        {
            var ket = await _orderCommandClient.DoiTrangThaiAsync(tenantId, don.MaDonHnC, "DANG_CHUAN_BI", null, ct);
            if (!ket.ThanhCong && !ket.ChuaCauHinh)
                return KetQuaThaoTac.Loi($"Không đẩy được trạng thái sang HanoiCheck: {ket.ThongBao}");
        }

        don.TrangThai = TrangThaiDonHangBan.DaXacNhan;
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã xác nhận đơn \"{don.MaDonHang}\".");
    }

    public async Task<IReadOnlyList<DongXuatKhoDto>> GoiYXuatKhoAsync(int id, CancellationToken ct = default)
    {
        var don = await QueryDayDu().AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (don is null) return Array.Empty<DongXuatKhoDto>();
        return await DungGoiYAsync(don, ct);
    }

    private async Task<List<DongXuatKhoDto>> DungGoiYAsync(DonHangBan don, CancellationToken ct)
    {
        var ma = don.Dong.Select(l => l.MaThanhPham).Distinct().ToList();
        var sanPham = await _db.Products.AsNoTracking().Where(p => ma.Contains(p.MaSanPham))
            .ToDictionaryAsync(p => p.MaSanPham, ct);

        // Tồn còn lại theo lô, trừ dần khi phân bổ cho từng dòng -> các dòng cùng thành phẩm không gợi ý trùng.
        // Đơn từ HanoiCheck: ưu tiên đúng các lô HnC đã phân bổ (để khớp truy xuất bên trường), còn lại theo FEFO.
        var loHnC = new Dictionary<string, List<string>>();
        if (don.Nguon == NguonDonHang.HanoiCheck && don.MaDonHnC is not null && _db.TenantInfo?.Id is { } tenantId)
        {
            loHnC = (await _db.DonHangNhanPhanBos.AsNoTracking()
                    .Where(p => p.TenantId == tenantId && p.MaLo != null
                                && p.DonHangNhanDong!.DonHangNhan!.MaDonHang == don.MaDonHnC)
                    .Select(p => new { p.DonHangNhanDong!.MaSanPham, p.MaLo, p.Id })
                    .ToListAsync(ct))
                .OrderBy(x => x.Id)
                .GroupBy(x => x.MaSanPham)
                .ToDictionary(g => g.Key, g => g.Select(x => x.MaLo!).Distinct().ToList());
        }

        var conLai = new Dictionary<string, List<(string MaLo, DateOnly? Hsd, decimal Ton)>>();
        foreach (var m in ma)
        {
            var lots = await LayLoFefoAsync(m, don.MaKho, ct);
            if (loHnC.TryGetValue(m, out var uuTien))
                lots = lots.OrderBy(l => uuTien.IndexOf(l.MaLo) is var i && i >= 0 ? i : int.MaxValue).ToList();
            conLai[m] = lots;
        }
        var daGoiY = new Dictionary<(string, string), decimal>();

        var ket = new List<DongXuatKhoDto>();
        foreach (var l in don.Dong.OrderBy(x => x.Id))
        {
            var canPhanBo = l.SoLuong;
            var lo = new List<LoCoTheXuatDto>();
            foreach (var lot in conLai[l.MaThanhPham])
            {
                var daDung = daGoiY.GetValueOrDefault((l.MaThanhPham, lot.MaLo));
                var goiY = Math.Min(canPhanBo, Math.Max(0, lot.Ton - daDung));
                if (goiY > 0)
                {
                    daGoiY[(l.MaThanhPham, lot.MaLo)] = daDung + goiY;
                    canPhanBo -= goiY;
                }
                lo.Add(new LoCoTheXuatDto(lot.MaLo, lot.Hsd, lot.Ton, goiY));
            }
            var sp = sanPham.GetValueOrDefault(l.MaThanhPham);
            ket.Add(new DongXuatKhoDto(l.Id, l.MaThanhPham, sp?.TenSanPham ?? l.MaThanhPham, sp?.DonViTinh, l.SoLuong, lo));
        }
        return ket;
    }

    public async Task<KetQuaThaoTac> XuatKhoAsync(int id, IReadOnlyList<PhanBoLoRequest>? phanBo, string? maNguoiGiao,
                                                  string? ghiChu = null, IReadOnlyList<AnhDauVao>? anhTongQuan = null,
                                                  CancellationToken ct = default)
    {
        var don = await QueryDayDu().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (don is null) return KetQuaThaoTac.Loi("Không tìm thấy đơn hàng.");
        if (don.TrangThai is not (TrangThaiDonHangBan.ChoXacNhan or TrangThaiDonHangBan.DaXacNhan))
            return KetQuaThaoTac.Loi("Chỉ xuất kho được đơn đang chờ hoặc đã xác nhận.");

        maNguoiGiao = string.IsNullOrWhiteSpace(maNguoiGiao) ? don.MaNguoiGiao : maNguoiGiao.Trim();
        if (maNguoiGiao is not null && !await _db.Staff.AnyAsync(s => s.MaNhanSu == maNguoiGiao, ct))
            return KetQuaThaoTac.Loi($"Không có nhân sự \"{maNguoiGiao}\".");

        // Không truyền phân bổ -> lấy đúng gợi ý FEFO.
        var goiY = await DungGoiYAsync(don, ct);
        var chot = phanBo is { Count: > 0 }
            ? phanBo.Where(p => p.SoLuong > 0).Select(p => new PhanBoLoRequest(p.DongId, p.MaLo.Trim(), p.SoLuong)).ToList()
            : goiY.SelectMany(g => g.Lo.Where(x => x.GoiY > 0).Select(x => new PhanBoLoRequest(g.DongId, x.MaLo, x.GoiY)))
                  .ToList();

        var dongTheoId = don.Dong.ToDictionary(l => l.Id);
        if (chot.FirstOrDefault(p => !dongTheoId.ContainsKey(p.DongId)) is { } la)
            return KetQuaThaoTac.Loi($"Phân bổ gắn với dòng #{la.DongId} không thuộc đơn này.");

        // Mỗi dòng phải được phân bổ đủ, không thừa không thiếu.
        foreach (var l in don.Dong)
        {
            var tong = chot.Where(p => p.DongId == l.Id).Sum(p => p.SoLuong);
            if (tong != l.SoLuong)
                return KetQuaThaoTac.Loi(phanBo is not { Count: > 0 }
                    ? $"Không đủ tồn \"{l.MaThanhPham}\" để xuất {l.SoLuong:0.###} (các lô còn {tong:0.###})."
                    : $"\"{l.MaThanhPham}\": đã phân bổ {tong:0.###}, cần đúng {l.SoLuong:0.###}.");
        }

        // Tổng lấy từ mỗi lô không vượt tồn thực của lô đó.
        var tonLo = goiY.SelectMany(g => g.Lo.Select(x => (g.MaThanhPham, x.MaLo, x.HanSuDung, x.Ton)))
            .DistinctBy(x => (x.MaThanhPham, x.MaLo))
            .ToDictionary(x => (x.MaThanhPham, x.MaLo), x => (x.HanSuDung, x.Ton));
        foreach (var nhom in chot.GroupBy(p => (dongTheoId[p.DongId].MaThanhPham, p.MaLo)))
        {
            var can = nhom.Sum(p => p.SoLuong);
            var ton = tonLo.TryGetValue(nhom.Key, out var t) ? t.Ton : 0;
            if (can > ton)
                return KetQuaThaoTac.Loi($"Lô \"{nhom.Key.MaLo}\" của \"{nhom.Key.MaThanhPham}\" chỉ còn {ton:0.###}, "
                                         + $"không xuất được {can:0.###}.");
        }

        // Đơn nguồn HanoiCheck: đẩy ngược process (người giao/ghi chú/ảnh/nguồn hàng) rồi status (Đang giao)
        // TRƯỚC khi chạm vào kho nội bộ - lỗi thật ở 1 trong 2 bước thì dừng hẳn, giữ nguyên "Đã xác nhận"
        // để NCC bấm lại (process ghi đè toàn bộ mỗi lần gọi nên gọi lại an toàn).
        if (don.Nguon == NguonDonHang.HanoiCheck && don.MaDonHnC is not null && _db.TenantInfo?.Id is { } tenantId)
        {
            var chiTiet = chot.Where(p => dongTheoId[p.DongId].MaTruyVetHnC is not null)
                .GroupBy(p => dongTheoId[p.DongId].MaTruyVetHnC!)
                .Select(g => new ProcessOrderLine
                {
                    TraceCode = g.Key,
                    PhanBo = g.Select(p => new ProcessOrderAllocation(p.MaLo, don.MaKho, p.SoLuong)).ToList()
                }).ToList();

            var ketXuLy = await _orderCommandClient.XuLyDonAsync(tenantId, don.MaDonHnC, new ProcessOrderRequest
            {
                MaNguoiGiao = maNguoiGiao,
                GhiChu = ghiChu,
                DanhSachAnh = anhTongQuan?.Select(a => a.DuongDan).ToList(),
                ChiTiet = chiTiet
            }, ct);
            if (!ketXuLy.ThanhCong && !ketXuLy.ChuaCauHinh)
                return KetQuaThaoTac.Loi($"Không đẩy được xử lý đơn sang HanoiCheck: {ketXuLy.ThongBao}");

            if (ketXuLy.ThanhCong)
            {
                var ketTrangThai = await _orderCommandClient.DoiTrangThaiAsync(tenantId, don.MaDonHnC, "DANG_GIAO", null, ct);
                if (!ketTrangThai.ThanhCong)
                    return KetQuaThaoTac.Loi($"Không đẩy được trạng thái \"Đang giao\" sang HanoiCheck: {ketTrangThai.ThongBao}");
            }
        }

        var now = DateTime.UtcNow;
        foreach (var p in chot)
        {
            var dong = dongTheoId[p.DongId];
            var hsd = tonLo[(dong.MaThanhPham, p.MaLo)].HanSuDung;
            _db.KhoGiaoDichs.Add(new KhoGiaoDich
            {
                MaSanPham = dong.MaThanhPham, MaKho = don.MaKho, MaLo = p.MaLo, SoLuong = -p.SoLuong,
                HanSuDung = hsd, Loai = LoaiGiaoDichKho.XuatBan, ChungTu = don.MaDonHang, ThoiGianUtc = now
            });
            dong.XuatLo.Add(new DonHangBanXuatLo { MaLo = p.MaLo, HanSuDung = hsd, SoLuong = p.SoLuong });
        }

        don.MaNguoiGiao = maNguoiGiao;
        don.GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? don.GhiChu : ghiChu.Trim();
        if (anhTongQuan is not null)
        {
            _db.DonHangBanAnhs.RemoveRange(don.AnhTongQuan);
            don.AnhTongQuan = anhTongQuan.Select(a => new DonHangBanAnh { TenAnh = a.TenAnh, DuongDan = a.DuongDan }).ToList();
        }
        don.TrangThai = TrangThaiDonHangBan.DangGiao;
        don.ThoiGianXuatKhoUtc = now;
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã xuất kho đơn \"{don.MaDonHang}\" ({chot.Select(p => p.MaLo).Distinct().Count()} lô), "
                                + "chuyển sang Đang giao.");
    }

    public async Task<KetQuaThaoTac> HoanTatGiaoAsync(int id, CancellationToken ct = default)
    {
        var don = await _db.DonHangBans.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (don is null) return KetQuaThaoTac.Loi("Không tìm thấy đơn hàng.");
        if (don.TrangThai != TrangThaiDonHangBan.DangGiao)
            return KetQuaThaoTac.Loi("Chỉ xác nhận đã giao cho đơn đang giao.");

        don.TrangThai = TrangThaiDonHangBan.DaGiao;
        don.ThoiGianGiaoUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đơn \"{don.MaDonHang}\" đã giao.");
    }

    public async Task<KetQuaThaoTac> HuyAsync(int id, string? lyDo, CancellationToken ct = default)
    {
        lyDo = lyDo?.Trim();
        if (string.IsNullOrWhiteSpace(lyDo)) return KetQuaThaoTac.Loi("Vui lòng nhập lý do huỷ.");
        if (lyDo.Length > 500) lyDo = lyDo[..500];

        var don = await QueryDayDu().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (don is null) return KetQuaThaoTac.Loi("Không tìm thấy đơn hàng.");
        if (don.TrangThai == TrangThaiDonHangBan.DaHuy) return KetQuaThaoTac.Loi("Đơn này đã huỷ rồi.");
        if (don.TrangThai == TrangThaiDonHangBan.DaGiao)
            return KetQuaThaoTac.Loi("Đơn đã giao không huỷ được. Hàng bị trả lại thì dùng Kiểm kê / Điều chỉnh tồn.");

        var now = DateTime.UtcNow;
        var traVeKho = don.TrangThai == TrangThaiDonHangBan.DangGiao;
        if (traVeKho)
        {
            // Đảo đúng các lô đã xuất (giữ lô + hạn dùng) để tồn và FEFO về như trước khi xuất.
            foreach (var l in don.Dong)
            foreach (var x in l.XuatLo)
            {
                _db.KhoGiaoDichs.Add(new KhoGiaoDich
                {
                    MaSanPham = l.MaThanhPham, MaKho = don.MaKho, MaLo = x.MaLo, SoLuong = x.SoLuong,
                    HanSuDung = x.HanSuDung, Loai = LoaiGiaoDichKho.HoanTacXuatBan, ChungTu = don.MaDonHang,
                    GhiChu = $"Trả về kho do huỷ đơn {don.MaDonHang}: {lyDo}", ThoiGianUtc = now
                });
            }
        }

        don.TrangThai = TrangThaiDonHangBan.DaHuy;
        don.LyDoHuy = lyDo;
        don.ThoiGianHuyUtc = now;
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok(traVeKho
            ? $"Đã huỷ đơn \"{don.MaDonHang}\" và trả hàng về đúng các lô đã xuất."
            : $"Đã huỷ đơn \"{don.MaDonHang}\".");
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var don = await QueryDayDu().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (don is null) return KetQuaThaoTac.Loi("Không tìm thấy đơn hàng.");
        if (don.ThoiGianXuatKhoUtc is not null)
            return KetQuaThaoTac.Loi("Không xoá được đơn đã xuất kho (đã phát sinh giao dịch kho) - dùng Huỷ nếu cần.");
        if (don.Nguon == NguonDonHang.HanoiCheck)
            return KetQuaThaoTac.Loi("Đơn từ HanoiCheck không xoá được (lần đồng bộ sau sẽ tạo lại) - dùng Huỷ nếu không giao.");

        _db.DonHangBans.Remove(don);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã xoá đơn \"{don.MaDonHang}\".");
    }

    /// <summary>Các lô còn tồn của (thành phẩm, kho), sắp FEFO: hết hạn sớm trước, lô không HSD sau cùng.</summary>
    private async Task<List<(string MaLo, DateOnly? Hsd, decimal Ton)>> LayLoFefoAsync(
        string maSanPham, string maKho, CancellationToken ct)
    {
        var gd = await _db.KhoGiaoDichs.AsNoTracking()
            .Where(g => g.MaSanPham == maSanPham && g.MaKho == maKho)
            .ToListAsync(ct);

        return gd.GroupBy(g => g.MaLo)
            .Select(g => (MaLo: g.Key, Hsd: g.Where(x => x.HanSuDung.HasValue).Max(x => x.HanSuDung), Ton: g.Sum(x => x.SoLuong)))
            .Where(x => x.Ton > 0)
            .OrderBy(x => x.Hsd ?? DateOnly.MaxValue).ThenBy(x => x.MaLo)
            .ToList();
    }
}
