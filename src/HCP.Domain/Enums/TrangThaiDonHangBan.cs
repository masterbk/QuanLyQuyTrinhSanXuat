namespace HCP.Domain.Enums;

/// <summary>Trạng thái đơn hàng bán nội bộ.</summary>
public enum TrangThaiDonHangBan
{
    /// <summary>Mới tạo / vừa nhận, chờ nhà cung cấp xác nhận.</summary>
    ChoXacNhan = 0,

    /// <summary>Đã xác nhận, đang chuẩn bị hàng.</summary>
    DaXacNhan = 1,

    /// <summary>Đã xuất kho (trừ tồn theo lô), đang giao.</summary>
    DangGiao = 2,

    /// <summary>Đã giao cho khách.</summary>
    DaGiao = 3,

    /// <summary>Đã huỷ; nếu huỷ khi đang giao thì hàng được trả về kho bằng bút toán đảo.</summary>
    DaHuy = 4
}

/// <summary>Đơn hàng bán đến từ đâu.</summary>
public enum NguonDonHang
{
    /// <summary>Nhà cung cấp tự lập (cửa hàng, đại lý, khách lẻ, trường không qua HanoiCheck...).</summary>
    NoiBo = 0,

    /// <summary>Trường đặt trên HanoiCheck, kéo về hệ thống.</summary>
    HanoiCheck = 1
}
