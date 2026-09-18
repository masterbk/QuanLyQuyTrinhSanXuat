import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/tinh_nang/don_hang/kho_du_lieu.dart';
import 'package:hcp_mobile/tinh_nang/don_hang/man_danh_sach.dart';
import 'package:hcp_mobile/tinh_nang/don_hang/mo_hinh.dart';
import 'package:hcp_mobile/tinh_nang/lenh_san_xuat/kho_du_lieu.dart' show AnhDaChon;
import 'package:hcp_mobile/tinh_nang/lenh_san_xuat/mo_hinh.dart' show TrangDuLieu;
import 'package:hcp_mobile/tinh_nang/xac_thuc/xac_thuc.dart';

/// Kho dữ liệu giả: không gọi mạng, ghi lại lệnh nhận đơn để kiểm chứng.
class KhoDonGia implements KhoDonHang {
  final List<DonHangBan> duLieu;
  int? daNhan;

  KhoDonGia(this.duLieu);

  @override
  Future<TrangDuLieu<DonHangBan>> danhSach(
      {bool canGiao = true, bool cuaToi = false, int trang = 1, int soDong = 20}) async {
    final loc = cuaToi ? duLieu.where((d) => (d.maNguoiGiao ?? '') == 'NS01').toList() : duLieu;
    return TrangDuLieu(duLieu: loc, trang: trang, soDong: soDong, tongSo: loc.length);
  }

  @override
  Future<DonHangBan> chiTiet(int id) async => duLieu.firstWhere((d) => d.id == id);
  @override
  Future<DonHangBan> quet(String noiDung) async => duLieu.first;
  @override
  Future<String> nhanDon(int id) async {
    daNhan = id;
    return 'Đã nhận';
  }

  @override
  Future<String> daGiao(int id, List<AnhDaChon> anh) async => 'Đã giao';
}

DonHangBan _don({int id = 1, String ma = 'DH-001', String trangThai = 'DangGiao', String? nguoiGiao}) => DonHangBan(
      id: id,
      maDonHang: ma,
      tenKhachHang: 'Trường A',
      trangThai: trangThai,
      trangThaiHienThi: trangThai == 'DangGiao' ? 'Đang giao' : 'Đã giao',
      maNguoiGiao: nguoiGiao,
      tenNguoiGiao: nguoiGiao == null ? null : 'Người giao $nguoiGiao',
      dong: const [DongDonHang(id: 1, maThanhPham: 'BANH_MI', tenThanhPham: 'Bánh mì', soLuong: 2)],
    );

class _XacThucGia extends XacThucNotifier {
  @override
  TrangThaiXacThuc build() => TrangThaiXacThuc(
      nguoiDung: NguoiDung(id: 'u1', email: '', vaiTro: ['TenantGiaoHang'], maNhanSu: 'NS01'));
}

Widget _app(KhoDonGia kho) => ProviderScope(
      overrides: [
        khoDonProvider.overrideWithValue(kho),
        xacThucProvider.overrideWith(_XacThucGia.new),
      ],
      child: const MaterialApp(home: ManDanhSachDon()),
    );

void main() {
  testWidgets('Đơn chưa ai nhận thì có nút Nhận đơn; đơn của người khác thì không', (t) async {
    final kho = KhoDonGia([
      _don(id: 1, ma: 'DH-001'),
      _don(id: 2, ma: 'DH-002', nguoiGiao: 'NS09'),
    ]);
    await t.pumpWidget(_app(kho));
    await t.pumpAndSettle();

    expect(find.text('DH-001'), findsOneWidget);
    expect(find.text('Nhận đơn'), findsOneWidget);                 // chỉ đơn chưa ai nhận
    expect(find.text('Người giao: Người giao NS09'), findsOneWidget);
    expect(find.text('Đã giao'), findsOneWidget);                  // nút xác nhận của đơn chưa ai nhận

    await t.tap(find.text('Nhận đơn'));
    await t.pumpAndSettle();
    expect(kho.daNhan, 1);
  });

  testWidgets('Đơn mình đã nhận: hiện "Bạn nhận" và nút xác nhận đã giao', (t) async {
    await t.pumpWidget(_app(KhoDonGia([_don(id: 3, ma: 'DH-003', nguoiGiao: 'NS01')])));
    await t.pumpAndSettle();

    expect(find.text('Bạn nhận'), findsOneWidget);
    expect(find.text('Nhận đơn'), findsNothing);
    expect(find.text('Đã giao'), findsOneWidget);
  });
}
