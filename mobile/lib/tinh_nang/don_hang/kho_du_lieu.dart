import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import '../lenh_san_xuat/kho_du_lieu.dart' show AnhDaChon;
import '../lenh_san_xuat/mo_hinh.dart' show ThanhPham, TrangDuLieu;
import '../xac_thuc/xac_thuc.dart';
import 'mo_hinh.dart';

/// Một lô đã chốt khi xuất kho: dòng nào, lô nào, lấy bao nhiêu.
typedef PhanBoLo = ({int dongId, String maLo, double soLuong});

/// Gọi nhóm API /api/v1/don-hang-ban và các danh mục liên quan (khách hàng, thành phẩm).
class KhoDonHang {
  final ApiClient _api;

  KhoDonHang(this._api);

  /// [canGiao] = chỉ đơn đang giao (việc của shipper); [cuaToi] = đơn mình đã nhận;
  /// [trangThai] = lọc đúng 1 trạng thái (VD "ChoXacNhan" cho quản lý/nhập liệu xác nhận đơn mới).
  Future<TrangDuLieu<DonHangBan>> danhSach(
      {bool canGiao = true, bool cuaToi = false, String? trangThai, int trang = 1, int soDong = 20}) async {
    final j = await _api.get('/api/v1/don-hang-ban', thamSo: {
      'trangThai': ?trangThai,
      if (canGiao && trangThai == null) 'canGiao': true,
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

  /// Xác nhận đơn mới (Chờ xác nhận -> Đã xác nhận) - việc của quản lý/nhập liệu.
  Future<String> xacNhan(int id) async {
    final j = await _api.post('/api/v1/don-hang-ban/$id/xac-nhan') as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã xác nhận đơn hàng.';
  }

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

  Future<String> taoDon(Map<String, dynamic> than) async {
    final j = await _api.post('/api/v1/don-hang-ban', than: than) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã tạo đơn hàng.';
  }

  Future<String> suaDon(int id, Map<String, dynamic> than) async {
    final j = await _api.put('/api/v1/don-hang-ban/$id', than: than) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã cập nhật đơn hàng.';
  }

  Future<String> xoaDon(int id) async {
    final j = await _api.delete('/api/v1/don-hang-ban/$id') as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã xoá đơn hàng.';
  }

  Future<String> huyDon(int id, String lyDo) async {
    final j = await _api.post('/api/v1/don-hang-ban/$id/huy', than: {'lyDo': lyDo}) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã huỷ đơn hàng.';
  }

  Future<List<DongXuatKho>> goiYXuatKho(int id) async =>
      ((await _api.get('/api/v1/don-hang-ban/$id/goi-y-xuat-kho')) as List)
          .map((e) => DongXuatKho.tuJson(e as Map<String, dynamic>)).toList();

  /// [phanBo] rỗng = dùng gợi ý FEFO phía máy chủ. [anh]: ảnh tổng quan (chỉ đơn nguồn HanoiCheck cần).
  Future<String> xuatKho(int id, List<PhanBoLo> phanBo, {String? maNguoiGiao, String? ghiChu,
      List<AnhDaChon> anh = const []}) async {
    final form = FormData();
    if (phanBo.isNotEmpty) {
      form.fields.add(MapEntry('phanBo', jsonEncode(phanBo
          .map((p) => {'dongId': p.dongId, 'maLo': p.maLo, 'soLuong': p.soLuong}).toList())));
    }
    if (maNguoiGiao != null) form.fields.add(MapEntry('maNguoiGiao', maNguoiGiao));
    if (ghiChu != null) form.fields.add(MapEntry('ghiChu', ghiChu));
    for (final a in anh) {
      form.files.add(MapEntry('anh', MultipartFile.fromBytes(a.byte, filename: a.ten)));
    }
    final j = await _api.postFile('/api/v1/don-hang-ban/$id/xuat-kho', form) as Map<String, dynamic>;
    return j['thongBao'] as String? ?? 'Đã xuất kho.';
  }

  Future<List<KhachHang>> khachHang() async => ((await _api.get('/api/v1/danh-muc/khach-hang')) as List)
      .map((e) => KhachHang.tuJson(e as Map<String, dynamic>)).toList();

  Future<List<ThanhPham>> thanhPhamBan() async => ((await _api.get('/api/v1/danh-muc/thanh-pham-ban')) as List)
      .map((e) => ThanhPham.tuJson(e as Map<String, dynamic>)).toList();
}

final khoDonProvider = Provider<KhoDonHang>((ref) => KhoDonHang(ref.watch(apiProvider)));

/// Bộ lọc danh sách đơn: "Cần giao" (mặc định), "Của tôi", "Chờ xác nhận"/"Cần xuất kho"
/// (quản lý/nhập liệu), "Tất cả".
enum LocDon { canGiao, cuaToi, choXacNhan, canXuatKho, tatCa }

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
  int _trang = 1;
  bool _conTrangSau = false;
  bool _dangTaiThem = false;

  bool get conTrangSau => _conTrangSau;

  @override
  Future<List<DonHangBan>> build() async {
    final loc = ref.watch(locDonProvider);
    _trang = 1;
    final trang = await ref.read(khoDonProvider).danhSach(
          canGiao: loc == LocDon.canGiao || loc == LocDon.cuaToi,
          cuaToi: loc == LocDon.cuaToi,
          trangThai: switch (loc) {
            LocDon.choXacNhan => 'ChoXacNhan',
            LocDon.canXuatKho => 'DaXacNhan',
            _ => null,
          },
          soDong: _soDong,
        );
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
      final loc = ref.read(locDonProvider);
      final trang = await ref.read(khoDonProvider).danhSach(
            canGiao: loc == LocDon.canGiao || loc == LocDon.cuaToi,
            cuaToi: loc == LocDon.cuaToi,
            trangThai: switch (loc) {
              LocDon.choXacNhan => 'ChoXacNhan',
              LocDon.canXuatKho => 'DaXacNhan',
              _ => null,
            },
            trang: _trang + 1,
            soDong: _soDong,
          );
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

final khachHangProvider = FutureProvider<List<KhachHang>>((ref) => ref.watch(khoDonProvider).khachHang());
final thanhPhamBanProvider = FutureProvider<List<ThanhPham>>((ref) => ref.watch(khoDonProvider).thanhPhamBan());
