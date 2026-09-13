import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/loi/api.dart';
import 'package:hcp_mobile/tinh_nang/lenh_san_xuat/kho_du_lieu.dart';
import 'package:hcp_mobile/tinh_nang/lenh_san_xuat/man_danh_sach.dart';
import 'package:hcp_mobile/tinh_nang/lenh_san_xuat/mo_hinh.dart';

/// Kho dữ liệu giả: trả sẵn dữ liệu, không gọi mạng.
class KhoGia implements KhoLenhSanXuat {
  final List<LenhSanXuat> duLieu;
  final Object? nem;
  String? locDaNhan;

  KhoGia({this.duLieu = const [], this.nem});

  @override
  Future<TrangDuLieu<LenhSanXuat>> danhSach({String? trangThai, int trang = 1, int soDong = 20}) async {
    if (nem != null) throw nem!;
    locDaNhan = trangThai;
    final loc = trangThai == null ? duLieu : duLieu.where((l) => l.trangThai == trangThai).toList();
    return TrangDuLieu(duLieu: loc, trang: trang, soDong: soDong, tongSo: loc.length);
  }

  @override
  Future<LenhSanXuat> chiTiet(int id) async => duLieu.firstWhere((l) => l.id == id);
  @override
  Future<String> tao(Map<String, dynamic> than) async => 'Đã tạo';
  @override
  Future<String> sua(int id, Map<String, dynamic> than) async => 'Đã sửa';
  @override
  Future<String> xoa(int id) async => 'Đã xoá';
  @override
  Future<String> huy(int id, String lyDo) async => 'Đã huỷ';
  @override
  Future<String> hoanThanh(int id, Map<int, List<AnhDaChon>> anh, List<Map<String, dynamic>> khau) async =>
      'Đã hoàn thành';
  @override
  Future<List<NguyenLieuCan>> nguyenLieuCan(
          List<({String maThanhPham, double soLuong})> dong, String maKho) async => [];
  @override
  Future<List<ThanhPham>> thanhPham() async => [];
  @override
  Future<List<Kho>> kho() async => [];
  @override
  Future<List<QuyTrinh>> quyTrinh() async => [];
  @override
  Future<List<CoSo>> coSo() async => [];
  @override
  Future<List<NhanSu>> nhanSu() async => [];
}

LenhSanXuat _lenh({
  int id = 1,
  String ma = 'LSX-001',
  String trangThai = 'MoiTao',
  String hienThi = 'Mới tạo',
  double soLuong = 10,
}) =>
    LenhSanXuat(
      id: id,
      maLenh: ma,
      maKho: 'KHO01',
      ngaySanXuat: DateTime(2026, 9, 13),
      trangThai: trangThai,
      trangThaiHienThi: hienThi,
      sanPham: [
        SanPhamLenh(
          id: id * 10,
          maThanhPham: 'BANH_MI',
          tenThanhPham: 'Bánh mì',
          soLuong: soLuong,
          maLoThanhPham: 'LO-$id',
          maQuyTrinh: 'QT01',
        ),
      ],
    );

Widget _app(KhoGia kho) => ProviderScope(
      overrides: [khoLenhProvider.overrideWithValue(kho)],
      child: const MaterialApp(home: ManDanhSachLenh()),
    );

void main() {
  testWidgets('Hiện danh sách lệnh với mã, tóm tắt sản phẩm và trạng thái', (t) async {
    await t.pumpWidget(_app(KhoGia(duLieu: [
      _lenh(id: 1, ma: 'LSX-001'),
      _lenh(id: 2, ma: 'LSX-002', trangThai: 'HoanThanh', hienThi: 'Hoàn thành'),
    ])));
    await t.pumpAndSettle();

    expect(find.text('LSX-001'), findsOneWidget);
    expect(find.text('LSX-002'), findsOneWidget);
    expect(find.text('Mới tạo'), findsWidgets);
    expect(find.text('Hoàn thành'), findsWidgets);
    expect(find.text('Bánh mì ×10'), findsNWidgets(2));  // tóm tắt sản phẩm + số lượng
    expect(find.text('1 lô'), findsNWidgets(2));
  });

  testWidgets('Chưa có lệnh nào thì hiện lời nhắc, không hiện danh sách trống trơn', (t) async {
    await t.pumpWidget(_app(KhoGia()));
    await t.pumpAndSettle();

    expect(find.text('Chưa có lệnh sản xuất nào'), findsOneWidget);
    expect(find.text('Tạo lệnh'), findsOneWidget);      // vẫn còn nút tạo
  });

  testWidgets('Bấm bộ lọc thì gọi lại danh sách theo đúng trạng thái', (t) async {
    final kho = KhoGia(duLieu: [
      _lenh(id: 1, ma: 'LSX-001'),
      _lenh(id: 2, ma: 'LSX-002', trangThai: 'HoanThanh', hienThi: 'Hoàn thành'),
    ]);
    await t.pumpWidget(_app(kho));
    await t.pumpAndSettle();

    await t.tap(find.widgetWithText(ChoiceChip, 'Hoàn thành'));
    await t.pumpAndSettle();

    expect(kho.locDaNhan, 'HoanThanh');
    expect(find.text('LSX-002'), findsOneWidget);
    expect(find.text('LSX-001'), findsNothing);
  });

  testWidgets('Mất mạng thì hiện thông báo của máy chủ kèm nút thử lại', (t) async {
    await t.pumpWidget(_app(KhoGia(nem: LoiApi('Không kết nối được máy chủ.'))));
    await t.pumpAndSettle();

    expect(find.text('Không kết nối được máy chủ.'), findsOneWidget);
    expect(find.text('Thử lại'), findsOneWidget);
  });
}
