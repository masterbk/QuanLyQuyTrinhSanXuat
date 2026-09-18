/// Mô hình dữ liệu lệnh sản xuất - khớp DTO của API /api/v1/lenh-san-xuat.
library;

import '../../loi/gio_viet_nam.dart';

/// Lệnh sản xuất: phần đầu dùng chung + nhiều sản phẩm, mỗi sản phẩm là một lô riêng.
class LenhSanXuat {
  final int id;
  final String maLenh;
  final String maKho;
  final DateTime ngaySanXuat;
  final String trangThai;          // MoiTao | HoanThanh | DaHuy
  final String trangThaiHienThi;
  final bool taoLoDongBo;
  final DateTime? thoiGianHoanThanhUtc;
  final DateTime? thoiGianHuyUtc;
  final String? lyDoHuy;
  final String? ghiChu;
  final List<SanPhamLenh> sanPham;

  /// Nhân viên đã quét mã QR tham gia lệnh.
  final List<ThamGiaLenh> thamGia;

  const LenhSanXuat({
    required this.id,
    required this.maLenh,
    required this.maKho,
    required this.ngaySanXuat,
    required this.trangThai,
    required this.trangThaiHienThi,
    this.taoLoDongBo = false,
    this.thoiGianHoanThanhUtc,
    this.thoiGianHuyUtc,
    this.lyDoHuy,
    this.ghiChu,
    this.sanPham = const [],
    this.thamGia = const [],
  });

  bool get moiTao => trangThai == 'MoiTao';
  bool get hoanThanh => trangThai == 'HoanThanh';
  bool get daHuy => trangThai == 'DaHuy';

  /// Tóm tắt một dòng cho danh sách: "Bánh mì ×10, Bánh ngọt ×5".
  String get tomTat => sanPham.isEmpty
      ? '(chưa có sản phẩm)'
      : sanPham.map((s) => '${s.tenHienThi} ×${soGon(s.soLuong)}').join(', ');

  factory LenhSanXuat.tuJson(Map<String, dynamic> j) => LenhSanXuat(
        id: j['id'] as int,
        maLenh: j['maLenh'] as String? ?? '',
        maKho: j['maKho'] as String? ?? '',
        ngaySanXuat: _ngay(j['ngaySanXuat']) ?? bayGioVietNam(),
        trangThai: j['trangThai'] as String? ?? '',
        trangThaiHienThi: j['trangThaiHienThi'] as String? ?? '',
        taoLoDongBo: j['taoLoDongBo'] as bool? ?? false,
        thoiGianHoanThanhUtc: _ngay(j['thoiGianHoanThanhUtc']),
        thoiGianHuyUtc: _ngay(j['thoiGianHuyUtc']),
        lyDoHuy: j['lyDoHuy'] as String?,
        ghiChu: j['ghiChu'] as String?,
        sanPham: _ds(j['sanPham'], SanPhamLenh.tuJson),
        thamGia: _ds(j['thamGia'], ThamGiaLenh.tuJson),
      );
}

/// Một nhân viên đã quét mã QR tham gia lệnh.
class ThamGiaLenh {
  final String maNhanSu;
  final String hoTen;
  final DateTime thoiGianUtc;

  const ThamGiaLenh({required this.maNhanSu, required this.hoTen, required this.thoiGianUtc});

  factory ThamGiaLenh.tuJson(Map<String, dynamic> j) => ThamGiaLenh(
        maNhanSu: j['maNhanSu'] as String? ?? '',
        hoTen: j['hoTen'] as String? ?? j['maNhanSu'] as String? ?? '',
        thoiGianUtc: _ngay(j['thoiGianUtc']) ?? DateTime.now().toUtc(),
      );
}

/// Tiền tố nội dung QR của lệnh sản xuất (khớp máy chủ).
const tienToQrLenh = 'LSX:';

/// Mã lệnh đọc từ QR hoặc ô nhập tay. QR phải có tiền tố "LSX:" (QR khác loại như link tra cứu lô trả null);
/// ô nhập tay nhận thêm mã lệnh gõ thẳng.
String? maLenhTuQr(String? noiDung, {bool nhapTay = false}) {
  final s = (noiDung ?? '').trim();
  if (s.toUpperCase().startsWith(tienToQrLenh)) {
    final ma = s.substring(tienToQrLenh.length).trim();
    return ma.isEmpty ? null : ma;
  }
  if (!nhapTay || s.isEmpty || s.contains('://') || s.contains(' ')) return null;
  return s;
}

