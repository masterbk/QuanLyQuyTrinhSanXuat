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

        if (dsMoi.Count == 0)
        {
            return KetQuaThaoTac.Loi("HanoiCheck không trả về danh mục nào.");
        }

        var hienCo = await _db.StandardFoodCategories.ToDictionaryAsync(c => c.Code, ct);

        foreach (var moi in dsMoi)
        {
            var code = moi.Code.Trim();

            if (hienCo.TryGetValue(code, out var cu))
            {
                cu.Name = moi.Name.Trim();
                cu.MeasureName = moi.MeasureName?.Trim();
                cu.CapNhatLucUtc = DateTime.UtcNow;
            }
            else
            {
                _db.StandardFoodCategories.Add(new StandardFoodCategory
                {
                    Code = code,
                    Name = moi.Name.Trim(),
                    MeasureName = moi.MeasureName?.Trim(),
                    CapNhatLucUtc = DateTime.UtcNow
                });
            }
        }

        // KHÔNG xoá mã cũ không còn trong danh sách: dữ liệu nghiệp vụ của cơ sở có thể
        // đang tham chiếu tới chúng. Tài liệu HnC cũng nêu nguyên tắc không xoá dữ liệu gốc.
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã cập nhật {dsMoi.Count} danh mục thực phẩm chuẩn.");
    }
}
