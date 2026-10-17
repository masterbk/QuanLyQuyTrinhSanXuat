/// Mô hình cho màn Suất ăn: hôm nay nấu món gì (thực đơn theo thứ) và bao nhiêu suất cho từng trường.
library;

/// Một món trong thực đơn, kèm bữa ăn phục vụ.
class MonTrongThucDon {
  final String maMonAn;
  final String tenMonAn;
  final String tenBuoiAn;

  const MonTrongThucDon({required this.maMonAn, required this.tenMonAn, required this.tenBuoiAn});

  factory MonTrongThucDon.tuJson(Map<String, dynamic> j) => MonTrongThucDon(
        maMonAn: j['maMonAn'] as String? ?? '',
        tenMonAn: j['tenMonAn'] as String? ?? '',
        tenBuoiAn: j['tenBuoiAn'] as String? ?? '',
      );
}

/// Một thực đơn áp dụng cho ngày đang xem.
class ThucDonTrongNgay {
  final int id;
  final String maThucDon;
  final String tenThucDon;
  final String tenNhomTuoi;

  /// Rỗng = thực đơn dùng chung cho mọi trường.
  final List<String> tenTruong;
  final List<MonTrongThucDon> danhSachMon;

  const ThucDonTrongNgay({
    required this.id,
    required this.maThucDon,
    required this.tenThucDon,
    required this.tenNhomTuoi,
    this.tenTruong = const [],
    this.danhSachMon = const [],
  });

  factory ThucDonTrongNgay.tuJson(Map<String, dynamic> j) => ThucDonTrongNgay(
        id: j['id'] as int? ?? 0,
        maThucDon: j['maThucDon'] as String? ?? '',
        tenThucDon: j['tenThucDon'] as String? ?? '',
        tenNhomTuoi: j['tenNhomTuoi'] as String? ?? '',
        tenTruong: ((j['tenTruong'] as List?) ?? const []).map((e) => e as String).toList(),
        danhSachMon: ((j['danhSachMon'] as List?) ?? const [])
            .map((e) => MonTrongThucDon.tuJson(e as Map<String, dynamic>))
            .toList(),
      );

  /// Các món gom theo bữa ăn, giữ nguyên thứ tự bữa mà máy chủ đã sắp.
  Map<String, List<MonTrongThucDon>> get monTheoBua {
    final theoBua = <String, List<MonTrongThucDon>>{};
    for (final m in danhSachMon) {
      theoBua.putIfAbsent(m.tenBuoiAn, () => []).add(m);
    }
    return theoBua;
  }
}

/// Số suất của một món.
class SuatTheoMon {
  final String maMonAn;
  final String tenMonAn;
  final double soSuat;

  const SuatTheoMon({required this.maMonAn, required this.tenMonAn, required this.soSuat});

  factory SuatTheoMon.tuJson(Map<String, dynamic> j) => SuatTheoMon(
        maMonAn: j['maMonAn'] as String? ?? '',
        tenMonAn: j['tenMonAn'] as String? ?? '',
        soSuat: (j['soSuat'] as num?)?.toDouble() ?? 0,
      );
}

/// Số suất phải nấu cho một khách hàng (trường) trong ngày.
class SuatTheoKhach {
  final String maKhachHang;
  final String tenKhachHang;
  final double tongSuat;
  final List<SuatTheoMon> danhSachMon;

  const SuatTheoKhach({
    required this.maKhachHang,
    required this.tenKhachHang,
    required this.tongSuat,
    this.danhSachMon = const [],
  });

  factory SuatTheoKhach.tuJson(Map<String, dynamic> j) => SuatTheoKhach(
        maKhachHang: j['maKhachHang'] as String? ?? '',
        tenKhachHang: j['tenKhachHang'] as String? ?? '',
        tongSuat: (j['tongSuat'] as num?)?.toDouble() ?? 0,
        danhSachMon: ((j['danhSachMon'] as List?) ?? const [])
            .map((e) => SuatTheoMon.tuJson(e as Map<String, dynamic>))
            .toList(),
      );
}

/// Bản tin suất ăn của một ngày.
class SuatAnNgay {
  final DateTime ngay;
  final String tenThu;
  final List<ThucDonTrongNgay> thucDon;
  final List<SuatTheoKhach> theoKhachHang;
  final double tongSuat;
  final List<SuatTheoMon> tongTheoMon;

  const SuatAnNgay({
    required this.ngay,
    required this.tenThu,
    this.thucDon = const [],
    this.theoKhachHang = const [],
    this.tongSuat = 0,
    this.tongTheoMon = const [],
  });

  factory SuatAnNgay.tuJson(Map<String, dynamic> j) => SuatAnNgay(
        ngay: DateTime.tryParse(j['ngay'] as String? ?? '') ?? DateTime.now(),
        tenThu: j['tenThu'] as String? ?? '',
        thucDon: ((j['thucDon'] as List?) ?? const [])
            .map((e) => ThucDonTrongNgay.tuJson(e as Map<String, dynamic>))
            .toList(),
        theoKhachHang: ((j['theoKhachHang'] as List?) ?? const [])
            .map((e) => SuatTheoKhach.tuJson(e as Map<String, dynamic>))
            .toList(),
        tongSuat: (j['tongSuat'] as num?)?.toDouble() ?? 0,
        tongTheoMon: ((j['tongTheoMon'] as List?) ?? const [])
            .map((e) => SuatTheoMon.tuJson(e as Map<String, dynamic>))
            .toList(),
      );

  bool get trong => thucDon.isEmpty && theoKhachHang.isEmpty;
}
