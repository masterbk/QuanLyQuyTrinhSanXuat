import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import '../lenh_san_xuat/mo_hinh.dart' show Kho, ThanhPham;
import '../xac_thuc/xac_thuc.dart';
import 'mo_hinh.dart';

/// Gọi nhóm API /api/v1/kho-noi-bo (tồn theo lô, nhập nguyên liệu, kiểm kê/điều chỉnh) và danh mục liên
/// quan (nguyên liệu, NCC đầu vào). Nghiệp vụ vận hành nội bộ, KHÔNG đồng bộ HanoiCheck.
class KhoNoiBoData {
  final ApiClient _api;

  KhoNoiBoData(this._api);

  Future<List<TonKho>> ton() async => ((await _api.get('/api/v1/kho-noi-bo/ton')) as List)
      .map((e) => TonKho.tuJson(e as Map<String, dynamic>)).toList();

  Future<List<LichSuKho>> lichSu({int gioiHan = 100}) async =>
      ((await _api.get('/api/v1/kho-noi-bo/lich-su', thamSo: {'gioiHan': gioiHan})) as List)
          .map((e) => LichSuKho.tuJson(e as Map<String, dynamic>)).toList();

  Future<String> nhap({
    required String maSanPham,
    required String maKho,
    required String maLo,
    required double soLuong,
    DateTime? hanSuDung,
    String? maNccDauVao,
    String? ghiChu,
  }) async {
    final j = await _api.post('/api/v1/kho-noi-bo/nhap', than: {
      'maSanPham': maSanPham,
      'maKho': maKho,
      'maLo': maLo,
      'soLuong': soLuong,
      if (hanSuDung != null) 'hanSuDung': _ngayIso(hanSuDung),
      'maNccDauVao': maNccDauVao,
      'ghiChu': ghiChu,
    }) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã nhập kho.';
  }

  Future<String> dieuChinh({
    required String maSanPham,
    required String maKho,
    required String maLo,
    required double soLuongThucTe,
    String? lyDo,
  }) async {
    final j = await _api.post('/api/v1/kho-noi-bo/dieu-chinh', than: {
      'maSanPham': maSanPham,
      'maKho': maKho,
      'maLo': maLo,
      'soLuongThucTe': soLuongThucTe,
      'lyDo': lyDo,
    }) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã điều chỉnh.';
  }

  Future<List<ThanhPham>> nguyenLieu() async => ((await _api.get('/api/v1/danh-muc/nguyen-lieu')) as List)
      .map((e) => ThanhPham.tuJson(e as Map<String, dynamic>)).toList();

  Future<List<Kho>> kho() async => ((await _api.get('/api/v1/danh-muc/kho')) as List)
      .map((e) => Kho.tuJson(e as Map<String, dynamic>)).toList();

  Future<List<NccDauVao>> nccDauVao() async => ((await _api.get('/api/v1/danh-muc/ncc-dau-vao')) as List)
      .map((e) => NccDauVao.tuJson(e as Map<String, dynamic>)).toList();

  static String _ngayIso(DateTime d) => d.toIso8601String().substring(0, 10);
}

final khoNoiBoProvider = Provider<KhoNoiBoData>((ref) => KhoNoiBoData(ref.watch(apiProvider)));

final tonKhoProvider = FutureProvider.autoDispose<List<TonKho>>((ref) => ref.watch(khoNoiBoProvider).ton());
final lichSuKhoProvider =
    FutureProvider.autoDispose<List<LichSuKho>>((ref) => ref.watch(khoNoiBoProvider).lichSu());
final nguyenLieuProvider = FutureProvider<List<ThanhPham>>((ref) => ref.watch(khoNoiBoProvider).nguyenLieu());
final khoDanhMucProvider = FutureProvider<List<Kho>>((ref) => ref.watch(khoNoiBoProvider).kho());
final nccDauVaoProvider = FutureProvider<List<NccDauVao>>((ref) => ref.watch(khoNoiBoProvider).nccDauVao());
