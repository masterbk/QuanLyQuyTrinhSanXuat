import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import '../../loi/luu_tru.dart';

/// Người dùng đang đăng nhập.
class NguoiDung {
  final String id;
  final String email;
  final String? hoTen;
  final String? maCoSo;
  final List<String> vaiTro;

  NguoiDung({required this.id, required this.email, this.hoTen, this.maCoSo,
             this.vaiTro = const []});

  factory NguoiDung.tuJson(Map<String, dynamic> j) => NguoiDung(
        id: j['id'] as String? ?? '',
        email: j['email'] as String? ?? '',
        hoTen: j['hoTen'] as String?,
        maCoSo: j['maCoSo'] as String?,
        vaiTro: (j['vaiTro'] as List?)?.map((e) => e.toString()).toList() ?? const [],
      );

  String get tenHienThi => (hoTen != null && hoTen!.isNotEmpty) ? hoTen! : email;
}

/// Trạng thái đăng nhập của app.
class TrangThaiXacThuc {
  final bool dangTai;
  final NguoiDung? nguoiDung;
  final String? loi;

  const TrangThaiXacThuc({this.dangTai = false, this.nguoiDung, this.loi});

  bool get daDangNhap => nguoiDung != null;

  TrangThaiXacThuc sao({bool? dangTai, NguoiDung? nguoiDung, String? loi, bool xoaNguoiDung = false}) =>
      TrangThaiXacThuc(
        dangTai: dangTai ?? this.dangTai,
        nguoiDung: xoaNguoiDung ? null : (nguoiDung ?? this.nguoiDung),
        loi: loi,
      );
}

final luuTruProvider = Provider<LuuTru>((ref) => LuuTru());

final apiProvider = Provider<ApiClient>((ref) {
  final api = ApiClient(ref.watch(luuTruProvider));
  api.khiHetPhien = () => ref.read(xacThucProvider.notifier).datLaiPhien();
  return api;
});

final xacThucProvider =
    NotifierProvider<XacThucNotifier, TrangThaiXacThuc>(XacThucNotifier.new);

class XacThucNotifier extends Notifier<TrangThaiXacThuc> {
  @override
  TrangThaiXacThuc build() => const TrangThaiXacThuc();

  LuuTru get _luuTru => ref.read(luuTruProvider);
  ApiClient get _api => ref.read(apiProvider);

  /// Mở app: nếu còn token thì vào thẳng, khỏi bắt đăng nhập lại.
  Future<void> khoiPhucPhien() async {
    state = state.sao(dangTai: true);
    try {
      if (await _luuTru.accessToken() == null) {
        state = const TrangThaiXacThuc();
        return;
      }
      final j = await _api.get('/api/v1/auth/toi') as Map<String, dynamic>;
      state = TrangThaiXacThuc(nguoiDung: NguoiDung.tuJson(j));
    } catch (_) {
      await _luuTru.xoaPhien();
      state = const TrangThaiXacThuc();
    }
  }

  Future<bool> dangNhap({
    required String mayChu,
    required String email,
    required String matKhau,
    String? thietBi,
  }) async {
    state = state.sao(dangTai: true, loi: null);
    try {
      await _luuTru.datMayChu(mayChu.trim());
      final j = await _api.postKhongToken('/api/v1/auth/dang-nhap', {
        'email': email.trim(),
        'matKhau': matKhau,
        'thietBi': thietBi,
      }) as Map<String, dynamic>;

      await _luuTru.luuPhien(
        accessToken: j['accessToken'] as String,
        refreshToken: j['refreshToken'] as String,
        email: email.trim(),
      );
      state = TrangThaiXacThuc(nguoiDung: NguoiDung.tuJson(j['nguoiDung'] as Map<String, dynamic>));
      return true;
    } on LoiApi catch (e) {
      state = TrangThaiXacThuc(loi: e.thongBao);
      return false;
    } catch (e) {
      state = TrangThaiXacThuc(loi: 'Lỗi không mong đợi: $e');
      return false;
    }
  }

  Future<void> dangXuat() async {
    final refresh = await _luuTru.refreshToken();
    if (refresh != null) {
      // Báo máy chủ thu hồi token; mạng hỏng cũng vẫn đăng xuất tại máy.
      try {
        await _api.postKhongToken('/api/v1/auth/dang-xuat', {'refreshToken': refresh});
      } catch (_) {}
    }
    await _luuTru.xoaPhien();
    state = const TrangThaiXacThuc();
  }

  /// Token hết hiệu lực giữa chừng: đưa app về màn đăng nhập.
  Future<void> datLaiPhien() async {
    await _luuTru.xoaPhien();
    state = const TrangThaiXacThuc(loi: 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
  }
}
