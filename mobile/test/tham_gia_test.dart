import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/tinh_nang/lenh_san_xuat/mo_hinh.dart';

void main() {
  test('Đọc mã lệnh từ QR hoặc ô nhập tay', () {
    expect(maLenhTuQr('LSX:LSX-20260918-001'), 'LSX-20260918-001');
    expect(maLenhTuQr(' lsx: LSX-1 '), 'LSX-1');
    expect(maLenhTuQr('https://app.vn/tra-cuu/lo/abc'), isNull);     // QR tra cứu lô, không phải lệnh
    expect(maLenhTuQr('LSX-20260918-001'), isNull);                   // quét camera phải có tiền tố
    expect(maLenhTuQr('LSX-20260918-001', nhapTay: true), 'LSX-20260918-001');
    expect(maLenhTuQr('https://x.vn/a', nhapTay: true), isNull);
    expect(maLenhTuQr('LSX:', nhapTay: true), isNull);
  });

  test('Khâu tick sẵn: chưa tham gia thì chọn tất cả, đã tham gia thì giữ khâu của mình', () {
    KhauLenh k(int id, List<String> nguoi) =>
        KhauLenh(id: id, maKhau: 'K$id', thuTu: id, maCoSo: 'CS', nguoiThucHien: nguoi);
    final l = LenhSanXuat(
      id: 1,
      maLenh: 'LSX-1',
      maKho: 'KHO',
      ngaySanXuat: DateTime(2026, 9, 18),
      trangThai: 'MoiTao',
      trangThaiHienThi: 'Mới tạo',
      sanPham: [
        SanPhamLenh(id: 1, maThanhPham: 'A', soLuong: 1, maLoThanhPham: 'LO1', maQuyTrinh: 'Q',
            khau: [k(1, []), k(2, ['NS02'])]),
        SanPhamLenh(id: 2, maThanhPham: 'B', soLuong: 1, maLoThanhPham: 'LO2', maQuyTrinh: 'Q',
            khau: [k(3, ['ns01'])]),
      ],
    );

    expect(khauChonMacDinh(l, 'NS01'), {3});
    expect(khauChonMacDinh(l, 'NS09'), {1, 2, 3});
  });
}
