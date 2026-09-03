using System.ComponentModel.DataAnnotations;

namespace HCP.Infrastructure.Services;

/// <summary>Dữ liệu cơ sở sản xuất tự khai khi đăng ký sử dụng nền tảng.</summary>
public class DangKyCoSoRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên cơ sở")]
    [StringLength(255)]
    public string TenCoSo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã số thuế")]
    [StringLength(20)]
    public string MaSoThue { get; set; } = string.Empty;

    [StringLength(500)]
    public string? DiaChi { get; set; }

    [StringLength(255)]
    public string? NguoiDaiDien { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    public string? SoDienThoai { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên người quản trị")]
    [StringLength(255)]
    public string HoTenQuanTri { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu tối thiểu 8 ký tự")]
    [DataType(DataType.Password)]
    public string MatKhau { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(MatKhau), ErrorMessage = "Mật khẩu nhập lại không khớp")]
    public string XacNhanMatKhau { get; set; } = string.Empty;
}
