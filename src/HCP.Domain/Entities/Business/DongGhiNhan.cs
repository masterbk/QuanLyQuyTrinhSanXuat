using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>Một dòng dữ liệu của phiếu ghi nhận. Giá trị các trường lưu dạng JSON (khoá = Ma của trường).</summary>
public class DongGhiNhan : TenantEntity
{
    public int Id { get; set; }

    public int PhieuGhiNhanId { get; set; }
    public PhieuGhiNhan? PhieuGhiNhan { get; set; }

    /// <summary>Hạng mục cố định tương ứng (chỉ với biểu mẫu Checklist); null với các bố cục khác.</summary>
    public int? HangMucBieuMauId { get; set; }

    public int ThuTu { get; set; }

    /// <summary>Giá trị nhập theo từng trường: JSON dạng {"ma_truong": "giá trị", ...}.</summary>
    public string GiaTriJson { get; set; } = "{}";

    public string? GhiChu { get; set; }
}
