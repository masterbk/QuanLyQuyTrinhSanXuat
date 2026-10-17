import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/tinh_nang/danh_muc/kho_du_lieu.dart';
import 'package:hcp_mobile/tinh_nang/danh_muc/man_quy_trinh.dart';
import 'package:hcp_mobile/tinh_nang/danh_muc/man_thuc_pham.dart';
import 'package:hcp_mobile/tinh_nang/danh_muc/mo_hinh.dart';
import 'package:hcp_mobile/tinh_nang/xac_thuc/xac_thuc.dart';

/// Kho dữ liệu giả: danh sách trả bản CŨ, còn chi tiết theo id trả bản MỚI - để thấy rõ màn Sửa
/// lấy dữ liệu từ đâu. Ghi lại id đã hỏi chi tiết để kiểm chứng có gọi lại máy chủ hay không.
class KhoDanhMucGia implements DanhMucQuanLyData {
  int? idDaHoiChiTietQuyTrinh;
  int? idDaHoiChiTietThucPham;

  @override
  Future<List<QuyTrinhQuanLy>> quyTrinh() async =>
      [const QuyTrinhQuanLy(id: 7, maQuyTrinh: 'QT01', tenQuyTrinh: 'Tên CŨ trong danh sách')];

  @override
  Future<QuyTrinhQuanLy> chiTietQuyTrinh(int id) async {
    idDaHoiChiTietQuyTrinh = id;
    return const QuyTrinhQuanLy(id: 7, maQuyTrinh: 'QT01', tenQuyTrinh: 'Tên MỚI từ máy chủ');
  }

  @override
  Future<List<SanPham>> thucPham() async =>
      [const SanPham(id: 9, maSanPham: 'SP01', tenSanPham: 'Tên CŨ trong danh sách')];

  @override
  Future<SanPham> chiTietThucPham(int id) async {
    idDaHoiChiTietThucPham = id;
    return const SanPham(id: 9, maSanPham: 'SP01', tenSanPham: 'Tên MỚI từ máy chủ');
  }

  @override
  Future<List<Khau>> khauSanXuat() async => const [];

  @override
  Future<List<DinhMucDong>> dinhMuc(int idThanhPham) async => const [];

  @override
  Future<String> luuThucPham(SanPham sp) async => 'Đã lưu.';
  @override
  Future<String> xoaThucPham(int id) async => 'Đã xoá.';
  @override
  Future<String> luuDinhMuc(int idThanhPham, List<DinhMucDong> dong) async => 'Đã lưu.';
  @override
  Future<String> luuKhau({int? id, required bool dongBoHnC, required String tenKhau, String? ghiChu}) async =>
      'Đã lưu.';
  @override
  Future<String> xoaKhau(int id) async => 'Đã xoá.';
  @override
  Future<String> xoaQuyTrinh(int id) async => 'Đã xoá.';
  @override
  Future<String> luuQuyTrinh({
    int? id,
    required bool dongBoHnC,
    required String maQuyTrinh,
    required String tenQuyTrinh,
    required String loaiSanPham,
    required List<String> danhSachMaKhau,
  }) async =>
      'Đã lưu.';
}

class _XacThucGia extends XacThucNotifier {
  @override
  TrangThaiXacThuc build() => TrangThaiXacThuc(
      nguoiDung: NguoiDung(id: 'u1', email: '', vaiTro: ['TenantAdmin'], maNhanSu: 'NS01'));
}

Widget _app(KhoDanhMucGia kho, Widget man) => ProviderScope(
      overrides: [
        danhMucQuanLyProvider.overrideWithValue(kho),
        xacThucProvider.overrideWith(_XacThucGia.new),
      ],
      child: MaterialApp(home: man),
    );

void main() {
  testWidgets('Quy trình: bấm Sửa thì tải lại bản mới nhất, không dùng dòng cũ trong danh sách', (t) async {
    final kho = KhoDanhMucGia();
    await t.pumpWidget(_app(kho, const ManQuyTrinh()));
    await t.pumpAndSettle();

    expect(find.textContaining('Tên CŨ trong danh sách'), findsOneWidget);

    await t.tap(find.byTooltip('Sửa'));
    await t.pumpAndSettle();

    expect(kho.idDaHoiChiTietQuyTrinh, 7);                      // đã hỏi lại máy chủ
    expect(find.text('Tên MỚI từ máy chủ'), findsOneWidget);    // form nhận bản mới, không phải bản cũ
  });

  testWidgets('Thực phẩm: bấm Sửa thì tải lại bản mới nhất, không dùng dòng cũ trong danh sách', (t) async {
    final kho = KhoDanhMucGia();
    await t.pumpWidget(_app(kho, const ManThucPham()));
    await t.pumpAndSettle();

    await t.tap(find.byTooltip('Sửa'));
    await t.pumpAndSettle();

    expect(kho.idDaHoiChiTietThucPham, 9);
    expect(find.text('Tên MỚI từ máy chủ'), findsOneWidget);
  });
}