/// Khâu tick sẵn ở màn tham gia: đã tham gia thì giữ các khâu đang làm, chưa tham gia thì chọn tất cả.
Set<int> khauChonMacDinh(LenhSanXuat lenh, String maNhanSu) {
  final tatCa = [for (final s in lenh.sanPham) ...s.khau];
  final cuaToi = tatCa
      .where((k) => k.nguoiThucHien.any((m) => m.toLowerCase() == maNhanSu.toLowerCase()))
      .map((k) => k.id)
      .toSet();
  return cuaToi.isNotEmpty ? cuaToi : tatCa.map((k) => k.id).toSet();
}

/// Một thành phẩm trong lệnh = một lô: quy trình, các khâu (người + cơ sở) và ảnh lô.
class SanPhamLenh {
  final int id;
  final String maThanhPham;
  final String? tenThanhPham;
  final double soLuong;
  final String maLoThanhPham;
  final DateTime? hanSuDung;
  final String maQuyTrinh;
  final String? tenQuyTrinh;
  final String? maLoDaTao;
  final List<KhauLenh> khau;
  final List<AnhLenh> anh;

  const SanPhamLenh({
    required this.id,
    required this.maThanhPham,
    this.tenThanhPham,
    required this.soLuong,
    required this.maLoThanhPham,
    this.hanSuDung,
    required this.maQuyTrinh,
    this.tenQuyTrinh,
    this.maLoDaTao,
    this.khau = const [],
    this.anh = const [],
  });

  String get tenHienThi => tenThanhPham ?? maThanhPham;

  factory SanPhamLenh.tuJson(Map<String, dynamic> j) => SanPhamLenh(
        id: j['id'] as int? ?? 0,
        maThanhPham: j['maThanhPham'] as String? ?? '',
        tenThanhPham: j['tenThanhPham'] as String?,
        soLuong: _so(j['soLuong']),
        maLoThanhPham: j['maLoThanhPham'] as String? ?? '',
        hanSuDung: _ngay(j['hanSuDung']),
        maQuyTrinh: j['maQuyTrinh'] as String? ?? '',
        tenQuyTrinh: j['tenQuyTrinh'] as String?,
        maLoDaTao: j['maLoDaTao'] as String?,
        khau: _ds(j['khau'], KhauLenh.tuJson),
        anh: _ds(j['anh'], AnhLenh.tuJson),
      );
}

class KhauLenh {
  final int id;
  final String maKhau;
  final String? tenKhau;
  final int thuTu;
  final String maCoSo;
  final String? tenCoSo;
  final List<String> nguoiThucHien;
  final String? ghiChu;

  const KhauLenh({
    required this.id,
    required this.maKhau,
    this.tenKhau,
    required this.thuTu,
    required this.maCoSo,
    this.tenCoSo,
    this.nguoiThucHien = const [],
    this.ghiChu,
  });

  String get tenHienThi => tenKhau ?? maKhau;

  factory KhauLenh.tuJson(Map<String, dynamic> j) => KhauLenh(
        id: j['id'] as int? ?? 0,
        maKhau: j['maKhau'] as String? ?? '',
        tenKhau: j['tenKhau'] as String?,
        thuTu: j['thuTu'] as int? ?? 0,
        maCoSo: j['maCoSo'] as String? ?? '',
        tenCoSo: j['tenCoSo'] as String?,
        nguoiThucHien: (j['nguoiThucHien'] as List?)?.map((e) => '$e').toList() ?? const [],
        ghiChu: j['ghiChu'] as String?,
      );
}

class AnhLenh {
  final String maFile;
  final String tenFile;
  final String duongDan;

  const AnhLenh({required this.maFile, required this.tenFile, required this.duongDan});

  factory AnhLenh.tuJson(Map<String, dynamic> j) => AnhLenh(
        maFile: j['maFile'] as String? ?? '',
        tenFile: j['tenFile'] as String? ?? '',
        duongDan: j['duongDan'] as String? ?? '',
      );
}

// ==================== Danh mục ====================

class ThanhPham {
  final String maSanPham;
  final String tenSanPham;
  final String? donViTinh;

  /// Quy trình mặc định khai ở danh mục thành phẩm (đổi được khi lập lệnh).
  final String? maQuyTrinh;

  const ThanhPham({required this.maSanPham, required this.tenSanPham, this.donViTinh, this.maQuyTrinh});

  factory ThanhPham.tuJson(Map<String, dynamic> j) => ThanhPham(
        maSanPham: j['maSanPham'] as String? ?? '',
        tenSanPham: j['tenSanPham'] as String? ?? '',
        donViTinh: j['donViTinh'] as String?,
        maQuyTrinh: j['maQuyTrinh'] as String?,
      );
}

class Kho {
  final String maKho;
  final String tenKho;

  const Kho({required this.maKho, required this.tenKho});

