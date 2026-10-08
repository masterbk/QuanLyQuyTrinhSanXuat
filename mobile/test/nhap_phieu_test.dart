import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/tinh_nang/bieu_mau/kho_du_lieu.dart';
import 'package:hcp_mobile/tinh_nang/bieu_mau/man_nhap_phieu.dart';
import 'package:hcp_mobile/tinh_nang/bieu_mau/mo_hinh.dart';
import 'package:hcp_mobile/tinh_nang/xac_thuc/xac_thuc.dart';

/// Kho giả: ngày nào cũng chưa có phiếu (form bắt đầu trống).
class _KhoGia extends Fake implements KhoBieuMau {
  @override
  Future<PhieuGhiNhan?> phieuTheoNgay(int bieuMauId, DateTime ngay) async => null;
}

class _XacThucGia extends XacThucNotifier {
  @override
  TrangThaiXacThuc build() =>
      TrangThaiXacThuc(nguoiDung: NguoiDung(id: 'u1', email: '', vaiTro: ['TenantSanXuat'], maNhanSu: 'NS01'));
}

// Giống mẫu nhiệt độ tủ: mỗi buổi có ô "Người kiểm tra" riêng (cùng Nhóm cột).
const _mau = BieuMau(id: 1, maHieu: 'BM-TU', ten: 'Nhiệt độ tủ', boCuc: 'TheoNgay', truong: [
  TruongBieuMau(ma: 'nd_sang', ten: 'Nhiệt độ', kieu: 'So', nhom: 'Sáng'),
  TruongBieuMau(ma: 'nguoi_sang', ten: 'Người kiểm tra', kieu: 'ChonNhanSu', nhom: 'Sáng'),
  TruongBieuMau(ma: 'nd_chieu', ten: 'Nhiệt độ chiều', kieu: 'So', nhom: 'Chiều'),
  TruongBieuMau(ma: 'nguoi_chieu', ten: 'Người kiểm tra chiều', kieu: 'ChonNhanSu', nhom: 'Chiều'),
]);

void main() {
  test('Trường Ngày luôn hiển thị dd/MM/yyyy (dữ liệu lưu yyyy-MM-dd)', () {
    expect(hienThiNgay('2026-12-25'), '25/12/2026');
    expect(hienThiNgay('2026-01-05'), '05/01/2026');
    expect(hienThiNgay(null), isNull);
    expect(hienThiNgay('không phải ngày'), 'không phải ngày');
  });

  testWidgets('Nhập dữ liệu thì ô nhân sự cùng nhóm tự điền người đăng nhập, nhóm khác để trống', (t) async {
    await t.pumpWidget(ProviderScope(
      overrides: [
        khoBieuMauProvider.overrideWithValue(_KhoGia()),
        xacThucProvider.overrideWith(_XacThucGia.new),
        nhanSuBmProvider.overrideWith((ref) async => const [MucChon('NS01', 'Nguyễn Văn A'), MucChon('NS02', 'Trần B')]),
        coSoBmProvider.overrideWith((ref) async => const <MucChon>[]),
        thanhPhamBmProvider.overrideWith((ref) async => const <MucChon>[]),
        nccBmProvider.overrideWith((ref) async => const <MucChon>[]),
      ],
      child: const MaterialApp(home: ManNhapPhieu(mau: _mau)),
    ));
    await t.pumpAndSettle();

    // Mở phiếu: chưa ai được điền sẵn.
    expect(find.text('Nguyễn Văn A'), findsNothing);

    // Nhập nhiệt độ buổi sáng -> "Người kiểm tra" buổi sáng = người đăng nhập; buổi chiều vẫn trống.
    await t.enterText(find.widgetWithText(TextFormField, 'Nhiệt độ'), '-18');
    await t.pumpAndSettle();
    expect(find.text('Nguyễn Văn A'), findsOneWidget);
  });
}
