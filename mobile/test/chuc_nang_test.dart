import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/tinh_nang/xac_thuc/xac_thuc.dart';

void main() {
  test('Máy chủ trả danh sách chức năng: chỉ mở module có trong danh sách', () {
    final nd = NguoiDung.tuJson({'id': '1', 'email': 'a@x.vn', 'chucNang': ['BanHang', 'KhoSanXuat']});
    expect(nd.coChucNang('BanHang'), isTrue);
    expect(nd.coChucNang('KiemSoatAttp'), isFalse);
  });

  test('Cơ sở cơ bản (danh sách rỗng): không mở module nào', () {
    final nd = NguoiDung.tuJson({'id': '1', 'email': 'a@x.vn', 'chucNang': <String>[]});
    expect(nd.coChucNang('KhoSanXuat'), isFalse);
  });

  test('Máy chủ bản cũ chưa trả chucNang: coi như mở hết', () {
    final nd = NguoiDung.tuJson({'id': '1', 'email': 'a@x.vn'});
    expect(nd.coChucNang('KiemSoatAttp'), isTrue);
  });
}
