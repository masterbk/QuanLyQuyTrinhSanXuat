import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/tinh_nang/xac_thuc/man_doi_mat_khau.dart';
import 'package:hcp_mobile/tinh_nang/xac_thuc/xac_thuc.dart';

class _XacThucGia extends XacThucNotifier {
  int soLanGoi = 0;
  String? moiDaGui;

  @override
  TrangThaiXacThuc build() =>
      TrangThaiXacThuc(nguoiDung: NguoiDung(id: 'u1', email: 'a@b.vn', vaiTro: const ['TenantBieuMau']));

  @override
  Future<String> doiMatKhau({required String hienTai, required String moi}) async {
    soLanGoi++;
    moiDaGui = moi;
    return 'Đã đổi mật khẩu.';
  }
}

void main() {
  test('Kiểm tra trước khi gửi', () {
    expect(kiemTraDoiMatKhau('', 'Abc@12345', 'Abc@12345'), contains('hiện tại'));
    expect(kiemTraDoiMatKhau('Cu@12345', 'ngan', 'ngan'), contains('8 ký tự'));
    expect(kiemTraDoiMatKhau('Cu@12345', 'Cu@12345', 'Cu@12345'), contains('khác'));
    expect(kiemTraDoiMatKhau('Cu@12345', 'Moi@12345', 'Moi@1234'), contains('không khớp'));
    expect(kiemTraDoiMatKhau('Cu@12345', 'Moi@12345', 'Moi@12345'), isNull);
  });

  testWidgets('Nhập lại không khớp thì báo lỗi, không gọi máy chủ; khớp thì gửi và đóng màn', (t) async {
    final gia = _XacThucGia();
    await t.pumpWidget(ProviderScope(
      overrides: [xacThucProvider.overrideWith(() => gia)],
      child: MaterialApp(home: Builder(builder: (c) => Scaffold(
        body: Center(child: TextButton(
          onPressed: () => Navigator.push(c, MaterialPageRoute(builder: (_) => const ManDoiMatKhau())),
          child: const Text('mở'),
        )),
      ))),
    ));
    await t.tap(find.text('mở'));
    await t.pumpAndSettle();

    final o = find.byType(TextField);
    await t.enterText(o.at(0), 'Cu@12345');
    await t.enterText(o.at(1), 'Moi@12345');
    await t.enterText(o.at(2), 'Moi@99999');
    await t.tap(find.widgetWithText(FilledButton, 'Đổi mật khẩu'));
    await t.pumpAndSettle();
    expect(find.text('Mật khẩu nhập lại không khớp.'), findsOneWidget);
    expect(gia.soLanGoi, 0);

    await t.enterText(o.at(2), 'Moi@12345');
    await t.tap(find.widgetWithText(FilledButton, 'Đổi mật khẩu'));
    await t.pumpAndSettle();
    expect(gia.soLanGoi, 1);
    expect(gia.moiDaGui, 'Moi@12345');
    expect(find.byType(ManDoiMatKhau), findsNothing);   // đóng màn sau khi đổi xong
    expect(find.text('Đã đổi mật khẩu.'), findsOneWidget);
  });
}
