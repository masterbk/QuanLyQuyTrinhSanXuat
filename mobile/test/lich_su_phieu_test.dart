import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/tinh_nang/bieu_mau/kho_du_lieu.dart';
import 'package:hcp_mobile/tinh_nang/bieu_mau/man_chi_tiet_phieu.dart';
import 'package:hcp_mobile/tinh_nang/bieu_mau/man_lich_su.dart';
import 'package:hcp_mobile/tinh_nang/bieu_mau/mo_hinh.dart';
import 'package:hcp_mobile/tinh_nang/lenh_san_xuat/mo_hinh.dart' show TrangDuLieu;

const _checklist = BieuMau(
  id: 7,
  maHieu: 'BM-CL',
  ten: 'Check list vệ sinh',
  boCuc: 'Checklist',
  truong: [
    TruongBieuMau(ma: 'dau_ca', ten: 'Đầu ca', kieu: 'DatKhongDat'),
    TruongBieuMau(ma: 'nguoi', ten: 'Người kiểm tra', kieu: 'ChonNhanSu'),
  ],
  hangMuc: [
    HangMucBieuMau(id: 1, ten: 'Sàn nhà'),
    HangMucBieuMau(id: 2, ten: 'Đèn bắt côn trùng'),
    HangMucBieuMau(id: 3, ten: 'Thùng rác'),
  ],
);

class _KhoGia extends Fake implements KhoBieuMau {
  final List<PhieuTomTat> ds;
  final PhieuGhiNhan? phieuGia;
  _KhoGia({this.ds = const [], this.phieuGia});

  @override
  Future<TrangDuLieu<PhieuTomTat>> lichSu(
          {DateTime? tuNgay, DateTime? denNgay, int? bieuMauId, bool cuaToi = false, int trang = 1, int soDong = 20}) async =>
      TrangDuLieu(duLieu: ds, trang: 1, soDong: soDong, tongSo: ds.length);

  @override
  Future<PhieuGhiNhan> chiTietPhieu(int id) async => phieuGia!;

  @override
  Future<BieuMau> bieuMauTheoId(int id) async => _checklist;
}

Widget _app(KhoBieuMau kho, Widget man) => ProviderScope(
      overrides: [
        khoBieuMauProvider.overrideWithValue(kho),
        bieuMauProvider.overrideWith((ref) async => const [_checklist]),
        nhanSuBmProvider.overrideWith((ref) async => const [MucChon('NS01', 'Nguyễn Văn A')]),
        coSoBmProvider.overrideWith((ref) async => const <MucChon>[]),
        thanhPhamBmProvider.overrideWith((ref) async => const <MucChon>[]),
        nccBmProvider.overrideWith((ref) async => const <MucChon>[]),
      ],
      child: MaterialApp(home: man),
    );

void main() {
  test('Nhãn nhóm ngày: Hôm nay / Hôm qua / dd/MM/yyyy', () {
    final h = DateTime(2026, 10, 8, 15);
    expect(nhanNgay(DateTime(2026, 10, 8), h), 'Hôm nay');
    expect(nhanNgay(DateTime(2026, 10, 7), h), 'Hôm qua');
    expect(nhanNgay(DateTime(2026, 9, 30), h), '30/09/2026');
  });

  testWidgets('Danh sách: nhóm theo ngày, tóm tắt KHÔNG ĐẠT, tên người lập, chip Nháp', (t) async {
    final kho = _KhoGia(ds: [
      PhieuTomTat(id: 1, bieuMauId: 7, maHieu: 'BM-CL', tenMau: 'Check list vệ sinh', ngay: DateTime(2026, 9, 30),
          trangThai: 'DaGhiNhan', nguoiLap: 'NS01', thoiGianUtc: DateTime.utc(2026, 9, 30, 1, 15),
          soDong: 3, soDat: 2, soKhongDat: 1, dauPhieu: const ['Khu vực: Xưởng 1']),
      PhieuTomTat(id: 2, bieuMauId: 9, maHieu: 'BM-KPH', tenMau: 'Sản phẩm KPH', ngay: DateTime(2026, 9, 29),
          trangThai: 'Nhap', thoiGianUtc: DateTime.utc(2026, 9, 29, 2), soDong: 4),
    ]);
    await t.pumpWidget(_app(kho, const ManLichSuPhieu()));
    await t.pumpAndSettle();

    expect(find.text('30/09/2026'), findsOneWidget);
    expect(find.text('29/09/2026'), findsOneWidget);
    expect(find.textContaining('KHÔNG ĐẠT'), findsOneWidget);
    expect(find.text('BM-CL · Khu vực: Xưởng 1'), findsOneWidget);
    expect(find.text('Nguyễn Văn A · 08:15'), findsOneWidget);   // giờ VN = UTC+7, mã NS01 đổi ra tên
    expect(find.text('4 dòng'), findsOneWidget);                 // phiếu không có ô Đạt/KĐ thì hiện số dòng
    expect(find.text('Nháp'), findsOneWidget);
  });

  testWidgets('Chi tiết checklist: lọc Không đạt, đổi mã nhân sự ra tên, nháp có nút Nhập tiếp', (t) async {
    final kho = _KhoGia(
      phieuGia: PhieuGhiNhan(id: 5, bieuMauId: 7, ngay: DateTime(2026, 10, 8), trangThai: 'Nhap', nguoiLap: 'NS01', dong: const [
        DongGhiNhan(hangMucId: 1, giaTri: {'dau_ca': 'Đạt', 'nguoi': 'NS01'}),
        DongGhiNhan(hangMucId: 2, giaTri: {'dau_ca': 'Không đạt'}, ghiChu: 'Đèn hỏng'),
      ]),
    );
    await t.pumpWidget(_app(kho, const ManChiTietPhieu(phieuId: 5)));
    await t.pumpAndSettle();

    expect(find.text('Sàn nhà'), findsOneWidget);
    expect(find.text('Thùng rác'), findsOneWidget);
    expect(find.text('Chưa nhập'), findsOneWidget);              // hạng mục chưa có dòng
    expect(find.text('Nguyễn Văn A'), findsNWidgets(2));         // người lập + ô Người kiểm tra
    expect(find.text('Nhập tiếp'), findsOneWidget);

    await t.tap(find.text('Không đạt (1)'));
    await t.pumpAndSettle();
    expect(find.text('Sàn nhà'), findsNothing);
    expect(find.text('Đèn bắt côn trùng'), findsOneWidget);
    expect(find.text('Đèn hỏng'), findsOneWidget);
  });

  testWidgets('Phiếu đã ghi nhận không có nút Nhập tiếp', (t) async {
    final kho = _KhoGia(phieuGia: PhieuGhiNhan(id: 6, bieuMauId: 7, ngay: DateTime(2026, 10, 8), trangThai: 'DaGhiNhan'));
    await t.pumpWidget(_app(kho, const ManChiTietPhieu(phieuId: 6)));
    await t.pumpAndSettle();
    expect(find.text('Nhập tiếp'), findsNothing);
    expect(find.text('Đã ghi nhận'), findsOneWidget);
  });
}
