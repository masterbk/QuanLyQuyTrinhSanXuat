using HCP.Domain.Entities.Business;

namespace HCP.Infrastructure.Services.Kho;

/// <summary>Quản lý định mức (công thức) nguyên liệu cho thành phẩm. Nội bộ, không gửi HanoiCheck.</summary>
public interface IDinhMucService
{
    Task<IReadOnlyList<DinhMucNguyenLieu>> LayTheoThanhPhamAsync(int productId, CancellationToken ct = default);

    /// <summary>Thay thế toàn bộ định mức của một thành phẩm.</summary>
    Task<KetQuaThaoTac> LuuAsync(int productId, IEnumerable<DinhMucNguyenLieu> dongDinhMuc,
                                CancellationToken ct = default);
}
