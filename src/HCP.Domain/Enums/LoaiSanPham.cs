namespace HCP.Domain.Enums;

/// <summary>Phân loại Thực phẩm/SKU để phục vụ quản lý kho + sản xuất nội bộ.</summary>
public enum LoaiSanPham
{
    /// <summary>Thành phẩm - sản phẩm làm ra để bán (vd bánh mì, bánh ngọt).</summary>
    ThanhPham = 0,

    /// <summary>Nguyên liệu - đầu vào để sản xuất (vd bột mì, đường, trứng).</summary>
    NguyenLieu = 1
}
