using System.ComponentModel.DataAnnotations;
using Finbuckle.MultiTenant.Abstractions;
using HCP.Domain.Constants;
using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Identity;

/// <summary>Tài khoản đăng nhập gắn với một nhân sự.</summary>
public sealed record TaiKhoanNhanVien(string UserId, int NhanSuId, string TenDangNhap, string? Email,
                                      IReadOnlyList<string> VaiTro, bool DangHoatDong);

/// <summary>Thông tin tài khoản nhập ở màn Nhân sự.</summary>
public sealed class YeuCauTaiKhoan
{
    /// <summary>false với nhân sự đang có tài khoản = xoá tài khoản.</summary>
    public bool CoTaiKhoan { get; set; }

    public string? Email { get; set; }

    /// <summary>Bắt buộc khi tạo mới; khi sửa để trống = giữ mật khẩu cũ.</summary>
    public string? MatKhau { get; set; }

    /// <summary>Các role trong <see cref="AppRoles.VaiTroNhanVien"/>, ít nhất một.</summary>
    public List<string> VaiTro { get; set; } = new();

    public bool DangHoatDong { get; set; } = true;
}

/// <summary>
/// Tài khoản đăng nhập (web + app) cho nhân sự của cơ sở đang đăng nhập. Tên đăng nhập = số điện thoại của nhân sự,
/// email không bắt buộc; một người có thể có nhiều vai trò (nhập liệu, sản xuất chế biến, giao hàng).
/// Chỉ quản trị cơ sở được dùng (màn Nhân sự tự ẩn phần tài khoản với người khác).
/// </summary>
public interface ITaiKhoanNhanVienService
{
    /// <summary>Tài khoản của mọi nhân sự trong cơ sở, khoá theo Id nhân sự.</summary>
    Task<IReadOnlyDictionary<int, TaiKhoanNhanVien>> LayTheoCoSoAsync(CancellationToken ct = default);

    /// <summary>Kiểm tra trước khi lưu (gọi TRƯỚC khi lưu nhân sự để khỏi lưu nửa chừng). null = hợp lệ.</summary>
    Task<string?> KiemTraAsync(Staff nhanSu, YeuCauTaiKhoan yeuCau, CancellationToken ct = default);

    /// <summary>Tạo / cập nhật / xoá tài khoản theo yêu cầu. Nhân sự phải đã được lưu (có Id).</summary>
    Task<KetQuaThaoTac> LuuAsync(Staff nhanSu, YeuCauTaiKhoan yeuCau, CancellationToken ct = default);

    /// <summary>Xoá tài khoản khi xoá nhân sự (không có tài khoản thì thôi).</summary>
    Task<KetQuaThaoTac> XoaTheoNhanSuAsync(int nhanSuId, CancellationToken ct = default);
}

