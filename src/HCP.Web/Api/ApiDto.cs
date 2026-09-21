namespace HCP.Web.Api;

// ================== Xác thực ==================

public sealed record DangNhapRequest(string Email, string MatKhau, string? ThietBi);

public sealed record LamMoiRequest(string RefreshToken, string? ThietBi);

public sealed record DangXuatRequest(string RefreshToken);

/// <param name="HanoiCheckBat">Công tắc đồng bộ HanoiCheck của cơ sở: tắt thì app ẩn phần HanoiCheck.</param>
public sealed record NguoiDungDto(string Id, string Email, string? HoTen, string? MaCoSo,
                                  IReadOnlyList<string> VaiTro, bool HanoiCheckBat, string? MaNhanSu);

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

/// <summary>Đăng ký token thiết bị (FCM) để nhận thông báo đẩy.</summary>
public sealed record DangKyThietBiRequest(string Token, string? ThietBi);

// ================== Lệnh sản xuất ==================

/// <summary>
/// Lệnh sản xuất: phần đầu dùng chung + danh sách sản phẩm, mỗi sản phẩm là một lô riêng.
/// </summary>
public sealed record LenhSanXuatDto(
    int Id, string MaLenh, string MaKho, DateOnly NgaySanXuat,
    string TrangThai, string TrangThaiHienThi, bool TaoLoDongBo,
    DateTime? ThoiGianHoanThanhUtc, DateTime? ThoiGianHuyUtc, string? LyDoHuy, string? GhiChu,
    IReadOnlyList<LenhSanXuatSanPhamDto> SanPham,
    IReadOnlyList<LenhSanXuatThamGiaDto> ThamGia);

/// <summary>Nhân viên đã quét mã QR tham gia lệnh.</summary>
public sealed record LenhSanXuatThamGiaDto(string MaNhanSu, string? HoTen, DateTime ThoiGianUtc);

/// <summary>Các khâu (Id) nhân viên chọn tham gia; rỗng = rời lệnh.</summary>
public sealed record ThamGiaLenhRequest(IReadOnlyList<int>? KhauIds);

public sealed record LenhSanXuatSanPhamDto(
    int Id, string MaThanhPham, string? TenThanhPham, decimal SoLuong,
    string MaLoThanhPham, DateOnly? HanSuDung, string MaQuyTrinh, string? TenQuyTrinh,
    string? MaLoDaTao,
    IReadOnlyList<LenhSanXuatKhauDto> Khau,
    IReadOnlyList<AnhLenhDto> Anh);

public sealed record LenhSanXuatKhauDto(
    int Id, string MaKhau, string? TenKhau, int ThuTu,
    string MaCoSo, string? TenCoSo, IReadOnlyList<string> NguoiThucHien, string? GhiChu);

public sealed record AnhLenhDto(string MaFile, string TenFile, string DuongDan);

/// <summary>MaLenh bị bỏ qua: mã lệnh do hệ thống sinh.</summary>
public sealed record LenhSanXuatLuuRequest(
    string? MaLenh, string MaKho, DateOnly? NgaySanXuat, bool TaoLoDongBo, string? GhiChu,
    IReadOnlyList<LenhSanXuatSanPhamRequest>? SanPham);

/// <summary>MaLoThanhPham: để trống với dòng mới (hệ thống cấp mã); dòng cũ gửi lại đúng mã đã cấp.</summary>
public sealed record LenhSanXuatSanPhamRequest(
    string MaThanhPham, decimal SoLuong, string? MaLoThanhPham, DateOnly? HanSuDung,
    string MaQuyTrinh, IReadOnlyList<LenhSanXuatKhauRequest>? Khau);

public sealed record LenhSanXuatKhauRequest(
    string MaKhau, int ThuTu, string MaCoSo, IReadOnlyList<string>? NguoiThucHien, string? GhiChu);

/// <summary>Sửa lại người thực hiện / cơ sở của một khâu ngay lúc bấm Hoàn thành.</summary>
public sealed record KhauSuaLaiRequest(
    int Id, string MaCoSo, IReadOnlyList<string>? NguoiThucHien, string? GhiChu);

public sealed record HuyLenhRequest(string LyDo);

