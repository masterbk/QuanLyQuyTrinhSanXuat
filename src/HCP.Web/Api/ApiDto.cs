namespace HCP.Web.Api;

// ================== Xác thực ==================

public sealed record DangNhapRequest(string Email, string MatKhau, string? ThietBi);

public sealed record LamMoiRequest(string RefreshToken, string? ThietBi);

public sealed record DangXuatRequest(string RefreshToken);

public sealed record NguoiDungDto(string Id, string Email, string? HoTen, string? MaCoSo,
                                  IReadOnlyList<string> VaiTro);

public sealed record PhienDto(string AccessToken, DateTime AccessTokenHetHanUtc,
                              string RefreshToken, DateTime RefreshTokenHetHanUtc,
                              NguoiDungDto NguoiDung);

// ================== Dùng chung ==================

/// <summary>Kết quả có phân trang - app dựa vào TongSo/Trang để cuộn vô hạn.</summary>
public sealed record TrangDuLieu<T>(IReadOnlyList<T> DuLieu, int Trang, int SoDong, int TongSo)
{
    public int TongTrang => SoDong <= 0 ? 1 : (int)Math.Ceiling(TongSo / (double)SoDong);
}

/// <summary>Lỗi nghiệp vụ trả về cho app (khác lỗi 500): luôn có thông báo tiếng Việt để hiển thị thẳng.</summary>
public sealed record LoiDto(string ThongBao);

public sealed record KetQuaDto(bool ThanhCong, string ThongBao);

// ================== Lệnh sản xuất ==================

public sealed record LenhSanXuatDto(
    int Id, string MaLenh, string MaThanhPham, string? TenThanhPham, decimal SoLuong,
    string MaKho, string MaLoThanhPham, DateOnly? HanSuDungThanhPham, DateOnly NgaySanXuat,
    string TrangThai, string TrangThaiHienThi, bool TaoLoDongBo, string? MaLoDaTao,
    DateTime? ThoiGianHoanThanhUtc, DateTime? ThoiGianHuyUtc, string? LyDoHuy, string? GhiChu,
    IReadOnlyList<AnhLenhDto> Anh);

public sealed record AnhLenhDto(string MaFile, string TenFile, string DuongDan);

public sealed record LenhSanXuatLuuRequest(
    string MaLenh, string MaThanhPham, decimal SoLuong, string MaKho, string MaLoThanhPham,
    DateOnly? HanSuDungThanhPham, DateOnly? NgaySanXuat, bool TaoLoDongBo, string? GhiChu);

public sealed record HuyLenhRequest(string LyDo);

public sealed record NguyenLieuCanDtoApi(string MaNguyenLieu, string TenNguyenLieu, string? DonViTinh,
                                         decimal Can, decimal Ton, bool Du);

public sealed record ThanhPhamDto(string MaSanPham, string TenSanPham, string? DonViTinh);

public sealed record KhoDto(string MaKho, string TenKho);

// ================== Đơn hàng từ trường (kéo về từ HanoiCheck) ==================

public sealed record DonHangNhanDto(
    int Id, string MaDonHang, string? TenTruong, string? TrangThai, string TrangThaiHienThi,
    DateOnly? NgayGiao, string? DiemTruong, string? LoaiDon, string? KhoXuat,
    string? MaNguoiGiao, string? TenNguoiGiao, string? SdtNguoiGiao, string? PhuongTienGiao,
    string? BienSoXe, string? DiaChiGiao, string? GhiChu, string? LinkTruyXuat,
    DateTime? NgayTaoTrenHnC, DateTime LanDongBoUtc, bool DaLayChiTiet,
    IReadOnlyList<DonHangNhanDongDto> Dong);

public sealed record DonHangNhanDongDto(
    string MaSanPham, string? TenSanPham, decimal? SoLuong, string? DonViTinh,
    string? MaTruyVet, string? MaThucDon, IReadOnlyList<PhanBoDto> PhanBo);

public sealed record PhanBoDto(string? MaPhieuXuat, string? MaThucPhamNcc, string? MaLo,
                               string? TenLo, string? MaKho, string? TenKho, decimal? SoLuong);

// ================== Đơn hàng cơ sở tự tạo (đẩy lên HanoiCheck) ==================

public sealed record DonHangDto(
    int Id, string MaDonHang, string LoaiDonHang, string MaTruong, DateOnly? NgayDonHang,
    string? DiemGiao, string? DiaChiNhan, string? MaNguoiGiao, string TrangThai,
    string TrangThaiHienThi, string? GhiChu, IReadOnlyList<DonHangDongDto> Dong);

public sealed record DonHangDongDto(string? MaSanPham, string? MaLoaiSp, string? MaMonAn, decimal SoLuong);
