namespace HCP.Domain.Enums;

/// <summary>Trạng thái phiếu xuất bán thành phẩm.</summary>
public enum TrangThaiXuatBan
{
    /// <summary>Mới tạo, chưa trừ kho.</summary>
    MoiTao = 0,

    /// <summary>Đã xuất: đã trừ tồn thành phẩm khỏi kho.</summary>
    HoanThanh = 1
}