/// <inheritdoc cref="ITaiKhoanNhanVienService"/>
public sealed class TaiKhoanNhanVienService : ITaiKhoanNhanVienService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IMultiTenantContextAccessor _tenantAccessor;

    public TaiKhoanNhanVienService(UserManager<ApplicationUser> userManager, AppDbContext db,
                                   IMultiTenantContextAccessor tenantAccessor)
    {
        _userManager = userManager;
        _db = db;
        _tenantAccessor = tenantAccessor;
    }

    private string TenantIdBatBuoc() =>
        _tenantAccessor.MultiTenantContext?.TenantInfo?.Id is { Length: > 0 } id
            ? id
            : throw new InvalidOperationException("Không xác định được cơ sở đang đăng nhập.");

    private Task<ApplicationUser?> TimAsync(string tenantId, int nhanSuId, CancellationToken ct) =>
        _userManager.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.NhanSuId == nhanSuId, ct);

    public async Task<IReadOnlyDictionary<int, TaiKhoanNhanVien>> LayTheoCoSoAsync(CancellationToken ct = default)
    {
        var tenantId = TenantIdBatBuoc();
        var users = await _userManager.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.NhanSuId != null)
            .ToListAsync(ct);
        var ids = users.Select(u => u.Id).ToList();

        var vaiTro = await (from ur in _db.UserRoles
                            join r in _db.Roles on ur.RoleId equals r.Id
                            where ids.Contains(ur.UserId)
                            select new { ur.UserId, r.Name })
                           .ToListAsync(ct);

        return users.ToDictionary(
            u => u.NhanSuId!.Value,
            u => new TaiKhoanNhanVien(u.Id, u.NhanSuId!.Value, u.UserName ?? "", u.Email,
                AppRoles.VaiTroNhanVien.Where(v => vaiTro.Any(x => x.UserId == u.Id && x.Name == v)).ToList(),
                u.DangHoatDong));
    }

    public async Task<string?> KiemTraAsync(Staff nhanSu, YeuCauTaiKhoan yeuCau, CancellationToken ct = default)
    {
        if (!yeuCau.CoTaiKhoan) return null;
        var tenantId = TenantIdBatBuoc();

        var tenDangNhap = TenDangNhap.ChuanHoaSoDienThoai(nhanSu.DienThoai);
        if (tenDangNhap is null)
            return "Tài khoản đăng nhập bằng số điện thoại của nhân sự - vui lòng nhập số điện thoại hợp lệ (9-11 chữ số).";

        var vaiTro = yeuCau.VaiTro.Distinct().ToList();
        if (vaiTro.Count == 0) return "Chọn ít nhất một vai trò cho tài khoản.";
        if (vaiTro.Any(v => !AppRoles.VaiTroNhanVien.Contains(v))) return "Vai trò tài khoản không hợp lệ.";

        var hienTai = nhanSu.Id == 0 ? null : await TimAsync(tenantId, nhanSu.Id, ct);

        var trungTen = await _userManager.FindByNameAsync(tenDangNhap);
        if (trungTen is not null && trungTen.Id != hienTai?.Id)
            return $"Số điện thoại {tenDangNhap} đã được dùng cho một tài khoản đăng nhập khác.";

        var email = ChuanHoaEmail(yeuCau.Email);
        if (email is not null)
        {
            if (!new EmailAddressAttribute().IsValid(email)) return "Email không hợp lệ.";
            var trungEmail = await _userManager.FindByEmailAsync(email);
            if (trungEmail is not null && trungEmail.Id != hienTai?.Id)
                return $"Email {email} đã được dùng cho một tài khoản khác.";
        }

        if (hienTai is null && string.IsNullOrWhiteSpace(yeuCau.MatKhau))
            return "Nhập mật khẩu cho tài khoản mới.";

        if (!string.IsNullOrWhiteSpace(yeuCau.MatKhau))
        {
            foreach (var kiemTra in _userManager.PasswordValidators)
            {
                var kq = await kiemTra.ValidateAsync(_userManager, hienTai ?? new ApplicationUser(), yeuCau.MatKhau);
                if (!kq.Succeeded) return MoTaLoi(kq);
            }
        }

        return null;
    }

    public async Task<KetQuaThaoTac> LuuAsync(Staff nhanSu, YeuCauTaiKhoan yeuCau, CancellationToken ct = default)
    {
        var tenantId = TenantIdBatBuoc();
        if (nhanSu.Id == 0) return KetQuaThaoTac.Loi("Cần lưu nhân sự trước khi tạo tài khoản.");

        var loi = await KiemTraAsync(nhanSu, yeuCau, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        var user = await TimAsync(tenantId, nhanSu.Id, ct);
        if (!yeuCau.CoTaiKhoan)
            return user is null ? KetQuaThaoTac.Ok("") : await XoaAsync(user, ct);

        var tenDangNhap = TenDangNhap.ChuanHoaSoDienThoai(nhanSu.DienThoai)!;
        var email = ChuanHoaEmail(yeuCau.Email);
        var vaiTro = AppRoles.VaiTroNhanVien.Where(yeuCau.VaiTro.Contains).ToList();
        // Nhân sự đã nghỉ việc thì tài khoản bị khoá, kể cả khi ô "cho phép đăng nhập" còn bật.
        var dangHoatDong = yeuCau.DangHoatDong && nhanSu.TrangThai;

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = tenDangNhap, Email = email, EmailConfirmed = email is not null,
                HoTen = nhanSu.HoTen, TenantId = tenantId, NhanSuId = nhanSu.Id, DangHoatDong = dangHoatDong
            };
            var tao = await _userManager.CreateAsync(user, yeuCau.MatKhau!);
            if (!tao.Succeeded) return KetQuaThaoTac.Loi("Không tạo được tài khoản: " + MoTaLoi(tao));

            var gan = await _userManager.AddToRolesAsync(user, vaiTro);
            if (!gan.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                return KetQuaThaoTac.Loi("Không gán được vai trò: " + MoTaLoi(gan));
            }
            return KetQuaThaoTac.Ok($"Đã tạo tài khoản đăng nhập {tenDangNhap} ({TenVaiTro(vaiTro)}).");
        }

        // Đổi quyền / khoá / đổi mật khẩu thì phiên đang mở phải đăng nhập lại để nhận quyền mới.
        var phaiDangNhapLai = false;
        user.HoTen = nhanSu.HoTen;
        if (user.DangHoatDong != dangHoatDong)
        {
            user.DangHoatDong = dangHoatDong;
            phaiDangNhapLai = true;
        }
        if (user.UserName != tenDangNhap) user.UserName = tenDangNhap;
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            user.Email = email;
            user.EmailConfirmed = email is not null;
        }
        var capNhat = await _userManager.UpdateAsync(user);   // tự chuẩn hoá UserName/Email
        if (!capNhat.Succeeded) return KetQuaThaoTac.Loi("Không cập nhật được tài khoản: " + MoTaLoi(capNhat));

        var cu = await _userManager.GetRolesAsync(user);
        var bo = cu.Where(r => AppRoles.VaiTroNhanVien.Contains(r) && !vaiTro.Contains(r)).ToList();
        var them = vaiTro.Except(cu).ToList();
        if (bo.Count > 0) { await _userManager.RemoveFromRolesAsync(user, bo); phaiDangNhapLai = true; }
        if (them.Count > 0) { await _userManager.AddToRolesAsync(user, them); phaiDangNhapLai = true; }

        var doiMatKhau = !string.IsNullOrWhiteSpace(yeuCau.MatKhau);
        if (doiMatKhau)
        {
            // Đã kiểm tra độ mạnh ở trên nên thao tác gỡ-rồi-đặt không để tài khoản rơi vào trạng thái không mật khẩu.
            await _userManager.RemovePasswordAsync(user);
            var dat = await _userManager.AddPasswordAsync(user, yeuCau.MatKhau!);
            if (!dat.Succeeded) return KetQuaThaoTac.Loi("Không đặt lại được mật khẩu: " + MoTaLoi(dat));
            phaiDangNhapLai = true;
        }

        if (phaiDangNhapLai)
        {
            await _userManager.UpdateSecurityStampAsync(user);                      // phiên web
            if (!dangHoatDong || doiMatKhau || bo.Count > 0) await ThuHoiPhienAppAsync(user.Id, ct);
        }

        return KetQuaThaoTac.Ok($"Đã cập nhật tài khoản {tenDangNhap} ({TenVaiTro(vaiTro)}"
                                + (dangHoatDong ? "" : ", đang khoá") + (doiMatKhau ? ", đã đặt lại mật khẩu" : "") + ").");
    }

    public async Task<KetQuaThaoTac> XoaTheoNhanSuAsync(int nhanSuId, CancellationToken ct = default)
    {
        var user = await TimAsync(TenantIdBatBuoc(), nhanSuId, ct);
        return user is null ? KetQuaThaoTac.Ok("") : await XoaAsync(user, ct);
    }

    private async Task<KetQuaThaoTac> XoaAsync(ApplicationUser user, CancellationToken ct)
    {
        await ThuHoiPhienAppAsync(user.Id, ct);
        var kq = await _userManager.DeleteAsync(user);
        return kq.Succeeded
            ? KetQuaThaoTac.Ok($"Đã xoá tài khoản đăng nhập {user.UserName}.")
            : KetQuaThaoTac.Loi("Không xoá được tài khoản: " + MoTaLoi(kq));
    }

    /// <summary>Thu hồi mọi refresh token của app: hết phiên truy cập hiện tại (tối đa 2 giờ) là phải đăng nhập lại.</summary>
    private async Task ThuHoiPhienAppAsync(string userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var dangMo = await _db.MobileRefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in dangMo) t.RevokedAtUtc = now;
        if (dangMo.Count > 0) await _db.SaveChangesAsync(ct);
    }

    private static string? ChuanHoaEmail(string? email) => string.IsNullOrWhiteSpace(email) ? null : email.Trim();

    private static string TenVaiTro(IEnumerable<string> vaiTro) => string.Join(", ", vaiTro.Select(AppRoles.TenHienThi));

    /// <summary>Thông báo lỗi Identity bằng tiếng Việt (mặc định Identity trả tiếng Anh).</summary>
    private static string MoTaLoi(IdentityResult kq) => string.Join(" ", kq.Errors.Select(e => e.Code switch
    {
        "PasswordTooShort" => "Mật khẩu phải có ít nhất 8 ký tự.",
        "PasswordRequiresDigit" => "Mật khẩu phải có ít nhất một chữ số.",
        "PasswordRequiresLower" => "Mật khẩu phải có ít nhất một chữ thường.",
        "PasswordRequiresUpper" => "Mật khẩu phải có ít nhất một chữ hoa.",
        "PasswordRequiresNonAlphanumeric" => "Mật khẩu phải có ít nhất một ký tự đặc biệt (vd @, #, !).",
        "PasswordRequiresUniqueChars" => "Mật khẩu cần nhiều ký tự khác nhau hơn.",
        "DuplicateUserName" => "Tên đăng nhập đã được dùng.",
        "DuplicateEmail" => "Email đã được dùng.",
        _ => e.Description
    }));
}
