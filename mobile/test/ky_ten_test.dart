import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/tinh_nang/bieu_mau/man_ky_ten.dart';

void main() {
  testWidgets('Chưa ký thì nhắc, không đóng; ký xong trả ảnh PNG', (t) async {
    Uint8List? kq;
    var daDong = false;
    await t.pumpWidget(MaterialApp(home: Builder(builder: (c) => Scaffold(
      body: Center(child: TextButton(
        onPressed: () async {
          kq = await Navigator.push<Uint8List>(
              c, MaterialPageRoute(builder: (_) => const ManKyTen(tenNguoiKy: 'Nguyễn Thị Nghĩa')));
          daDong = true;
        },
        child: const Text('mở'),
      )),
    ))));
    await t.tap(find.text('mở'));
    await t.pumpAndSettle();
    expect(find.text('Người ký: Nguyễn Thị Nghĩa'), findsOneWidget);

    await t.tap(find.text('Ký & Hoàn thành'));
    await t.pumpAndSettle();
    expect(find.text('Vui lòng ký vào khung trước khi hoàn thành.'), findsOneWidget);
    expect(daDong, isFalse);
    await t.pump(const Duration(seconds: 5));   // đợi thông báo nhắc tự ẩn (nó che nút ở đáy màn)
    await t.pumpAndSettle();

    // Ký: kéo một nét trên khung.
    await t.drag(find.byKey(const ValueKey('khung-ky')), const Offset(150, 40));
    await t.pump();
    // Xuất ảnh dùng engine thật -> chạy trong runAsync.
    await t.tap(find.text('Ký & Hoàn thành'));
    for (var i = 0; i < 10 && !daDong; i++) {
      await t.runAsync(() => Future<void>.delayed(const Duration(milliseconds: 100)));
      await t.pump();
    }
    await t.pumpAndSettle();
    expect(daDong, isTrue);
    expect(kq, isNotNull);
    expect(kq!.sublist(1, 4), [0x50, 0x4E, 0x47]);   // chữ ký PNG ("PNG")
  });
}
