import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import '../../loi/luu_tru.dart';
import '../../loi/thong_bao_day.dart';

/// Người dùng đang đăng nhập.
class NguoiDung {
  final String id;
  final String email;
  final String? hoTen;
  final String? maCoSo;

  /// Tên cơ sở để hiển thị (máy chủ bản cũ không trả thì dùng mã).
  final String? tenCoSo;
  final List<String> vaiTro;

  /// Cơ sở có bật đồng bộ HanoiCheck không. Tắt thì app ẩn phần HanoiCheck (tạo lô đồng bộ, giới hạn ảnh...).
  final bool hanoiCheckBat;

  /// Mã nhân sự gắn với tài khoản nhân viên (null với quản trị cơ sở).
  final String? maNhanSu;

  NguoiDung({required this.id, required this.email, this.hoTen, this.maCoSo, this.tenCoSo,
             this.vaiTro = const [], this.hanoiCheckBat = true, this.maNhanSu});

  /// Nhân viên sản xuất chế biến có hồ sơ nhân sự thì mới quét mã lệnh để tham gia các khâu.
  bool get coTheThamGiaLenh => vaiTro.contains('TenantSanXuat') && (maNhanSu?.isNotEmpty ?? false);

  /// Quản trị cơ sở/nhân viên nhập liệu: được xác nhận đơn hàng mới (khớp QuyenNhapLieu phía máy chủ).
  bool get coQuyenNhapLieu => vaiTro.any({'TenantAdmin', 'TenantStaff'}.contains);

  /// Chủ cơ sở (quản trị cơ sở): bao quát mọi đơn, không đi giao nên không cần tab "Chờ giao hàng"/"Của tôi".
  bool get laChuCoSo => vaiTro.contains('TenantAdmin');

  factory NguoiDung.tuJson(Map<String, dynamic> j) => NguoiDung(
        id: j['id'] as String? ?? '',
        email: j['email'] as String? ?? '',
        hoTen: j['hoTen'] as String?,
        maCoSo: j['maCoSo'] as String?,
        tenCoSo: j['tenCoSo'] as String?,
        vaiTro: (j['vaiTro'] as List?)?.map((e) => e.toString()).toList() ?? const [],
        // Máy chủ bản cũ chưa trả trường này: coi như bật để app giữ nguyên cách làm trước đây.
        hanoiCheckBat: j['hanoiCheckBat'] as bool? ?? true,
        maNhanSu: j['maNhanSu'] as String?,
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

/// Công tắc HanoiCheck MỚI NHẤT của cơ sở: đọc lại /auth/toi mỗi lần mở màn cần dùng, vì quản trị có thể vừa đổi
/// trên web. Không gọi được (mất sóng...) thì dùng giá trị lúc đăng nhập.
final hanoiCheckBatProvider = FutureProvider.autoDispose<bool>((ref) async {
  try {
    final j = await ref.read(apiProvider).get('/api/v1/auth/toi') as Map<String, dynamic>;
    return NguoiDung.tuJson(j).hanoiCheckBat;
  } catch (_) {
    return ref.read(xacThucProvider).nguoiDung?.hanoiCheckBat ?? true;
  }
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
      unawaited(ThongBaoDay.dangKyThietBiAsync(_dangKyThietBi));
    } on LoiApi catch (e) {
      // CHỈ xoá phiên khi máy chủ thực sự từ chối token. Mất sóng lúc mở app mà xoá
      // phiên thì người dùng phải đăng nhập lại oan, dù token vẫn còn hạn.
      if (e.hetPhien) await _luuTru.xoaPhien();
      state = TrangThaiXacThuc(loi: e.hetPhien ? null : e.thongBao);
    } catch (e) {
      state = TrangThaiXacThuc(loi: 'Không khôi phục được phiên: $e');
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
      unawaited(ThongBaoDay.dangKyThietBiAsync(_dangKyThietBi));
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
    // Bỏ đăng ký thiết bị TRƯỚC khi mất access token (API cần đăng nhập) - máy khác đăng nhập
    // tiếp không bị nhận nhầm thông báo của người vừa đăng xuất.
    try {
      final token = await ThongBaoDay.layTokenHienTaiAsync();
      if (token != null) await _api.delete('/api/v1/auth/thiet-bi?token=${Uri.encodeQueryComponent(token)}');
    } catch (_) {}

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

  /// Tự đổi mật khẩu. Máy chủ thu hồi mọi phiên cũ và trả phiên MỚI cho máy này (vẫn giữ đăng nhập; máy khác
  /// phải đăng nhập lại). Trả thông báo thành công; lỗi (sai mật khẩu hiện tại, mật khẩu yếu...) ném [LoiApi].
  Future<String> doiMatKhau({required String hienTai, required String moi}) async {
    final j = await _api.post('/api/v1/auth/doi-mat-khau',
        than: {'matKhauHienTai': hienTai, 'matKhauMoi': moi}) as Map<String, dynamic>;
    final access = j['accessToken'] as String?;
    final refresh = j['refreshToken'] as String?;
    if (access != null && refresh != null) {
      await _luuTru.capNhatToken(accessToken: access, refreshToken: refresh);
      final nd = j['nguoiDung'];
      if (nd is Map<String, dynamic>) state = TrangThaiXacThuc(nguoiDung: NguoiDung.tuJson(nd));
      return 'Đã đổi mật khẩu. Các thiết bị khác cần đăng nhập lại bằng mật khẩu mới.';
    }
    // Đổi được nhưng máy chủ không cấp phiên mới: đăng nhập lại cho chắc.
    await datLaiPhien();
    return j['thongBao'] as String? ?? 'Đã đổi mật khẩu. Vui lòng đăng nhập lại.';
  }

  /// Token hết hiệu lực giữa chừng: đưa app về màn đăng nhập.
  Future<void> datLaiPhien() async {
    await _luuTru.xoaPhien();
    state = const TrangThaiXacThuc(loi: 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
  }

  /// Gửi token thiết bị (FCM) lên máy chủ - lỗi không chặn đăng nhập, chỉ đơn giản là chưa nhận
  /// được thông báo đẩy.
  Future<void> _dangKyThietBi(String token) async {
    try {
      await _api.post('/api/v1/auth/thiet-bi', than: {'token': token});
    } catch (_) {}
  }
}
