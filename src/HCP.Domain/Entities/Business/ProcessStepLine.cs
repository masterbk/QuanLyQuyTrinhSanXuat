using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Một khâu trong quy trình sản xuất, kèm thứ tự thực hiện.
/// Tương ứng phần tử của danh_sach_khau trong POST /supplier/processes/merge.
///
/// Kế thừa TenantEntity dù đã có quy trình cha: nếu bảng con không bị lọc theo tenant,
/// một truy vấn trực tiếp vào bảng này (vd tìm quy trình nào đang dùng một mã khâu)
/// sẽ chạm vào dữ liệu của cơ sở khác - mã khâu là mã chuẩn nên rất dễ trùng giữa các cơ sở.
/// </summary>
public class ProcessStepLine : TenantEntity
{
    public int Id { get; set; }

    public int ProductionProcessId { get; set; }
    public ProductionProcess? ProductionProcess { get; set; }

    /// <summary>ma_khau - phải tồn tại trong danh mục khâu, nếu không HnC trả 422.</summary>
    public string MaKhau { get; set; } = string.Empty;

    /// <summary>thu_tu - thứ tự thực hiện khâu trong quy trình.</summary>
    public int ThuTu { get; set; }
}
