import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/tinh_nang/xac_thuc/xac_thuc.dart';

void main() {
  test('Đọc công tắc HanoiCheck từ máy chủ', () {
    expect(NguoiDung.tuJson({'id': '1', 'email': 'a@b.vn', 'hanoiCheckBat': false}).hanoiCheckBat, isFalse);
    expect(NguoiDung.tuJson({'id': '1', 'email': 'a@b.vn', 'hanoiCheckBat': true}).hanoiCheckBat, isTrue);
  });

  test('Máy chủ bản cũ chưa trả công tắc thì coi như bật (giữ cách làm cũ)', () {
    expect(NguoiDung.tuJson({'id': '1', 'email': 'a@b.vn'}).hanoiCheckBat, isTrue);
  });
}
