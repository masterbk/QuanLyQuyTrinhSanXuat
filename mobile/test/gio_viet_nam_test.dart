import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/loi/gio_viet_nam.dart';

void main() {
  test('Mốc UTC đổi sang giờ Việt Nam, qua được sang ngày hôm sau', () {
    final vn = gioVietNam(DateTime.parse('2026-09-13T17:30:00Z'));
    expect([vn.year, vn.month, vn.day, vn.hour, vn.minute], [2026, 9, 14, 0, 30]);
  });

  test('Chuỗi không có Z (máy chủ bản cũ) vẫn coi là UTC', () {
    final vn = gioVietNam(DateTime.parse('2026-09-13T02:57:56'));
    expect([vn.day, vn.hour, vn.minute], [13, 9, 57]);
  });
}
