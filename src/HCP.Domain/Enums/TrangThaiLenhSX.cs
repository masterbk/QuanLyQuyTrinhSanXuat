namespace HCP.Domain.Enums;

/// <summary>Trạng thái lệnh sản xuất nội bộ.</summary>
public enum TrangThaiLenhSX
{
    /// <summary>Mới tạo, chưa trừ kho.</summary>
    MoiTao = 0,

    /// <summary>Đã thực hiện: đã trừ nguyên liệu và nhập thành phẩm vào kho.</summary>
    HoanThanh = 1
}
