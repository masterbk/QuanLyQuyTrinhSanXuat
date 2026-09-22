/// Mô hình Quản lý danh mục (Thực phẩm/SKU + Định mức, Khâu sản xuất, Quy trình sản xuất) - khớp DTO
/// của /api/v1/quan-ly. Chỉ quản trị/nhân viên nhập liệu được sửa.
library;

/// Thực phẩm/SKU: nguyên liệu hoặc thành phẩm.
class SanPham {
  final int id;
  final bool dongBoHnC;
  final String maSanPham;
  final String tenSanPham;
  final String loaiSanPham; // 'ThanhPham' | 'NguyenLieu'
  final String? donViTinh;
  final double? tonToiThieu;
  final String maLoaiSp;
  final String? maThucPhamChuan;
  final String? gtin;
  final String? quocGia;
  final String? moTa;
  final String? maQuyTrinh;

  const SanPham({
    required this.id,
    this.dongBoHnC = true,
    required this.maSanPham,
    required this.tenSanPham,
    this.loaiSanPham = 'ThanhPham',
    this.donViTinh,
    this.tonToiThieu,
    this.maLoaiSp = '',
    this.maThucPhamChuan,
    this.gtin,
    this.quocGia,
    this.moTa,
    this.maQuyTrinh,
  });

  bool get laThanhPham => loaiSanPham == 'ThanhPham';

  factory SanPham.tuJson(Map<String, dynamic> j) => SanPham(
        id: j['id'] as int? ?? 0,
        dongBoHnC: j['dongBoHnC'] as bool? ?? true,
        maSanPham: j['maSanPham'] as String? ?? '',
        tenSanPham: j['tenSanPham'] as String? ?? '',
        loaiSanPham: j['loaiSanPham'] as String? ?? 'ThanhPham',
        donViTinh: j['donViTinh'] as String?,
        tonToiThieu: _so(j['tonToiThieu']),
        maLoaiSp: j['maLoaiSp'] as String? ?? '',
        maThucPhamChuan: j['maThucPhamChuan'] as String?,
        gtin: j['gtin'] as String?,
        quocGia: j['quocGia'] as String?,
        moTa: j['moTa'] as String?,
        maQuyTrinh: j['maQuyTrinh'] as String?,
      );

  Map<String, dynamic> raJson() => {
        'dongBoHnC': dongBoHnC,
        'maSanPham': maSanPham,
        'tenSanPham': tenSanPham,
        'loaiSanPham': loaiSanPham,
        'donViTinh': donViTinh,
        'tonToiThieu': tonToiThieu,
        'maLoaiSp': maLoaiSp,
        'maThucPhamChuan': maThucPhamChuan,
        'gtin': gtin,
        'quocGia': quocGia,
        'moTa': moTa,
        'maQuyTrinh': maQuyTrinh,
      };
}

/// Một dòng định mức (công thức) của thành phẩm: cần bao nhiêu nguyên liệu cho 1 đơn vị thành phẩm.
class DinhMucDong {
  final String maNguyenLieu;
  final String? tenNguyenLieu;
  final double soLuong;
  final double haoHutPhanTram;

  const DinhMucDong({
    required this.maNguyenLieu,
    this.tenNguyenLieu,
    required this.soLuong,
    this.haoHutPhanTram = 0,
  });

  String get tenHienThi => tenNguyenLieu ?? maNguyenLieu;

  factory DinhMucDong.tuJson(Map<String, dynamic> j) => DinhMucDong(
        maNguyenLieu: j['maNguyenLieu'] as String? ?? '',
        tenNguyenLieu: j['tenNguyenLieu'] as String?,
        soLuong: _so(j['soLuong']) ?? 0,
        haoHutPhanTram: _so(j['haoHutPhanTram']) ?? 0,
      );

  Map<String, dynamic> raJson() =>
      {'maNguyenLieu': maNguyenLieu, 'soLuong': soLuong, 'haoHutPhanTram': haoHutPhanTram};

  DinhMucDong sao({double? soLuong, double? haoHutPhanTram}) => DinhMucDong(
        maNguyenLieu: maNguyenLieu,
        tenNguyenLieu: tenNguyenLieu,
        soLuong: soLuong ?? this.soLuong,
        haoHutPhanTram: haoHutPhanTram ?? this.haoHutPhanTram,
      );
}

/// Khâu sản xuất (danh mục dùng lại cho quy trình, lô sản xuất). Mã do hệ thống cấp, không sửa được.
class Khau {
  final int id;
  final bool dongBoHnC;
  final String maKhau;
  final String tenKhau;
  final String? ghiChu;

  const Khau({
    required this.id,
    this.dongBoHnC = true,
    required this.maKhau,
    required this.tenKhau,
    this.ghiChu,
  });

  factory Khau.tuJson(Map<String, dynamic> j) => Khau(
        id: j['id'] as int? ?? 0,
        dongBoHnC: j['dongBoHnC'] as bool? ?? true,
        maKhau: j['maKhau'] as String? ?? '',
        tenKhau: j['tenKhau'] as String? ?? '',
        ghiChu: j['ghiChu'] as String?,
      );
}

/// Một khâu trong quy trình, kèm thứ tự thực hiện.
class QuyTrinhKhauDong {
  final String maKhau;
  final String? tenKhau;
  final int thuTu;

  const QuyTrinhKhauDong({required this.maKhau, this.tenKhau, required this.thuTu});

  String get tenHienThi => tenKhau ?? maKhau;

  factory QuyTrinhKhauDong.tuJson(Map<String, dynamic> j) => QuyTrinhKhauDong(
        maKhau: j['maKhau'] as String? ?? '',
        tenKhau: j['tenKhau'] as String?,
        thuTu: j['thuTu'] as int? ?? 0,
      );
}

/// Quy trình sản xuất: chuỗi khâu có thứ tự.
class QuyTrinhQuanLy {
  final int id;
  final bool dongBoHnC;
  final String maQuyTrinh;
  final String tenQuyTrinh;
  final int? maDanhMucThucPham;
  final List<QuyTrinhKhauDong> danhSachKhau;

  const QuyTrinhQuanLy({
    required this.id,
    this.dongBoHnC = true,
    required this.maQuyTrinh,
    required this.tenQuyTrinh,
    this.maDanhMucThucPham,
    this.danhSachKhau = const [],
  });

  String get tomTatKhau =>
      danhSachKhau.isEmpty ? '(chưa có khâu)' : danhSachKhau.map((k) => k.tenHienThi).join(' → ');

  factory QuyTrinhQuanLy.tuJson(Map<String, dynamic> j) => QuyTrinhQuanLy(
        id: j['id'] as int? ?? 0,
        dongBoHnC: j['dongBoHnC'] as bool? ?? true,
        maQuyTrinh: j['maQuyTrinh'] as String? ?? '',
        tenQuyTrinh: j['tenQuyTrinh'] as String? ?? '',
        maDanhMucThucPham: j['maDanhMucThucPham'] as int?,
        danhSachKhau: (j['danhSachKhau'] as List?)
                ?.map((e) => QuyTrinhKhauDong.tuJson(e as Map<String, dynamic>))
                .toList() ??
            const [],
      );
}

double? _so(dynamic v) {
  if (v == null) return null;
  if (v is num) return v.toDouble();
  return double.tryParse('$v');
}
