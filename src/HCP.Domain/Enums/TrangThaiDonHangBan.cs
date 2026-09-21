namespace HCP.Domain.Enums;

/// <summary>
/// Trạng thái đơn hàng bán nội bộ. Vòng đời: ChoXacNhan → DaXacNhan → xuất kho (trừ tồn) rẽ theo có
/// chọn người giao hay không → ChoGiaoHang (chưa chọn, chờ nhân viên tự nhận) hoặc thẳng DangGiao (đã
/// chọn) → nhân viên nhận (nếu đang ChoGiaoHang) thì cũng thành DangGiao → DaGiao; hoặc DaHuy bất kỳ lúc nào
/// trước DaGiao.
/// </summary>
public enum TrangThaiDonHangBan
{
    /// <summary>Mới tạo / vừa nhận, chờ nhà cung cấp xác nhận.</summary>
    ChoXacNhan = 0,

    /// <summary>Đã xác nhận, đang chuẩn bị hàng.</summary>
    DaXacNhan = 1,

    /// <summary>Đã xuất kho (trừ tồn theo lô) và đã có người giao - đang giao.</summary>
    DangGiao = 2,

    /// <summary>Đã giao cho khách.</summary>
    DaGiao = 3,

    /// <summary>Đã huỷ; nếu huỷ khi đã xuất kho (ChoGiaoHang hoặc DangGiao) thì hàng được trả về kho bằng bút toán đảo.</summary>
    DaHuy = 4,

    /// <summary>Đã xuất kho (trừ tồn) nhưng CHƯA có người giao - chờ nhân viên giao hàng tự nhận (hoặc quản lý gán sau).</summary>
    ChoGiaoHang = 5
}

/// <summary>Đơn hàng bán đến từ đâu.</summary>
public enum NguonDonHang
{
    /// <summary>Nhà cung cấp tự lập (cửa hàng, đại lý, khách lẻ, trường không qua HanoiCheck...).</summary>
    NoiBo = 0,

    /// <summary>Trường đặt trên HanoiCheck, kéo về hệ thống.</summary>
    HanoiCheck = 1
}
