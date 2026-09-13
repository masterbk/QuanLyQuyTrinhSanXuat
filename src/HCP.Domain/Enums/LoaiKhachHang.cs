namespace HCP.Domain.Enums;

/// <summary>Phân loại khách hàng mua thành phẩm của cơ sở (bán hàng nội bộ).</summary>
public enum LoaiKhachHang
{
    /// <summary>Cửa hàng bán lẻ.</summary>
    CuaHang = 0,

    /// <summary>Đại lý / bán buôn.</summary>
    DaiLy = 1,

    /// <summary>Khách lẻ vãng lai.</summary>
    KhachLe = 2,

    /// <summary>Khác (bếp ăn tập thể...).</summary>
    Khac = 3,

    /// <summary>Trường học - có thể đặt hàng qua HanoiCheck (gắn mã trường HanoiCheck).</summary>
    TruongHoc = 4
}
