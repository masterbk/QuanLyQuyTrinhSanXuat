import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import '../xac_thuc/xac_thuc.dart';
import 'mo_hinh.dart';

/// Ảnh đã chọn, giữ sẵn byte để gửi đi.
typedef AnhDaChon = ({String ten, Uint8List byte});

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

  /// Hoàn thành lệnh. [anh]: ảnh theo Id dòng sản phẩm (mỗi dòng bắt buộc ≥ 1 ảnh), gửi trong
  /// trường "anh_{id}". [khau]: người thực hiện / cơ sở sửa lại lúc hoàn thành (nếu có).
  Future<String> hoanThanh(int id, Map<int, List<AnhDaChon>> anh, List<Map<String, dynamic>> khau) async {
    final form = FormData();
    anh.forEach((idSanPham, ds) {
      for (final a in ds) {
        form.files.add(MapEntry('anh_$idSanPham', MultipartFile.fromBytes(a.byte, filename: a.ten)));
      }
    });
    if (khau.isNotEmpty) form.fields.add(MapEntry('khau', jsonEncode(khau)));
    final j = await _api.postFile('/api/v1/lenh-san-xuat/$id/hoan-thanh', form) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã hoàn thành lệnh.';
  }

  /// Nguyên liệu cần cho nhiều dòng sản phẩm - nhu cầu được cộng dồn phía máy chủ.
  Future<List<NguyenLieuCan>> nguyenLieuCan(
      List<({String maThanhPham, double soLuong})> dong, String maKho) async {
    final j = await _api.post('/api/v1/lenh-san-xuat/nguyen-lieu-can', than: {
      'maKho': maKho,
      'dong': dong.map((d) => {'maThanhPham': d.maThanhPham, 'soLuong': d.soLuong}).toList(),
    }) as List;
    return j.map((e) => NguyenLieuCan.tuJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<ThanhPham>> thanhPham() => _dm('thanh-pham', ThanhPham.tuJson);
  Future<List<Kho>> kho() => _dm('kho', Kho.tuJson);
  Future<List<QuyTrinh>> quyTrinh() => _dm('quy-trinh', QuyTrinh.tuJson);
  Future<List<CoSo>> coSo() => _dm('co-so', CoSo.tuJson);
  Future<List<NhanSu>> nhanSu() => _dm('nhan-su', NhanSu.tuJson);

  Future<List<T>> _dm<T>(String duongDan, T Function(Map<String, dynamic>) doc) async =>
      ((await _api.get('/api/v1/danh-muc/$duongDan')) as List)
          .map((e) => doc(e as Map<String, dynamic>)).toList();
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

/// Chỉ giữ quy trình có ít nhất một khâu - quy trình rỗng không lập lệnh được.
final quyTrinhProvider = FutureProvider<List<QuyTrinh>>(
    (ref) async => (await ref.watch(khoLenhProvider).quyTrinh()).where((q) => q.khau.isNotEmpty).toList());
final coSoProvider = FutureProvider<List<CoSo>>((ref) => ref.watch(khoLenhProvider).coSo());
final nhanSuProvider = FutureProvider<List<NhanSu>>((ref) => ref.watch(khoLenhProvider).nhanSu());
