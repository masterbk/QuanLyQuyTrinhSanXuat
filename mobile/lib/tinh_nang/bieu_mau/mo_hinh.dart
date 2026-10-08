/// Mô hình dữ liệu biểu mẫu kiểm soát - khớp DTO của API /api/v1/bieu-mau và /api/v1/phieu-ghi-nhan.
library;

import 'dart:convert';

class BieuMau {
  final int id;
  final String maHieu;
  final String ten;
  final String boCuc; // TheoNgay | Checklist | NhieuDongTuDo
  final String? tanSuat;
  final String? ghiChuChan;
  final bool motPhieuMoiNgay; // true = 1 phiếu/ngày (khóa theo ngày); false = nhiều phiếu/ngày (sổ/log)
  final List<TruongBieuMau> truong;
  final List<HangMucBieuMau> hangMuc;

  const BieuMau({
    required this.id,
    required this.maHieu,
    required this.ten,
    required this.boCuc,
    this.tanSuat,
    this.ghiChuChan,
    this.motPhieuMoiNgay = true,
    this.truong = const [],
    this.hangMuc = const [],
  });

  bool get laChecklist => boCuc == 'Checklist';
  bool get laNhieuDong => boCuc == 'NhieuDongTuDo';

  factory BieuMau.tuJson(Map<String, dynamic> j) => BieuMau(
        id: j['id'] as int? ?? 0,
        maHieu: j['maHieu'] as String? ?? '',
        ten: j['ten'] as String? ?? '',
        boCuc: j['boCuc'] as String? ?? 'TheoNgay',
        tanSuat: j['tanSuat'] as String?,
        ghiChuChan: j['ghiChuChan'] as String?,
        motPhieuMoiNgay: j['motPhieuMoiNgay'] as bool? ?? true,
        truong: ((j['truong'] as List?) ?? const [])
            .map((e) => TruongBieuMau.tuJson(e as Map<String, dynamic>))
            .toList(),
        hangMuc: ((j['hangMuc'] as List?) ?? const [])
            .map((e) => HangMucBieuMau.tuJson(e as Map<String, dynamic>))
            .toList(),
      );
}

class TruongBieuMau {
  final String ma;
  final String ten;
  final String kieu; // Text | So | Gio | Ngay | DatKhongDat | ChonSanPham | ChonNhanSu | ChonNcc | ChonCoSo | Anh | LuaChon
  final bool laDauPhieu; // true = trường đầu phiếu (nhập 1 lần cho cả phiếu)
  final bool batBuoc;
  final String? donVi;
  final String? giaTriChuan;
  final String? tuyChonCsv;
  final String? nhom;

  const TruongBieuMau({
    required this.ma,
    required this.ten,
    required this.kieu,
    this.laDauPhieu = false,
    this.batBuoc = false,
    this.donVi,
    this.giaTriChuan,
    this.tuyChonCsv,
    this.nhom,
  });

  List<String> get tuyChon =>
      (tuyChonCsv ?? '').split(',').map((e) => e.trim()).where((e) => e.isNotEmpty).toList();

  factory TruongBieuMau.tuJson(Map<String, dynamic> j) => TruongBieuMau(
        ma: j['ma'] as String? ?? '',
        ten: j['ten'] as String? ?? '',
        kieu: j['kieu'] as String? ?? 'Text',
        laDauPhieu: j['laDauPhieu'] as bool? ?? false,
        batBuoc: j['batBuoc'] as bool? ?? false,
        donVi: j['donVi'] as String?,
        giaTriChuan: j['giaTriChuan'] as String?,
        tuyChonCsv: j['tuyChonCsv'] as String?,
        nhom: j['nhom'] as String?,
      );
}

class HangMucBieuMau {
  final int id;
  final String ten;
  final String? dienGiai;
  final String? tanSuat;

  const HangMucBieuMau({required this.id, required this.ten, this.dienGiai, this.tanSuat});

  factory HangMucBieuMau.tuJson(Map<String, dynamic> j) => HangMucBieuMau(
        id: j['id'] as int? ?? 0,
        ten: j['ten'] as String? ?? '',
        dienGiai: j['dienGiai'] as String?,
        tanSuat: j['tanSuat'] as String?,
      );
}

class PhieuGhiNhan {
  final int id;
  final int bieuMauId;
  final DateTime ngay;
  final Map<String, String> giaTriDau; // giá trị các trường đầu phiếu
  final String? nguoiLap;
  final String trangThai;
  final String? ghiChu;
  final List<DongGhiNhan> dong;

  const PhieuGhiNhan({
    required this.id,
    required this.bieuMauId,
    required this.ngay,
    this.giaTriDau = const {},
    this.nguoiLap,
    this.trangThai = 'DaGhiNhan',
    this.ghiChu,
    this.dong = const [],
  });

  /// true nếu phiếu đã chốt (không còn nháp) -> chỉ xem, không sửa.
  bool get laHoanThanh => trangThai != 'Nhap';

