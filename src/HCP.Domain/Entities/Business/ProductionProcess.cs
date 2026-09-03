using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Quy trình sản xuất: một chuỗi khâu sản xuất có thứ tự, gắn với một danh mục thực phẩm.
/// Đồng bộ qua POST /supplier/processes/merge (đồng bộ, trả 200).
/// </summary>
public class ProductionProcess : TenantEntity
{
    public int Id { get; set; }

    /// <summary>ma_quy_trinh - khoá nghiệp vụ.</summary>
    public string MaQuyTrinh { get; set; } = string.Empty;

    /// <summary>ten_quy_trinh.</summary>
    public string TenQuyTrinh { get; set; } = string.Empty;

    /// <summary>
    /// ma_danh_muc_thuc_pham. Đặc tả ghi kiểu integer và ví dụ gửi số 1.
    /// LƯU Ý: GET /supplier/standard-foods chỉ trả về name/code/measure_name, KHÔNG có id số,
    /// nên chưa tra cứu được giá trị này từ API. Cần hỏi lại đơn vị vận hành HanoiCheck
    /// khi kiểm thử tích hợp. Xem README mục "Điểm cần làm rõ với HanoiCheck".
    /// </summary>
    public int? MaDanhMucThucPham { get; set; }

    /// <summary>danh_sach_khau - tối thiểu 1 khâu, có thứ tự thực hiện.</summary>
    public List<ProcessStepLine> DanhSachKhau { get; set; } = new();
}
