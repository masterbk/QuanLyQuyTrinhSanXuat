using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HCP.Infrastructure.Services.DanhMuc;

public interface IDanhMucChuanService
{
    Task<IReadOnlyList<StandardFoodCategory>> LayTatCaAsync(CancellationToken ct = default);

    /// <summary>Ghi đè danh mục chuẩn bằng dữ liệu mới lấy từ HanoiCheck.</summary>
    Task<KetQuaThaoTac> CapNhatTuHnCAsync(IEnumerable<StandardFoodCategory> danhMuc,
                                          CancellationToken ct = default);

    /// <summary>Danh mục đơn vị tính HnC (GET /measures), sắp theo tên. Có thể rỗng nếu chưa tải về.</summary>
    Task<IReadOnlyList<DonViTinhHnC>> LayDonViTinhAsync(CancellationToken ct = default);

    /// <summary>Ghi đè danh mục đơn vị tính bằng dữ liệu mới lấy từ HanoiCheck.</summary>
    Task<KetQuaThaoTac> CapNhatDonViTinhTuHnCAsync(IEnumerable<DonViTinhHnC> danhMuc, CancellationToken ct = default);

    /// <summary>Danh mục nhóm tuổi HnC (GET /student-groups), sắp theo id. Có thể rỗng nếu chưa tải về.</summary>
    Task<IReadOnlyList<NhomTuoiHnC>> LayNhomTuoiAsync(CancellationToken ct = default);

    /// <summary>Ghi đè danh mục nhóm tuổi bằng dữ liệu mới lấy từ HanoiCheck.</summary>
    Task<KetQuaThaoTac> CapNhatNhomTuoiTuHnCAsync(IEnumerable<NhomTuoiHnC> danhMuc, CancellationToken ct = default);

    /// <summary>Danh mục bữa ăn HnC (GET /meal-types), theo thứ tự bữa trong ngày. Có thể rỗng nếu chưa tải về.</summary>
    Task<IReadOnlyList<BuaAnHnC>> LayBuaAnAsync(CancellationToken ct = default);

    /// <summary>Ghi đè danh mục bữa ăn bằng dữ liệu mới lấy từ HanoiCheck (giữ đúng thứ tự HnC trả về).</summary>
    Task<KetQuaThaoTac> CapNhatBuaAnTuHnCAsync(IReadOnlyList<BuaAnHnC> danhMuc, CancellationToken ct = default);
}

