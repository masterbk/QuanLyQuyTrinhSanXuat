using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// File minh chứng gắn theo khâu của lô - phần tử của danh_sach_file
/// (giấy kiểm dịch, hình ảnh...).
/// </summary>
public class BatchFile : TenantEntity
{
    public int Id { get; set; }

    public int BatchId { get; set; }
    public Batch? Batch { get; set; }

    /// <summary>ma_file - mã file.</summary>
    public string MaFile { get; set; } = string.Empty;

    /// <summary>ten_file - tên hiển thị.</summary>
    public string TenFile { get; set; } = string.Empty;

    /// <summary>duong_dan - URL/đường dẫn file, tối đa 1000 ký tự.</summary>
    public string DuongDan { get; set; } = string.Empty;

    /// <summary>loai - vd GIAY_KIEM_DICH, HINH_ANH.</summary>
    public string Loai { get; set; } = string.Empty;

    /// <summary>ma_khau - khâu mà file thuộc về (nếu có).</summary>
    public string? MaKhau { get; set; }

    /// <summary>ma_buoc_sx - bước sản xuất cụ thể (nếu có).</summary>
    public string? MaBuocSx { get; set; }
}
