using System.Text.RegularExpressions;
using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>
/// Quản lý Thực đơn theo thứ trong tuần của cơ sở. Đồng bộ sang HanoiCheck qua POST /supplier/menus/merge
/// (đặc tả v2.6, dịch vụ 10).
/// </summary>
public class ThucDonService : IDanhMucService<Menu>
{
    /// <summary>Đặc tả Phần III mục 4: mã nghiệp vụ chỉ gồm chữ cái, số, "_" và "-".</summary>
    private static readonly Regex MaHopLe = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly ISyncOutboxWriter _outbox;

    public ThucDonService(AppDbContext db, ISyncOutboxWriter outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task<IReadOnlyList<Menu>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.Menus.AsNoTracking().Include(m => m.DanhSachMon)
            .OrderBy(m => m.NhomTuoiId).ThenBy(m => m.ThuApDung == 0 ? 7 : m.ThuApDung).ThenBy(m => m.MaThucDon)
            .ToListAsync(ct);

    public Task<Menu?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        _db.Menus.Include(m => m.DanhSachMon).FirstOrDefaultAsync(m => m.Id == id, ct);

    /// <inheritdoc />
    public Task<Menu?> LayDeSuaAsync(int id, CancellationToken ct = default) =>
        _db.Menus.AsNoTracking().Include(m => m.DanhSachMon).FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<KetQuaThaoTac> ThemAsync(Menu entity, CancellationToken ct = default)
    {
        ChuanHoa(entity);

        if (await _db.Menus.AnyAsync(m => m.MaThucDon == entity.MaThucDon, ct))
            return KetQuaThaoTac.Loi($"Mã thực đơn \"{entity.MaThucDon}\" đã tồn tại.");

        var loi = await KiemTraAsync(entity, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        entity.DanhSachMon = entity.DanhSachMon.Select(SaoChep).ToList();
        _db.Menus.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dongBo = await _outbox.GuiAsync(entity, ct);
        return KetQuaThaoTac.Ok($"Đã thêm thực đơn \"{entity.TenThucDon}\".").KemGhiChu(dongBo);
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(Menu entity, CancellationToken ct = default)
    {
        var hienTai = await LayTheoIdAsync(entity.Id, ct);
        if (hienTai is null) return KetQuaThaoTac.Loi("Không tìm thấy thực đơn cần sửa.");

        ChuanHoa(entity);

        if (await _db.Menus.AnyAsync(m => m.MaThucDon == entity.MaThucDon && m.Id != entity.Id, ct))
            return KetQuaThaoTac.Loi($"Mã thực đơn \"{entity.MaThucDon}\" đã được dùng cho thực đơn khác.");

        var loi = await KiemTraAsync(entity, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        hienTai.MaThucDon = entity.MaThucDon;
        hienTai.TenThucDon = entity.TenThucDon;
        hienTai.NhomTuoiId = entity.NhomTuoiId;
        hienTai.ThuApDung = entity.ThuApDung;
        hienTai.TrangThai = entity.TrangThai;
        hienTai.DanhSachTruongCsv = entity.DanhSachTruongCsv;
        hienTai.DongBoHnC = entity.DongBoHnC;
        hienTai.UpdatedAtUtc = DateTime.UtcNow;

        _db.MenuItems.RemoveRange(hienTai.DanhSachMon);
        hienTai.DanhSachMon = entity.DanhSachMon.Select(SaoChep).ToList();

        await _db.SaveChangesAsync(ct);

        var dongBo = await _outbox.GuiAsync(hienTai, ct);
        return KetQuaThaoTac.Ok($"Đã cập nhật thực đơn \"{hienTai.TenThucDon}\".").KemGhiChu(dongBo);
    }

    /// <summary>
    /// HanoiCheck không có dịch vụ xoá thực đơn - thực đơn không còn dùng phải gửi trang_thai = false.
    /// Vì vậy trước khi xoá ở phần mềm, gửi bản "ngừng áp dụng" để trường không còn thấy thực đơn này.
    /// </summary>
    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var td = await LayTheoIdAsync(id, ct);
        if (td is null) return KetQuaThaoTac.Loi("Không tìm thấy thực đơn cần xoá.");

        string? ghiChu = null;
        if (td.DongBoHnC && td.TrangThai && await _outbox.DangBatAsync(ct))
        {
            td.TrangThai = false;
            await _outbox.GuiAsync(td, ct);
            ghiChu = "Đã gửi HanoiCheck ngừng áp dụng thực đơn này.";
        }

        _db.Menus.Remove(td); // dòng món xoá theo cascade.
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã xoá thực đơn \"{td.TenThucDon}\".").KemGhiChu(ghiChu);
    }

    /// <summary>
    /// Ràng buộc của dịch vụ 10. Phần dữ liệu cơ bản luôn kiểm tra; phần phụ thuộc danh mục HanoiCheck
    /// (mã, nhóm tuổi, bữa ăn, trường được phân công) chỉ áp khi thực đơn thật sự gửi HanoiCheck.
    /// </summary>
    private async Task<string?> KiemTraAsync(Menu m, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(m.MaThucDon)) return "Vui lòng nhập mã thực đơn.";
        if (string.IsNullOrWhiteSpace(m.TenThucDon)) return "Vui lòng nhập tên thực đơn.";
        if (m.ThuApDung is < 0 or > 6) return "Thứ áp dụng không hợp lệ.";
        if (m.NhomTuoiId <= 0) return "Vui lòng chọn nhóm tuổi của thực đơn.";

        var dong = m.DanhSachMon.Where(i => !string.IsNullOrWhiteSpace(i.MaMonAn)).ToList();
        if (dong.Count == 0 || dong.Count != m.DanhSachMon.Count)
            return dong.Count == 0 ? "Thực đơn phải có ít nhất một món." : "Vui lòng chọn món cho mọi dòng của thực đơn.";
        if (dong.Any(i => i.BuoiAnId <= 0)) return "Vui lòng chọn bữa ăn cho mọi dòng của thực đơn.";

        var trung = dong.GroupBy(i => (i.BuoiAnId, Ma: i.MaMonAn.Trim())).FirstOrDefault(g => g.Count() > 1);
        if (trung is not null)
            return $"Món \"{trung.Key.Ma}\" bị lặp trong cùng một bữa của thực đơn.";

        var maMon = dong.Select(i => i.MaMonAn.Trim()).Distinct().ToList();
        var monAn = await _db.Dishes.AsNoTracking().Where(d => maMon.Contains(d.MaMonAn))
            .Select(d => new { d.MaMonAn, d.TenMonAn, d.NhomTuoiId }).ToListAsync(ct);
        var khongCo = maMon.Except(monAn.Select(d => d.MaMonAn)).ToList();
        if (khongCo.Count > 0) return "Món không có trong danh mục món ăn: " + string.Join(", ", khongCo);

        // HanoiCheck: món phải cùng nhóm tuổi với thực đơn.
        var lechTuoi = monAn.Where(d => d.NhomTuoiId != m.NhomTuoiId).Select(d => d.TenMonAn).ToList();
        if (lechTuoi.Count > 0)
            return "Món khác nhóm tuổi với thực đơn: " + string.Join(", ", lechTuoi)
                   + ". Mỗi món chỉ thuộc một nhóm tuổi - dùng món khai cho đúng nhóm tuổi này.";

        if (!m.DongBoHnC || !await _outbox.DangBatAsync(ct)) return null;

        // ---------- Quy định riêng của HanoiCheck ----------

        if (!MaHopLe.IsMatch(m.MaThucDon))
            return "Mã thực đơn chỉ được chứa chữ cái không dấu, số, dấu gạch dưới (_) và gạch ngang (-).";

        var nhomTuoi = await _db.NhomTuoiHnCs.AsNoTracking().Select(n => n.Id).ToListAsync(ct);
        if (nhomTuoi.Count > 0 && !nhomTuoi.Contains(m.NhomTuoiId))
            return $"Nhóm tuổi {m.NhomTuoiId} không có trong danh mục nhóm tuổi của HanoiCheck. Vui lòng chọn lại.";

        var buaAn = await _db.BuaAnHnCs.AsNoTracking().Select(b => b.Id).ToListAsync(ct);
        var buaSai = dong.Select(i => i.BuoiAnId).Distinct().Where(id => buaAn.Count > 0 && !buaAn.Contains(id)).ToList();
        if (buaSai.Count > 0)
            return "Bữa ăn không có trong danh mục bữa ăn của HanoiCheck: " + string.Join(", ", buaSai);

        var truong = m.DanhSachTruong;
        if (truong.Count > 0)
        {
            var duocPhanCong = await _db.TruongHocHnCs.AsNoTracking().Select(t => t.TruongId).ToListAsync(ct);
            var truongSai = truong.Except(duocPhanCong).ToList();
            if (truongSai.Count > 0)
                return "Trường không nằm trong danh sách trường được phân công phục vụ (id " + string.Join(", ", truongSai)
                       + "). Bấm \"Tải danh sách trường\" để cập nhật, hoặc để trống để dùng chung cho mọi trường.";
        }

        return null;
    }

    private static void ChuanHoa(Menu m)
    {
        m.MaThucDon = m.MaThucDon.Trim();
        m.TenThucDon = m.TenThucDon.Trim();
        // Chuẩn hoá CSV trường: bỏ trùng/giá trị rác; danh sách rỗng lưu null (= dùng chung).
        var truong = m.DanhSachTruong;
        m.DanhSachTruongCsv = truong.Count == 0 ? null : string.Join(",", truong);
    }

    private static MenuItem SaoChep(MenuItem i) => new() { BuoiAnId = i.BuoiAnId, MaMonAn = i.MaMonAn.Trim() };
}
