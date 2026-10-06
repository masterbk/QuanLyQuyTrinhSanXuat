namespace HCP.Domain.Enums;

/// <summary>Kiểu dữ liệu của một trường (cột) trong biểu mẫu - quyết định widget nhập trên web/app.</summary>
public enum KieuTruongBieuMau
{
    /// <summary>Chữ tự do.</summary>
    Text = 0,

    /// <summary>Số (có thể kèm đơn vị) - vd nhiệt độ, số lượng.</summary>
    So = 1,

    /// <summary>Giờ trong ngày (hh:mm) - vd giờ bật/tắt, giờ vào/ra lò.</summary>
    Gio = 2,

    /// <summary>Ngày - vd NSX/HSD.</summary>
    Ngay = 3,

    /// <summary>Đạt / Không đạt (Đ/KĐ).</summary>
    DatKhongDat = 4,

    /// <summary>Chọn một thành phẩm từ danh mục sản phẩm.</summary>
    ChonSanPham = 5,

    /// <summary>Chọn một nhân sự từ danh mục nhân sự.</summary>
    ChonNhanSu = 6,

    /// <summary>Chọn một nhà cung ứng đầu vào.</summary>
    ChonNcc = 7,

    /// <summary>Chọn một cơ sở sản xuất.</summary>
    ChonCoSo = 8,

    /// <summary>Ảnh chứng minh (một hoặc nhiều).</summary>
    Anh = 9,

    /// <summary>Chọn một giá trị từ danh sách tuỳ biến (khai ở TuyChonCsv).</summary>
    LuaChon = 10
}
