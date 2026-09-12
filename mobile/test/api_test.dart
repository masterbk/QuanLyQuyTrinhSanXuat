import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hcp_mobile/loi/api.dart';
import 'package:hcp_mobile/loi/luu_tru.dart';

/// Kho giả lập trong bộ nhớ - không đụng Keystore thật khi chạy test.
class KhoGia extends FlutterSecureStorage {
  final Map<String, String> data;
  const KhoGia(this.data);

  @override
  Future<String?> read({required String key, iOptions, aOptions, lOptions, wOptions, mOptions, webOptions}) async =>
      data[key];

  @override
  Future<void> write({required String key, required String? value, iOptions, aOptions, lOptions, wOptions, mOptions, webOptions}) async {
    if (value == null) {
      data.remove(key);
    } else {
      data[key] = value;
    }
  }

  @override
  Future<void> delete({required String key, iOptions, aOptions, lOptions, wOptions, mOptions, webOptions}) async =>
      data.remove(key);
}

/// Trả lời thay máy chủ để kiểm chứng hành vi của ApiClient.
class MayChuGia extends Interceptor {
  final List<RequestOptions> daNhan = [];
  int soLanGoiDuLieu = 0;
  bool tokenConHan = false;
  bool choLamMoi = true;

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    daNhan.add(options);

    if (options.path.endsWith('/auth/lam-moi')) {
      return handler.resolve(Response(
        requestOptions: options,
        statusCode: choLamMoi ? 200 : 401,
        data: choLamMoi
            ? {'accessToken': 'token-moi', 'refreshToken': 'refresh-moi'}
            : {'thongBao': 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'},
      ));
    }

    if (options.path.endsWith('/lenh-san-xuat')) {
      soLanGoiDuLieu++;
      final hopLe = options.headers['Authorization'] == 'Bearer token-moi' || tokenConHan;
      return handler.resolve(Response(
        requestOptions: options,
        statusCode: hopLe ? 200 : 401,
        data: hopLe ? {'duLieu': [], 'tongSo': 0} : {'thongBao': 'Chưa đăng nhập'},
      ));
    }

    handler.resolve(Response(requestOptions: options, statusCode: 404, data: {'thongBao': 'Không có'}));
  }
}

void main() {
  late Map<String, String> duLieuKho;
  late LuuTru luuTru;
  late MayChuGia mayChu;
  late ApiClient api;

  setUp(() {
    duLieuKho = {
      'may_chu': 'https://vidu.vn/',
      'access_token': 'token-cu',
      'refresh_token': 'refresh-cu',
    };
    luuTru = LuuTru(KhoGia(duLieuKho));
    mayChu = MayChuGia();
    // ApiClient dựng interceptor của nó trước, rồi mới cắm máy chủ giả vào sau -
    // interceptor chạy theo thứ tự thêm, nếu cắm trước thì request bị chặn khi
    // chưa kịp gắn baseUrl và token.
    final dio = Dio();
    api = ApiClient(luuTru, dio: dio);
    dio.interceptors.add(mayChu);
  });

  test('Gắn token và bỏ dấu / thừa ở địa chỉ máy chủ', () async {
    mayChu.tokenConHan = true;
    await api.get('/api/v1/lenh-san-xuat');

    final req = mayChu.daNhan.last;
    expect(req.baseUrl, 'https://vidu.vn');            // không còn dấu / cuối
    expect(req.headers['Authorization'], 'Bearer token-cu');
  });

  test('Token hết hạn thì tự làm mới rồi gửi lại đúng một lần', () async {
    final kq = await api.get('/api/v1/lenh-san-xuat') as Map;

    expect(kq['tongSo'], 0);
    expect(mayChu.soLanGoiDuLieu, 2, reason: 'gọi lần đầu 401, sau khi làm mới gọi lại 1 lần');
    expect(duLieuKho['access_token'], 'token-moi');
    expect(duLieuKho['refresh_token'], 'refresh-moi');
  });

  test('Làm mới cũng hỏng thì báo hết phiên và gọi khiHetPhien', () async {
    mayChu.choLamMoi = false;
    var daBaoHetPhien = false;
    api.khiHetPhien = () => daBaoHetPhien = true;

    await expectLater(
      api.get('/api/v1/lenh-san-xuat'),
      throwsA(isA<LoiApi>().having((e) => e.hetPhien, 'hetPhien', true)),
    );
    expect(daBaoHetPhien, true);
  });

  test('Chưa cấu hình máy chủ thì báo lỗi rõ ràng', () async {
    duLieuKho.remove('may_chu');
    await expectLater(
      api.get('/api/v1/lenh-san-xuat'),
      throwsA(isA<LoiApi>().having((e) => e.thongBao, 'thongBao', contains('địa chỉ máy chủ'))),
    );
  });

  test('Đăng xuất giữ lại địa chỉ máy chủ, chỉ xoá token', () async {
    await luuTru.xoaPhien();

    expect(duLieuKho['may_chu'], isNotNull);
    expect(duLieuKho['access_token'], isNull);
    expect(duLieuKho['refresh_token'], isNull);
  });
}
