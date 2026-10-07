import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import '../xac_thuc/xac_thuc.dart';
import 'mo_hinh.dart';

/// Kho dữ liệu biểu mẫu kiểm soát: tải mẫu được phép điền, gửi phiếu, và danh mục để chọn.
class KhoBieuMau {
  final ApiClient _api;
  KhoBieuMau(this._api);

  Future<List<BieuMau>> bieuMau() async =>
      ((await _api.get('/api/v1/bieu-mau')) as List)
          .map((e) => BieuMau.tuJson(e as Map<String, dynamic>)).toList();

  Future<List<PhieuGhiNhan>> phieu({DateTime? ngay, bool cuaToi = false, int? bieuMauId}) async {
    final thamSo = <String, dynamic>{if (cuaToi) 'cuaToi': true};
    if (ngay != null) thamSo['ngay'] = _chuoiNgay(ngay);
    if (bieuMauId != null) thamSo['bieuMauId'] = bieuMauId;
    return ((await _api.get('/api/v1/phieu-ghi-nhan', thamSo: thamSo)) as List)
        .map((e) => PhieuGhiNhan.tuJson(e as Map<String, dynamic>)).toList();
  }

  /// Phiếu NHÁP của tôi cho biểu mẫu + ngày (để nhập tiếp). Null nếu chưa có.
  Future<PhieuGhiNhan?> phieuNhap(int bieuMauId, DateTime ngay) async {
    final r = await _api.get('/api/v1/phieu-ghi-nhan/nhap',
        thamSo: {'bieuMauId': bieuMauId, 'ngay': _chuoiNgay(ngay)});
    return r is Map<String, dynamic> ? PhieuGhiNhan.tuJson(r) : null;
  }

  /// Danh sách thiết bị/dòng đến hoặc quá hạn trong [soNgay] ngày tới (gồm quá hạn).
  Future<List<NhacHan>> nhacHan({int soNgay = 30}) async =>
      ((await _api.get('/api/v1/bieu-mau/nhac-han', thamSo: {'soNgay': soNgay})) as List)
          .map((e) => NhacHan.tuJson(e as Map<String, dynamic>)).toList();

  /// Tải một ảnh cho trường kiểu Ảnh; trả đường dẫn đã lưu (để gán vào giá trị trường).
  Future<String> taiAnh(List<int> bytes, String ten) async {
    final form = FormData();
    form.files.add(MapEntry('anh', MultipartFile.fromBytes(bytes, filename: ten)));
    final j = await _api.postFile('/api/v1/phieu-ghi-nhan/anh', form) as Map<String, dynamic>;
    return j['duongDan'] as String? ?? '';
  }

  /// Phiếu của biểu mẫu trong một NGÀY (mọi trạng thái) - cho biểu mẫu "1 phiếu/ngày". Null nếu chưa có.
  Future<PhieuGhiNhan?> phieuTheoNgay(int bieuMauId, DateTime ngay) async {
    final r = await _api.get('/api/v1/phieu-ghi-nhan/theo-ngay',
        thamSo: {'bieuMauId': bieuMauId, 'ngay': _chuoiNgay(ngay)});
    return r is Map<String, dynamic> ? PhieuGhiNhan.tuJson(r) : null;
  }

  /// Tạo phiếu mới. [hoanThanh]=false lưu nháp (nhập tiếp sau). [giaTriDau]: giá trị trường đầu phiếu.
  /// [dong]: mỗi dòng {hangMucBieuMauId?, giaTri: {ma_truong: giá trị}, ghiChu?}.
  Future<String> taoPhieu({
    required int bieuMauId,
    DateTime? ngay,
    Map<String, String>? giaTriDau,
    String? ghiChu,
    required List<Map<String, dynamic>> dong,
    bool hoanThanh = true,
  }) async {
    final j = await _api.post('/api/v1/phieu-ghi-nhan',
        than: _than(bieuMauId, ngay, giaTriDau, ghiChu, dong, hoanThanh)) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã lưu phiếu.';
  }