  factory PhieuGhiNhan.tuJson(Map<String, dynamic> j) {
    Map<String, String> dau = {};
    final raw = j['giaTriDauJson'];
    if (raw is String && raw.isNotEmpty) {
      try {
        dau = (jsonDecode(raw) as Map<String, dynamic>).map((k, v) => MapEntry(k, v?.toString() ?? ''));
      } catch (_) {}
    }
    return PhieuGhiNhan(
      id: j['id'] as int? ?? 0,
      bieuMauId: j['bieuMauId'] as int? ?? 0,
      ngay: DateTime.tryParse(j['ngay'] as String? ?? '') ?? DateTime.now(),
      giaTriDau: dau,
      nguoiLap: j['nguoiLap'] as String?,
      trangThai: j['trangThai'] as String? ?? 'DaGhiNhan',
      ghiChu: j['ghiChu'] as String?,
      dong: ((j['dong'] as List?) ?? const [])
          .map((e) => DongGhiNhan.tuJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}

class DongGhiNhan {
  final int? hangMucId;
  final int thuTu;
  final Map<String, String> giaTri;
  final String? ghiChu;

  const DongGhiNhan({this.hangMucId, this.thuTu = 0, this.giaTri = const {}, this.ghiChu});

  factory DongGhiNhan.tuJson(Map<String, dynamic> j) {
    Map<String, String> gt = {};
    final raw = j['giaTriJson'];
    if (raw is String && raw.isNotEmpty) {
      try {
        final m = jsonDecode(raw) as Map<String, dynamic>;
        gt = m.map((k, v) => MapEntry(k, v?.toString() ?? ''));
      } catch (_) {}
    }
    return DongGhiNhan(
      hangMucId: j['hangMucBieuMauId'] as int?,
      thuTu: j['thuTu'] as int? ?? 0,
      giaTri: gt,
      ghiChu: j['ghiChu'] as String?,
    );
  }
}

/// Một mục danh mục để chọn (nhân sự, cơ sở, thành phẩm) trong trường "Chọn...".
class MucChon {
  final String ma;
  final String ten;
  const MucChon(this.ma, this.ten);
}

/// Một mục nhắc hạn (thiết bị đến/quá hạn hiệu chuẩn/bảo dưỡng).
class NhacHan {
  final int bieuMauId;
  final String tenBieuMau;
  final int phieuId;
  final String nhan; // nhãn dòng (tên thiết bị...)
  final String tenTruong; // tên trường hạn (vd "Hạn hiệu chuẩn kế tiếp")
  final DateTime han;
  final int soNgayConLai; // < 0 = quá hạn

  const NhacHan({
    required this.bieuMauId,
    required this.tenBieuMau,
    required this.phieuId,
    required this.nhan,
    required this.tenTruong,
    required this.han,
    required this.soNgayConLai,
  });

  bool get quaHan => soNgayConLai < 0;

  factory NhacHan.tuJson(Map<String, dynamic> j) => NhacHan(
        bieuMauId: j['bieuMauId'] as int? ?? 0,
        tenBieuMau: j['tenBieuMau'] as String? ?? '',
        phieuId: j['phieuId'] as int? ?? 0,
        nhan: j['nhan'] as String? ?? '',
        tenTruong: j['tenTruong'] as String? ?? '',
        han: DateTime.tryParse(j['han'] as String? ?? '') ?? DateTime.now(),
        soNgayConLai: j['soNgayConLai'] as int? ?? 0,
      );
}

/// Một phiếu trong danh sách "Phiếu đã nhập": tên mẫu + tóm tắt (số ô Đạt/Không đạt, đầu phiếu dạng chữ).
class PhieuTomTat {
  final int id;
  final int bieuMauId;
  final String maHieu;
  final String tenMau;
  final String boCuc;
  final DateTime ngay;
  final String trangThai;
  final String? nguoiLap;
  final DateTime thoiGianUtc;
  final int soDong;
  final int soDat;
  final int soKhongDat;
  final List<String> dauPhieu;

  const PhieuTomTat({
    required this.id,
    required this.bieuMauId,
    required this.maHieu,
    required this.tenMau,
    this.boCuc = '',
    required this.ngay,
    this.trangThai = 'DaGhiNhan',
    this.nguoiLap,
    required this.thoiGianUtc,
    this.soDong = 0,
    this.soDat = 0,
    this.soKhongDat = 0,
    this.dauPhieu = const [],
  });

  bool get laNhap => trangThai == 'Nhap';

  factory PhieuTomTat.tuJson(Map<String, dynamic> j) => PhieuTomTat(
        id: j['id'] as int? ?? 0,
        bieuMauId: j['bieuMauId'] as int? ?? 0,
        maHieu: j['maHieu'] as String? ?? '',
        tenMau: j['tenMau'] as String? ?? '',
        boCuc: j['boCuc'] as String? ?? '',
        ngay: DateTime.tryParse(j['ngay'] as String? ?? '') ?? DateTime.now(),
        trangThai: j['trangThai'] as String? ?? 'DaGhiNhan',
        nguoiLap: j['nguoiLap'] as String?,
        thoiGianUtc: DateTime.tryParse(j['thoiGianUtc'] as String? ?? '') ?? DateTime.now().toUtc(),
        soDong: j['soDong'] as int? ?? 0,
        soDat: j['soDat'] as int? ?? 0,
        soKhongDat: j['soKhongDat'] as int? ?? 0,
        dauPhieu: ((j['dauPhieu'] as List?) ?? const []).map((e) => e.toString()).toList(),
      );
}
