import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import '../xac_thuc/xac_thuc.dart';
import 'mo_hinh.dart';

/// Gọi nhóm API /api/v1/lenh-san-xuat và /api/v1/danh-muc.
class KhoLenhSanXuat {
  final ApiClient _api;

  KhoLenhSanXuat(this._api);

  Future<TrangDuLieu<LenhSanXuat>> danhSach({String? trangThai, int trang = 1, int soDong = 20}) async {
    final j = await _api.get('/api/v1/lenh-san-xuat', thamSo: {
      'trangThai': ?trangThai,
      'trang': trang,
      'soDong': soDong,
    }) as Map<String, dynamic>;
    return TrangDuLieu.tuJson(j, LenhSanXuat.tuJson);
  }

  Future<LenhSanXuat> chiTiet(int id) async =>
      LenhSanXuat.tuJson(await _api.get('/api/v1/lenh-san-xuat/$id') as Map<String, dynamic>);

  Future<String> tao(Map<String, dynamic> than) async {
    final j = await _api.post('/api/v1/lenh-san-xuat', than: than) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã tạo lệnh.';
  }

  Future<String> sua(int id, Map<String, dynamic> than) async {
    final j = await _api.put('/api/v1/lenh-san-xuat/$id', than: than) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã cập nhật lệnh.';
  }

  Future<String> xoa(int id) async {
    final j = await _api.delete('/api/v1/lenh-san-xuat/$id') as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã xoá lệnh.';
  }

  Future<String> huy(int id, String lyDo) async {
    final j = await _api.post('/api/v1/lenh-san-xuat/$id/huy', than: {'lyDo': lyDo}) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã huỷ lệnh.';
  }

  /// Hoàn thành lệnh kèm ảnh lô thành phẩm (bắt buộc ít nhất 1 ảnh).
  Future<String> hoanThanh(int id, List<({String ten, Uint8List byte})> anh) async {
    final form = FormData();
    for (final a in anh) {
      form.files.add(MapEntry('anh', MultipartFile.fromBytes(a.byte, filename: a.ten)));
    }
    final j = await _api.postFile('/api/v1/lenh-san-xuat/$id/hoan-thanh', form) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã hoàn thành lệnh.';
  }

  Future<List<NguyenLieuCan>> nguyenLieuCan(String maThanhPham, double soLuong, String maKho) async {
    final j = await _api.get('/api/v1/lenh-san-xuat/nguyen-lieu-can', thamSo: {
      'maThanhPham': maThanhPham,
      'soLuong': soLuong,
      'maKho': maKho,
    }) as List;
    return j.map((e) => NguyenLieuCan.tuJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<ThanhPham>> thanhPham() async =>
      ((await _api.get('/api/v1/danh-muc/thanh-pham')) as List)
          .map((e) => ThanhPham.tuJson(e as Map<String, dynamic>)).toList();

  Future<List<Kho>> kho() async =>
      ((await _api.get('/api/v1/danh-muc/kho')) as List)
          .map((e) => Kho.tuJson(e as Map<String, dynamic>)).toList();
}

final khoLenhProvider = Provider<KhoLenhSanXuat>((ref) => KhoLenhSanXuat(ref.watch(apiProvider)));

/// Lọc theo trạng thái đang chọn trên màn danh sách (null = tất cả).
/// Riverpod 3 đã bỏ StateProvider nên dùng Notifier cho một giá trị đơn giản.
final locTrangThaiProvider =
    NotifierProvider<LocTrangThaiNotifier, String?>(LocTrangThaiNotifier.new);

class LocTrangThaiNotifier extends Notifier<String?> {
  @override
  String? build() => null;

  void dat(String? ma) => state = ma;
}

/// Danh sách lệnh theo bộ lọc hiện tại. Tự tải lại khi đổi bộ lọc.
final danhSachLenhProvider =
    AsyncNotifierProvider<DanhSachLenhNotifier, List<LenhSanXuat>>(DanhSachLenhNotifier.new);

class DanhSachLenhNotifier extends AsyncNotifier<List<LenhSanXuat>> {
  static const _soDong = 20;
  int _trang = 1;
  bool _conTrangSau = false;
  bool _dangTaiThem = false;

  bool get conTrangSau => _conTrangSau;

  @override
  Future<List<LenhSanXuat>> build() async {
    final loc = ref.watch(locTrangThaiProvider);
    _trang = 1;
    final trang = await ref.read(khoLenhProvider).danhSach(trangThai: loc, trang: 1, soDong: _soDong);
    _conTrangSau = trang.conTrangSau;
    return trang.duLieu;
  }

  Future<void> taiLai() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() => build());
  }

  /// Cuộn tới cuối danh sách thì lấy thêm trang sau.
  Future<void> taiThem() async {
    if (_dangTaiThem || !_conTrangSau) return;
    _dangTaiThem = true;
    try {
      final loc = ref.read(locTrangThaiProvider);
      final trang = await ref.read(khoLenhProvider)
          .danhSach(trangThai: loc, trang: _trang + 1, soDong: _soDong);
      _trang++;
      _conTrangSau = trang.conTrangSau;
      state = AsyncValue.data([...(state.value ?? []), ...trang.duLieu]);
    } on LoiApi {
      // Lỗi tải thêm không nên xoá trắng danh sách đang xem.
    } finally {
      _dangTaiThem = false;
    }
  }
}

final thanhPhamProvider = FutureProvider<List<ThanhPham>>((ref) => ref.watch(khoLenhProvider).thanhPham());
final khoProvider = FutureProvider<List<Kho>>((ref) => ref.watch(khoLenhProvider).kho());
