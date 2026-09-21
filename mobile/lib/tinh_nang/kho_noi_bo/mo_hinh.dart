/// Mô hình Kho nội bộ (tồn theo lô, nhập, điều chỉnh) - khớp DTO của /api/v1/kho-noi-bo.
/// Nghiệp vụ vận hành nội bộ, KHÔNG đồng bộ HanoiCheck.
library;

class TonKho {
  final String maSanPham;
  final String tenSanPham;
  final String loaiSanPham; // 'NguyenLieu' | 'ThanhPham'
  final String? donViTinh;
  final String maKho;
  final String tenKho;
  final String maLo;
  final DateTime? hanSuDung;
  final double soLuongTon;

  const TonKho({
    required this.maSanPham,
    required this.tenSanPham,
    required this.loaiSanPham,
    this.donViTinh,
    required this.maKho,
    required this.tenKho,
    required this.maLo,
    this.hanSuDung,
    required this.soLuongTon,
  });

  bool get laNguyenLieu => loaiSanPham == 'NguyenLieu';

  factory TonKho.tuJson(Map<String, dynamic> j) => TonKho(
        maSanPham: j['maSanPham'] as String? ?? '',
        tenSanPham: j['tenSanPham'] as String? ?? '',
        loaiSanPham: j['loaiSanPham'] as String? ?? '',
        donViTinh: j['donViTinh'] as String?,
        maKho: j['maKho'] as String? ?? '',
        tenKho: j['tenKho'] as String? ?? '',
        maLo: j['maLo'] as String? ?? '',
        hanSuDung: _ngay(j['hanSuDung']),
        soLuongTon: _so(j['soLuongTon']),
      );
}

/// Một dòng lịch sử giao dịch kho (nhập/xuất/điều chỉnh) gần đây.
class LichSuKho {
  final DateTime thoiGianUtc;
  final String tenLoai;
  final String maSanPham;
  final String? tenSanPham;
  final String maKho;
  final String maLo;
  final double soLuong;
  final String? ghiChu;

  const LichSuKho({
    required this.thoiGianUtc,
    required this.tenLoai,
    required this.maSanPham,
    this.tenSanPham,
    required this.maKho,
    required this.maLo,
    required this.soLuong,
    this.ghiChu,
  });

  String get tenHienThi => tenSanPham ?? maSanPham;

  factory LichSuKho.tuJson(Map<String, dynamic> j) => LichSuKho(
        thoiGianUtc: DateTime.tryParse(j['thoiGianUtc'] as String? ?? '') ?? DateTime.now(),
        tenLoai: j['tenLoai'] as String? ?? '',
        maSanPham: j['maSanPham'] as String? ?? '',
        tenSanPham: j['tenSanPham'] as String?,
        maKho: j['maKho'] as String? ?? '',
        maLo: j['maLo'] as String? ?? '',
        soLuong: _so(j['soLuong']),
        ghiChu: j['ghiChu'] as String?,
      );
}

class NccDauVao {
  final String maNccDauVao;
  final String ten;

  const NccDauVao({required this.maNccDauVao, required this.ten});

  factory NccDauVao.tuJson(Map<String, dynamic> j) => NccDauVao(
        maNccDauVao: j['maNccDauVao'] as String? ?? '',
        ten: j['ten'] as String? ?? '',
      );
}

double _so(dynamic v) => v is num ? v.toDouble() : double.tryParse('${v ?? ''}') ?? 0;

DateTime? _ngay(dynamic v) => (v is String && v.isNotEmpty) ? DateTime.tryParse(v) : null;
