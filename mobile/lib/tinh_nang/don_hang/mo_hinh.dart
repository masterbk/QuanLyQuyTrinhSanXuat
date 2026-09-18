/// Mô hình đơn hàng bán cho app - khớp DTO của API /api/v1/don-hang-ban.
library;

import '../lenh_san_xuat/mo_hinh.dart' show soGon;

class DonHangBan {
  final int id;
  final String maDonHang;
  final String? tenKhachHang;
  final String maKho;
  final DateTime? ngayGiao;
  final String? diaChiGiao;
  final String? maNguoiGiao;
  final String? tenNguoiGiao;
  final String trangThai;            // ChoXacNhan | DaXacNhan | DangGiao | DaGiao | DaHuy
  final String trangThaiHienThi;
  final String nguon;                // NoiBo | HanoiCheck
  final String? ghiChu;
  final double tongTien;
  final DateTime? thoiGianGiaoUtc;
  final List<DongDonHang> dong;

  const DonHangBan({
    required this.id,
    required this.maDonHang,
    this.tenKhachHang,
    this.maKho = '',
    this.ngayGiao,
    this.diaChiGiao,
    this.maNguoiGiao,
    this.tenNguoiGiao,
    required this.trangThai,
    required this.trangThaiHienThi,
    this.nguon = 'NoiBo',
    this.ghiChu,
    this.tongTien = 0,
    this.thoiGianGiaoUtc,
    this.dong = const [],
  });

  bool get dangGiao => trangThai == 'DangGiao';
  bool get daGiao => trangThai == 'DaGiao';
  bool get chuaCoNguoiGiao => (maNguoiGiao ?? '').isEmpty;

  /// Đơn này có phải của tôi không (tôi đã nhận).
  bool cuaToi(String? maNhanSu) =>
      (maNhanSu ?? '').isNotEmpty && (maNguoiGiao ?? '').toLowerCase() == maNhanSu!.toLowerCase();

  String get tomTat => dong.isEmpty
      ? '(chưa có hàng)'
      : dong.map((d) => '${d.tenHienThi} ×${soGon(d.soLuong)}').join(', ');

  factory DonHangBan.tuJson(Map<String, dynamic> j) => DonHangBan(
        id: j['id'] as int,
        maDonHang: j['maDonHang'] as String? ?? '',
        tenKhachHang: j['tenKhachHang'] as String? ?? j['maKhachHang'] as String?,
        maKho: j['maKho'] as String? ?? '',
        ngayGiao: _ngay(j['ngayGiao']),
        diaChiGiao: j['diaChiGiao'] as String?,
        maNguoiGiao: j['maNguoiGiao'] as String?,
        tenNguoiGiao: j['tenNguoiGiao'] as String?,
        trangThai: j['trangThai'] as String? ?? '',
        trangThaiHienThi: j['trangThaiHienThi'] as String? ?? '',
        nguon: j['nguon'] as String? ?? 'NoiBo',
        ghiChu: j['ghiChu'] as String?,
        tongTien: _so(j['tongTien']),
        thoiGianGiaoUtc: _ngay(j['thoiGianGiaoUtc']),
        dong: ((j['dong'] as List?) ?? const [])
            .map((e) => DongDonHang.tuJson(e as Map<String, dynamic>))
            .toList(),
      );
}

class DongDonHang {
  final int id;
  final String maThanhPham;
  final String? tenThanhPham;
  final double soLuong;
  final List<XuatLoDon> xuatLo;

  const DongDonHang({
    required this.id,
    required this.maThanhPham,
    this.tenThanhPham,
    required this.soLuong,
    this.xuatLo = const [],
  });

  String get tenHienThi => tenThanhPham ?? maThanhPham;

  factory DongDonHang.tuJson(Map<String, dynamic> j) => DongDonHang(
        id: j['id'] as int? ?? 0,
        maThanhPham: j['maThanhPham'] as String? ?? '',
        tenThanhPham: j['tenThanhPham'] as String?,
        soLuong: _so(j['soLuong']),
        xuatLo: ((j['xuatLo'] as List?) ?? const [])
            .map((e) => XuatLoDon.tuJson(e as Map<String, dynamic>))
            .toList(),
      );
}

class XuatLoDon {
  final String maLo;
  final DateTime? hanSuDung;
  final double soLuong;

  const XuatLoDon({required this.maLo, this.hanSuDung, required this.soLuong});

  factory XuatLoDon.tuJson(Map<String, dynamic> j) => XuatLoDon(
        maLo: j['maLo'] as String? ?? '',
        hanSuDung: _ngay(j['hanSuDung']),
        soLuong: _so(j['soLuong']),
      );
}

double _so(dynamic v) => v is num ? v.toDouble() : double.tryParse('${v ?? ''}') ?? 0;

DateTime? _ngay(dynamic v) => (v is String && v.isNotEmpty) ? DateTime.tryParse(v) : null;
