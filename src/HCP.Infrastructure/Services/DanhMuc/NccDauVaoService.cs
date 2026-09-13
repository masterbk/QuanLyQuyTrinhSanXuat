using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.MaTuSinh;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>
/// Quản lý Nhà cung ứng đầu vào của cơ sở đang đăng nhập.
/// Đồng bộ sang HanoiCheck qua POST /supplier/sub-suppliers/merge.
/// </summary>
public class NccDauVaoService : IDanhMucService<SubSupplier>
{
    private readonly AppDbContext _db;
    private readonly ISyncOutboxWriter _outbox;
    private readonly IMaTuSinhService _maTuSinh;

    public NccDauVaoService(AppDbContext db, ISyncOutboxWriter outbox, IMaTuSinhService maTuSinh)
    {
        _db = db;
        _outbox = outbox;
        _maTuSinh = maTuSinh;
    }

    public async Task<IReadOnlyList<SubSupplier>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.SubSuppliers
            .AsNoTracking()
            .Include(s => s.NhomThucPham)
            .OrderBy(s => s.MaNccDauVao)
            .ToListAsync(ct);

    public Task<SubSupplier?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        _db.SubSuppliers
            .Include(s => s.NhomThucPham)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<KetQuaThaoTac> ThemAsync(SubSupplier entity, CancellationToken ct = default)
    {
        var loi = KiemTraDuLieu(entity);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        ChuanHoa(entity);

        // Mã do hệ thống cấp (NCC-0001...), bỏ qua mọi mã gửi lên. Sinh SAU kiểm tra để không phí số.
        entity.MaNccDauVao = await _maTuSinh.SinhAsync(LoaiMaTuSinh.NccDauVao, ct: ct);

        _db.SubSuppliers.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dongBo = await _outbox.GuiAsync(entity, ct);

        return KetQuaThaoTac.Ok($"Đã thêm nhà cung ứng \"{entity.Ten}\" (mã {entity.MaNccDauVao}).").KemGhiChu(dongBo);
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(SubSupplier entity, CancellationToken ct = default)
    {
        var hienTai = await LayTheoIdAsync(entity.Id, ct);
        if (hienTai is null) return KetQuaThaoTac.Loi("Không tìm thấy nhà cung ứng cần sửa.");

        // Mã nhà cung ứng KHÔNG sửa được: là khoá đối chiếu với HanoiCheck.
        var loi = KiemTraDuLieu(entity);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        ChuanHoa(entity);

        hienTai.Ten = entity.Ten;
        hienTai.MaSoThue = entity.MaSoThue;
        hienTai.DiaChi = entity.DiaChi;
        hienTai.DienThoai = entity.DienThoai;

        hienTai.AttpTenGiay = entity.AttpTenGiay;
        hienTai.AttpSoGiay = entity.AttpSoGiay;
        hienTai.AttpNgayCap = entity.AttpNgayCap;
        hienTai.AttpNgayHetHan = entity.AttpNgayHetHan;

        hienTai.HopDongSo = entity.HopDongSo;
        hienTai.HopDongNgayKy = entity.HopDongNgayKy;
        hienTai.HopDongNgayHetHan = entity.HopDongNgayHetHan;

        hienTai.DongBoHnC = entity.DongBoHnC;
        hienTai.UpdatedAtUtc = DateTime.UtcNow;

        _db.SubSupplierFoodGroups.RemoveRange(hienTai.NhomThucPham);
        hienTai.NhomThucPham = entity.NhomThucPham
            .Select(g => new SubSupplierFoodGroup { MaNhom = g.MaNhom })
            .ToList();

        await _db.SaveChangesAsync(ct);

        var dongBo = await _outbox.GuiAsync(hienTai, ct);

        return KetQuaThaoTac.Ok($"Đã cập nhật nhà cung ứng \"{hienTai.Ten}\".").KemGhiChu(dongBo);
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var ncc = await LayTheoIdAsync(id, ct);
        if (ncc is null) return KetQuaThaoTac.Loi("Không tìm thấy nhà cung ứng cần xoá.");

        _db.SubSuppliers.Remove(ncc);
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã xoá nhà cung ứng \"{ncc.Ten}\".");
    }

    /// <summary>
    /// Kiểm tra các ràng buộc của đặc tả trước khi lưu, để không bị HanoiCheck trả 422:
    /// nhom_thuc_pham tối thiểu 1 phần tử; giấy ATTP và hợp đồng nếu có khai thì phải đủ 3 trường.
    /// </summary>
    private static string? KiemTraDuLieu(SubSupplier entity)
    {
        if (entity.NhomThucPham.Count == 0)
        {
            return "Phải chọn ít nhất một nhóm thực phẩm mà nhà cung ứng cung cấp.";
        }

        if (entity.CoGiayChungNhanAttp)
        {
            if (string.IsNullOrWhiteSpace(entity.AttpSoGiay)
                || !entity.AttpNgayCap.HasValue
                || !entity.AttpNgayHetHan.HasValue)
            {
                return "Giấy chứng nhận ATTP đã khai thì phải nhập đủ số giấy, ngày cấp và ngày hết hạn.";
            }

            if (entity.AttpNgayHetHan < entity.AttpNgayCap)
            {
                return "Ngày hết hạn giấy ATTP không được trước ngày cấp.";
            }
        }

        if (entity.CoHopDong)
        {
            if (string.IsNullOrWhiteSpace(entity.HopDongSo)
                || !entity.HopDongNgayKy.HasValue
                || !entity.HopDongNgayHetHan.HasValue)
            {
                return "Hợp đồng đã khai thì phải nhập đủ số hợp đồng, ngày ký và ngày hết hạn.";
            }

            if (entity.HopDongNgayHetHan < entity.HopDongNgayKy)
            {
                return "Ngày hết hạn hợp đồng không được trước ngày ký.";
            }
        }

        return null;
    }

    private static void ChuanHoa(SubSupplier entity)
    {
        entity.Ten = entity.Ten.Trim();
        entity.MaSoThue = entity.MaSoThue?.Trim();
        entity.DiaChi = entity.DiaChi?.Trim();
        entity.DienThoai = entity.DienThoai?.Trim();
        entity.AttpTenGiay = entity.AttpTenGiay?.Trim();
        entity.AttpSoGiay = entity.AttpSoGiay?.Trim();
        entity.HopDongSo = entity.HopDongSo?.Trim();

        // Bỏ mã nhóm trùng nhau (unique index trên SubSupplierId + MaNhom).
        entity.NhomThucPham = entity.NhomThucPham
            .Where(g => !string.IsNullOrWhiteSpace(g.MaNhom))
            .GroupBy(g => g.MaNhom.Trim(), StringComparer.Ordinal)
            .Select(g => new SubSupplierFoodGroup { MaNhom = g.Key })
            .ToList();
    }
}
