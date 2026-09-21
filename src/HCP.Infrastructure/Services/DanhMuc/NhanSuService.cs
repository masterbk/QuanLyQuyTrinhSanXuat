using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>
/// Quản lý Nhân sự của cơ sở đang đăng nhập.
/// Đồng bộ sang HanoiCheck qua POST /supplier/users/merge.
///
/// CCCD được mã hoá khi lưu (dữ liệu cá nhân nhạy cảm): service tự mã hoá lúc ghi và giải mã
/// lúc đọc, các tầng khác chỉ làm việc với Staff.Cccd ở dạng plaintext trong bộ nhớ.
/// </summary>
public class NhanSuService : IDanhMucService<Staff>
{
    private readonly AppDbContext _db;
    private readonly ISyncOutboxWriter _outbox;
    private readonly ISecretProtector _protector;

    public NhanSuService(AppDbContext db, ISyncOutboxWriter outbox, ISecretProtector protector)
    {
        _db = db;
        _outbox = outbox;
        _protector = protector;
    }

    public async Task<IReadOnlyList<Staff>> LayTatCaAsync(CancellationToken ct = default)
    {
        var ds = await _db.Staff.AsNoTracking().OrderBy(s => s.MaNhanSu).ToListAsync(ct);
        foreach (var s in ds) GiaiMaCccd(s);
        return ds;
    }

    public async Task<Staff?> LayTheoIdAsync(int id, CancellationToken ct = default)
    {
        var s = await _db.Staff.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (s is not null) GiaiMaCccd(s);
        return s;
    }

