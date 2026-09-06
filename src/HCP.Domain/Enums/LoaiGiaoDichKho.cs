namespace HCP.Domain.Enums;

/// <summary>Loại giao dịch trong sổ kho nội bộ. Quy ước số lượng: nhập dương, xuất âm.</summary>
public enum LoaiGiaoDichKho
{
    /// <summary>Nhập nguyên liệu từ nhà cung ứng (+).</summary>
    NhapNguyenLieu = 0,

    /// <summary>Xuất nguyên liệu cho sản xuất (−).</summary>
    XuatSanXuat = 1,

    /// <summary>Nhập thành phẩm sau sản xuất (+).</summary>
    NhapThanhPham = 2,

    /// <summary>Xuất bán thành phẩm cho khách (−).</summary>
    XuatBan = 3,

    /// <summary>Điều chỉnh/kiểm kê (+/−).</summary>
    DieuChinh = 4
}
