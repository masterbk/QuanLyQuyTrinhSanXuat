using HCP.Domain.Constants;
using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.MaTuSinh;
using HCP.Infrastructure.Services.ThongBao;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.BanHang;

/// <summary>Một lô có thể xuất cho một dòng đơn: tồn hiện tại của lô và số lượng hệ thống gợi ý (FEFO).</summary>
public sealed record LoCoTheXuatDto(string MaLo, DateOnly? HanSuDung, decimal Ton, decimal GoiY);

/// <summary>
/// Bảng phân bổ lô cho một dòng đơn khi xuất kho. <see cref="MaThanhPham"/> là mã hàng trừ kho: chính thành phẩm,
/// hoặc - với dòng món ăn - một NGUYÊN LIỆU của món (khi đó <see cref="MaNguyenLieu"/> có giá trị, <see cref="SoLuong"/>
/// = định lượng × số suất và mỗi nguyên liệu là một bảng riêng cùng <see cref="DongId"/>).
/// </summary>
public sealed record DongXuatKhoDto(int DongId, string MaThanhPham, string TenThanhPham, string? DonViTinh,
                                    decimal SoLuong, IReadOnlyList<LoCoTheXuatDto> Lo,
                                    string? MaNguyenLieu = null, string? TenMonAn = null, decimal? SoSuat = null);

/// <summary>Người dùng chốt: lấy bao nhiêu từ lô nào cho dòng nào (dòng món ăn: kèm nguyên liệu của lô).</summary>
public sealed record PhanBoLoRequest(int DongId, string MaLo, decimal SoLuong, string? MaNguyenLieu = null);

/// <summary>Số lượng mới cho một dòng của đơn đã xuất kho (sửa số lượng đơn đang giao).</summary>
public sealed record SuaSoLuongDongRequest(int DongId, decimal SoLuongMoi);

/// <summary>Một ảnh tổng quan đã tải lên (tên gốc + đường dẫn công khai) gắn cho đơn lúc xuất kho.</summary>
public sealed record AnhDauVao(string TenAnh, string DuongDan);

/// <summary>Một nguyên liệu cần cho các đơn đã chọn: <see cref="Can"/> = định mức/công thức × số lượng (gộp mọi
/// dòng, cả món ăn lẫn thực phẩm); <see cref="Ton"/> = tồn hiện có (mọi kho). Gợi ý nhập = max(0, Can − Ton).</summary>
public sealed record NhuCauNguyenLieuDto(string MaNguyenLieu, string TenNguyenLieu, string? DonViTinh,
                                         decimal Can, decimal Ton);

/// <summary>
/// Đơn hàng bán: lập đơn → xác nhận → xuất kho (trừ tồn theo lô, gợi ý FEFO, cho sửa) → đã giao; huỷ khi
/// đang giao thì trả hàng về đúng lô bằng bút toán đảo.
/// </summary>
public interface IDonHangBanService
{
    Task<IReadOnlyList<DonHangBan>> LayTatCaAsync(CancellationToken ct = default);
    Task<DonHangBan?> LayTheoIdAsync(int id, CancellationToken ct = default);

    /// <summary>Đọc bản mới nhất để đổ vào form sửa - KHÔNG theo dõi thay đổi, xem
    /// <see cref="HCP.Infrastructure.Services.DanhMuc.IDanhMucService{T}.LayDeSuaAsync"/>.</summary>
    Task<DonHangBan?> LayDeSuaAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Trang kết quả lọc + phân trang NGAY Ở CSDL (không tải cả bảng vào bộ nhớ rồi mới cắt trang) - dùng cho
    /// API danh sách (mobile), nơi mỗi lần chuyển tab/kéo thêm trang đều gọi lại. Sắp xếp theo Ngày hẹn giao
    /// (mới nhất trước, đơn chưa có ngày hẹn xếp cuối). <paramref name="trangThai"/> lọc đúng 1 trạng thái;
    /// <paramref name="choGiaoHang"/> true thu hẹp thêm về đúng "Chờ giao hàng"; <paramref name="maNguoiGiao"/>
    /// có giá trị thì chỉ lấy đơn của đúng người này (mọi trạng thái); <paramref name="ngayGiaoTu"/>/
    /// <paramref name="ngayGiaoDen"/> lọc theo khoảng Ngày hẹn giao (đơn chưa có ngày hẹn bị loại khi đang lọc);
    /// <paramref name="maKhachHang"/> có giá trị thì chỉ lấy đơn của đúng khách hàng đó.
    /// </summary>
    Task<(IReadOnlyList<DonHangBan> Trang, int TongSo)> LayTrangAsync(
        TrangThaiDonHangBan? trangThai, bool choGiaoHang, string? maNguoiGiao,
        DateOnly? ngayGiaoTu, DateOnly? ngayGiaoDen, string? maKhachHang, int trang, int soDong,
        CancellationToken ct = default);

    Task<KetQuaThaoTac> TaoAsync(DonHangBan don, CancellationToken ct = default);

    /// <summary>Sửa đơn chưa xuất kho (thay toàn bộ dòng hàng). Mã đơn không đổi.</summary>
    Task<KetQuaThaoTac> CapNhatAsync(DonHangBan don, CancellationToken ct = default);

    /// <summary>
    /// Sửa SỐ LƯỢNG của đơn ĐÃ XUẤT KHO (Đang giao / Chờ giao hàng) và tự điều chỉnh tồn kho theo chênh lệch:
    /// giảm thì trả phần dư về đúng các lô đã xuất (bút toán đảo), tăng thì trừ thêm từ kho theo FEFO (chặn nếu
    /// không đủ tồn). Đơn nguồn HanoiCheck được đẩy lại "process" với nguồn hàng mới trước khi ghi kho.
    /// </summary>
    Task<KetQuaThaoTac> SuaSoLuongDaXuatKhoAsync(int id, IReadOnlyList<SuaSoLuongDongRequest> thayDoi,
                                                 CancellationToken ct = default);

    Task<KetQuaThaoTac> XacNhanAsync(int id, CancellationToken ct = default);

    /// <summary>Các lô còn tồn cho từng dòng, kèm số lượng gợi ý theo FEFO (cộng dồn các dòng cùng thành phẩm).</summary>
    Task<IReadOnlyList<DongXuatKhoDto>> GoiYXuatKhoAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Xuất kho: trừ tồn theo phân bổ lô. <paramref name="phanBo"/> rỗng = dùng gợi ý FEFO. Có chọn người giao thì
    /// chuyển "Đang giao"; không chọn thì chuyển "Chờ giao hàng" (chờ nhân viên tự nhận qua <see cref="NhanDonAsync"/>).
    /// Với đơn nguồn HanoiCheck: trước khi trừ tồn, luôn đẩy ngược "process" (người giao/ghi chú/ảnh/nguồn hàng)
    /// sang HnC; CHỈ đẩy tiếp "status" (Đang giao) nếu đã chọn người giao - HnC bắt buộc có người giao mới cho
    /// chuyển trạng thái đó. Lỗi thật thì KHÔNG trừ tồn/đổi trạng thái nội bộ để 2 bên luôn khớp.
    /// </summary>
    Task<KetQuaThaoTac> XuatKhoAsync(int id, IReadOnlyList<PhanBoLoRequest>? phanBo, string? maNguoiGiao,
                                     string? ghiChu = null, IReadOnlyList<AnhDauVao>? anhTongQuan = null,
                                     CancellationToken ct = default);

