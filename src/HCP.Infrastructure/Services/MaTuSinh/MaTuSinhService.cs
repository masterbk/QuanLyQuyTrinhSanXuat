using HCP.Domain;
using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.MaTuSinh;

/// <summary>Các loại mã hệ thống tự sinh khi tạo mới.</summary>
public enum LoaiMaTuSinh
{
    /// <summary>CS-0001</summary>
    CoSo,

    /// <summary>KHAU-0001</summary>
    Khau,

    /// <summary>NCC-0001</summary>
    NccDauVao,

    /// <summary>LO-yyyyMMdd-001 - dùng chung cho Lô sản xuất và lô thành phẩm trong lệnh sản xuất.</summary>
    LoSanXuat,

    /// <summary>LSX-yyyyMMdd-001</summary>
    LenhSanXuat,

    /// <summary>DH-yyyyMMdd-001 - đơn hàng bán.</summary>
    DonHangBan,

    /// <summary>KH-0001 - khách hàng tự tạo (vd trường học từ đơn HanoiCheck).</summary>
    KhachHang
}

/// <summary>
/// Sinh mã nghiệp vụ theo format cố định, đánh số riêng cho từng cơ sở.
///
/// LƯU Ý: hàm sinh tự SaveChanges bộ đếm, nên phải gọi TRƯỚC khi Add/sửa entity nghiệp vụ vào
/// DbContext (và sau khi đã kiểm tra dữ liệu hợp lệ, để không phí số khi dữ liệu bị từ chối).
/// </summary>
public interface IMaTuSinhService
{
    /// <param name="ngay">Ngày gắn vào mã lô/lệnh; bỏ trống = hôm nay theo giờ Việt Nam. Danh mục bỏ qua tham số này.</param>
    Task<string> SinhAsync(LoaiMaTuSinh loai, DateOnly? ngay = null, CancellationToken ct = default);

    /// <summary>Cấp liền một lúc <paramref name="soLuong"/> mã liên tiếp (vd nhiều lô trong một lệnh).</summary>
    Task<IReadOnlyList<string>> SinhNhieuAsync(LoaiMaTuSinh loai, int soLuong, DateOnly? ngay = null,
                                               CancellationToken ct = default);
}

/// <inheritdoc cref="IMaTuSinhService"/>
public sealed class MaTuSinhService : IMaTuSinhService
{
    private const int SoLanThuLai = 5;

    private readonly AppDbContext _db;

    public MaTuSinhService(AppDbContext db) => _db = db;

    /// <summary>Hôm nay theo giờ Việt Nam (UTC+7, không có giờ mùa hè) - máy chủ có thể chạy giờ UTC.</summary>
    public static DateOnly HomNay => GioVietNam.HomNay;

    /// <summary>Tiền tố đầy đủ (kể cả dấu gạch cuối) và số chữ số của phần đánh số.</summary>
    public static (string TienTo, int DoRong) DinhDang(LoaiMaTuSinh loai, DateOnly ngay) => loai switch
    {
        LoaiMaTuSinh.CoSo => ("CS-", 4),
        LoaiMaTuSinh.Khau => ("KHAU-", 4),
        LoaiMaTuSinh.NccDauVao => ("NCC-", 4),
        LoaiMaTuSinh.LoSanXuat => ($"LO-{ngay:yyyyMMdd}-", 3),
        LoaiMaTuSinh.LenhSanXuat => ($"LSX-{ngay:yyyyMMdd}-", 3),
        LoaiMaTuSinh.DonHangBan => ($"DH-{ngay:yyyyMMdd}-", 3),
        LoaiMaTuSinh.KhachHang => ("KH-", 4),
        _ => throw new ArgumentOutOfRangeException(nameof(loai))
    };

    public async Task<string> SinhAsync(LoaiMaTuSinh loai, DateOnly? ngay = null, CancellationToken ct = default) =>
        (await SinhNhieuAsync(loai, 1, ngay, ct))[0];

