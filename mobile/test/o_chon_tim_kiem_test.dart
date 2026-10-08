import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/tinh_nang/bieu_mau/mo_hinh.dart';
import 'package:hcp_mobile/tinh_nang/bieu_mau/o_chon_tim_kiem.dart';

const _ds = [
  MucChon('BTN-001', 'Lê Anh Tuấn'),
  MucChon('BTN-002', 'Nguyễn Thị Nghĩa'),
  MucChon('BTN-003', 'Đỗ Văn Đức'),
];

void main() {
  test('Bỏ dấu tiếng Việt đủ mọi nguyên âm + đ', () {
    expect(boDau('Nguyễn Thị Nghĩa'), 'nguyen thi nghia');
    expect(boDau('ĐỖ VĂN ĐỨC'), 'do van duc');
    expect(boDau('àáạảãâầấậẩẫăằắặẳẵ'), 'a' * 17);
    expect(boDau('ơờớợởỡưừứựửữỳýỵỷỹ'), 'oooooouuuuuuyyyyy');
  });

  test('Lọc theo tên không dấu hoặc mã', () {
    expect(locMucChon(_ds, 'nghia').map((m) => m.ma), ['BTN-002']);
    expect(locMucChon(_ds, 'DUC').map((m) => m.ma), ['BTN-003']);
    expect(locMucChon(_ds, 'btn-00').length, 3);
    expect(locMucChon(_ds, '  ').length, 3);
  });

  testWidgets('Chạm ô -> gõ tìm không dấu -> chọn; có Bỏ chọn', (t) async {
    String? chon = 'BTN-001';
    await t.pumpWidget(MaterialApp(
      home: Scaffold(
        body: StatefulBuilder(
          builder: (c, set) => OChonTimKiem(
            nhan: 'Người kiểm tra',
            giaTri: chon,
            ds: _ds,
            onChanged: (v) => set(() => chon = v),
          ),
        ),
      ),
    ));
    expect(find.text('Lê Anh Tuấn'), findsOneWidget);

    await t.tap(find.byType(OChonTimKiem));
    await t.pumpAndSettle();
    await t.enterText(find.byType(TextField), 'nghia');
    await t.pumpAndSettle();
    expect(find.text('Nguyễn Thị Nghĩa'), findsOneWidget);
    expect(find.text('Đỗ Văn Đức'), findsNothing);
    await t.tap(find.text('Nguyễn Thị Nghĩa'));
    await t.pumpAndSettle();
    expect(chon, 'BTN-002');
    expect(find.text('Nguyễn Thị Nghĩa'), findsOneWidget);

    await t.tap(find.byType(OChonTimKiem));
    await t.pumpAndSettle();
    await t.tap(find.text('Bỏ chọn'));
    await t.pumpAndSettle();
    expect(chon, '');
  });

  testWidgets('Chỉ xem (onChanged null): chạm không mở danh sách', (t) async {
    await t.pumpWidget(const MaterialApp(
      home: Scaffold(body: OChonTimKiem(nhan: 'Người kiểm tra', giaTri: 'BTN-002', ds: _ds)),
    ));
    await t.tap(find.byType(OChonTimKiem));
    await t.pumpAndSettle();
    expect(find.byType(TextField), findsNothing);
    expect(find.text('Nguyễn Thị Nghĩa'), findsOneWidget);
  });
}