    /// <summary>
    /// Xác nhận đã giao - BẮT BUỘC ít nhất 1 ảnh chứng minh (app chụp tại chỗ, web chọn tệp). Đơn nguồn HanoiCheck:
    /// gửi ảnh sang HnC (điều kiện bắt buộc của trạng thái "Đã giao") rồi đẩy trạng thái DA_GIAO.
    /// </summary>
    Task<KetQuaThaoTac> HoanTatGiaoAsync(int id, IReadOnlyList<AnhDauVao> anhGiao, CancellationToken ct = default);

    /// <summary>Nhân viên giao hàng nhận đơn đang "Chờ giao hàng" (đã xuất kho, chưa có người giao): gán mình
    /// làm người giao rồi chuyển đơn sang "Đang giao".</summary>
    Task<KetQuaThaoTac> NhanDonAsync(int id, string maNhanSu, CancellationToken ct = default);

    /// <summary>Tìm đơn theo nội dung mã QR: QR tra cứu của hệ thống, link truy xuất HanoiCheck, hoặc chính mã đơn.</summary>
    Task<DonHangBan?> TimTheoQrAsync(string noiDung, CancellationToken ct = default);

    Task<KetQuaThaoTac> HuyAsync(int id, string? lyDo, CancellationToken ct = default);

    /// <summary>Xoá đơn chưa từng xuất kho.</summary>
    Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Tổng nhu cầu nguyên liệu của các đơn đã chọn, gộp theo nguyên liệu: dòng MÓN ĂN theo công thức
    /// (định lượng × số suất), dòng THỰC PHẨM theo định mức (× số lượng × (1 + hao hụt%)). Kèm tồn hiện có để gợi ý
    /// nhập phần thiếu. Dùng cho "Tạo lệnh nhập nguyên liệu" ở màn Đơn hàng bán.
    /// </summary>
    Task<IReadOnlyList<NhuCauNguyenLieuDto>> TongNhuCauNguyenLieuAsync(IReadOnlyList<int> donIds,
                                                                      CancellationToken ct = default);
}

/// <inheritdoc cref="IDonHangBanService"/>
public sealed class DonHangBanService : IDonHangBanService
{
    private readonly AppDbContext _db;
    private readonly IMaTuSinhService _maTuSinh;
    private readonly IHanoiCheckOrderCommandClient _orderCommandClient;
    private readonly IPushNotificationService _push;

    public DonHangBanService(AppDbContext db, IMaTuSinhService maTuSinh, IHanoiCheckOrderCommandClient orderCommandClient,
                             IPushNotificationService push)
    {
        _db = db;
        _maTuSinh = maTuSinh;
        _orderCommandClient = orderCommandClient;
        _push = push;
    }

    /// <summary>Dữ liệu kèm mọi thông báo đẩy của đơn hàng - app dùng để mở đúng màn chi tiết khi bấm vào.</summary>
    private static Dictionary<string, string> DuLieuThongBao(int donId) =>
        new() { ["loaiThongBao"] = "don_hang", ["donHangId"] = donId.ToString() };

    // AsSplitQuery(): Dong->XuatLo (lồng nhau) + AnhTongQuan là 3 collection cùng lúc - gộp 1 câu JOIN sẽ
    // nhân dòng theo tích số dòng hàng × lô xuất × ảnh, đơn nào nhiều dòng/ảnh là phình rất to, tách câu
    // truy vấn cho rẻ hơn hẳn (khớp cảnh báo MultipleCollectionInclude của EF Core).
    private IQueryable<DonHangBan> QueryDayDu() =>
        _db.DonHangBans.Include(d => d.Dong).ThenInclude(l => l.XuatLo).Include(d => d.AnhTongQuan)
            .AsSplitQuery();

    public async Task<IReadOnlyList<DonHangBan>> LayTatCaAsync(CancellationToken ct = default) =>
        await QueryDayDu().AsNoTracking()
            .OrderByDescending(d => d.NgayGiao).ThenByDescending(d => d.Id)
            .ToListAsync(ct);

    public Task<DonHangBan?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        QueryDayDu().FirstOrDefaultAsync(d => d.Id == id, ct);

    /// <inheritdoc />
    public Task<DonHangBan?> LayDeSuaAsync(int id, CancellationToken ct = default) =>
        QueryDayDu().AsNoTrackingWithIdentityResolution().FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<(IReadOnlyList<DonHangBan> Trang, int TongSo)> LayTrangAsync(
        TrangThaiDonHangBan? trangThai, bool choGiaoHang, string? maNguoiGiao,
        DateOnly? ngayGiaoTu, DateOnly? ngayGiaoDen, string? maKhachHang, int trang, int soDong,
        CancellationToken ct = default)
    {
        var q = _db.DonHangBans.AsNoTracking().AsQueryable();
        if (trangThai is { } tt) q = q.Where(d => d.TrangThai == tt);
        if (choGiaoHang) q = q.Where(d => d.TrangThai == TrangThaiDonHangBan.ChoGiaoHang);
        if (maNguoiGiao is not null)
        {
            var ma = maNguoiGiao.ToLower();
            q = q.Where(d => d.MaNguoiGiao != null && d.MaNguoiGiao.ToLower() == ma);
        }
        if (ngayGiaoTu is { } tu) q = q.Where(d => d.NgayGiao != null && d.NgayGiao >= tu);
        if (ngayGiaoDen is { } den) q = q.Where(d => d.NgayGiao != null && d.NgayGiao <= den);
        if (!string.IsNullOrWhiteSpace(maKhachHang)) q = q.Where(d => d.MaKhachHang == maKhachHang);

        var tongSo = await q.CountAsync(ct);
        var (t, n) = (Math.Max(1, trang), Math.Clamp(soDong, 1, 100));
        // KHÔNG dùng QueryDayDu() (có AnhTongQuan) - danh sách không cần ảnh, đỡ tải thêm dữ liệu vô ích.
        // AsSplitQuery(): Dong->XuatLo là 1-nhiều-tới-nhiều, gộp 1 câu JOIN sẽ nhân dòng cả trang, tách câu
        // truy vấn cho rẻ hơn và đúng khi kết hợp Skip/Take.
        var ds = await q.OrderByDescending(d => d.NgayGiao).ThenByDescending(d => d.Id)
            .Skip((t - 1) * n).Take(n)
            .Include(d => d.Dong).ThenInclude(l => l.XuatLo)
            .AsSplitQuery()
            .ToListAsync(ct);
        return (ds, tongSo);
    }

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

        if (_db.TenantInfo?.Id is { } tenantId)
            await _push.GuiTheoQuyenAsync(tenantId, AppRoles.QuyenNhapLieu.Split(','),
                "Đơn hàng mới", $"Đơn mới: {don.MaDonHang}", DuLieuThongBao(don.Id), ct);

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

