import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import '../xac_thuc/xac_thuc.dart';
import 'mo_hinh.dart';

/// Gọi nhóm API /api/v1/quan-ly (Thực phẩm/SKU + Định mức, Khâu sản xuất, Quy trình sản xuất).
/// Chỉ quản trị/nhân viên nhập liệu được sửa (khớp AppRoles.QuyenNhapLieu phía máy chủ).
class DanhMucQuanLyData {
  final ApiClient _api;

  DanhMucQuanLyData(this._api);

  // ---------- Thực phẩm / SKU ----------

  Future<List<SanPham>> thucPham() async =>
      ((await _api.get('/api/v1/quan-ly/thuc-pham')) as List)
          .map((e) => SanPham.tuJson(e as Map<String, dynamic>)).toList();

  Future<String> luuThucPham(SanPham sp) async {
    final j = (sp.id == 0
        ? await _api.post('/api/v1/quan-ly/thuc-pham', than: sp.raJson())
        : await _api.put('/api/v1/quan-ly/thuc-pham/${sp.id}', than: sp.raJson())) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã lưu.';
  }

  Future<String> xoaThucPham(int id) async {
    final j = await _api.delete('/api/v1/quan-ly/thuc-pham/$id') as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã xoá.';
  }

  Future<List<DinhMucDong>> dinhMuc(int idThanhPham) async =>
      ((await _api.get('/api/v1/quan-ly/thuc-pham/$idThanhPham/dinh-muc')) as List)
          .map((e) => DinhMucDong.tuJson(e as Map<String, dynamic>)).toList();

  Future<String> luuDinhMuc(int idThanhPham, List<DinhMucDong> dong) async {
    final j = await _api.put('/api/v1/quan-ly/thuc-pham/$idThanhPham/dinh-muc',
        than: {'dong': dong.map((d) => d.raJson()).toList()}) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã lưu định mức.';
  }

  // ---------- Khâu sản xuất ----------

  Future<List<Khau>> khauSanXuat() async =>
      ((await _api.get('/api/v1/quan-ly/khau-san-xuat')) as List)
          .map((e) => Khau.tuJson(e as Map<String, dynamic>)).toList();

  Future<String> luuKhau({int? id, required bool dongBoHnC, required String tenKhau, String? ghiChu}) async {
    final than = {'dongBoHnC': dongBoHnC, 'tenKhau': tenKhau, 'ghiChu': ghiChu};
    final j = (id == null
        ? await _api.post('/api/v1/quan-ly/khau-san-xuat', than: than)
        : await _api.put('/api/v1/quan-ly/khau-san-xuat/$id', than: than)) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã lưu.';
  }

  Future<String> xoaKhau(int id) async {
    final j = await _api.delete('/api/v1/quan-ly/khau-san-xuat/$id') as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã xoá.';
  }

  // ---------- Quy trình sản xuất ----------

  Future<List<QuyTrinhQuanLy>> quyTrinh() async =>
      ((await _api.get('/api/v1/quan-ly/quy-trinh')) as List)
          .map((e) => QuyTrinhQuanLy.tuJson(e as Map<String, dynamic>)).toList();

  Future<String> luuQuyTrinh({
    int? id,
    required bool dongBoHnC,
    required String maQuyTrinh,
    required String tenQuyTrinh,
    int? maDanhMucThucPham,
    required List<String> danhSachMaKhau,
  }) async {
    final than = {
      'dongBoHnC': dongBoHnC,
      'maQuyTrinh': maQuyTrinh,
      'tenQuyTrinh': tenQuyTrinh,
      'maDanhMucThucPham': maDanhMucThucPham,
      'danhSachKhau': [
        for (var i = 0; i < danhSachMaKhau.length; i++) {'maKhau': danhSachMaKhau[i], 'thuTu': i + 1},
      ],
    };
    final j = (id == null
        ? await _api.post('/api/v1/quan-ly/quy-trinh', than: than)
        : await _api.put('/api/v1/quan-ly/quy-trinh/$id', than: than)) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã lưu.';
  }

  Future<String> xoaQuyTrinh(int id) async {
    final j = await _api.delete('/api/v1/quan-ly/quy-trinh/$id') as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã xoá.';
  }
}

final danhMucQuanLyProvider = Provider<DanhMucQuanLyData>((ref) => DanhMucQuanLyData(ref.watch(apiProvider)));

final dsThucPhamProvider = FutureProvider.autoDispose<List<SanPham>>((ref) => ref.watch(danhMucQuanLyProvider).thucPham());
final dsKhauProvider = FutureProvider.autoDispose<List<Khau>>((ref) => ref.watch(danhMucQuanLyProvider).khauSanXuat());
final dsQuyTrinhProvider = FutureProvider.autoDispose<List<QuyTrinhQuanLy>>((ref) => ref.watch(danhMucQuanLyProvider).quyTrinh());