    public async Task<IReadOnlyList<string>> SinhNhieuAsync(LoaiMaTuSinh loai, int soLuong, DateOnly? ngay = null,
                                                            CancellationToken ct = default)
    {
        if (soLuong <= 0) return Array.Empty<string>();

        var (tienTo, doRong) = DinhDang(loai, ngay ?? HomNay);
        var khoa = tienTo.TrimEnd('-');

        for (var lan = 0; lan < SoLanThuLai; lan++)
        {
            var dem = await _db.BoDemMas.FirstOrDefaultAsync(b => b.Khoa == khoa, ct);
            if (dem is null)
            {
                // Dãy mới: bắt đầu sau số lớn nhất ĐANG CÓ cùng format (vd mã gõ tay trước đây),
                // để không bao giờ sinh ra mã trùng dữ liệu cũ.
                dem = new BoDemMa { Khoa = khoa, GiaTri = await SoLonNhatDangCoAsync(loai, tienTo, ct) };
                _db.BoDemMas.Add(dem);
            }

            var batDau = dem.GiaTri + 1;
            dem.GiaTri += soLuong;

            try
            {
                await _db.SaveChangesAsync(ct);
                return Enumerable.Range(batDau, soLuong)
                    .Select(n => tienTo + n.ToString($"D{doRong}"))
                    .ToList();
            }
            catch (DbUpdateException)
            {
                // Người khác vừa cấp số (concurrency) hoặc vừa tạo cùng dãy (trùng khoá): đọc lại rồi thử lần nữa.
                _db.Entry(dem).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException("Không sinh được mã do nhiều người thao tác cùng lúc. Vui lòng thử lại.");
    }

    private async Task<int> SoLonNhatDangCoAsync(LoaiMaTuSinh loai, string tienTo, CancellationToken ct)
    {
        List<string> ma = loai switch
        {
            LoaiMaTuSinh.CoSo => await _db.Facilities.Where(x => x.MaCoSo.StartsWith(tienTo))
                .Select(x => x.MaCoSo).ToListAsync(ct),
            LoaiMaTuSinh.Khau => await _db.ProductionSteps.Where(x => x.MaKhau.StartsWith(tienTo))
                .Select(x => x.MaKhau).ToListAsync(ct),
            LoaiMaTuSinh.NccDauVao => await _db.SubSuppliers.Where(x => x.MaNccDauVao.StartsWith(tienTo))
                .Select(x => x.MaNccDauVao).ToListAsync(ct),
            LoaiMaTuSinh.KhachHang => await _db.KhachHangs.Where(x => x.MaKhachHang.StartsWith(tienTo))
                .Select(x => x.MaKhachHang).ToListAsync(ct),
            LoaiMaTuSinh.DonHangBan => await _db.DonHangBans.Where(x => x.MaDonHang.StartsWith(tienTo))
                .Select(x => x.MaDonHang).ToListAsync(ct),
            LoaiMaTuSinh.LenhSanXuat => await _db.LenhSanXuats.Where(x => x.MaLenh.StartsWith(tienTo))
                .Select(x => x.MaLenh).ToListAsync(ct),
            // Lô: một dãy chung cho Lô sản xuất, lô thành phẩm trong lệnh và mọi lô trong sổ kho.
            LoaiMaTuSinh.LoSanXuat =>
            [
                .. await _db.Batches.Where(x => x.MaLo.StartsWith(tienTo)).Select(x => x.MaLo).ToListAsync(ct),
                .. await _db.LenhSanXuatSanPhams.Where(x => x.MaLoThanhPham.StartsWith(tienTo))
                    .Select(x => x.MaLoThanhPham).ToListAsync(ct),
                .. await _db.KhoGiaoDichs.Where(x => x.MaLo.StartsWith(tienTo)).Select(x => x.MaLo)
                    .Distinct().ToListAsync(ct)
            ],
            _ => new List<string>()
        };

        return ma.Select(m => int.TryParse(m[tienTo.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
    }
}