/// <summary>
/// Danh mục thực phẩm chuẩn lấy từ GET /supplier/standard-foods.
///
/// Dữ liệu dùng chung cho mọi cơ sở (không lọc theo tenant) vì do HanoiCheck ban hành.
/// Mục đích: cho người dùng chọn mã nhóm từ danh sách thay vì gõ tay, tránh sai mã
/// dẫn tới lỗi 422 khi đồng bộ.
///
/// Việc gọi API và lên lịch cập nhật định kỳ sẽ làm ở Giai đoạn 3 cùng engine đồng bộ,
/// vì cần token và cơ chế job nền. Hiện tại danh mục có thể trống - các màn hình phải
/// hoạt động được kể cả khi chưa có dữ liệu.
/// </summary>
public class DanhMucChuanService : IDanhMucChuanService
{
    /// <summary>Khoá cache dùng chung cho mọi cơ sở - danh mục này KHÔNG theo tenant.</summary>
    private const string KhoaCache = "danh-muc-thuc-pham-chuan";

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public DanhMucChuanService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>
    /// Bảng này rất lớn (HanoiCheck trả hơn 5.000 dòng) nhưng cả tháng mới đổi một lần, trong khi mỗi lần mở
    /// màn Quy trình / Thực phẩm / NCC đầu vào lại nạp lại toàn bộ - đó là một phần lý do các màn đó tải lâu.
    /// Giữ sẵn trong bộ nhớ cho tới khi tải lại danh mục từ HanoiCheck (CapNhatTuHnCAsync tự xoá cache).
    /// Danh sách trả về là dùng chung, chỉ để đọc - nơi gọi không được sửa phần tử bên trong.
    /// </summary>
    public async Task<IReadOnlyList<StandardFoodCategory>> LayTatCaAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(KhoaCache, out IReadOnlyList<StandardFoodCategory>? daCo) && daCo is not null)
        {
            return daCo;
        }

        var danhSach = await _db.StandardFoodCategories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        _cache.Set(KhoaCache, (IReadOnlyList<StandardFoodCategory>)danhSach, TimeSpan.FromHours(12));
        return danhSach;
    }

    public async Task<KetQuaThaoTac> CapNhatTuHnCAsync(IEnumerable<StandardFoodCategory> danhMuc,
                                                       CancellationToken ct = default)
    {
        var dsMoi = danhMuc
            .Where(c => !string.IsNullOrWhiteSpace(c.Code))
            .GroupBy(c => c.Code.Trim(), StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        // Chốt an toàn: HnC trả rỗng (hoặc lỗi tạm) thì KHÔNG xoá danh mục hiện có.
        if (dsMoi.Count == 0)
        {
            return KetQuaThaoTac.Loi("HanoiCheck không trả về danh mục nào.");
        }

        // THAY THẾ TOÀN BỘ: xoá hết rồi nạp lại theo đúng danh mục HnC trả về. Đây chỉ là bảng
        // tra cứu (cache) để đổ dropdown - sản phẩm lưu bản sao giá trị ma_loai_sp nên xoá an toàn,
        // không có khoá ngoại. Cách này tránh lẫn bản cũ khi HnC đổi/bỏ mã (vd khoá đổi từ slug -> id).
        var hienCo = await _db.StandardFoodCategories.ToListAsync(ct);
        _db.StandardFoodCategories.RemoveRange(hienCo);

        _db.StandardFoodCategories.AddRange(dsMoi.Select(moi => new StandardFoodCategory
        {
            Code = moi.Code.Trim(),
            Name = moi.Name.Trim(),
            MeasureName = moi.MeasureName?.Trim(),
            CapNhatLucUtc = DateTime.UtcNow
        }));

        // Xoá + thêm trong một SaveChanges -> EF bọc trong transaction (toàn bộ hoặc không gì).
        await _db.SaveChangesAsync(ct);
        _cache.Remove(KhoaCache);

        return KetQuaThaoTac.Ok($"Đã cập nhật {dsMoi.Count} danh mục thực phẩm chuẩn.");
    }

    private const string KhoaCacheDonViTinh = "danh-muc-don-vi-tinh-hnc";

    public async Task<IReadOnlyList<DonViTinhHnC>> LayDonViTinhAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(KhoaCacheDonViTinh, out IReadOnlyList<DonViTinhHnC>? daCo) && daCo is not null)
        {
            return daCo;
        }

        var danhSach = await _db.DonViTinhHnCs.AsNoTracking().OrderBy(d => d.Ten).ToListAsync(ct);
        _cache.Set(KhoaCacheDonViTinh, (IReadOnlyList<DonViTinhHnC>)danhSach, TimeSpan.FromHours(12));
        return danhSach;
    }

    public async Task<KetQuaThaoTac> CapNhatDonViTinhTuHnCAsync(IEnumerable<DonViTinhHnC> danhMuc,
                                                                CancellationToken ct = default)
    {
        var dsMoi = danhMuc.Where(d => d.Id > 0 && !string.IsNullOrWhiteSpace(d.Ten))
            .GroupBy(d => d.Id).Select(g => g.First()).ToList();

        // Chốt an toàn như danh mục chuẩn: HnC trả rỗng thì giữ nguyên danh mục đang có.
        if (dsMoi.Count == 0) return KetQuaThaoTac.Loi("HanoiCheck không trả về đơn vị tính nào.");

        // Thay thế toàn bộ theo id (Id là id của HnC nên không xoá-rồi-thêm được - EF báo trùng khoá):
        // sửa tên cái đã có, thêm cái mới, bỏ cái HnC không còn trả. Thực phẩm chỉ lưu bản sao id, không có khoá ngoại.
        var hienCo = await _db.DonViTinhHnCs.ToDictionaryAsync(d => d.Id, ct);
        var now = DateTime.UtcNow;
        foreach (var moi in dsMoi)
        {
            if (hienCo.Remove(moi.Id, out var cu)) { cu.Ten = moi.Ten.Trim(); cu.CapNhatLucUtc = now; }
            else _db.DonViTinhHnCs.Add(new DonViTinhHnC { Id = moi.Id, Ten = moi.Ten.Trim(), CapNhatLucUtc = now });
        }
        _db.DonViTinhHnCs.RemoveRange(hienCo.Values);
        await _db.SaveChangesAsync(ct);
        _cache.Remove(KhoaCacheDonViTinh);

        return KetQuaThaoTac.Ok($"Đã cập nhật {dsMoi.Count} đơn vị tính.");
    }

    private const string KhoaCacheNhomTuoi = "danh-muc-nhom-tuoi-hnc";

    public async Task<IReadOnlyList<NhomTuoiHnC>> LayNhomTuoiAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(KhoaCacheNhomTuoi, out IReadOnlyList<NhomTuoiHnC>? daCo) && daCo is not null)
        {
            return daCo;
        }

        var danhSach = await _db.NhomTuoiHnCs.AsNoTracking().OrderBy(d => d.Id).ToListAsync(ct);
        // Bảng chỉ vài dòng: chưa tải về (rỗng) thì không giữ cache để lần mở sau thấy ngay khi đã có.
        if (danhSach.Count > 0)
            _cache.Set(KhoaCacheNhomTuoi, (IReadOnlyList<NhomTuoiHnC>)danhSach, TimeSpan.FromHours(12));
        return danhSach;
    }

    public async Task<KetQuaThaoTac> CapNhatNhomTuoiTuHnCAsync(IEnumerable<NhomTuoiHnC> danhMuc,
                                                               CancellationToken ct = default)
    {
        var dsMoi = danhMuc.Where(d => d.Id > 0 && !string.IsNullOrWhiteSpace(d.Ten))
            .GroupBy(d => d.Id).Select(g => g.First()).ToList();

        // Chốt an toàn: HnC trả rỗng thì giữ nguyên danh mục đang có.
        if (dsMoi.Count == 0) return KetQuaThaoTac.Loi("HanoiCheck không trả về nhóm tuổi nào.");

        // Cùng cách với đơn vị tính: Id là id của HnC nên sửa/thêm/bỏ theo id. Món ăn chỉ lưu bản sao id.
        var hienCo = await _db.NhomTuoiHnCs.ToDictionaryAsync(d => d.Id, ct);
        var now = DateTime.UtcNow;
        foreach (var moi in dsMoi)
        {
            if (hienCo.Remove(moi.Id, out var cu)) { cu.Ten = moi.Ten.Trim(); cu.CapNhatLucUtc = now; }
            else _db.NhomTuoiHnCs.Add(new NhomTuoiHnC { Id = moi.Id, Ten = moi.Ten.Trim(), CapNhatLucUtc = now });
        }
        _db.NhomTuoiHnCs.RemoveRange(hienCo.Values);
        await _db.SaveChangesAsync(ct);
        _cache.Remove(KhoaCacheNhomTuoi);

        return KetQuaThaoTac.Ok($"Đã cập nhật {dsMoi.Count} nhóm tuổi.");
    }

    private const string KhoaCacheBuaAn = "danh-muc-bua-an-hnc";

    public async Task<IReadOnlyList<BuaAnHnC>> LayBuaAnAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(KhoaCacheBuaAn, out IReadOnlyList<BuaAnHnC>? daCo) && daCo is not null)
        {
            return daCo;
        }

        var danhSach = await _db.BuaAnHnCs.AsNoTracking().OrderBy(d => d.ThuTu).ThenBy(d => d.Id).ToListAsync(ct);
        if (danhSach.Count > 0)
            _cache.Set(KhoaCacheBuaAn, (IReadOnlyList<BuaAnHnC>)danhSach, TimeSpan.FromHours(12));
        return danhSach;
    }

    public async Task<KetQuaThaoTac> CapNhatBuaAnTuHnCAsync(IReadOnlyList<BuaAnHnC> danhMuc,
                                                            CancellationToken ct = default)
    {
        var dsMoi = danhMuc.Where(d => d.Id > 0 && !string.IsNullOrWhiteSpace(d.Ten))
            .GroupBy(d => d.Id).Select(g => g.First()).ToList();
        if (dsMoi.Count == 0) return KetQuaThaoTac.Loi("HanoiCheck không trả về bữa ăn nào.");

        var hienCo = await _db.BuaAnHnCs.ToDictionaryAsync(d => d.Id, ct);
        var now = DateTime.UtcNow;
        for (var i = 0; i < dsMoi.Count; i++)
        {
            var moi = dsMoi[i];
            if (hienCo.Remove(moi.Id, out var cu)) { cu.Ten = moi.Ten.Trim(); cu.ThuTu = i + 1; cu.CapNhatLucUtc = now; }
            else _db.BuaAnHnCs.Add(new BuaAnHnC { Id = moi.Id, Ten = moi.Ten.Trim(), ThuTu = i + 1, CapNhatLucUtc = now });
        }
        _db.BuaAnHnCs.RemoveRange(hienCo.Values);
        await _db.SaveChangesAsync(ct);
        _cache.Remove(KhoaCacheBuaAn);

        return KetQuaThaoTac.Ok($"Đã cập nhật {dsMoi.Count} bữa ăn.");
    }
}
