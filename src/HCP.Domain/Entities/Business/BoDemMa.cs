using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Bộ đếm để tự sinh mã nghiệp vụ theo từng cơ sở (vd khoá "CS" → CS-0001, "LO-20260913" → LO-20260913-001).
///
/// Chỉ tăng, KHÔNG BAO GIỜ lùi: xoá bản ghi mới nhất cũng không cấp lại mã đó, vì mã có thể đã gửi
/// HanoiCheck - cấp lại sẽ khiến HanoiCheck ghi đè dữ liệu cũ bằng bản ghi khác.
/// </summary>
public class BoDemMa : TenantEntity
{
    public int Id { get; set; }

    /// <summary>Khoá dãy số: tiền tố, kèm ngày với dãy đánh số theo ngày.</summary>
    public string Khoa { get; set; } = string.Empty;

    /// <summary>Số cuối cùng đã cấp. Là concurrency token để hai người tạo cùng lúc không nhận trùng số.</summary>
    public int GiaTri { get; set; }
}
