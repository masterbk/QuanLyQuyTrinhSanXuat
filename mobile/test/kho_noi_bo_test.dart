import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/tinh_nang/kho_noi_bo/kho_du_lieu.dart';
import 'package:hcp_mobile/tinh_nang/kho_noi_bo/man_danh_sach.dart';
import 'package:hcp_mobile/tinh_nang/kho_noi_bo/mo_hinh.dart';
import 'package:hcp_mobile/tinh_nang/lenh_san_xuat/mo_hinh.dart' show Kho, ThanhPham;
import 'package:hcp_mobile/tinh_nang/xac_thuc/xac_thuc.dart';

/// Kho dữ liệu giả: trả sẵn dữ liệu, không gọi mạng. Ghi lại lệnh nhập để kiểm chứng.
class KhoNoiBoGia implements KhoNoiBoData {
  final List<TonKho> tonDs;
  final List<LichSuKho> lichSuDs;
  final List<ThanhPham> nguyenLieuDs;
  final List<Kho> khoDs;
  final List<NccDauVao> nccDs;
  Map<String, dynamic>? nhapDaGoi;

  KhoNoiBoGia({
    this.tonDs = const [],
    this.lichSuDs = const [],
    this.nguyenLieuDs = const [],
    this.khoDs = const [],
    this.nccDs = const [],
  });

  @override
  Future<List<TonKho>> ton() async => tonDs;

  @override
  Future<List<LichSuKho>> lichSu({int gioiHan = 100}) async => lichSuDs;

  @override
  Future<List<ThanhPham>> nguyenLieu() async => nguyenLieuDs;

  @override
  Future<List<Kho>> kho() async => khoDs;

  @override
  Future<List<NccDauVao>> nccDauVao() async => nccDs;

  @override
  Future<String> nhap({
    required String maSanPham,
    required String maKho,
    required String maLo,
    required double soLuong,
    DateTime? hanSuDung,
    String? maNccDauVao,
    String? ghiChu,
  }) async {
    nhapDaGoi = {'maSanPham': maSanPham, 'maKho': maKho, 'maLo': maLo, 'soLuong': soLuong};
    return 'Đã nhập kho.';
  }

  @override
  Future<String> dieuChinh({
    required String maSanPham,
    required String maKho,
    required String maLo,
    required double soLuongThucTe,
    String? lyDo,
  }) async =>
      'Đã điều chỉnh.';
}

class _XacThucGia extends XacThucNotifier {
  final NguoiDung? nguoiDung;

  _XacThucGia(this.nguoiDung);

  @override
  TrangThaiXacThuc build() => TrangThaiXacThuc(nguoiDung: nguoiDung);
}

Widget _app(KhoNoiBoGia kho, {NguoiDung? nguoiDung}) => ProviderScope(
      overrides: [
        khoNoiBoProvider.overrideWithValue(kho),
        xacThucProvider.overrideWith(() => _XacThucGia(nguoiDung)),
      ],
      child: const MaterialApp(home: ManKhoNoiBo()),
    );

TonKho _ton({
  String ma = 'BOT_MI',
  String ten = 'Bột mì',
  String loai = 'NguyenLieu',
  double soLuong = 10,
  DateTime? hanSuDung,
}) =>
    TonKho(
      maSanPham: ma,
      tenSanPham: ten,
      loaiSanPham: loai,
      donViTinh: 'kg',
      maKho: 'KHO01',
      tenKho: 'Kho chính',
      maLo: 'LO-001',
      hanSuDung: hanSuDung,
      soLuongTon: soLuong,
    );

void main() {
  testWidgets('Hiện danh sách tồn kho, cảnh báo lô sắp/đã hết hạn', (t) async {
    final homNay = DateTime.now();
    final kho = KhoNoiBoGia(tonDs: [
      _ton(ma: 'BOT_MI', ten: 'Bột mì', soLuong: 10),
      _ton(ma: 'DUONG', ten: 'Đường', loai: 'NguyenLieu', soLuong: 5, hanSuDung: homNay.subtract(const Duration(days: 1))),
      _ton(ma: 'BANH_MI', ten: 'Bánh mì', loai: 'ThanhPham', soLuong: 20, hanSuDung: homNay.add(const Duration(days: 5))),
    ]);
    await t.pumpWidget(_app(kho, nguoiDung: NguoiDung(id: 'u1', email: '', vaiTro: ['TenantSanXuat'])));
    await t.pumpAndSettle();

    expect(find.text('Bột mì'), findsOneWidget);
    expect(find.text('Đường'), findsOneWidget);
    expect(find.text('Bánh mì'), findsOneWidget);
    expect(find.text('Đã hết hạn'), findsOneWidget);
    expect(find.text('Sắp hết hạn'), findsOneWidget);
    expect(find.text('Nguyên liệu'), findsNWidgets(2));
    expect(find.text('Thành phẩm'), findsOneWidget);
  });

  testWidgets('Chưa có tồn kho thì hiện lời nhắc', (t) async {
    await t.pumpWidget(_app(KhoNoiBoGia(), nguoiDung: NguoiDung(id: 'u1', email: '', vaiTro: ['TenantSanXuat'])));
    await t.pumpAndSettle();
    expect(find.text('Chưa có tồn kho.'), findsOneWidget);
  });

  testWidgets('Nhân viên sản xuất thuần KHÔNG thấy tab Nhập nguyên liệu và nút Điều chỉnh', (t) async {
    final kho = KhoNoiBoGia(tonDs: [_ton()]);
    await t.pumpWidget(_app(kho, nguoiDung: NguoiDung(id: 'u1', email: '', vaiTro: ['TenantSanXuat'])));
    await t.pumpAndSettle();

    expect(find.text('Tồn kho'), findsOneWidget);
    expect(find.text('Nhập nguyên liệu'), findsNothing);
    expect(find.byIcon(Icons.fact_check_outlined), findsNothing);
  });

  testWidgets('Quản lý/nhập liệu thấy cả 2 tab và nút Điều chỉnh', (t) async {
    final kho = KhoNoiBoGia(tonDs: [_ton()]);
    await t.pumpWidget(_app(kho, nguoiDung: NguoiDung(id: 'u2', email: '', vaiTro: ['TenantStaff'])));
    await t.pumpAndSettle();

    expect(find.text('Tồn kho'), findsOneWidget);
    expect(find.text('Nhập nguyên liệu'), findsOneWidget);
    expect(find.byIcon(Icons.fact_check_outlined), findsOneWidget);
  });

  testWidgets('Nhập kho: chặn khi chưa chọn đủ nguyên liệu/kho/mã lô', (t) async {
    final kho = KhoNoiBoGia(
      nguyenLieuDs: const [ThanhPham(maSanPham: 'BOT_MI', tenSanPham: 'Bột mì', donViTinh: 'kg')],
      khoDs: const [Kho(maKho: 'KHO01', tenKho: 'Kho chính')],
    );
    await t.pumpWidget(_app(kho, nguoiDung: NguoiDung(id: 'u2', email: '', vaiTro: ['TenantStaff'])));
    await t.pumpAndSettle();

    await t.tap(find.text('Nhập nguyên liệu'));
    await t.pumpAndSettle();

    await t.tap(find.text('Nhập kho'));
    await t.pumpAndSettle();

    expect(find.text('Vui lòng chọn nguyên liệu, kho và nhập mã lô.'), findsOneWidget);
    expect(kho.nhapDaGoi, isNull);
  });
}
