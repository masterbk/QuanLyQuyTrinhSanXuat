namespace HCP.Domain.Enums;

/// <summary>
/// Trạng thái một phiếu ghi nhận biểu mẫu. Hiện chỉ dùng Nhap/DaGhiNhan; ChoThamTra/DaThamTra để dành
/// cho bước QC thẩm tra sẽ bật sau (đã đặt sẵn để không phải sửa dữ liệu cũ).
/// </summary>
public enum TrangThaiPhieu
{
    /// <summary>Đang nhập, chưa gửi.</summary>
    Nhap = 0,

    /// <summary>Đã ghi nhận (nhân viên nhập xong).</summary>
    DaGhiNhan = 1,

    /// <summary>Chờ QC thẩm tra (để dành).</summary>
    ChoThamTra = 2,

    /// <summary>Đã QC thẩm tra (để dành).</summary>
    DaThamTra = 3
}
