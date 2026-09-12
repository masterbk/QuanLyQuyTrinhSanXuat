/// Mô hình dữ liệu lệnh sản xuất - khớp DTO của API /api/v1/lenh-san-xuat.
library;

class LenhSanXuat {
  final int id;
  final String maLenh;
  final String maThanhPham;
  final String? tenThanhPham;
  final double soLuong;
  final String maKho;
  final String maLoThanhPham;
  final DateTime? hanSuDungThanhPham;
  final DateTime ngaySanXuat;
  final String trangThai;          // MoiTao | HoanThanh | DaHuy
  final String trangThaiHienThi;
  final bool taoLoDongBo;
  final String? maLoDaTao;
  final DateTime? thoiGianHoanThanhUtc;
  final DateTime? thoiGianHuyUtc;
  final String? lyDoHuy;
  final String? ghiChu;
  final List<AnhLenh> anh;

  const LenhSanXuat({
    required this.id,
    required this.maLenh,
    required this.maThanhPham,
    this.tenThanhPham,
    required this.soLuong,
    required this.maKho,
    required this.maLoThanhPham,
    this.hanSuDungThanhPham,
    required this.ngaySanXuat,
    required this.trangThai,
    required this.trangThaiHienThi,
    this.taoLoDongBo = false,
    this.maLoDaTao,
    this.thoiGianHoanThanhUtc,
    this.thoiGianHuyUtc,
    this.lyDoHuy,
    this.ghiChu,
    this.anh = const [],
  });

  bool get moiTao => trangThai == 'MoiTao';
  bool get hoanThanh => trangThai == 'HoanThanh';
  bool get daHuy => trangThai == 'DaHuy';

  String get tenHienThi => tenThanhPham ?? maThanhPham;

  factory LenhSanXuat.tuJson(Map<String, dynamic> j) => LenhSanXuat(
        id: j['id'] as int,
        maLenh: j['maLenh'] as String? ?? '',
        maThanhPham: j['maThanhPham'] as String? ?? '',
        tenThanhPham: j['tenThanhPham'] as String?,
        soLuong: _so(j['soLuong']),
        maKho: j['maKho'] as String? ?? '',
        maLoThanhPham: j['maLoThanhPham'] as String? ?? '',
        hanSuDungThanhPham: _ngay(j['hanSuDungThanhPham']),
        ngaySanXuat: _ngay(j['ngaySanXuat']) ?? DateTime.now(),
        trangThai: j['trangThai'] as String? ?? '',
        trangThaiHienThi: j['trangThaiHienThi'] as String? ?? '',
        taoLoDongBo: j['taoLoDongBo'] as bool? ?? false,
        maLoDaTao: j['maLoDaTao'] as String?,
        thoiGianHoanThanhUtc: _ngay(j['thoiGianHoanThanhUtc']),
        thoiGianHuyUtc: _ngay(j['thoiGianHuyUtc']),
        lyDoHuy: j['lyDoHuy'] as String?,
        ghiChu: j['ghiChu'] as String?,
        anh: (j['anh'] as List?)?.map((e) => AnhLenh.tuJson(e as Map<String, dynamic>)).toList() ?? const [],
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

class ThanhPham {
  final String maSanPham;
  final String tenSanPham;
  final String? donViTinh;

  const ThanhPham({required this.maSanPham, required this.tenSanPham, this.donViTinh});

  factory ThanhPham.tuJson(Map<String, dynamic> j) => ThanhPham(
        maSanPham: j['maSanPham'] as String? ?? '',
        tenSanPham: j['tenSanPham'] as String? ?? '',
        donViTinh: j['donViTinh'] as String?,
      );
}

class Kho {
  final String maKho;
  final String tenKho;

  const Kho({required this.maKho, required this.tenKho});

  factory Kho.tuJson(Map<String, dynamic> j) =>
      Kho(maKho: j['maKho'] as String? ?? '', tenKho: j['tenKho'] as String? ?? '');
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
        duLieu: (j['duLieu'] as List? ?? []).map((e) => doc(e as Map<String, dynamic>)).toList(),
        trang: j['trang'] as int? ?? 1,
        soDong: j['soDong'] as int? ?? 20,
        tongSo: j['tongSo'] as int? ?? 0,
      );
}

double _so(dynamic v) => v == null ? 0 : (v is num ? v.toDouble() : double.tryParse('$v') ?? 0);

DateTime? _ngay(dynamic v) => (v is String && v.isNotEmpty) ? DateTime.tryParse(v) : null;

/// Bỏ số 0 thừa: 2.000 -> "2", 1.500 -> "1.5"
String soGon(double v) {
  final s = v.toStringAsFixed(3);
  return s.contains('.') ? s.replaceAll(RegExp(r'0+$'), '').replaceAll(RegExp(r'\.$'), '') : s;
}
