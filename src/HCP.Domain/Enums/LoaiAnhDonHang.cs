namespace HCP.Domain.Enums;

/// <summary>Ảnh gắn với đơn hàng bán.</summary>
public enum LoaiAnhDonHang
{
    /// <summary>Ảnh tổng quan chụp lúc xuất kho - gửi kèm sang HanoiCheck.</summary>
    TongQuan = 0,

    /// <summary>Ảnh chứng minh đã giao hàng, nhân viên giao hàng chụp khi xác nhận.</summary>
    Giao = 1
}