    public async Task<KetQuaThaoTac> ThemAsync(Staff entity, CancellationToken ct = default)
    {
        entity.MaNhanSu = entity.MaNhanSu.Trim();

        if (await _db.Staff.AnyAsync(s => s.MaNhanSu == entity.MaNhanSu, ct))
        {
            return KetQuaThaoTac.Loi($"Mã nhân sự \"{entity.MaNhanSu}\" đã tồn tại.");
        }

        var loi = KiemTraDuLieu(entity);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        ChuanHoa(entity);
        MaHoaCccd(entity);

        _db.Staff.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dongBo = await _outbox.GuiAsync(entity, ct);

        return KetQuaThaoTac.Ok($"Đã thêm nhân sự \"{entity.HoTen}\".").KemGhiChu(dongBo);
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(Staff entity, CancellationToken ct = default)
    {
        var hienTai = await _db.Staff.FirstOrDefaultAsync(s => s.Id == entity.Id, ct);
        if (hienTai is null) return KetQuaThaoTac.Loi("Không tìm thấy nhân sự cần sửa.");

        var maMoi = entity.MaNhanSu.Trim();

        if (await _db.Staff.AnyAsync(s => s.MaNhanSu == maMoi && s.Id != entity.Id, ct))
        {
            return KetQuaThaoTac.Loi($"Mã nhân sự \"{maMoi}\" đã được dùng cho người khác.");
        }

        var loi = KiemTraDuLieu(entity);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        ChuanHoa(entity);

        hienTai.MaNhanSu = maMoi;
        hienTai.HoTen = entity.HoTen;
        hienTai.ViTri = entity.ViTri;
        hienTai.NgaySinh = entity.NgaySinh;
        hienTai.DiaChi = entity.DiaChi;
        hienTai.DienThoai = entity.DienThoai;
        hienTai.LaChuCoSo = entity.LaChuCoSo;
        hienTai.LaNguoiCheBien = entity.LaNguoiCheBien;
        hienTai.LaNguoiGiaoHang = entity.LaNguoiGiaoHang;
        hienTai.PhuongTien = entity.PhuongTien;
        hienTai.BienSo = entity.BienSo;
        hienTai.TrangThai = entity.TrangThai;

        hienTai.KskSoGiay = entity.KskSoGiay;
        hienTai.KskNgayKham = entity.KskNgayKham;
        hienTai.KskNgayHetHan = entity.KskNgayHetHan;
        hienTai.KskNoiKham = entity.KskNoiKham;

        hienTai.AttpSoChungNhan = entity.AttpSoChungNhan;
        hienTai.AttpNgayCap = entity.AttpNgayCap;
        hienTai.AttpCoQuanCap = entity.AttpCoQuanCap;

        hienTai.Cccd = entity.Cccd;      // giữ plaintext cho payload
        MaHoaCccd(hienTai);              // đồng thời cập nhật bản mã hoá lưu DB
        hienTai.DongBoHnC = entity.DongBoHnC;
        hienTai.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var dongBo = await _outbox.GuiAsync(hienTai, ct);

        return KetQuaThaoTac.Ok($"Đã cập nhật nhân sự \"{hienTai.HoTen}\".").KemGhiChu(dongBo);
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var ns = await _db.Staff.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (ns is null) return KetQuaThaoTac.Loi("Không tìm thấy nhân sự cần xoá.");

        var loi = await KiemTraDangThamChieuAsync(ns.MaNhanSu, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        _db.Staff.Remove(ns);
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã xoá nhân sự \"{ns.HoTen}\".");
    }

    /// <summary>
    /// Chặn xoá nhân sự đang được tham chiếu bằng mã (không phải khoá ngoại nên CSDL không tự chặn) - giữ
    /// toàn vẹn lịch sử người giao/người thực hiện, giống cách KhachHangService chặn xoá khi đã có đơn hàng.
    /// Các trường "...Csv" lưu nhiều mã cách nhau bằng dấu phẩy: lọc thô bằng Contains ở CSDL rồi tách chuỗi
    /// so khớp đúng token trong bộ nhớ, tránh khớp nhầm (VD "NS1" khớp nhầm "NS10").
    /// </summary>
    private async Task<string?> KiemTraDangThamChieuAsync(string maNhanSu, CancellationToken ct)
    {
        const string goiY = " Cho nhân sự này \"nghỉ việc\" thay vì xoá để giữ đúng dữ liệu đã có.";

        if (await _db.DonHangBans.AnyAsync(d => d.MaNguoiGiao == maNhanSu, ct))
            return "Không xoá được: nhân sự đang là người giao của đơn hàng bán." + goiY;
        if (await _db.DonHangNhans.AnyAsync(d => d.MaNguoiGiao == maNhanSu, ct))
            return "Không xoá được: nhân sự đang là người giao của đơn hàng từ trường." + goiY;
        if (await _db.LenhSanXuatThamGias.AnyAsync(t => t.MaNhanSu == maNhanSu, ct))
            return "Không xoá được: nhân sự đã tham gia lệnh sản xuất." + goiY;
        if (await CoTrongCsvAsync(_db.LenhSanXuatKhaus.Select(k => k.NguoiThucHienCsv), maNhanSu, ct))
            return "Không xoá được: nhân sự đang là người thực hiện một khâu của lệnh sản xuất." + goiY;
        if (await CoTrongCsvAsync(_db.BatchSteps.Select(s => s.NguoiThucHienCsv), maNhanSu, ct))
            return "Không xoá được: nhân sự đang là người thực hiện một bước của lô sản xuất." + goiY;
        if (await CoTrongCsvAsync(_db.DishSteps.Select(s => s.NguoiThucHienCsv), maNhanSu, ct))
            return "Không xoá được: nhân sự đang là người thực hiện một bước chế biến món ăn." + goiY;
        return null;
    }

    private static async Task<bool> CoTrongCsvAsync(IQueryable<string?> cotCsv, string maNhanSu, CancellationToken ct)
    {
        var ungVien = await cotCsv.Where(c => c != null && c.Contains(maNhanSu)).ToListAsync(ct);
        return ungVien.Any(c => (c ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(maNhanSu, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ràng buộc đặc tả: giấy khám sức khoẻ nếu khai phải đủ số giấy + ngày khám + ngày hết hạn;
    /// chứng nhận tập huấn ATTP nếu khai phải đủ số chứng nhận + ngày cấp.
    /// Kiểm tra tại đây để người dùng sửa ngay thay vì chờ HanoiCheck trả 422.
    /// </summary>
    private static string? KiemTraDuLieu(Staff s)
    {
        if (string.IsNullOrWhiteSpace(s.MaNhanSu)) return "Vui lòng nhập mã nhân sự.";
        if (string.IsNullOrWhiteSpace(s.HoTen)) return "Vui lòng nhập họ tên.";

        if (s.CoGiayKhamSucKhoe)
        {
            if (string.IsNullOrWhiteSpace(s.KskSoGiay) || !s.KskNgayKham.HasValue || !s.KskNgayHetHan.HasValue)
            {
                return "Giấy khám sức khoẻ đã khai thì phải nhập đủ số giấy, ngày khám và ngày hết hạn.";
            }
            if (s.KskNgayHetHan < s.KskNgayKham)
            {
                return "Ngày hết hạn giấy khám sức khoẻ không được trước ngày khám.";
            }
        }

        if (s.CoChungNhanAttp)
        {
            if (string.IsNullOrWhiteSpace(s.AttpSoChungNhan) || !s.AttpNgayCap.HasValue)
            {
                return "Chứng nhận tập huấn ATTP đã khai thì phải nhập đủ số chứng nhận và ngày cấp.";
            }
        }

        return null;
    }

    private static void ChuanHoa(Staff s)
    {
        s.HoTen = s.HoTen.Trim();
        s.ViTri = s.ViTri?.Trim();
        s.DiaChi = s.DiaChi?.Trim();
        s.DienThoai = s.DienThoai?.Trim();
        s.Cccd = s.Cccd?.Trim();
        s.PhuongTien = s.PhuongTien?.Trim();
        s.BienSo = s.BienSo?.Trim();
        s.KskSoGiay = s.KskSoGiay?.Trim();
        s.KskNoiKham = s.KskNoiKham?.Trim();
        s.AttpSoChungNhan = s.AttpSoChungNhan?.Trim();
        s.AttpCoQuanCap = s.AttpCoQuanCap?.Trim();
    }

    private void MaHoaCccd(Staff s) =>
        s.CccdEncrypted = string.IsNullOrWhiteSpace(s.Cccd) ? string.Empty : _protector.Protect(s.Cccd);

    private void GiaiMaCccd(Staff s) =>
        s.Cccd = string.IsNullOrEmpty(s.CccdEncrypted) ? null : _protector.TryUnprotect(s.CccdEncrypted);
}