  /// Cập nhật phiếu nháp (nhập tiếp). [hoanThanh]=true thì chốt.
  Future<String> capNhatPhieu({
    required int id,
    required int bieuMauId,
    DateTime? ngay,
    Map<String, String>? giaTriDau,
    String? ghiChu,
    required List<Map<String, dynamic>> dong,
    bool hoanThanh = true,
  }) async {
    final j = await _api.put('/api/v1/phieu-ghi-nhan/$id',
        than: _than(bieuMauId, ngay, giaTriDau, ghiChu, dong, hoanThanh)) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã lưu phiếu.';
  }

  Map<String, dynamic> _than(int bieuMauId, DateTime? ngay, Map<String, String>? giaTriDau, String? ghiChu,
          List<Map<String, dynamic>> dong, bool hoanThanh) =>
      {
        'bieuMauId': bieuMauId,
        'ngay': ngay == null ? null : _chuoiNgay(ngay),
        'giaTriDau': giaTriDau ?? <String, String>{},
        'ghiChu': ghiChu,
        'dong': dong,
        'hoanThanh': hoanThanh,
      };

  // ---- Danh mục để chọn trong trường "Chọn..." ----
  Future<List<MucChon>> nhanSu() => _dm('nhan-su', (j) => MucChon(j['maNhanSu'] as String? ?? '', j['hoTen'] as String? ?? ''));
  Future<List<MucChon>> coSo() => _dm('co-so', (j) => MucChon(j['maCoSo'] as String? ?? '', j['tenCoSo'] as String? ?? ''));
  Future<List<MucChon>> thanhPham() => _dm('thanh-pham-ban', (j) => MucChon(j['maSanPham'] as String? ?? '', j['tenSanPham'] as String? ?? ''));
  Future<List<MucChon>> ncc() => _dm('ncc', (j) => MucChon(j['ma'] as String? ?? '', j['ten'] as String? ?? ''));

  Future<List<MucChon>> _dm(String duongDan, MucChon Function(Map<String, dynamic>) doc) async =>
      ((await _api.get('/api/v1/danh-muc/$duongDan')) as List)
          .map((e) => doc(e as Map<String, dynamic>)).toList();

  static String _chuoiNgay(DateTime d) =>
      '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';
}

final khoBieuMauProvider = Provider<KhoBieuMau>((ref) => KhoBieuMau(ref.watch(apiProvider)));

/// Danh sách biểu mẫu người đang đăng nhập được phép điền.
final bieuMauProvider = FutureProvider<List<BieuMau>>((ref) => ref.watch(khoBieuMauProvider).bieuMau());

/// Phiếu đã ghi trong NGÀY HÔM NAY (mọi biểu mẫu) - để hiện trạng thái "đã nhập/chưa nhập hôm nay".
final phieuHomNayProvider = FutureProvider.autoDispose<List<PhieuGhiNhan>>(
    (ref) => ref.watch(khoBieuMauProvider).phieu(ngay: DateTime.now()));

/// Danh sách nhắc hạn trong [soNgay] ngày tới.
final nhacHanProvider = FutureProvider.autoDispose.family<List<NhacHan>, int>(
    (ref, soNgay) => ref.watch(khoBieuMauProvider).nhacHan(soNgay: soNgay));

/// Danh mục dùng chung cho các trường chọn (tải một lần).
final nhanSuBmProvider = FutureProvider<List<MucChon>>((ref) => ref.watch(khoBieuMauProvider).nhanSu());
final coSoBmProvider = FutureProvider<List<MucChon>>((ref) => ref.watch(khoBieuMauProvider).coSo());
final thanhPhamBmProvider = FutureProvider<List<MucChon>>((ref) => ref.watch(khoBieuMauProvider).thanhPham());
final nccBmProvider = FutureProvider<List<MucChon>>((ref) => ref.watch(khoBieuMauProvider).ncc());
