import 'dart:async';
import 'dart:convert';
import 'dart:developer' as developer;

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';

/// Tin nhắn tới lúc app đã tắt hẳn PHẢI xử lý bằng hàm top-level (không phải closure/method) -
/// yêu cầu của firebase_messaging. Không cần làm gì thêm ở đây: hệ điều hành tự hiện thông báo
/// hệ thống từ phần "notification" của gói FCM khi app không chạy.
@pragma('vm:entry-point')
Future<void> xuLyThongBaoNenAsync(RemoteMessage message) async {
  await Firebase.initializeApp();
}

/// Lõi thông báo đẩy (FCM): khởi tạo, lấy/theo dõi token thiết bị, tự hiện banner cục bộ khi app
/// đang mở (Android KHÔNG tự hiện phần "notification" của FCM lúc foreground), và phát dữ liệu
/// khi người dùng bấm vào thông báo. KHÔNG tự điều hướng màn hình ở đây - đó là việc của tầng
/// trên (main.dart, nơi biết cả màn hình nghiệp vụ), file này chỉ thuần hạ tầng.
class ThongBaoDay {
  ThongBaoDay._();

  static const _kenh = AndroidNotificationChannel(
    'don_hang',
    'Đơn hàng',
    description: 'Thông báo đơn hàng mới và đổi trạng thái',
    importance: Importance.max,
  );

  static final _cucBo = FlutterLocalNotificationsPlugin();
  static final _controller = StreamController<Map<String, String>>.broadcast();

  static bool _daKhoiTao = false;

  /// Dữ liệu kèm thông báo (VD donHangId) khi người dùng bấm vào - lúc app đang mở, mở lại từ
  /// nền, hoặc mở app từ trạng thái tắt hẳn.
  static Stream<Map<String, String>> get onMoTuThongBao => _controller.stream;

  static Future<void> khoiTaoAsync() async {
    if (_daKhoiTao) return;
    try {
      await Firebase.initializeApp();
    } catch (e) {
      // Chưa có google-services.json hợp lệ (máy/CI chưa cấu hình Firebase) - app vẫn chạy bình
      // thường, chỉ đơn giản là chưa có thông báo đẩy.
      developer.log('Không khởi tạo được Firebase: $e', name: 'ThongBaoDay');
      return;
    }
    _daKhoiTao = true;

    FirebaseMessaging.onBackgroundMessage(xuLyThongBaoNenAsync);

    await _cucBo.initialize(
      settings: const InitializationSettings(
        android: AndroidInitializationSettings('@mipmap/ic_launcher'),
      ),
      onDidReceiveNotificationResponse: (resp) => _phatDuLieu(_giaiMa(resp.payload)),
    );
    await _cucBo
        .resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>()
        ?.createNotificationChannel(_kenh);

    await FirebaseMessaging.instance.requestPermission();

    FirebaseMessaging.onMessage.listen(_hienCucBo);
    FirebaseMessaging.onMessageOpenedApp.listen((m) => _phatDuLieu(_tuDuLieu(m.data)));

    final khoiDong = await FirebaseMessaging.instance.getInitialMessage();
    if (khoiDong != null) _phatDuLieu(_tuDuLieu(khoiDong.data));
  }

  /// Đăng ký token thiết bị hiện tại qua [guiLenMayChu], và tự gửi lại mỗi khi FCM đổi token.
  /// Gọi lại an toàn ở mỗi lần đăng nhập/khôi phục phiên - máy chủ upsert theo token.
  static Future<void> dangKyThietBiAsync(Future<void> Function(String token) guiLenMayChu) async {
    if (!_daKhoiTao) return;
    try {
      final token = await FirebaseMessaging.instance.getToken();
      if (token != null) await guiLenMayChu(token);
    } catch (e) {
      developer.log('Không đăng ký được thiết bị nhận thông báo: $e', name: 'ThongBaoDay');
    }
    FirebaseMessaging.instance.onTokenRefresh.listen((t) => guiLenMayChu(t).catchError((_) {}));
  }

  /// Token hiện tại của thiết bị (dùng lúc đăng xuất để bỏ đăng ký đúng token).
  static Future<String?> layTokenHienTaiAsync() async {
    if (!_daKhoiTao) return null;
    try {
      return await FirebaseMessaging.instance.getToken();
    } catch (_) {
      return null;
    }
  }

  static Future<void> _hienCucBo(RemoteMessage message) async {
    final noti = message.notification;
    if (noti == null) return;
    await _cucBo.show(
      id: DateTime.now().millisecondsSinceEpoch.remainder(1 << 31),
      title: noti.title,
      body: noti.body,
      notificationDetails: NotificationDetails(
        android: AndroidNotificationDetails(_kenh.id, _kenh.name,
            channelDescription: _kenh.description, importance: Importance.max, priority: Priority.high),
      ),
      payload: message.data.isEmpty ? null : jsonEncode(message.data),
    );
  }

  static Map<String, String> _tuDuLieu(Map<String, dynamic> d) => d.map((k, v) => MapEntry(k, v.toString()));

  static Map<String, String> _giaiMa(String? payload) {
    if (payload == null || payload.isEmpty) return const {};
    try {
      return _tuDuLieu((jsonDecode(payload) as Map).map((k, v) => MapEntry(k.toString(), v)));
    } catch (_) {
      return const {};
    }
  }

  static void _phatDuLieu(Map<String, String> duLieu) {
    if (duLieu.isNotEmpty) _controller.add(duLieu);
  }
}
