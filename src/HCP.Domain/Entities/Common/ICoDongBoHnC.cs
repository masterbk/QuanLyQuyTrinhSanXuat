namespace HCP.Domain.Entities.Common;

/// <summary>
/// Bản ghi danh mục có thể gửi sang HanoiCheck. Khi cơ sở bật đồng bộ, chỉ các bản ghi có
/// <see cref="DongBoHnC"/> = true mới được xếp hàng gửi; bỏ tick để giữ bản ghi là dữ liệu nội bộ.
/// </summary>
public interface ICoDongBoHnC
{
    bool DongBoHnC { get; set; }
}
