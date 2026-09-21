import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/tinh_nang/don_hang/kho_du_lieu.dart';
import 'package:hcp_mobile/tinh_nang/don_hang/man_danh_sach.dart';
import 'package:hcp_mobile/tinh_nang/don_hang/mo_hinh.dart';
import 'package:hcp_mobile/tinh_nang/lenh_san_xuat/kho_du_lieu.dart' show AnhDaChon;
import 'package:hcp_mobile/tinh_nang/lenh_san_xuat/mo_hinh.dart' show ThanhPham, TrangDuLieu;
import 'package:hcp_mobile/tinh_nang/xac_thuc/xac_thuc.dart';

/// Kho dữ liệu giả: không gọi mạng, ghi lại lệnh nhận đơn/xác nhận để kiểm chứng.
class KhoDonGia implements KhoDonHang {
  final List<DonHangBan> duLieu;
  int? daNhan;
  int? daXacNhan;

  KhoDonGia(this.duLieu);

  @override
  Future<TrangDuLieu<DonHangBan>> danhSach(
      {bool canGiao = true, bool cuaToi = false, String? trangThai, int trang = 1, int soDong = 20}) async {
    var loc = duLieu;
    if (cuaToi) loc = loc.where((d) => (d.maNguoiGiao ?? '') == 'NS01').toList();
    if (trangThai != null) loc = loc.where((d) => d.trangThai == trangThai).toList();
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
  Future<String> xacNhan(int id) async {
    daXacNhan = id;
    return 'Đã xác nhận';
  }

  @override
  Future<String> daGiao(int id, List<AnhDaChon> anh) async => 'Đã giao';

  @override
  Future<String> taoDon(Map<String, dynamic> than) async => 'Đã tạo đơn hàng.';
  @override
  Future<String> suaDon(int id, Map<String, dynamic> than) async => 'Đã cập nhật đơn hàng.';
  @override
  Future<String> xoaDon(int id) async => 'Đã xoá đơn hàng.';
  @override
  Future<String> huyDon(int id, String lyDo) async => 'Đã huỷ đơn hàng.';
  @override
  Future<List<DongXuatKho>> goiYXuatKho(int id) async => const [];
  @override
  Future<String> xuatKho(int id, List<PhanBoLo> phanBo,
          {String? maNguoiGiao, String? ghiChu, List<AnhDaChon> anh = const []}) async =>
      'Đã xuất kho.';
  @override
  Future<List<KhachHang>> khachHang() async => const [];
  @override
  Future<List<ThanhPham>> thanhPhamBan() async => const [];
  @override
  Future<Uint8List> qrPng(int id) async => Uint8List(0);
}

DonHangBan _don({int id = 1, String ma = 'DH-001', String trangThai = 'DangGiao', String? nguoiGiao}) => DonHangBan(
      id: id,
      maDonHang: ma,
      tenKhachHang: 'Trường A',
      trangThai: trangThai,
      trangThaiHienThi: switch (trangThai) {
        'ChoGiaoHang' => 'Chờ giao hàng',
        'DangGiao' => 'Đang giao',
        'DaXacNhan' => 'Đã xác nhận',
        _ => 'Đã giao',
      },
      maNguoiGiao: nguoiGiao,
      tenNguoiGiao: nguoiGiao == null ? null : 'Người giao $nguoiGiao',
      dong: const [DongDonHang(id: 1, maThanhPham: 'BANH_MI', tenThanhPham: 'Bánh mì', soLuong: 2)],
    );

class _XacThucGia extends XacThucNotifier {
  @override
  TrangThaiXacThuc build() => TrangThaiXacThuc(
      nguoiDung: NguoiDung(id: 'u1', email: '', vaiTro: ['TenantGiaoHang'], maNhanSu: 'NS01'));
}

/// Quản lý/nhập liệu: được Tạo đơn, Xác nhận, Xuất kho - khác shipper thuần ở trên.
class _XacThucNhapLieuGia extends XacThucNotifier {
  @override
  TrangThaiXacThuc build() => TrangThaiXacThuc(
      nguoiDung: NguoiDung(id: 'u2', email: '', vaiTro: ['TenantStaff'], maNhanSu: 'NS02'));
}

Widget _app(KhoDonGia kho, {bool nhapLieu = false}) => ProviderScope(
      overrides: [
        khoDonProvider.overrideWithValue(kho),
        xacThucProvider.overrideWith(nhapLieu ? _XacThucNhapLieuGia.new : _XacThucGia.new),
      ],
      child: const MaterialApp(home: ManDanhSachDon()),
    );

void main() {
  testWidgets('Đơn chưa ai nhận thì có nút Nhận đơn; đơn của người khác thì không', (t) async {
    final kho = KhoDonGia([
      _don(id: 1, ma: 'DH-001', trangThai: 'ChoGiaoHang'),   // chưa ai nhận
      _don(id: 2, ma: 'DH-002', nguoiGiao: 'NS09'),          // đã có người nhận, đang giao
    ]);
    await t.pumpWidget(_app(kho));
    await t.pumpAndSettle();

    expect(find.text('DH-001'), findsOneWidget);
    expect(find.text('Nhận đơn'), findsOneWidget);                 // chỉ đơn chưa ai nhận
    expect(find.text('Người giao: Người giao NS09'), findsOneWidget);
    // "Đã giao" chỉ hiện với đơn CHÍNH MÌNH đã nhận - đơn chưa ai nhận hoặc của người khác đều không có.
    expect(find.text('Đã giao'), findsNothing);

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

  testWidgets('Nút Tạo đơn KHÔNG hiện với shipper thuần', (t) async {
    await t.pumpWidget(_app(KhoDonGia([_don(id: 1)])));
    await t.pumpAndSettle();
    expect(find.byTooltip('Tạo đơn hàng'), findsNothing);
  });

  testWidgets('Nút Tạo đơn hiện với quản lý/nhập liệu', (t) async {
    await t.pumpWidget(_app(KhoDonGia([_don(id: 1)]), nhapLieu: true));
    await t.pumpAndSettle();
    expect(find.byTooltip('Tạo đơn hàng'), findsOneWidget);
  });

  testWidgets('Đơn đã xác nhận: nút Xuất kho KHÔNG hiện với shipper thuần', (t) async {
    final don = _don(id: 4, ma: 'DH-004', trangThai: 'DaXacNhan');
    await t.pumpWidget(_app(KhoDonGia([don])));
    await t.pumpAndSettle();
    expect(find.text('Xuất kho'), findsNothing);
  });

  testWidgets('Đơn đã xác nhận: nút Xuất kho hiện với quản lý/nhập liệu', (t) async {
    final don = _don(id: 4, ma: 'DH-004', trangThai: 'DaXacNhan');
    await t.pumpWidget(_app(KhoDonGia([don]), nhapLieu: true));
    await t.pumpAndSettle();
    expect(find.text('Xuất kho'), findsOneWidget);
  });
}
