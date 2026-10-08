using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Các biểu mẫu kiểm soát một nhân sự (có role "Nhân viên nhập biểu mẫu") được nhập trên app - mỗi nhân sự tối đa
/// một dòng. <see cref="TatCa"/> = mọi mẫu, kể cả mẫu thêm sau; ngược lại chỉ các mẫu trong danh sách.
/// </summary>
public class PhanQuyenBieuMau : TenantEntity
{
    public int Id { get; set; }

    public int NhanSuId { get; set; }

    public bool TatCa { get; set; } = true;

    /// <summary>Id các biểu mẫu được giao, phân tách bằng dấu phẩy (chỉ dùng khi TatCa = false).</summary>
    public string? BieuMauIdsCsv { get; set; }

    public IReadOnlyList<int> BieuMauIds =>
        (BieuMauIdsCsv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0).Where(id => id > 0).Distinct().ToList();
}
