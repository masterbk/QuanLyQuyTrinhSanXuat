/// Mô hình đơn hàng bán cho app - khớp DTO của API /api/v1/don-hang-ban.
library;

import '../lenh_san_xuat/mo_hinh.dart' show soGon;

class DonHangBan {
  final int id;
  final String maDonHang;
  final String maKhachHang;
  final String? tenKhachHang;
  final String maKho;
  final DateTime? ngayDat;
  final DateTime? ngayGiao;
  final String? diaChiGiao;
  final String? maNguoiGiao;
  final String? tenNguoiGiao;
  final String trangThai;            // ChoXacNhan | DaXacNhan | DangGiao | DaGiao | DaHuy
  final String trangThaiHienThi;
  final String nguon;                // NoiBo | HanoiCheck
  final String? ghiChu;
  final String? lyDoHuy;
  final double tongTien;
  final DateTime? thoiGianGiaoUtc;
  final List<DongDonHang> dong;

  const DonHangBan({
    required this.id,
    required this.maDonHang,
    this.maKhachHang = '',
    this.tenKhachHang,
    this.maKho = '',
    this.ngayDat,
    this.ngayGiao,
    this.diaChiGiao,
    this.maNguoiGiao,
    this.tenNguoiGiao,
    required this.trangThai,
    required this.trangThaiHienThi,
    this.nguon = 'NoiBo',
    this.ghiChu,
    this.lyDoHuy,
    this.tongTien = 0,
    this.thoiGianGiaoUtc,
    this.dong = const [],
  });

  bool get choXacNhan => trangThai == 'ChoXacNhan';
  bool get daXacNhan => trangThai == 'DaXacNhan';
  bool get dangGiao => trangThai == 'DangGiao';
  bool get daGiao => trangThai == 'DaGiao';
  bool get daHuy => trangThai == 'DaHuy';
  bool get chuaXuatKho => trangThai == 'ChoXacNhan' || trangThai == 'DaXacNhan';
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
        maKhachHang: j['maKhachHang'] as String? ?? '',
        tenKhachHang: j['tenKhachHang'] as String? ?? j['maKhachHang'] as String?,
        maKho: j['maKho'] as String? ?? '',
        ngayDat: _ngay(j['ngayDat']),
        ngayGiao: _ngay(j['ngayGiao']),
        diaChiGiao: j['diaChiGiao'] as String?,
        maNguoiGiao: j['maNguoiGiao'] as String?,
        tenNguoiGiao: j['tenNguoiGiao'] as String?,
        trangThai: j['trangThai'] as String? ?? '',
        trangThaiHienThi: j['trangThaiHienThi'] as String? ?? '',
        nguon: j['nguon'] as String? ?? 'NoiBo',
        ghiChu: j['ghiChu'] as String?,
        lyDoHuy: j['lyDoHuy'] as String?,
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
  final double donGia;
  final List<XuatLoDon> xuatLo;

  const DongDonHang({
    required this.id,
    required this.maThanhPham,
    this.tenThanhPham,
    required this.soLuong,
    this.donGia = 0,
    this.xuatLo = const [],
  });

  String get tenHienThi => tenThanhPham ?? maThanhPham;

  factory DongDonHang.tuJson(Map<String, dynamic> j) => DongDonHang(
        id: j['id'] as int? ?? 0,
        maThanhPham: j['maThanhPham'] as String? ?? '',
        tenThanhPham: j['tenThanhPham'] as String?,
        soLuong: _so(j['soLuong']),
        donGia: _so(j['donGia']),
        xuatLo: ((j['xuatLo'] as List?) ?? const [])
            .map((e) => XuatLoDon.tuJson(e as Map<String, dynamic>))
            .toList(),
      );
}

/// Khách hàng của cơ sở - dùng cho dropdown khi lập/sửa đơn.
class KhachHang {
  final String maKhachHang;
  final String tenKhachHang;
  final String? diaChi;

  const KhachHang({required this.maKhachHang, required this.tenKhachHang, this.diaChi});

  factory KhachHang.tuJson(Map<String, dynamic> j) => KhachHang(
        maKhachHang: j['maKhachHang'] as String? ?? '',
        tenKhachHang: j['tenKhachHang'] as String? ?? '',
        diaChi: j['diaChi'] as String?,
      );
}

/// Một dòng hàng khi xuất kho: số lượng cần và các lô có thể lấy (gợi ý FEFO).
class DongXuatKho {
  final int dongId;
  final String maThanhPham;
  final String tenThanhPham;
  final String? donViTinh;
  final double soLuong;
  final List<LoCoTheXuat> lo;

  const DongXuatKho({
    required this.dongId,
    required this.maThanhPham,
    required this.tenThanhPham,
    this.donViTinh,
    required this.soLuong,
    this.lo = const [],
  });

  factory DongXuatKho.tuJson(Map<String, dynamic> j) => DongXuatKho(
        dongId: j['dongId'] as int? ?? 0,
        maThanhPham: j['maThanhPham'] as String? ?? '',
        tenThanhPham: j['tenThanhPham'] as String? ?? '',
        donViTinh: j['donViTinh'] as String?,
        soLuong: _so(j['soLuong']),
        lo: ((j['lo'] as List?) ?? const [])
            .map((e) => LoCoTheXuat.tuJson(e as Map<String, dynamic>))
            .toList(),
      );
}

/// Một lô còn tồn: tồn hiện tại và số lượng hệ thống gợi ý lấy (sẽ sửa được trước khi gửi).
class LoCoTheXuat {
  final String maLo;
  final DateTime? hanSuDung;
  final double ton;
  final double goiY;

  const LoCoTheXuat({required this.maLo, this.hanSuDung, required this.ton, required this.goiY});

  factory LoCoTheXuat.tuJson(Map<String, dynamic> j) => LoCoTheXuat(
        maLo: j['maLo'] as String? ?? '',
        hanSuDung: _ngay(j['hanSuDung']),
        ton: _so(j['ton']),
        goiY: _so(j['goiY']),
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
