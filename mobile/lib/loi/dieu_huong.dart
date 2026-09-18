import 'package:flutter/material.dart';

/// Khoá điều hướng toàn cục - cần để mở đúng màn khi bấm vào thông báo đẩy, lúc đó không có
/// BuildContext sẵn trong tay (xem thong_bao_day.dart). Gắn vào MaterialApp trong main.dart.
final navigatorKeyToanCuc = GlobalKey<NavigatorState>();
