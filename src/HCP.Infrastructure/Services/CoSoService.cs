using HCP.Domain.Constants;
using HCP.Domain.Entities.Infrastructure;
using HCP.Domain.Enums;
using HCP.Infrastructure.Identity;
using HCP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.Services;

public class CoSoService : ICoSoService
{
    private readonly TenantStoreDbContext _tenantStore;
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CoSoService> _logger;

    public CoSoService(TenantStoreDbContext tenantStore,
                       AppDbContext db,
                       UserManager<ApplicationUser> userManager,
                       ILogger<CoSoService> logger)
    {
        _tenantStore = tenantStore;
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<KetQuaThaoTac> DangKyAsync(DangKyCoSoRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim();
        var maSoThue = request.MaSoThue.Trim();

        // Chặn đăng ký trùng ngay từ đầu để không sinh dữ liệu rác - tài liệu HnC
        // nghiêm cấm phát sinh dữ liệu trùng lặp.
        if (await _tenantStore.TenantInfo.AnyAsync(t => t.MaSoThue == maSoThue, ct))
        {
            return KetQuaThaoTac.Loi($"Mã số thuế {maSoThue} đã được đăng ký trên hệ thống.");
        }

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            return KetQuaThaoTac.Loi($"Email {email} đã được sử dụng.");
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = request.TenCoSo.Trim(),
            Identifier = await TaoIdentifierDuyNhatAsync(request.TenCoSo, ct),
            MaSoThue = maSoThue,
            DiaChi = request.DiaChi?.Trim(),
            NguoiDaiDien = request.NguoiDaiDien?.Trim(),
            EmailLienHe = email,
            SoDienThoai = request.SoDienThoai?.Trim(),
            TrangThai = TenantStatus.PendingApproval,
            NgayDangKyUtc = DateTime.UtcNow
        };

        _tenantStore.TenantInfo.Add(tenant);
        await _tenantStore.SaveChangesAsync(ct);

        // Tài khoản quản trị đầu tiên của cơ sở. Chưa đăng nhập được cho tới khi hồ sơ được duyệt
        // (chặn ở SignInManager, xem LoginModel).
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            HoTen = request.HoTenQuanTri.Trim(),
            TenantId = tenant.Id,
            DangHoatDong = true,
            EmailConfirmed = true
        };

        var ketQua = await _userManager.CreateAsync(user, request.MatKhau);

        if (!ketQua.Succeeded)
        {
            // Không để lại tenant mồ côi khi tạo tài khoản thất bại.
            _tenantStore.TenantInfo.Remove(tenant);
            await _tenantStore.SaveChangesAsync(ct);

            return KetQuaThaoTac.Loi(string.Join(" ", ketQua.Errors.Select(e => e.Description)));
        }

        await _userManager.AddToRoleAsync(user, AppRoles.TenantAdmin);

        _logger.LogInformation("Cơ sở {TenCoSo} (MST {MaSoThue}) đã đăng ký, chờ duyệt.",
            tenant.Name, tenant.MaSoThue);

