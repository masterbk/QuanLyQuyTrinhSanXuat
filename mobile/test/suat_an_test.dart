import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/tinh_nang/suat_an/kho_du_lieu.dart';
import 'package:hcp_mobile/tinh_nang/suat_an/man_suat_an.dart';
import 'package:hcp_mobile/tinh_nang/suat_an/mo_hinh.dart';

/// Kho dữ liệu giả: ghi lại ngày đã hỏi để kiểm chứng "null = hôm nay".
class KhoSuatAnGia implements SuatAnData {
  final SuatAnNgay duLieu;
  bool daHoi = false;
  DateTime? ngayDaHoi;

  KhoSuatAnGia(this.duLieu);

  @override
  Future<SuatAnNgay> theoNgay(DateTime? ngay) async {
    daHoi = true;
    ngayDaHoi = ngay;
    return duLieu;
  }
}

SuatAnNgay _banTin({
  List<ThucDonTrongNgay> thucDon = const [],
  List<SuatTheoKhach> theoKhach = const [],
  double tongSuat = 0,
  List<SuatTheoMon> tongTheoMon = const [],
}) =>
    SuatAnNgay(
      ngay: DateTime(2026, 10, 15),
      tenThu: 'Thứ 5',
      thucDon: thucDon,
      theoKhachHang: theoKhach,
      tongSuat: tongSuat,
      tongTheoMon: tongTheoMon,
    );

Widget _app(KhoSuatAnGia kho) => ProviderScope(
      overrides: [suatAnProvider.overrideWithValue(kho)],
      child: const MaterialApp(home: ManSuatAn()),
    );

void main() {
  testWidgets('Mở màn là hỏi HÔM NAY (không gửi ngày) và hiện thứ + ngày', (t) async {
    final kho = KhoSuatAnGia(_banTin());
    await t.pumpWidget(_app(kho));
    await t.pumpAndSettle();

    expect(kho.daHoi, isTrue);
    expect(kho.ngayDaHoi, isNull);                       // null = để máy chủ lấy hôm nay theo giờ VN
    expect(find.text('Thứ 5, 15/10/2026'), findsOneWidget);
  });

  testWidgets('Hiện số suất theo trường, mở ra thấy từng món', (t) async {
    final kho = KhoSuatAnGia(_banTin(
      tongSuat: 320,
      theoKhach: const [
        SuatTheoKhach(
          maKhachHang: 'KH-MN',
          tenKhachHang: 'Mầm non Quang Trung',
          tongSuat: 240,
          danhSachMon: [
            SuatTheoMon(maMonAn: 'MON-CANH', tenMonAn: 'Canh bí', soSuat: 120),
            SuatTheoMon(maMonAn: 'MON-THIT', tenMonAn: 'Thịt kho', soSuat: 120),
          ],
        ),
        SuatTheoKhach(
          maKhachHang: 'KH-TH', tenKhachHang: 'Tiểu học Việt Úc', tongSuat: 80,
          danhSachMon: [SuatTheoMon(maMonAn: 'MON-THIT', tenMonAn: 'Thịt kho', soSuat: 80)],
        ),
      ],
    ));
    await t.pumpWidget(_app(kho));
    await t.pumpAndSettle();

    expect(find.text('320 suất'), findsOneWidget);                 // tổng ở tiêu đề
    expect(find.text('Mầm non Quang Trung'), findsOneWidget);
    expect(find.text('240 suất · 2 món'), findsOneWidget);
    expect(find.text('Canh bí'), findsNothing);                    // chưa mở thì chưa thấy món

    await t.tap(find.text('Mầm non Quang Trung'));
    await t.pumpAndSettle();
    expect(find.text('Canh bí'), findsOneWidget);
    expect(find.text('120 suất'), findsNWidgets(2));
  });

  testWidgets('Thực đơn gom theo bữa ăn', (t) async {
    final kho = KhoSuatAnGia(_banTin(thucDon: const [
      ThucDonTrongNgay(
        id: 1, maThucDon: 'TD-T5', tenThucDon: 'Thực đơn thứ 5', tenNhomTuoi: 'Mẫu giáo',
        tenTruong: ['Mầm non Quang Trung'],
        danhSachMon: [
          MonTrongThucDon(maMonAn: 'MON-CHAO', tenMonAn: 'Cháo gà', tenBuoiAn: 'Bữa sáng'),
          MonTrongThucDon(maMonAn: 'MON-THIT', tenMonAn: 'Thịt kho', tenBuoiAn: 'Bữa trưa'),
          MonTrongThucDon(maMonAn: 'MON-CANH', tenMonAn: 'Canh bí', tenBuoiAn: 'Bữa trưa'),
        ],
      ),
    ]));
    await t.pumpWidget(_app(kho));
    await t.pumpAndSettle();

    expect(find.text('Thực đơn thứ 5'), findsOneWidget);
    expect(find.text('Mẫu giáo · Mầm non Quang Trung'), findsOneWidget);
    expect(find.text('Bữa sáng'), findsOneWidget);
    expect(find.text('Bữa trưa'), findsOneWidget);                 // hai món bữa trưa gom một mục
    expect(find.text('• Thịt kho'), findsOneWidget);
    expect(find.text('• Canh bí'), findsOneWidget);
  });

  testWidgets('Ngày trống thì nói rõ, không để màn trắng', (t) async {
    await t.pumpWidget(_app(KhoSuatAnGia(_banTin())));
    await t.pumpAndSettle();

    expect(find.textContaining('chưa có thực đơn'), findsOneWidget);
  });
}
