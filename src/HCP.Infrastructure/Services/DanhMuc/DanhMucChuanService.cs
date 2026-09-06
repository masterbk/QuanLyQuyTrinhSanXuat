using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

public interface IDanhMucChuanService
{
    Task<IReadOnlyList<StandardFoodCategory>> LayTatCaAsync(CancellationToken ct = default);

    /// <summary>Ghi đè danh mục chuẩn bằng dữ liệu mới lấy từ HanoiCheck.</summary>
    Task<KetQuaThaoTac> CapNhatTuHnCAsync(IEnumerable<StandardFoodCategory> danhMuc,
                                          CancellationToken ct = default);
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
    private readonly AppDbContext _db;

    public DanhMucChuanService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<StandardFoodCategory>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.StandardFoodCategories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);

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

        return KetQuaThaoTac.Ok($"Đã cập nhật {dsMoi.Count} danh mục thực phẩm chuẩn.");
    }
}
