import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Nơi cất phiên đăng nhập. Token để trong kho bảo mật của hệ điều hành
/// (Keystore trên Android) chứ không để trong SharedPreferences.
class LuuTru {
  static const _kMayChu = 'may_chu';
  static const _kAccessToken = 'access_token';
  static const _kRefreshToken = 'refresh_token';
  static const _kEmail = 'email';

  final FlutterSecureStorage _kho;

  LuuTru([FlutterSecureStorage? kho])
      // Bản 11 của flutter_secure_storage đã mã hoá sẵn theo mặc định
      // (Keystore trên Android), không cần khai thêm tuỳ chọn.
      : _kho = kho ?? const FlutterSecureStorage();

  Future<String?> mayChu() => _kho.read(key: _kMayChu);
  Future<void> datMayChu(String diaChi) => _kho.write(key: _kMayChu, value: diaChi);

  Future<String?> accessToken() => _kho.read(key: _kAccessToken);
  Future<String?> refreshToken() => _kho.read(key: _kRefreshToken);
  Future<String?> email() => _kho.read(key: _kEmail);

  Future<void> luuPhien({
    required String accessToken,
    required String refreshToken,
    required String email,
  }) async {
    await _kho.write(key: _kAccessToken, value: accessToken);
    await _kho.write(key: _kRefreshToken, value: refreshToken);
    await _kho.write(key: _kEmail, value: email);
  }

  Future<void> capNhatToken({required String accessToken, required String refreshToken}) async {
    await _kho.write(key: _kAccessToken, value: accessToken);
    await _kho.write(key: _kRefreshToken, value: refreshToken);
  }

  /// Xoá phiên nhưng GIỮ địa chỉ máy chủ - đăng xuất rồi đăng nhập lại không phải gõ lại.
  Future<void> xoaPhien() async {
    await _kho.delete(key: _kAccessToken);
    await _kho.delete(key: _kRefreshToken);
  }
}