        return KetQuaThaoTac.Ok(
            "Đăng ký thành công. Hồ sơ của bạn đang chờ quản trị viên duyệt, "
            + "bạn sẽ đăng nhập được sau khi được phê duyệt.");
    }

    public async Task<IReadOnlyList<Tenant>> LayDanhSachAsync(TenantStatus? trangThai = null,
                                                              CancellationToken ct = default)
    {
        var query = _tenantStore.TenantInfo.AsNoTracking();

        if (trangThai.HasValue)
        {
            query = query.Where(t => t.TrangThai == trangThai.Value);
        }

        return await query.OrderByDescending(t => t.NgayDangKyUtc).ToListAsync(ct);
    }

    public Task<Tenant?> LayTheoIdAsync(string tenantId, CancellationToken ct = default) =>
        _tenantStore.TenantInfo.FirstOrDefaultAsync(t => t.Id == tenantId, ct);

    public async Task<KetQuaThaoTac> DuyetAsync(string tenantId, string nguoiDuyet,
                                                CancellationToken ct = default)
    {
        var tenant = await LayTheoIdAsync(tenantId, ct);
        if (tenant is null) return KetQuaThaoTac.Loi("Không tìm thấy hồ sơ cơ sở.");

        if (tenant.TrangThai == TenantStatus.Active)
        {
            return KetQuaThaoTac.Loi("Hồ sơ này đã được duyệt trước đó.");
        }

        tenant.TrangThai = TenantStatus.Active;
        tenant.NgayDuyetUtc = DateTime.UtcNow;
        tenant.NguoiDuyet = nguoiDuyet;
        tenant.LyDoTuChoi = null;
        await _tenantStore.SaveChangesAsync(ct);

        _logger.LogInformation("Cơ sở {TenantId} được duyệt bởi {NguoiDuyet}.", tenantId, nguoiDuyet);

        return KetQuaThaoTac.Ok($"Đã duyệt cơ sở \"{tenant.Name}\".");
    }

    public async Task<KetQuaThaoTac> TuChoiAsync(string tenantId, string nguoiDuyet, string lyDo,
                                                 CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            return KetQuaThaoTac.Loi("Vui lòng nhập lý do từ chối để cơ sở biết cần bổ sung gì.");
        }

        var tenant = await LayTheoIdAsync(tenantId, ct);
        if (tenant is null) return KetQuaThaoTac.Loi("Không tìm thấy hồ sơ cơ sở.");

        tenant.TrangThai = TenantStatus.Rejected;
        tenant.LyDoTuChoi = lyDo.Trim();
        tenant.NguoiDuyet = nguoiDuyet;
        tenant.NgayDuyetUtc = DateTime.UtcNow;
        await _tenantStore.SaveChangesAsync(ct);

        _logger.LogInformation("Cơ sở {TenantId} bị từ chối bởi {NguoiDuyet}.", tenantId, nguoiDuyet);

        return KetQuaThaoTac.Ok($"Đã từ chối cơ sở \"{tenant.Name}\".");
    }

    public async Task<KetQuaThaoTac> KhoaAsync(string tenantId, string nguoiThucHien, CancellationToken ct = default)
    {
        var tenant = await LayTheoIdAsync(tenantId, ct);
        if (tenant is null) return KetQuaThaoTac.Loi("Không tìm thấy cơ sở.");
        if (tenant.TrangThai != TenantStatus.Active)
            return KetQuaThaoTac.Loi("Chỉ khoá được cơ sở đang hoạt động.");

        tenant.TrangThai = TenantStatus.Suspended;
        tenant.NguoiDuyet = nguoiThucHien;
        await _tenantStore.SaveChangesAsync(ct);

        _logger.LogInformation("Cơ sở {TenantId} bị khoá bởi {NguoiThucHien}.", tenantId, nguoiThucHien);
        return KetQuaThaoTac.Ok($"Đã khoá cơ sở \"{tenant.Name}\". Cơ sở này tạm thời không đăng nhập được.");
    }

    public async Task<KetQuaThaoTac> MoKhoaAsync(string tenantId, string nguoiThucHien, CancellationToken ct = default)
    {
        var tenant = await LayTheoIdAsync(tenantId, ct);
        if (tenant is null) return KetQuaThaoTac.Loi("Không tìm thấy cơ sở.");
        if (tenant.TrangThai != TenantStatus.Suspended)
            return KetQuaThaoTac.Loi("Chỉ mở khoá được cơ sở đang bị khoá.");

        tenant.TrangThai = TenantStatus.Active;
        tenant.NguoiDuyet = nguoiThucHien;
        await _tenantStore.SaveChangesAsync(ct);

        _logger.LogInformation("Cơ sở {TenantId} được mở khoá bởi {NguoiThucHien}.", tenantId, nguoiThucHien);
        return KetQuaThaoTac.Ok($"Đã mở khoá cơ sở \"{tenant.Name}\".");
    }

    public async Task<IReadOnlyDictionary<string, bool>> LayTrangThaiKetNoiAsync(CancellationToken ct = default)
    {
        // TenantHnCCredentials là bảng hạ tầng (không lọc theo tenant) nên quản trị nền tảng
        // đọc được của mọi cơ sở. DaXacThuc = đã bấm "Kiểm tra kết nối" thành công ít nhất 1 lần.
        var ds = await _db.TenantHnCCredentials.AsNoTracking()
            .Select(c => new { c.TenantId, c.DaXacThuc })
            .ToListAsync(ct);
        return ds.ToDictionary(x => x.TenantId, x => x.DaXacThuc);
    }

    /// <summary>Sinh identifier dạng slug, đảm bảo không trùng (cột này có unique index).</summary>
    private async Task<string> TaoIdentifierDuyNhatAsync(string tenCoSo, CancellationToken ct)
    {
        var slug = TaoSlug(tenCoSo);
        if (string.IsNullOrEmpty(slug)) slug = "co-so";

        var identifier = slug;
        var stt = 1;

        while (await _tenantStore.TenantInfo.AnyAsync(t => t.Identifier == identifier, ct))
        {
            identifier = $"{slug}-{++stt}";
        }

        return identifier;
    }

    private static string TaoSlug(string input)
    {
        var chuanHoa = input.Trim().ToLowerInvariant()
            .Normalize(System.Text.NormalizationForm.FormD);

        var sb = new System.Text.StringBuilder();
        foreach (var c in chuanHoa)
        {
            var loai = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (loai == System.Globalization.UnicodeCategory.NonSpacingMark) continue;

            if (char.IsLetterOrDigit(c) && c < 128) sb.Append(c);
            else if (char.IsWhiteSpace(c) || c == '-') sb.Append('-');
        }

        var slug = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        return slug.Length > 60 ? slug[..60].Trim('-') : slug;
    }
}
