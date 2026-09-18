import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import '../lenh_san_xuat/kho_du_lieu.dart' show AnhDaChon;
import '../lenh_san_xuat/mo_hinh.dart' show TrangDuLieu;
import '../xac_thuc/xac_thuc.dart';
import 'mo_hinh.dart';

/// Gọi nhóm API /api/v1/don-hang-ban cho nhân viên giao hàng.
class KhoDonHang {
  final ApiClient _api;

  KhoDonHang(this._api);

  /// [canGiao] = chỉ đơn đang giao (việc của shipper); [cuaToi] = đơn mình đã nhận.
  Future<TrangDuLieu<DonHangBan>> danhSach(
      {bool canGiao = true, bool cuaToi = false, int trang = 1, int soDong = 20}) async {
    final j = await _api.get('/api/v1/don-hang-ban', thamSo: {
      if (canGiao) 'canGiao': true,
      if (cuaToi) 'cuaToi': true,
      'trang': trang,
      'soDong': soDong,
    }) as Map<String, dynamic>;
    return TrangDuLieu.tuJson(j, DonHangBan.tuJson);
  }

  Future<DonHangBan> chiTiet(int id) async =>
      DonHangBan.tuJson(await _api.get('/api/v1/don-hang-ban/$id') as Map<String, dynamic>);

  /// Tìm đơn từ nội dung mã QR quét được (QR tra cứu của đơn hoặc link truy xuất HanoiCheck).
  Future<DonHangBan> quet(String noiDung) async => DonHangBan.tuJson(
      await _api.get('/api/v1/don-hang-ban/quet', thamSo: {'noiDung': noiDung}) as Map<String, dynamic>);

  Future<String> nhanDon(int id) async {
    final j = await _api.post('/api/v1/don-hang-ban/$id/nhan-don') as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã nhận đơn.';
  }

  /// Xác nhận đã giao - bắt buộc kèm ảnh chụp tại chỗ.
  Future<String> daGiao(int id, List<AnhDaChon> anh) async {
    final form = FormData();
    for (final a in anh) {
      form.files.add(MapEntry('anh', MultipartFile.fromBytes(a.byte, filename: a.ten)));
    }
    final j = await _api.postFile('/api/v1/don-hang-ban/$id/da-giao', form) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã xác nhận giao hàng.';
  }
}

final khoDonProvider = Provider<KhoDonHang>((ref) => KhoDonHang(ref.watch(apiProvider)));

/// Bộ lọc danh sách đơn: "Cần giao" (mặc định), "Của tôi", "Tất cả".
enum LocDon { canGiao, cuaToi, tatCa }

final locDonProvider = NotifierProvider<LocDonNotifier, LocDon>(LocDonNotifier.new);

class LocDonNotifier extends Notifier<LocDon> {
  @override
  LocDon build() => LocDon.canGiao;

  void dat(LocDon v) => state = v;
}

final danhSachDonProvider =
    AsyncNotifierProvider<DanhSachDonNotifier, List<DonHangBan>>(DanhSachDonNotifier.new);

class DanhSachDonNotifier extends AsyncNotifier<List<DonHangBan>> {
  static const _soDong = 30;

  @override
  Future<List<DonHangBan>> build() async {
    final loc = ref.watch(locDonProvider);
    final trang = await ref.read(khoDonProvider).danhSach(
          canGiao: loc != LocDon.tatCa,
          cuaToi: loc == LocDon.cuaToi,
          soDong: _soDong,
        );
    return trang.duLieu;
  }

  Future<void> taiLai() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() => build());
  }
}
