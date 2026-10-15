import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/tinh_nang/don_hang/kho_du_lieu.dart';
import 'package:hcp_mobile/tinh_nang/don_hang/man_xuat_kho.dart';
import 'package:hcp_mobile/tinh_nang/don_hang/mo_hinh.dart';
import 'package:hcp_mobile/tinh_nang/lenh_san_xuat/kho_du_lieu.dart' show AnhDaChon, nhanSuProvider;
import 'package:hcp_mobile/tinh_nang/lenh_san_xuat/mo_hinh.dart' show ThanhPham, TrangDuLieu;

/// Gợi ý xuất kho của đơn suất ăn: dòng món (id 7) tách 2 nguyên liệu, mỗi nguyên liệu một bảng lô;
/// ghi lại phân bổ app gửi lên để kiểm chứng.
class _KhoGia implements KhoDonHang {
  List<PhanBoLo>? daGui;

  @override
  Future<List<DongXuatKho>> goiYXuatKho(int id) async => const [
        DongXuatKho(
            dongId: 7, maThanhPham: 'TP-BANHMI', tenThanhPham: 'Bánh mì', donViTinh: 'cái', soLuong: 10,
            maNguyenLieu: 'TP-BANHMI', tenMonAn: 'Bánh mì pate', soSuat: 10,
            lo: [LoCoTheXuat(maLo: 'LO-1', ton: 50, goiY: 10)]),
        DongXuatKho(
            dongId: 7, maThanhPham: 'TP-PATE', tenThanhPham: 'Pate', donViTinh: 'kg', soLuong: 0.00125,
            maNguyenLieu: 'TP-PATE', tenMonAn: 'Bánh mì pate', soSuat: 10,
            lo: [LoCoTheXuat(maLo: 'LO-P', ton: 2, goiY: 0.00125)]),
      ];

  @override
  Future<String> xuatKho(int id, List<PhanBoLo> phanBo,
      {String? maNguoiGiao, String? ghiChu, List<AnhDaChon> anh = const []}) async {
    daGui = phanBo;
    return 'Đã xuất kho.';
  }

  @override
  Future<TrangDuLieu<DonHangBan>> danhSach(
          {bool canGiao = true, bool cuaToi = false, String? trangThai, DateTime? tuNgay, DateTime? denNgay,
          String? maKhachHang, int trang = 1, int soDong = 20}) async =>
      TrangDuLieu(duLieu: const [], trang: 1, soDong: 20, tongSo: 0);
  @override
  Future<DonHangBan> chiTiet(int id) async => throw UnimplementedError();
  @override
  Future<DonHangBan> quet(String noiDung) async => throw UnimplementedError();
  @override
  Future<String> nhanDon(int id) async => '';
  @override
  Future<String> xacNhan(int id) async => '';
  @override
  Future<String> daGiao(int id, List<AnhDaChon> anh) async => '';
  @override
  Future<String> taoDon(Map<String, dynamic> than) async => '';
  @override
  Future<String> suaDon(int id, Map<String, dynamic> than) async => '';
  @override
  Future<String> xoaDon(int id) async => '';
  @override
  Future<String> huyDon(int id, String lyDo) async => '';
  @override
  Future<List<KhachHang>> khachHang() async => const [];
  @override
  Future<List<ThanhPham>> thanhPhamBan() async => const [];
  @override
  Future<Uint8List> qrPng(int id) async => Uint8List(0);
}

void main() {
  testWidgets('Đơn suất ăn: hiện món × suất, gửi phân bổ kèm mã nguyên liệu, giữ đủ 5 số lẻ', (t) async {
    final kho = _KhoGia();
    await t.pumpWidget(ProviderScope(
      overrides: [
        khoDonProvider.overrideWithValue(kho),
        nhanSuProvider.overrideWith((ref) async => []),
      ],
      child: MaterialApp(
        home: ManXuatKho(
          don: const DonHangBan(
            id: 1, maDonHang: 'DH-SA', tenKhachHang: 'Trường A', trangThai: 'DaXacNhan',
            trangThaiHienThi: 'Đã xác nhận',
            dong: [DongDonHang(id: 7, maThanhPham: 'MA-01', tenThanhPham: 'Bánh mì pate', soLuong: 10)],
          ),
        ),
      ),
    ));
    await t.pumpAndSettle();

    expect(find.text('Món Bánh mì pate × 10 suất — nguyên liệu theo công thức'), findsNWidgets(2));
    expect(find.text('Đã chọn 0.00125 / 0.00125'), findsOneWidget);   // không bị làm tròn còn 0.001

    await t.tap(find.text('Xác nhận xuất kho'));
    await t.pumpAndSettle();

    expect(kho.daGui, [
      (dongId: 7, maLo: 'LO-1', soLuong: 10.0, maNguyenLieu: 'TP-BANHMI'),
      (dongId: 7, maLo: 'LO-P', soLuong: 0.00125, maNguyenLieu: 'TP-PATE'),
    ]);
  });
}
