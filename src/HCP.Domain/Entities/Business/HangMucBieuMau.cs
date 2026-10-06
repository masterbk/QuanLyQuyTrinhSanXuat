using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>Một hạng mục cố định của biểu mẫu Checklist (vd "Đèn chiếu sáng", "Sàn nhà"). Mỗi hạng mục = một dòng để tick/điền.</summary>
public class HangMucBieuMau : TenantEntity
{
    public int Id { get; set; }

    public int BieuMauId { get; set; }
    public BieuMau? BieuMau { get; set; }

    public string Ten { get; set; } = string.Empty;

    /// <summary>Diễn giải chi tiết cần kiểm (hiển thị để nhân viên đối chiếu).</summary>
    public string? DienGiai { get; set; }

    /// <summary>Tần suất thực hiện của riêng hạng mục (vd "Hàng ngày", "Tuần 1 lần").</summary>
    public string? TanSuat { get; set; }

    public int ThuTu { get; set; }
}
