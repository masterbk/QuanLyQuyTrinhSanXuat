using HCP.Domain.Entities.Common;
using HCP.Domain.Enums;

namespace HCP.Domain.Entities.Business;

/// <summary>Một trường (cột) của biểu mẫu: tên, kiểu dữ liệu và ràng buộc. Giá trị nhập lưu ở DongGhiNhan theo khoá Ma.</summary>
public class TruongBieuMau : TenantEntity
{
    public int Id { get; set; }

    public int BieuMauId { get; set; }
    public BieuMau? BieuMau { get; set; }

    public string Ten { get; set; } = string.Empty;

    /// <summary>Khoá của trường (không dấu, dùng làm khoá trong GiaTriJson của dòng ghi nhận), vd "nhiet_do_dong".</summary>
    public string Ma { get; set; } = string.Empty;

    public KieuTruongBieuMau Kieu { get; set; } = KieuTruongBieuMau.Text;

    /// <summary>True = trường ĐẦU PHIẾU (nhập một lần cho cả phiếu: khu vực, biển số, tổ, lần BH...);
    /// false = cột dữ liệu của từng dòng.</summary>
    public bool LaDauPhieu { get; set; }

    public bool BatBuoc { get; set; }

    /// <summary>Đơn vị (cho kiểu Số), vd "°C", "phút".</summary>
    public string? DonVi { get; set; }

    /// <summary>Giá trị chuẩn / quy chuẩn để đối chiếu (mô tả tự do), vd "≤ 28", "17-19".</summary>
    public string? GiaTriChuan { get; set; }

    /// <summary>Danh sách tuỳ chọn (cho kiểu LuaChon), phân tách bằng dấu phẩy.</summary>
    public string? TuyChonCsv { get; set; }

    /// <summary>Tên nhóm cột để gộp hiển thị, vd "Buổi sáng", "Nhiệt độ".</summary>
    public string? Nhom { get; set; }

    /// <summary>True = trường Ngày này là "hạn kế tiếp" cần NHẮC (vd ngày hiệu chuẩn/bảo dưỡng kế tiếp của thiết bị):
    /// hệ thống quét các dòng để nhắc khi đến/quá hạn.</summary>
    public bool LaHanNhac { get; set; }

    public int ThuTu { get; set; }
}