        // Giữ trace_code HanoiCheck của dòng cũ cùng mã (lần lượt theo thứ tự): form sửa không mang mã này, mà thiếu nó
        // thì không đẩy được nguồn hàng cho mặt hàng xuất hiện nhiều dòng (vd cùng món ở bữa sáng và bữa trưa).
        var traceCu = goc.Dong.Where(l => l.MaTruyVetHnC is not null).OrderBy(l => l.Id)
            .GroupBy(l => l.MaThanhPham).ToDictionary(g => g.Key, g => new Queue<string>(g.Select(l => l.MaTruyVetHnC!)));
        _db.DonHangBanDongs.RemoveRange(goc.Dong);
        goc.Dong = don.Dong.Select(l => new DonHangBanDong
        {
            MaThanhPham = l.MaThanhPham, SoLuong = l.SoLuong, DonGia = l.DonGia, GhiChu = l.GhiChu, LaMonAn = l.LaMonAn,
            MaTruyVetHnC = l.MaTruyVetHnC
                           ?? (traceCu.TryGetValue(l.MaThanhPham, out var q) && q.Count > 0 ? q.Dequeue() : null)
        }).ToList();

        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã cập nhật đơn hàng \"{goc.MaDonHang}\".");
    }

    public async Task<KetQuaThaoTac> SuaSoLuongDaXuatKhoAsync(int id, IReadOnlyList<SuaSoLuongDongRequest> thayDoi,
                                                              CancellationToken ct = default)
    {
        var don = await QueryDayDu().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (don is null) return KetQuaThaoTac.Loi("Không tìm thấy đơn hàng.");
        if (don.ThoiGianXuatKhoUtc is null
            || don.TrangThai is not (TrangThaiDonHangBan.DangGiao or TrangThaiDonHangBan.ChoGiaoHang))
            return KetQuaThaoTac.Loi("Chỉ sửa số lượng được đơn đã xuất kho và đang giao / chờ giao hàng.");
        if (don.Dong.Any(l => l.LaMonAn))
            return KetQuaThaoTac.Loi("Đơn suất ăn chưa hỗ trợ sửa số suất sau khi xuất kho (số suất do trường đặt).");

        var theoId = don.Dong.ToDictionary(l => l.Id);
        var doi = new List<(DonHangBanDong Dong, decimal Moi, decimal Delta)>();
        foreach (var t in thayDoi ?? Array.Empty<SuaSoLuongDongRequest>())
        {
            if (!theoId.TryGetValue(t.DongId, out var dong))
                return KetQuaThaoTac.Loi($"Dòng #{t.DongId} không thuộc đơn này.");
            if (t.SoLuongMoi <= 0)
                return KetQuaThaoTac.Loi($"Số lượng của \"{dong.MaThanhPham}\" phải lớn hơn 0.");
            var delta = t.SoLuongMoi - dong.SoLuong;
            if (delta != 0) doi.Add((dong, t.SoLuongMoi, delta));
        }
        if (doi.Count == 0) return KetQuaThaoTac.Ok("Số lượng không thay đổi.");

        // Tồn còn lại theo (thành phẩm, lô) - phục vụ các dòng TĂNG. Phần trả về của dòng giảm cộng vào đây để
        // dòng tăng cùng thành phẩm dùng lại được.
        var tonConLai = new Dictionary<(string Sp, string Lo), (DateOnly? Hsd, decimal Ton)>();
        foreach (var sp in doi.Where(d => d.Delta > 0).Select(d => d.Dong.MaThanhPham).Distinct())
            foreach (var l in await LayLoFefoAsync(sp, don.MaKho, ct))
                tonConLai[(sp, l.MaLo)] = (l.Hsd, l.Ton);

        var now = DateTime.UtcNow;
        var butToan = new List<KhoGiaoDich>();
        var allocMoi = new Dictionary<int, List<(string MaLo, DateOnly? Hsd, decimal SoLuong)>>();

        // GIẢM trước: trả về đúng lô đã xuất (từ lô xuất sau cùng trở lên) và bơm vào tonConLai.
        foreach (var (dong, _, delta) in doi.Where(d => d.Delta < 0))
        {
            var alloc = dong.XuatLo.Select(x => (MaLo: x.MaLo, Hsd: x.HanSuDung, SoLuong: x.SoLuong)).ToList();
            var canTra = -delta;
            // Trả về lô có HSD muộn nhất trước (đảo FEFO) - giữ lô hết hạn sớm vẫn thuộc đơn, tồn còn lại vẫn FEFO đúng.
            var thuTuTra = Enumerable.Range(0, alloc.Count)
                .OrderByDescending(i => alloc[i].Hsd ?? DateOnly.MaxValue).ThenByDescending(i => alloc[i].MaLo).ToList();
            foreach (var i in thuTuTra)
            {
                if (canTra <= 0) break;
                var tra = Math.Min(canTra, alloc[i].SoLuong);
                if (tra <= 0) continue;
                alloc[i] = (alloc[i].MaLo, alloc[i].Hsd, alloc[i].SoLuong - tra);
                canTra -= tra;
                butToan.Add(new KhoGiaoDich
                {
                    MaSanPham = dong.MaThanhPham, MaKho = don.MaKho, MaLo = alloc[i].MaLo, SoLuong = tra,
                    HanSuDung = alloc[i].Hsd, Loai = LoaiGiaoDichKho.HoanTacXuatBan, ChungTu = don.MaDonHang,
                    GhiChu = $"Trả về kho do sửa số lượng đơn {don.MaDonHang}", ThoiGianUtc = now
                });
                var key = (dong.MaThanhPham, alloc[i].MaLo);
                var cu = tonConLai.TryGetValue(key, out var v) ? v : (Hsd: alloc[i].Hsd, Ton: 0m);
                tonConLai[key] = (cu.Hsd ?? alloc[i].Hsd, cu.Ton + tra);
            }
            allocMoi[dong.Id] = alloc.Where(a => a.SoLuong > 0).ToList();
        }

        // TĂNG: trừ thêm từ tồn còn lại theo FEFO (lô hết hạn trước xuất trước).
        foreach (var (dong, moi, delta) in doi.Where(d => d.Delta > 0))
        {
            var alloc = dong.XuatLo.Select(x => (MaLo: x.MaLo, Hsd: x.HanSuDung, SoLuong: x.SoLuong)).ToList();
            var canTru = delta;
            var loFefo = tonConLai.Where(k => k.Key.Sp == dong.MaThanhPham && k.Value.Ton > 0)
                .OrderBy(k => k.Value.Hsd ?? DateOnly.MaxValue).ThenBy(k => k.Key.Lo).ToList();
            foreach (var lo in loFefo)
            {
                if (canTru <= 0) break;
                var tru = Math.Min(canTru, lo.Value.Ton);
                canTru -= tru;
                tonConLai[lo.Key] = (lo.Value.Hsd, lo.Value.Ton - tru);
                butToan.Add(new KhoGiaoDich
                {
                    MaSanPham = dong.MaThanhPham, MaKho = don.MaKho, MaLo = lo.Key.Lo, SoLuong = -tru,
                    HanSuDung = lo.Value.Hsd, Loai = LoaiGiaoDichKho.XuatBan, ChungTu = don.MaDonHang,
                    GhiChu = $"Trừ thêm do sửa số lượng đơn {don.MaDonHang}", ThoiGianUtc = now
                });
                var idx = alloc.FindIndex(a => a.MaLo == lo.Key.Lo);
                if (idx >= 0) alloc[idx] = (alloc[idx].MaLo, alloc[idx].Hsd, alloc[idx].SoLuong + tru);
                else alloc.Add((lo.Key.Lo, lo.Value.Hsd, tru));
            }
            if (canTru > 0)
                return KetQuaThaoTac.Loi($"Không đủ tồn \"{dong.MaThanhPham}\" để tăng lên {moi:0.###} "
                                         + $"(còn thiếu {canTru:0.###}).");
            allocMoi[dong.Id] = alloc.Where(a => a.SoLuong > 0).ToList();
        }

        // Đơn nguồn HanoiCheck: đẩy lại "process" với nguồn hàng mới TRƯỚC khi ghi kho - lỗi thì không lưu gì.
        if (don.Nguon == NguonDonHang.HanoiCheck && don.MaDonHnC is not null && _db.TenantInfo?.Id is { } tenantId)
        {
            var chiTiet = don.Dong.Select(l =>
            {
                var alloc = allocMoi.TryGetValue(l.Id, out var a)
                    ? a
                    : l.XuatLo.Select(x => (x.MaLo, Hsd: x.HanSuDung, x.SoLuong)).ToList();
                return new ProcessOrderLine
                {
                    TraceCode = l.MaTruyVetHnC,
                    MaThucPham = l.MaTruyVetHnC is null ? l.MaThanhPham : null,
                    PhanBo = alloc.Select(x => new ProcessOrderAllocation(x.MaLo, don.MaKho, x.SoLuong)).ToList()
                };
            }).Where(x => x.PhanBo.Count > 0).ToList();

            var ket = await _orderCommandClient.XuLyDonAsync(tenantId, don.MaDonHnC, new ProcessOrderRequest
            {
                MaNguoiGiao = don.MaNguoiGiao,
                GhiChu = don.GhiChu,
                ChiTiet = chiTiet
            }, ct);
            if (!ket.ThanhCong && !ket.ChuaCauHinh)
                return KetQuaThaoTac.Loi($"Không đẩy được nguồn hàng mới sang HanoiCheck: {ket.ThongBao}");
        }

        // Chốt: ghi bút toán, thay phân bổ lô, đổi số lượng dòng.
        foreach (var gd in butToan) _db.KhoGiaoDichs.Add(gd);
        foreach (var (dong, moi, _) in doi)
        {
            if (allocMoi.TryGetValue(dong.Id, out var alloc))
            {
                _db.DonHangBanXuatLos.RemoveRange(dong.XuatLo);
                dong.XuatLo = alloc.Select(a => new DonHangBanXuatLo
                {
                    MaLo = a.MaLo, HanSuDung = a.Hsd, SoLuong = a.SoLuong
                }).ToList();
            }
            dong.SoLuong = moi;
        }
        don.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã sửa số lượng đơn \"{don.MaDonHang}\" và điều chỉnh tồn kho theo chênh lệch.");
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
        var monAn = await _db.Dishes.AsNoTracking().Where(d => ma.Contains(d.MaMonAn))
            .ToDictionaryAsync(d => d.MaMonAn, d => d.TenMonAn, ct);
        foreach (var l in don.Dong)
        {
            l.MaThanhPham = l.MaThanhPham.Trim();
            l.GhiChu = string.IsNullOrWhiteSpace(l.GhiChu) ? null : l.GhiChu.Trim();
            // Loại dòng suy theo danh mục (form sửa đơn không mang cờ LaMonAn): thành phẩm trước, rồi món ăn.
            string ten;
            if (sanPham.TryGetValue(l.MaThanhPham, out var sp) && sp.LoaiSanPham == LoaiSanPham.ThanhPham)
            {
                l.LaMonAn = false;
                ten = sp.TenSanPham;
            }
            else if (monAn.TryGetValue(l.MaThanhPham, out var tenMon))
            {
                l.LaMonAn = true;
                ten = tenMon;
            }
            else return $"\"{l.MaThanhPham}\" không phải thành phẩm hay món ăn hợp lệ.";
            if (l.SoLuong <= 0) return $"Số lượng của \"{ten}\" phải lớn hơn 0.";
            if (l.DonGia < 0) return $"Đơn giá của \"{ten}\" không được âm.";
        }
        return null;
    }

    // ==================== Chuyển trạng thái ====================

    public async Task<KetQuaThaoTac> XacNhanAsync(int id, CancellationToken ct = default)
    {
        var don = await QueryDayDu().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (don is null) return KetQuaThaoTac.Loi("Không tìm thấy đơn hàng.");
        if (don.TrangThai != TrangThaiDonHangBan.ChoXacNhan)
            return KetQuaThaoTac.Loi("Chỉ xác nhận được đơn đang chờ xác nhận.");

        if (don.Nguon == NguonDonHang.HanoiCheck && don.MaDonHnC is not null && _db.TenantInfo?.Id is { } tenantId)
        {
            // Chặn xác nhận khi đơn HnC chưa lấy xong chi tiết, hoặc còn dòng hàng chưa đưa vào đơn -
            // tránh đơn "kẹt" thiếu mặt hàng vĩnh viễn: sau khi Đã xác nhận, lần đồng bộ kế tiếp CHỈ
            // gắn cờ HnCCoThayDoi, KHÔNG tự bổ sung lại dòng hàng (xem DonHangHnCService.CapNhatDon) -
            // nên phải chắc chắn đủ dòng TRƯỚC khi xác nhận.
            var nhan = await _db.DonHangNhans.Include(n => n.Dong)
                .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.MaDonHang == don.MaDonHnC, ct);
            if (nhan is not null)
            {
                if (!nhan.DaLayChiTiet || nhan.Dong.Any(l => l.SoLuong is null))
                    return KetQuaThaoTac.Loi("Đơn từ HanoiCheck chưa lấy xong chi tiết (số lượng từng dòng hàng) - "
                        + "đợi hệ thống đồng bộ lại (tự động vài phút, hoặc bấm \"Đồng bộ ngay\" ở Đơn hàng từ trường) rồi xác nhận.");

                // Dòng có số lượng + khớp thành phẩm hiện có, nhưng chưa nằm trong đơn: xảy ra khi lúc
                // đồng bộ thành phẩm đó CHƯA có trong danh mục (tạo trên HnC trước), sau mới thêm vào -
                // lần đồng bộ đó bỏ dòng + ghi chú, đơn chưa xác nhận thì lần sau tự bổ sung được, còn
                // đã xác nhận thì không (theo đúng nhánh trên) nên phải chặn TRƯỚC khi xác nhận.
                var maDaCo = don.Dong.Select(l => l.MaThanhPham).ToHashSet(StringComparer.Ordinal);
                var maThanhPham = (await _db.Products.AsNoTracking()
                        .Where(p => p.LoaiSanPham == LoaiSanPham.ThanhPham).Select(p => p.MaSanPham).ToListAsync(ct))
                    .ToHashSet(StringComparer.Ordinal);
                maThanhPham.UnionWith(await _db.Dishes.AsNoTracking().Select(d => d.MaMonAn).ToListAsync(ct));
                var thieu = nhan.Dong
                    .Where(l => l.SoLuong is > 0 && maThanhPham.Contains(l.MaSanPham) && !maDaCo.Contains(l.MaSanPham))
                    .Select(l => string.IsNullOrWhiteSpace(l.TenSanPham) ? l.MaSanPham : $"{l.MaSanPham} ({l.TenSanPham})")
                    .Distinct().ToList();
                if (thieu.Count > 0)
                    return KetQuaThaoTac.Loi("Đơn từ HanoiCheck còn mặt hàng chưa được đưa vào đơn (có thể do lúc đồng "
                        + "bộ thành phẩm chưa có trong danh mục): " + string.Join(", ", thieu)
                        + ". Vào \"Sửa đơn\" bổ sung dòng thiếu trước khi xác nhận.");
            }

            // Đẩy ngược "Đang chuẩn bị" TRƯỚC khi lưu nội bộ, để 2 bên luôn khớp - công tắc HnC
            // tắt/chưa cấu hình thì bỏ qua (ChuaCauHinh), lỗi thật thì chặn xác nhận.
            var ket = await _orderCommandClient.DoiTrangThaiAsync(tenantId, don.MaDonHnC, "DANG_CHUAN_BI", null, ct);
            if (!ket.ThanhCong && !ket.ChuaCauHinh)
                return KetQuaThaoTac.Loi($"Không đẩy được trạng thái sang HanoiCheck: {ket.ThongBao}");
        }

        don.TrangThai = TrangThaiDonHangBan.DaXacNhan;
        await _db.SaveChangesAsync(ct);

        if (_db.TenantInfo?.Id is { } coSoId)
            await _push.GuiTheoQuyenAsync(coSoId, AppRoles.QuyenNhapLieu.Split(','), "Đơn đã xác nhận",
                $"Đơn {don.MaDonHang} đã xác nhận, có thể xuất kho.", DuLieuThongBao(don.Id), ct);

        return KetQuaThaoTac.Ok($"Đã xác nhận đơn \"{don.MaDonHang}\".");
    }

    public async Task<IReadOnlyList<DongXuatKhoDto>> GoiYXuatKhoAsync(int id, CancellationToken ct = default)
    {
        var don = await QueryDayDu().AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (don is null) return Array.Empty<DongXuatKhoDto>();
        return await DungGoiYAsync(don, ct);
    }

    /// <summary>
    /// Một "nhu cầu xuất" của đơn: dòng thực phẩm cần chính thành phẩm; dòng món ăn tách thành từng nguyên liệu với
    /// lượng = định lượng/suất × số suất (làm tròn 5 chữ số thập phân - giới hạn của HanoiCheck).
    /// </summary>
    public async Task<IReadOnlyList<NhuCauNguyenLieuDto>> TongNhuCauNguyenLieuAsync(
        IReadOnlyList<int> donIds, CancellationToken ct = default)
    {
        if (donIds is null || donIds.Count == 0) return Array.Empty<NhuCauNguyenLieuDto>();

        var dong = await _db.DonHangBanDongs.AsNoTracking()
            .Where(l => donIds.Contains(l.DonHangBanId) && l.SoLuong > 0 && l.MaThanhPham != "")
            .Select(l => new { l.MaThanhPham, l.SoLuong, l.LaMonAn })
            .ToListAsync(ct);
        if (dong.Count == 0) return Array.Empty<NhuCauNguyenLieuDto>();

        var gop = new Dictionary<string, decimal>();

        // Dòng món ăn: công thức (định lượng × số suất).
        var maMon = dong.Where(l => l.LaMonAn).Select(l => l.MaThanhPham).Distinct().ToList();
        if (maMon.Count > 0)
        {
            var monAn = await _db.Dishes.AsNoTracking().Include(d => d.DanhSachNguyenLieu)
                .Where(d => maMon.Contains(d.MaMonAn)).ToDictionaryAsync(d => d.MaMonAn, ct);
            foreach (var l in dong.Where(x => x.LaMonAn))
                if (monAn.TryGetValue(l.MaThanhPham, out var mon))
                    foreach (var nl in mon.DanhSachNguyenLieu)
                        gop[nl.MaNguyenLieu] = gop.GetValueOrDefault(nl.MaNguyenLieu) + decimal.Round(nl.DinhLuong * l.SoLuong, 5);
        }

        // Dòng thực phẩm: định mức × số lượng × (1 + hao hụt%).
        var maTp = dong.Where(l => !l.LaMonAn).Select(l => l.MaThanhPham).Distinct().ToList();
        if (maTp.Count > 0)
        {
            var thanhPham = await _db.Products.AsNoTracking().Include(p => p.DanhSachDinhMuc)
                .Where(p => maTp.Contains(p.MaSanPham)).ToDictionaryAsync(p => p.MaSanPham, ct);
            foreach (var l in dong.Where(x => !x.LaMonAn))
                if (thanhPham.TryGetValue(l.MaThanhPham, out var p))
                    foreach (var dm in p.DanhSachDinhMuc)
                        gop[dm.MaNguyenLieu] = gop.GetValueOrDefault(dm.MaNguyenLieu)
                            + decimal.Round(dm.SoLuong * l.SoLuong * (1 + dm.HaoHutPhanTram / 100m), 5);
        }

        if (gop.Count == 0) return Array.Empty<NhuCauNguyenLieuDto>();

        var maNL = gop.Keys.ToList();
        var sp = await _db.Products.AsNoTracking().Where(p => maNL.Contains(p.MaSanPham))
            .ToDictionaryAsync(p => p.MaSanPham, ct);
        var ton = await _db.KhoGiaoDichs.AsNoTracking().Where(g => maNL.Contains(g.MaSanPham))
            .GroupBy(g => g.MaSanPham).Select(g => new { Ma = g.Key, Ton = g.Sum(x => x.SoLuong) })
            .ToDictionaryAsync(x => x.Ma, x => x.Ton, ct);

        return gop.Select(x => new NhuCauNguyenLieuDto(x.Key,
                    sp.GetValueOrDefault(x.Key)?.TenSanPham ?? x.Key,
                    sp.GetValueOrDefault(x.Key)?.DonViTinh, x.Value, ton.GetValueOrDefault(x.Key, 0m)))
                  .OrderBy(x => x.TenNguyenLieu).ToList();
    }

    private sealed record NhuCauXuat(DonHangBanDong Dong, string MaHang, decimal SoLuong, string? MaNguyenLieu);

    /// <summary>Tách đơn thành các nhu cầu xuất. Trả thông báo lỗi nếu có dòng món ăn không tìm thấy công thức.</summary>
    private async Task<(List<NhuCauXuat> NhuCau, Dictionary<string, Dish> MonAn, string? Loi)> TachNhuCauAsync(
        DonHangBan don, CancellationToken ct)
    {
        var maMon = don.Dong.Where(l => l.LaMonAn).Select(l => l.MaThanhPham).Distinct().ToList();
        var monAn = maMon.Count == 0
            ? new Dictionary<string, Dish>()
            : await _db.Dishes.AsNoTracking().Include(d => d.DanhSachNguyenLieu)
                .Where(d => maMon.Contains(d.MaMonAn)).ToDictionaryAsync(d => d.MaMonAn, ct);

        var nhuCau = new List<NhuCauXuat>();
        foreach (var l in don.Dong.OrderBy(x => x.Id))
        {
            if (!l.LaMonAn)
            {
                nhuCau.Add(new NhuCauXuat(l, l.MaThanhPham, l.SoLuong, null));
                continue;
            }
            if (!monAn.TryGetValue(l.MaThanhPham, out var mon) || mon.DanhSachNguyenLieu.Count == 0)
                return (nhuCau, monAn, $"Món \"{l.MaThanhPham}\" không có trong danh mục món ăn hoặc chưa có công thức - "
                                       + "không tính được nguyên liệu cần xuất.");
            foreach (var nl in mon.DanhSachNguyenLieu.OrderBy(x => x.Id))
                nhuCau.Add(new NhuCauXuat(l, nl.MaNguyenLieu, decimal.Round(nl.DinhLuong * l.SoLuong, 5), nl.MaNguyenLieu));
        }
        return (nhuCau, monAn, null);
    }

    private async Task<List<DongXuatKhoDto>> DungGoiYAsync(DonHangBan don, CancellationToken ct)
    {
        var (nhuCau, monAn, _) = await TachNhuCauAsync(don, ct);
        var ma = nhuCau.Select(n => n.MaHang).Distinct().ToList();
        var sanPham = await _db.Products.AsNoTracking().Where(p => ma.Contains(p.MaSanPham))
            .ToDictionaryAsync(p => p.MaSanPham, ct);

        // Tồn còn lại theo lô, trừ dần khi phân bổ cho từng nhu cầu -> các dòng/nguyên liệu dùng chung một mã hàng
        // không gợi ý trùng. Đơn từ HanoiCheck: ưu tiên đúng các lô HnC đã phân bổ (để khớp truy xuất bên trường),
        // còn lại theo FEFO.
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
        foreach (var n in nhuCau)
        {
            var canPhanBo = n.SoLuong;
            var lo = new List<LoCoTheXuatDto>();
            foreach (var lot in conLai[n.MaHang])
            {
                var daDung = daGoiY.GetValueOrDefault((n.MaHang, lot.MaLo));
                var goiY = Math.Min(canPhanBo, Math.Max(0, lot.Ton - daDung));
                if (goiY > 0)
                {
                    daGoiY[(n.MaHang, lot.MaLo)] = daDung + goiY;
                    canPhanBo -= goiY;
                }
                lo.Add(new LoCoTheXuatDto(lot.MaLo, lot.Hsd, lot.Ton, goiY));
            }
            var sp = sanPham.GetValueOrDefault(n.MaHang);
            ket.Add(new DongXuatKhoDto(n.Dong.Id, n.MaHang, sp?.TenSanPham ?? n.MaHang, sp?.DonViTinh, n.SoLuong, lo,
                n.MaNguyenLieu,
                n.Dong.LaMonAn ? monAn.GetValueOrDefault(n.Dong.MaThanhPham)?.TenMonAn ?? n.Dong.MaThanhPham : null,
                n.Dong.LaMonAn ? n.Dong.SoLuong : null));
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

        var (_, _, loiCongThuc) = await TachNhuCauAsync(don, ct);
        if (loiCongThuc is not null) return KetQuaThaoTac.Loi(loiCongThuc);

        // Không truyền phân bổ -> lấy đúng gợi ý FEFO.
        var goiY = await DungGoiYAsync(don, ct);
        var chot = phanBo is { Count: > 0 }
            ? phanBo.Where(p => p.SoLuong > 0)
                .Select(p => new PhanBoLoRequest(p.DongId, p.MaLo.Trim(), p.SoLuong,
                                                 string.IsNullOrWhiteSpace(p.MaNguyenLieu) ? null : p.MaNguyenLieu.Trim()))
                .ToList()
            : goiY.SelectMany(g => g.Lo.Where(x => x.GoiY > 0)
                    .Select(x => new PhanBoLoRequest(g.DongId, x.MaLo, x.GoiY, g.MaNguyenLieu)))
                  .ToList();

        var dongTheoId = don.Dong.ToDictionary(l => l.Id);
        if (chot.FirstOrDefault(p => !dongTheoId.ContainsKey(p.DongId)) is { } la)
            return KetQuaThaoTac.Loi($"Phân bổ gắn với dòng #{la.DongId} không thuộc đơn này.");

        // Mỗi nhu cầu (dòng thực phẩm, hoặc một nguyên liệu của dòng món ăn) phải được phân bổ đủ, không thừa không thiếu.
        // HanoiCheck cũng bắt tổng phân bổ BẰNG ĐÚNG lượng phải cung ứng.
        static (int, string) Khoa(int dongId, string? maNguyenLieu) => (dongId, maNguyenLieu ?? "");
        var canCo = goiY.ToDictionary(g => Khoa(g.DongId, g.MaNguyenLieu));
        if (chot.FirstOrDefault(p => !canCo.ContainsKey(Khoa(p.DongId, p.MaNguyenLieu))) is { } thua)
            return KetQuaThaoTac.Loi($"Nguyên liệu \"{thua.MaNguyenLieu}\" không thuộc công thức món của dòng #{thua.DongId}.");
        foreach (var g in goiY)
        {
            var tong = chot.Where(p => Khoa(p.DongId, p.MaNguyenLieu) == Khoa(g.DongId, g.MaNguyenLieu)).Sum(p => p.SoLuong);
            if (tong == g.SoLuong) continue;
            var ten = g.MaNguyenLieu is null ? $"\"{g.MaThanhPham}\"" : $"nguyên liệu \"{g.TenThanhPham}\" của món \"{g.TenMonAn}\"";
            return KetQuaThaoTac.Loi(phanBo is not { Count: > 0 }
                ? $"Không đủ tồn {ten} để xuất {g.SoLuong:0.#####} (các lô còn {tong:0.#####})."
                : $"Phân bổ {ten}: đã chọn {tong:0.#####}, cần đúng {g.SoLuong:0.#####}.");
        }

        // Mã hàng thật bị trừ kho: nguyên liệu (dòng món ăn) hoặc chính thành phẩm.
        string MaHang(PhanBoLoRequest p) => p.MaNguyenLieu ?? dongTheoId[p.DongId].MaThanhPham;

        // Tổng lấy từ mỗi lô không vượt tồn thực của lô đó.
        var tonLo = goiY.SelectMany(g => g.Lo.Select(x => (g.MaThanhPham, x.MaLo, x.HanSuDung, x.Ton)))
            .DistinctBy(x => (x.MaThanhPham, x.MaLo))
            .ToDictionary(x => (x.MaThanhPham, x.MaLo), x => (x.HanSuDung, x.Ton));
        foreach (var nhom in chot.GroupBy(p => (MaHang(p), p.MaLo)))
        {
            var can = nhom.Sum(p => p.SoLuong);
            var ton = tonLo.TryGetValue(nhom.Key, out var t) ? t.Ton : 0;
            if (can > ton)
                return KetQuaThaoTac.Loi($"Lô \"{nhom.Key.MaLo}\" của \"{nhom.Key.Item1}\" chỉ còn {ton:0.###}, "
                                         + $"không xuất được {can:0.###}.");
        }

        // Đơn nguồn HanoiCheck: luôn đẩy "process" (khai nguồn hàng từng dòng - hàng đã thật sự rời kho) ngay khi
        // xuất kho; CHỈ đẩy tiếp "status" (Đang giao) nếu ĐÃ có người giao - HnC bắt buộc đơn phải có người giao
        // mới cho chuyển "Đang giao" (422 "Đơn đã có người giao hàng" nếu chưa - xem mục l đặc tả). Chưa có người
        // giao thì dừng ở "process": nguồn hàng vẫn được khai đúng lúc, chỉ chưa đổi trạng thái bên HnC - đơn nội
        // bộ chuyển "Chờ giao hàng", đợi nhân viên tự nhận (NhanDonAsync) mới đẩy nốt người giao + trạng thái.
        // Lỗi thật ở 1 trong 2 bước thì dừng hẳn, giữ nguyên "Đã xác nhận" để NCC bấm lại (process ghi đè nên gọi
        // lại an toàn).
        if (don.Nguon == NguonDonHang.HanoiCheck && don.MaDonHnC is not null && _db.TenantInfo?.Id is { } tenantId)
        {
            var ketXuLy = await _orderCommandClient.XuLyDonAsync(tenantId, don.MaDonHnC, new ProcessOrderRequest
            {
                MaNguoiGiao = maNguoiGiao,
                GhiChu = ghiChu,
                DanhSachAnh = anhTongQuan?.Select(a => a.DuongDan).Take(SoAnhGuiHnC).ToList(),
                ChiTiet = ChiTietXuLy(chot, dongTheoId, don.MaKho)
            }, ct);
            if (!ketXuLy.ThanhCong && !ketXuLy.ChuaCauHinh)
                return KetQuaThaoTac.Loi($"Không đẩy được xử lý đơn sang HanoiCheck: {ketXuLy.ThongBao}");

            if (ketXuLy.ThanhCong && maNguoiGiao is not null)
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
            var maHang = MaHang(p);
            var hsd = tonLo[(maHang, p.MaLo)].HanSuDung;
            _db.KhoGiaoDichs.Add(new KhoGiaoDich
            {
                MaSanPham = maHang, MaKho = don.MaKho, MaLo = p.MaLo, SoLuong = -p.SoLuong,
                HanSuDung = hsd, Loai = LoaiGiaoDichKho.XuatBan, ChungTu = don.MaDonHang, ThoiGianUtc = now,
                GhiChu = dong.LaMonAn ? $"Nguyên liệu món {dong.MaThanhPham} × {dong.SoLuong:0.###} suất" : null
            });
            dong.XuatLo.Add(new DonHangBanXuatLo { MaNguyenLieu = p.MaNguyenLieu, MaLo = p.MaLo, HanSuDung = hsd, SoLuong = p.SoLuong });
        }

        don.MaNguoiGiao = maNguoiGiao;
        don.GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? don.GhiChu : ghiChu.Trim();
        if (anhTongQuan is not null)
        {
            // Chỉ thay ảnh tổng quan; ảnh chứng minh đã giao (nếu có) giữ nguyên.
            _db.DonHangBanAnhs.RemoveRange(don.AnhTongQuan.Where(a => a.Loai == LoaiAnhDonHang.TongQuan).ToList());
            foreach (var a in anhTongQuan)
            {
                don.AnhTongQuan.Add(new DonHangBanAnh
                {
                    TenAnh = a.TenAnh, DuongDan = a.DuongDan, Loai = LoaiAnhDonHang.TongQuan
                });
            }
        }
        don.TrangThai = maNguoiGiao is null ? TrangThaiDonHangBan.ChoGiaoHang : TrangThaiDonHangBan.DangGiao;
        don.ThoiGianXuatKhoUtc = now;
        await _db.SaveChangesAsync(ct);

        if (_db.TenantInfo?.Id is { } coSoId)
        {
            var (tieuDe, noiDung) = maNguoiGiao is null
                ? ("Đơn chờ nhận giao", $"Đơn {don.MaDonHang} đã xuất kho, đang chờ nhận giao.")
                : ("Đơn sẵn sàng giao", $"Đơn {don.MaDonHang} sẵn sàng giao.");
            await _push.GuiTheoQuyenAsync(coSoId, AppRoles.QuyenGiaoHang.Split(','), tieuDe, noiDung, DuLieuThongBao(don.Id), ct);
        }

        var soLo = chot.Select(p => p.MaLo).Distinct().Count();
        return KetQuaThaoTac.Ok(maNguoiGiao is null
            ? $"Đã xuất kho đơn \"{don.MaDonHang}\" ({soLo} lô), chờ nhân viên nhận giao."
            : $"Đã xuất kho đơn \"{don.MaDonHang}\" ({soLo} lô), chuyển sang Đang giao.");
    }

    /// <summary>
    /// chi_tiet của "process". Dòng thực phẩm: phan_bo (lô + kho + số lượng). Dòng suất ăn/thực đơn: nguyen_lieu, mỗi
    /// nguyên liệu kèm phan_bo riêng (dish_ingredient_fulfillment). Dòng định danh bằng trace_code; đơn kéo về trước khi
    /// có mã truy vết thì dùng ma_thuc_pham / ma_mon_an (gộp theo mã để mỗi mã chỉ một dòng, như đặc tả yêu cầu).
    /// </summary>
    private static List<ProcessOrderLine> ChiTietXuLy(IEnumerable<PhanBoLoRequest> chot,
                                                      IReadOnlyDictionary<int, DonHangBanDong> dongTheoId, string maKho) =>
        chot.GroupBy(p =>
            {
                var d = dongTheoId[p.DongId];
                return (TruyVet: d.MaTruyVetHnC, Ma: d.MaTruyVetHnC is null ? d.MaThanhPham : null, d.LaMonAn);
            })
            .Select(g => g.Key.LaMonAn
                ? new ProcessOrderLine
                {
                    TraceCode = g.Key.TruyVet,
                    MaMonAn = g.Key.Ma,
                    NguyenLieu = g.GroupBy(p => p.MaNguyenLieu!)
                        .Select(n => new ProcessOrderIngredient(n.Key,
                            n.Select(p => new ProcessOrderAllocation(p.MaLo, maKho, p.SoLuong)).ToList()))
                        .ToList()
                }
                : new ProcessOrderLine
                {
                    TraceCode = g.Key.TruyVet,
                    MaThucPham = g.Key.Ma,
                    PhanBo = g.Select(p => new ProcessOrderAllocation(p.MaLo, maKho, p.SoLuong)).ToList()
                })
            .ToList();

    /// <summary>Số ảnh tối đa cho một đơn (upload + lưu nội bộ làm bằng chứng).</summary>
    public const int SoAnhToiDa = 10;

    /// <summary>Số ảnh tối đa gửi sang HanoiCheck cho một đơn (danh_sach_anh): API HnC chỉ nhận 1-3 ảnh,
    /// nên dù lưu nhiều hơn vẫn chỉ gửi 3 ảnh đầu.</summary>
    public const int SoAnhGuiHnC = 3;

    public async Task<KetQuaThaoTac> HoanTatGiaoAsync(int id, IReadOnlyList<AnhDauVao> anhGiao,
                                                      CancellationToken ct = default)
    {
        var don = await QueryDayDu().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (don is null) return KetQuaThaoTac.Loi("Không tìm thấy đơn hàng.");
        if (don.TrangThai != TrangThaiDonHangBan.DangGiao)
            return KetQuaThaoTac.Loi("Chỉ xác nhận đã giao cho đơn đang giao.");

        var anh = (anhGiao ?? Array.Empty<AnhDauVao>())
            .Where(a => !string.IsNullOrWhiteSpace(a.DuongDan)).Take(SoAnhToiDa).ToList();
        if (anh.Count == 0) return KetQuaThaoTac.Loi("Cần ít nhất 1 ảnh chứng minh đã giao hàng.");

        // Đơn từ trường: HanoiCheck bắt buộc đơn có 1-3 ảnh tổng quan trước khi chuyển "Đã giao" (API status không
        // nhận ảnh), nên gửi ảnh qua "process" trước - ưu tiên ảnh giao, thiếu chỗ thì bù ảnh lúc xuất kho.
        // Lỗi thật thì dừng, giữ nguyên "Đang giao" để bấm lại (process ghi đè nên gọi lại an toàn).
        if (don.Nguon == NguonDonHang.HanoiCheck && don.MaDonHnC is not null && _db.TenantInfo?.Id is { } tenantId)
        {
            var duongDanAnh = anh.Select(a => a.DuongDan)
                .Concat(don.AnhTongQuan.Where(a => a.Loai == LoaiAnhDonHang.TongQuan).Select(a => a.DuongDan))
                .Distinct().Take(SoAnhGuiHnC).ToList();

            var ketAnh = await _orderCommandClient.XuLyDonAsync(tenantId, don.MaDonHnC, new ProcessOrderRequest
            {
                MaNguoiGiao = don.MaNguoiGiao,   // gửi null là HnC hiểu "bỏ người giao" nên gửi lại người hiện tại
                GhiChu = don.GhiChu,
                DanhSachAnh = duongDanAnh
            }, ct);
            if (!ketAnh.ThanhCong && !ketAnh.ChuaCauHinh)
                return KetQuaThaoTac.Loi($"Không gửi được ảnh giao hàng sang HanoiCheck: {ketAnh.ThongBao}");

            if (ketAnh.ThanhCong)
            {
                var ketTrangThai = await _orderCommandClient.DoiTrangThaiAsync(tenantId, don.MaDonHnC, "DA_GIAO", null, ct);
                if (!ketTrangThai.ThanhCong)
                    return KetQuaThaoTac.Loi($"Không đẩy được trạng thái \"Đã giao\" sang HanoiCheck: {ketTrangThai.ThongBao}");
            }
        }

        // Ảnh giao thay thế ảnh giao của lần trước (nếu bấm lại), giữ nguyên ảnh tổng quan lúc xuất kho.
        _db.DonHangBanAnhs.RemoveRange(don.AnhTongQuan.Where(a => a.Loai == LoaiAnhDonHang.Giao).ToList());
        foreach (var a in anh)
            don.AnhTongQuan.Add(new DonHangBanAnh { TenAnh = a.TenAnh, DuongDan = a.DuongDan, Loai = LoaiAnhDonHang.Giao });

        don.TrangThai = TrangThaiDonHangBan.DaGiao;
        don.ThoiGianGiaoUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (_db.TenantInfo?.Id is { } coSoId)
            await _push.GuiTheoQuyenAsync(coSoId, AppRoles.QuyenNhapLieu.Split(','), "Đơn đã giao xong",
                $"Đơn {don.MaDonHang} đã giao xong.", DuLieuThongBao(don.Id), ct);

        return KetQuaThaoTac.Ok($"Đơn \"{don.MaDonHang}\" đã giao ({anh.Count} ảnh).");
    }

    public async Task<KetQuaThaoTac> NhanDonAsync(int id, string maNhanSu, CancellationToken ct = default)
    {
        var don = await QueryDayDu().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (don is null) return KetQuaThaoTac.Loi("Không tìm thấy đơn hàng.");
        if (don.TrangThai != TrangThaiDonHangBan.ChoGiaoHang)
        {
            if (don.TrangThai is TrangThaiDonHangBan.ChoXacNhan or TrangThaiDonHangBan.DaXacNhan)
                return KetQuaThaoTac.Loi("Đơn chưa xuất kho - bộ phận kho xuất hàng xong bạn mới nhận được.");
            if (don.TrangThai == TrangThaiDonHangBan.DangGiao)
            {
                if (string.Equals(don.MaNguoiGiao, maNhanSu, StringComparison.OrdinalIgnoreCase))
                    return KetQuaThaoTac.Ok($"Bạn đang giao đơn \"{don.MaDonHang}\".");
                var maCu = don.MaNguoiGiao;
                var tenCu = await _db.Staff.AsNoTracking().Where(s => s.MaNhanSu == maCu)
                    .Select(s => s.HoTen).FirstOrDefaultAsync(ct);
                return KetQuaThaoTac.Loi($"Đơn này do {tenCu ?? maCu} nhận rồi. Cần đổi người giao thì nhờ quản trị sửa trên web.");
            }
            return KetQuaThaoTac.Loi("Đơn này không còn cần giao.");
        }

        var nhanSu = await _db.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.MaNhanSu == maNhanSu, ct);
        if (nhanSu is null) return KetQuaThaoTac.Loi("Tài khoản chưa gắn với hồ sơ nhân sự của cơ sở.");
        if (!nhanSu.TrangThai) return KetQuaThaoTac.Loi("Hồ sơ nhân sự của bạn đang ở trạng thái nghỉ.");

        // Đơn từ trường: báo người giao mới rồi đẩy trạng thái "Đang giao" sang HanoiCheck TRƯỚC khi lưu nội bộ -
        // lỗi thật thì không nhận, giữ nguyên "Chờ giao hàng" để bấm lại (process ghi đè nên gọi lại an toàn).
        if (don.Nguon == NguonDonHang.HanoiCheck && don.MaDonHnC is not null && _db.TenantInfo?.Id is { } tenantId)
        {
            var ket = await _orderCommandClient.XuLyDonAsync(tenantId, don.MaDonHnC, new ProcessOrderRequest
            {
                MaNguoiGiao = maNhanSu,
                GhiChu = don.GhiChu
            }, ct);
            if (!ket.ThanhCong && !ket.ChuaCauHinh)
                return KetQuaThaoTac.Loi($"Không báo được người giao sang HanoiCheck: {ket.ThongBao}");

            if (ket.ThanhCong)
            {
                var ketTrangThai = await _orderCommandClient.DoiTrangThaiAsync(tenantId, don.MaDonHnC, "DANG_GIAO", null, ct);
                if (!ketTrangThai.ThanhCong)
                    return KetQuaThaoTac.Loi($"Không đẩy được trạng thái \"Đang giao\" sang HanoiCheck: {ketTrangThai.ThongBao}");
            }
        }

        don.MaNguoiGiao = maNhanSu;
        don.TrangThai = TrangThaiDonHangBan.DangGiao;
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Bạn đã nhận giao đơn \"{don.MaDonHang}\".");
    }

    public async Task<DonHangBan?> TimTheoQrAsync(string noiDung, CancellationToken ct = default)
    {
        var s = (noiDung ?? "").Trim();
        if (s.Length == 0) return null;

        // QR tra cứu của hệ thống: {tên miền}/tra-cuu/don-hang/{mã tra cứu}
        var vt = s.IndexOf("/tra-cuu/don-hang/", StringComparison.OrdinalIgnoreCase);
        if (vt >= 0)
        {
            var ma = DoanCuoi(s[(vt + "/tra-cuu/don-hang/".Length)..]);
            return await QueryDayDu().FirstOrDefaultAsync(d => d.MaTraCuu == ma, ct);
        }

        // QR truy xuất của HanoiCheck: .../truy-xuat/{mã đơn HanoiCheck}
        vt = s.IndexOf("/truy-xuat/", StringComparison.OrdinalIgnoreCase);
        if (vt >= 0)
        {
            var ma = DoanCuoi(s[(vt + "/truy-xuat/".Length)..]);
            return await QueryDayDu().FirstOrDefaultAsync(d => d.MaDonHnC == ma, ct);
        }

        if (s.Contains("://")) return null;   // link khác (vd QR của lô sản xuất) - không phải đơn hàng
        var maDon = s.StartsWith("DH:", StringComparison.OrdinalIgnoreCase) ? s[3..].Trim() : s;
        return await QueryDayDu().FirstOrDefaultAsync(d => d.MaDonHang == maDon, ct);
    }

    /// <summary>Đoạn cuối của đường dẫn (bỏ query/fragment) - chính là mã trong QR.</summary>
    private static string DoanCuoi(string duongDan)
    {
        var ma = duongDan.Split('?', '#')[0].TrimEnd('/');
        var vt = ma.LastIndexOf('/');
        return vt >= 0 ? ma[(vt + 1)..] : ma;
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
        var traVeKho = don.TrangThai is TrangThaiDonHangBan.DangGiao or TrangThaiDonHangBan.ChoGiaoHang;
        if (traVeKho)
        {
            // Đảo đúng các lô đã xuất (giữ lô + hạn dùng) để tồn và FEFO về như trước khi xuất.
            foreach (var l in don.Dong)
            foreach (var x in l.XuatLo)
            {
                _db.KhoGiaoDichs.Add(new KhoGiaoDich
                {
                    MaSanPham = x.MaNguyenLieu ?? l.MaThanhPham, MaKho = don.MaKho, MaLo = x.MaLo, SoLuong = x.SoLuong,
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
