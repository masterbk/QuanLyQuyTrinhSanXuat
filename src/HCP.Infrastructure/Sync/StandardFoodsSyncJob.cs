using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.DanhMuc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.Sync;

public interface IStandardFoodsSyncJob
{
    /// <summary>Lấy danh mục thực phẩm chuẩn từ HnC và cập nhật bảng dùng chung. Trả về số mục cập nhật.</summary>
    Task<int> DongBoAsync(CancellationToken ct = default);
}

/// <summary>
/// Job cập nhật định kỳ danh mục thực phẩm chuẩn (dùng chung mọi cơ sở).
///
/// Danh mục do HnC ban hành nên chỉ cần lấy bằng MỘT credential bất kỳ đã cấu hình
/// (ưu tiên credential đã xác thực). Không cần chạy cho từng cơ sở.
/// </summary>
public sealed class StandardFoodsSyncJob : IStandardFoodsSyncJob
{
    private readonly AppDbContext _db;
    private readonly ITenantTokenManager _tokenManager;
    private readonly IHanoiCheckStandardFoodsClient _client;
    private readonly IDanhMucChuanService _danhMucChuan;
    private readonly ILogger<StandardFoodsSyncJob> _logger;

    public StandardFoodsSyncJob(AppDbContext db,
                                ITenantTokenManager tokenManager,
                                IHanoiCheckStandardFoodsClient client,
                                IDanhMucChuanService danhMucChuan,
                                ILogger<StandardFoodsSyncJob> logger)
    {
        _db = db;
        _tokenManager = tokenManager;
        _client = client;
        _danhMucChuan = danhMucChuan;
        _logger = logger;
    }

    public async Task<int> DongBoAsync(CancellationToken ct = default)
    {
        // Chọn một cơ sở đã cấu hình đủ credential, ưu tiên cái đã xác thực kết nối.
        var cauHinh = await _db.TenantHnCCredentials
            .Where(c => c.BaseUrl != "" && c.ClientId != "" && c.ClientSecretEncrypted != "")
            .OrderByDescending(c => c.DaXacThuc)
            .FirstOrDefaultAsync(ct);

        if (cauHinh is null)
        {
            _logger.LogInformation("Chưa có cơ sở nào cấu hình HnC - bỏ qua cập nhật danh mục thực phẩm chuẩn.");
            return 0;
        }

        var token = await _tokenManager.LayAccessTokenAsync(cauHinh.TenantId, ct);
        if (token.TrangThai != TokenTrangThai.CoToken)
        {
            _logger.LogWarning("Không lấy được token để cập nhật danh mục chuẩn: {Loi}", token.ThongBao);
            return 0;
        }

        var ketQua = await _client.LayDanhMucAsync(cauHinh.BaseUrl, token.AccessToken!, ct);
        if (!ketQua.ThanhCong)
        {
            _logger.LogWarning("Lấy danh mục thực phẩm chuẩn thất bại: {Loi}", ketQua.ThongBao);
            return 0;
        }

        var danhMuc = ketQua.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Code))
            .Select(i => new StandardFoodCategory
            {
                Code = i.Code!.Trim(),
                Name = (i.Name ?? string.Empty).Trim(),
                MeasureName = i.MeasureName?.Trim()
            });

        await _danhMucChuan.CapNhatTuHnCAsync(danhMuc, ct);
        _logger.LogInformation("Đã cập nhật {SoLuong} danh mục thực phẩm chuẩn từ HnC.", ketQua.Items.Count);
        return ketQua.Items.Count;
    }
}