/// <summary>Xem trước nguyên liệu cho NHIỀU dòng sản phẩm cùng lúc (nhu cầu được cộng dồn).</summary>
public sealed record NguyenLieuCanRequest(string MaKho, IReadOnlyList<DongSanPhamRequest>? Dong);

public sealed record DongSanPhamRequest(string MaThanhPham, decimal SoLuong);

public sealed record NguyenLieuCanDtoApi(string MaNguyenLieu, string TenNguyenLieu, string? DonViTinh,
                                         decimal Can, decimal Ton, bool Du);

public sealed record ThanhPhamDto(string MaSanPham, string TenSanPham, string? DonViTinh,
                                  string? MaQuyTrinh);

public sealed record QuyTrinhDto(string MaQuyTrinh, string TenQuyTrinh,
                                 IReadOnlyList<KhauDto> Khau);

public sealed record KhauDto(string MaKhau, string? TenKhau, int ThuTu);

public sealed record CoSoDto(string MaCoSo, string TenCoSo, string? DiaChi);

public sealed record NhanSuDto(string MaNhanSu, string HoTen, string? ViTri);

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

public sealed record DonHangBanDto(
    int Id, string MaDonHang, string MaKhachHang, string? TenKhachHang, string MaKho, DateOnly NgayDat,
    DateOnly? NgayGiao, string? DiaChiGiao, string? MaNguoiGiao, string? TenNguoiGiao,
    string TrangThai, string TrangThaiHienThi,
    string Nguon, string? MaDonHnC, string? GhiChu, string? LyDoHuy, decimal TongTien,
    DateTime? ThoiGianXuatKhoUtc, DateTime? ThoiGianGiaoUtc, IReadOnlyList<DonHangBanDongDto> Dong);

public sealed record DonHangBanDongDto(int Id, string MaThanhPham, string? TenThanhPham, decimal SoLuong,
                                       decimal DonGia, decimal ThanhTien, IReadOnlyList<XuatLoDto> XuatLo);

public sealed record XuatLoDto(string MaLo, DateOnly? HanSuDung, decimal SoLuong);

/// <summary>NgayDat để trống với đơn mới (server tự lấy hôm nay); dòng cũ gửi lại đúng NgayDat để giữ mã đơn/số tự sinh.</summary>
public sealed record DonHangBanLuuRequest(
    string MaKhachHang, string MaKho, DateOnly? NgayDat, DateOnly? NgayGiao,
    string? DiaChiGiao, string? MaNguoiGiao, string? GhiChu,
    IReadOnlyList<DonHangBanDongRequest>? Dong);

public sealed record DonHangBanDongRequest(string MaThanhPham, decimal SoLuong, decimal DonGia, string? GhiChu);

public sealed record HuyDonHangRequest(string? LyDo);

public sealed record KhachHangDto(string MaKhachHang, string TenKhachHang, string? DiaChi);

// ================== Kho nội bộ (nhập-xuất-tồn theo lô, KHÔNG đồng bộ HanoiCheck) ==================

/// <summary>Một dòng tồn kho theo (sản phẩm, kho, lô).</summary>
public sealed record TonKhoDto(string MaSanPham, string TenSanPham, string LoaiSanPham, string? DonViTinh,
                               string MaKho, string TenKho, string MaLo, DateOnly? HanSuDung, decimal SoLuongTon);

/// <summary>Một dòng lịch sử giao dịch kho (nhập/xuất/điều chỉnh) gần đây.</summary>
public sealed record LichSuKhoDto(DateTime ThoiGianUtc, string Loai, string TenLoai, string MaSanPham,
                                  string? TenSanPham, string MaKho, string MaLo, decimal SoLuong,
                                  string? ChungTu, string? GhiChu);

public sealed record NhapKhoApiRequest(string MaSanPham, string MaKho, string MaLo, decimal SoLuong,
                                       DateOnly? HanSuDung, string? MaNccDauVao, string? GhiChu);

/// <summary>Đặt tồn của một lô về đúng số đếm được thực tế (kiểm kê) - hệ thống tự tính chênh lệch.</summary>
public sealed record DieuChinhTonApiRequest(string MaSanPham, string MaKho, string MaLo,
                                            decimal SoLuongThucTe, string? LyDo);

public sealed record NccDauVaoDto(string MaNccDauVao, string Ten);
