namespace HCP.Domain.Constants;

/// <summary>Loại đơn hàng theo đặc tả orders/merge (mục 12.1).</summary>
public static class LoaiDonHang
{
    public const string Food = "food";
    public const string Dish = "dish";

    public static readonly IReadOnlyList<string> TatCa = new[] { Food, Dish };
}

/// <summary>Trạng thái đơn hàng (enum trang_thai, đặc tả mục 12.4).</summary>
public static class TrangThaiDonHang
{
    // (giá trị gửi HnC, nhãn hiển thị)
    public static readonly IReadOnlyList<(string Ma, string Nhan)> DanhSach = new[]
    {
        ("CHO_XAC_NHAN", "Chờ xác nhận"),
        ("TU_CHOI", "Từ chối"),
        ("DANG_CHUAN_BI", "Đang chuẩn bị"),
        ("DANG_GIAO", "Đang giao"),
        ("DA_GIAO", "Đã giao hàng"),
        ("GIAO_HANG_THANH_CONG", "Giao hàng thành công"),
        ("TRA_HANG", "Trả hàng"),
        ("HUY", "Hủy"),
    };

    public static bool HopLe(string? ma) => ma is not null && DanhSach.Any(x => x.Ma == ma);

    public static string Nhan(string? ma) =>
        DanhSach.FirstOrDefault(x => x.Ma == ma).Nhan is { } n && !string.IsNullOrEmpty(n) ? n : (ma ?? "");
}
