import 'package:dio/dio.dart';

import 'luu_tru.dart';

/// Lỗi có thông báo tiếng Việt lấy từ máy chủ, hiển thị thẳng cho người dùng.
class LoiApi implements Exception {
  final String thongBao;
  final int? maHttp;

  LoiApi(this.thongBao, [this.maHttp]);

  bool get hetPhien => maHttp == 401;

  @override
  String toString() => thongBao;
}

/// Gọi API máy chủ HanoiCheck Platform.
///
/// Tự gắn access token vào mỗi request. Gặp 401 thì thử làm mới token đúng MỘT lần rồi
/// gửi lại request - người dùng không bị đá ra màn đăng nhập giữa chừng ca làm.
class ApiClient {
  /// Đánh dấu request KHÔNG gắn token (đăng nhập, làm mới, đăng xuất).
  /// Không lọc theo đường dẫn: '/auth/toi' cũng chứa '/auth/' nhưng lại CẦN token.
  static const _khongToken = 'khongToken';

  final Dio _dio;
  final LuuTru _luuTru;

  /// Gọi khi refresh token cũng hỏng - tầng ứng dụng chuyển về màn đăng nhập.
  void Function()? khiHetPhien;

  ApiClient(this._luuTru, {Dio? dio}) : _dio = dio ?? Dio() {
    _dio.options
      ..connectTimeout = const Duration(seconds: 20)
      ..receiveTimeout = const Duration(seconds: 60)
      ..validateStatus = (ma) => ma != null && ma < 500;

    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (opt, handler) async {
        opt.baseUrl = await _mayChu();
        if (opt.extra[_khongToken] != true) {
          final token = await _luuTru.accessToken();
          if (token != null) opt.headers['Authorization'] = 'Bearer $token';
        }
        handler.next(opt);
      },
    ));
  }

  Future<String> _mayChu() async {
    final dc = await _luuTru.mayChu();
    if (dc == null || dc.isEmpty) throw LoiApi('Chưa cấu hình địa chỉ máy chủ.');
    return dc.endsWith('/') ? dc.substring(0, dc.length - 1) : dc;
  }

  Future<dynamic> get(String duongDan, {Map<String, dynamic>? thamSo}) =>
      _goi(() => _dio.get(duongDan, queryParameters: thamSo));

  Future<dynamic> post(String duongDan, {Object? than}) =>
      _goi(() => _dio.post(duongDan, data: than));

  Future<dynamic> put(String duongDan, {Object? than}) =>
      _goi(() => _dio.put(duongDan, data: than));

  Future<dynamic> delete(String duongDan) => _goi(() => _dio.delete(duongDan));

  /// Gửi ảnh: cho thời gian gửi rộng rãi vì mạng di động có thể chậm.
  Future<dynamic> postFile(String duongDan, FormData form) =>
      _goi(() => _dio.post(duongDan, data: form,
          options: Options(sendTimeout: const Duration(minutes: 3))));

  /// Gọi KHÔNG kèm token và KHÔNG tự làm mới - dùng cho chính các API xác thực.
  Future<dynamic> postKhongToken(String duongDan, Object than) async {
    try {
      final r = await _dio.post(duongDan, data: than,
          options: Options(extra: {_khongToken: true}));
      return _docKetQua(r);
    } on DioException catch (e) {
      throw _tuDio(e);
    }
  }

  Future<dynamic> _goi(Future<Response> Function() ham) async {
    try {
      var r = await ham();
      if (r.statusCode == 401) {
        if (!await _lamMoiToken()) {
          khiHetPhien?.call();
          throw LoiApi('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.', 401);
        }
        r = await ham();   // thử lại đúng một lần với token mới
      }
      return _docKetQua(r);
    } on DioException catch (e) {
      throw _tuDio(e);
    }
  }

  bool _dangLamMoi = false;

  Future<bool> _lamMoiToken() async {
    if (_dangLamMoi) return false;
    _dangLamMoi = true;
    try {
      final refresh = await _luuTru.refreshToken();
      if (refresh == null) return false;
      final r = await _dio.post('/api/v1/auth/lam-moi',
          data: {'refreshToken': refresh},
          options: Options(extra: {_khongToken: true}));
      if (r.statusCode != 200 || r.data is! Map) return false;
      await _luuTru.capNhatToken(
        accessToken: r.data['accessToken'] as String,
        refreshToken: r.data['refreshToken'] as String,
      );
      return true;
    } catch (_) {
      return false;
    } finally {
      _dangLamMoi = false;
    }
  }

  dynamic _docKetQua(Response r) {
    final ma = r.statusCode ?? 0;
    if (ma >= 200 && ma < 300) return r.data;
    // Máy chủ trả {"thongBao": "..."} cho mọi lỗi nghiệp vụ.
    final tb = (r.data is Map && r.data['thongBao'] is String)
        ? r.data['thongBao'] as String
        : 'Máy chủ trả lỗi $ma.';
    throw LoiApi(tb, ma);
  }

  LoiApi _tuDio(DioException e) {
    if (e.error is LoiApi) return e.error as LoiApi;
    return switch (e.type) {
      DioExceptionType.connectionTimeout ||
      DioExceptionType.sendTimeout ||
      DioExceptionType.receiveTimeout =>
        LoiApi('Máy chủ phản hồi quá lâu. Kiểm tra lại mạng rồi thử lại.'),
      DioExceptionType.connectionError =>
        LoiApi('Không kết nối được máy chủ. Kiểm tra mạng và địa chỉ máy chủ.'),
      _ => LoiApi(e.message ?? 'Lỗi kết nối không rõ nguyên nhân.'),
    };
  }
}