  factory Kho.tuJson(Map<String, dynamic> j) =>
      Kho(maKho: j['maKho'] as String? ?? '', tenKho: j['tenKho'] as String? ?? '');
}

class QuyTrinh {
  final String maQuyTrinh;
  final String tenQuyTrinh;
  final List<KhauQuyTrinh> khau;

  const QuyTrinh({required this.maQuyTrinh, required this.tenQuyTrinh, this.khau = const []});

  factory QuyTrinh.tuJson(Map<String, dynamic> j) => QuyTrinh(
        maQuyTrinh: j['maQuyTrinh'] as String? ?? '',
        tenQuyTrinh: j['tenQuyTrinh'] as String? ?? '',
        khau: _ds(j['khau'], KhauQuyTrinh.tuJson),
      );
}

class KhauQuyTrinh {
  final String maKhau;
  final String? tenKhau;
  final int thuTu;

  const KhauQuyTrinh({required this.maKhau, this.tenKhau, required this.thuTu});

  factory KhauQuyTrinh.tuJson(Map<String, dynamic> j) => KhauQuyTrinh(
        maKhau: j['maKhau'] as String? ?? '',
        tenKhau: j['tenKhau'] as String?,
        thuTu: j['thuTu'] as int? ?? 0,
      );
}

class CoSo {
  final String maCoSo;
  final String tenCoSo;
  final String? diaChi;

  const CoSo({required this.maCoSo, required this.tenCoSo, this.diaChi});

  factory CoSo.tuJson(Map<String, dynamic> j) => CoSo(
        maCoSo: j['maCoSo'] as String? ?? '',
        tenCoSo: j['tenCoSo'] as String? ?? '',
        diaChi: j['diaChi'] as String?,
      );
}

class NhanSu {
  final String maNhanSu;
  final String hoTen;
  final String? viTri;

  const NhanSu({required this.maNhanSu, required this.hoTen, this.viTri});

  factory NhanSu.tuJson(Map<String, dynamic> j) => NhanSu(
        maNhanSu: j['maNhanSu'] as String? ?? '',
        hoTen: j['hoTen'] as String? ?? '',
        viTri: j['viTri'] as String?,
      );
}

/// Một dòng trong bảng xem trước nguyên liệu cần cho mẻ sản xuất.
class NguyenLieuCan {
  final String maNguyenLieu;
  final String tenNguyenLieu;
  final String? donViTinh;
  final double can;
  final double ton;
  final bool du;

  const NguyenLieuCan({
    required this.maNguyenLieu,
    required this.tenNguyenLieu,
    this.donViTinh,
    required this.can,
    required this.ton,
    required this.du,
  });

  factory NguyenLieuCan.tuJson(Map<String, dynamic> j) => NguyenLieuCan(
        maNguyenLieu: j['maNguyenLieu'] as String? ?? '',
        tenNguyenLieu: j['tenNguyenLieu'] as String? ?? '',
        donViTinh: j['donViTinh'] as String?,
        can: _so(j['can']),
        ton: _so(j['ton']),
        du: j['du'] as bool? ?? false,
      );
}

/// Một trang dữ liệu kèm tổng số - dùng cho cuộn tải thêm.
class TrangDuLieu<T> {
  final List<T> duLieu;
  final int trang;
  final int soDong;
  final int tongSo;

  const TrangDuLieu({required this.duLieu, required this.trang, required this.soDong, required this.tongSo});

  bool get conTrangSau => trang * soDong < tongSo;

  factory TrangDuLieu.tuJson(Map<String, dynamic> j, T Function(Map<String, dynamic>) doc) =>
      TrangDuLieu(
        duLieu: _ds(j['duLieu'], doc),
        trang: j['trang'] as int? ?? 1,
        soDong: j['soDong'] as int? ?? 20,
        tongSo: j['tongSo'] as int? ?? 0,
      );
}

List<T> _ds<T>(dynamic v, T Function(Map<String, dynamic>) doc) =>
    (v as List? ?? const []).map((e) => doc(e as Map<String, dynamic>)).toList();

double _so(dynamic v) => v == null ? 0 : (v is num ? v.toDouble() : double.tryParse('$v') ?? 0);

DateTime? _ngay(dynamic v) => (v is String && v.isNotEmpty) ? DateTime.tryParse(v) : null;

/// Bỏ số 0 thừa: 2.000 -> "2", 1.500 -> "1.5"
String soGon(double v) {
  final s = v.toStringAsFixed(3);
  return s.contains('.') ? s.replaceAll(RegExp(r'0+$'), '').replaceAll(RegExp(r'\.$'), '') : s;
}

/// API nhận ngày dạng yyyy-MM-dd (không kèm giờ).
String chuoiNgay(DateTime d) =>
    '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';
