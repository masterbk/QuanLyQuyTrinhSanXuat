namespace HCP.Domain.Constants;

/// <summary>
/// Vai trò 2 tầng: tầng nền tảng (công ty vận hành) và tầng tenant (từng cơ sở sản xuất).
/// Nhân viên của cơ sở có thể mang NHIỀU vai trò cùng lúc, quyền được cộng gộp.
/// </summary>
public static class AppRoles
{
    /// <summary>Quản trị nền tảng: duyệt/khoá tenant, giám sát đồng bộ toàn hệ thống.
    /// KHÔNG sửa dữ liệu nghiệp vụ của tenant.</summary>
    public const string PlatformSuperAdmin = "PlatformSuperAdmin";

    /// <summary>Quản trị của một cơ sở: toàn quyền, quản lý tài khoản nhân viên, cấu hình credential HanoiCheck.</summary>
    public const string TenantAdmin = "TenantAdmin";

    /// <summary>Nhân viên nhập liệu: mọi màn nghiệp vụ trừ cài đặt HanoiCheck.</summary>
    public const string TenantStaff = "TenantStaff";

    /// <summary>Nhân viên thực hiện sản xuất chế biến: Lệnh sản xuất + xem tồn kho.</summary>
    public const string TenantSanXuat = "TenantSanXuat";

    /// <summary>Nhân viên giao hàng: Đơn hàng bán (xem, xuất kho, xác nhận đã giao).</summary>
    public const string TenantGiaoHang = "TenantGiaoHang";

    /// <summary>Nhân viên nhập biểu mẫu kiểm soát trên app - chỉ các mẫu được giao (xem PhanQuyenBieuMau).</summary>
    public const string TenantBieuMau = "TenantBieuMau";

    // Chuỗi role cho [Authorize(Roles = ...)] / AuthorizeView (phân tách bằng dấu phẩy = một trong các role).
    public const string QuyenNhapLieu = TenantAdmin + "," + TenantStaff;
    public const string QuyenSanXuat = TenantAdmin + "," + TenantStaff + "," + TenantSanXuat;
    public const string QuyenGiaoHang = TenantAdmin + "," + TenantStaff + "," + TenantGiaoHang;
    /// <summary>Nhập biểu mẫu: quản trị + nhập liệu (mọi mẫu) và nhân viên nhập biểu mẫu (mẫu được giao).</summary>
    public const string QuyenBieuMau = TenantAdmin + "," + TenantStaff + "," + TenantBieuMau;
    public const string MoiNguoiDungCoSo = TenantAdmin + "," + TenantStaff + "," + TenantSanXuat + "," + TenantGiaoHang
                                           + "," + TenantBieuMau;

    public static readonly string[] All = [PlatformSuperAdmin, TenantAdmin, TenantStaff, TenantSanXuat, TenantGiaoHang, TenantBieuMau];

    /// <summary>Mọi role của người dùng thuộc cơ sở.</summary>
    public static readonly string[] VaiTroCoSo = [TenantAdmin, TenantStaff, TenantSanXuat, TenantGiaoHang, TenantBieuMau];

    /// <summary>Các role quản trị cơ sở được gán cho tài khoản nhân viên.</summary>
    public static readonly string[] VaiTroNhanVien = [TenantStaff, TenantSanXuat, TenantGiaoHang, TenantBieuMau];

    public static string TenHienThi(string role) => role switch
    {
        PlatformSuperAdmin => "Quản trị nền tảng",
        TenantAdmin => "Quản trị cơ sở",
        TenantStaff => "Nhân viên nhập liệu",
        TenantSanXuat => "Nhân viên sản xuất chế biến",
        TenantGiaoHang => "Nhân viên giao hàng",
        TenantBieuMau => "Nhân viên nhập biểu mẫu",
        _ => role
    };

    public static string MoTa(string role) => role switch
    {
        TenantStaff => "mọi màn nghiệp vụ, trừ cài đặt HanoiCheck",
        TenantSanXuat => "lệnh sản xuất (tạo, hoàn thành kèm ảnh), xem tồn kho",
        TenantGiaoHang => "đơn hàng bán: xem, xuất kho, xác nhận đã giao",
        TenantBieuMau => "nhập phiếu biểu mẫu kiểm soát trên app (các mẫu được giao)",
        _ => ""
    };
}
