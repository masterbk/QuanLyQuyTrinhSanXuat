import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'loi/dieu_huong.dart';
import 'loi/thong_bao_day.dart';
import 'man_chinh.dart';
import 'tinh_nang/don_hang/man_chi_tiet.dart';
import 'tinh_nang/xac_thuc/man_dang_nhap.dart';
import 'tinh_nang/xac_thuc/xac_thuc.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await ThongBaoDay.khoiTaoAsync();
  runApp(const ProviderScope(child: UngDung()));
}

class UngDung extends StatelessWidget {
  const UngDung({super.key});

  @override
  Widget build(BuildContext context) {
    final mau = ColorScheme.fromSeed(seedColor: const Color(0xFF5B4636));   // nâu bánh mì
    return MaterialApp(
      navigatorKey: navigatorKeyToanCuc,
      title: 'Quản lý sản xuất',
      debugShowCheckedModeBanner: false,
      locale: const Locale('vi'),
      supportedLocales: const [Locale('vi'), Locale('en')],
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      theme: ThemeData(
        colorScheme: mau,
        useMaterial3: true,
        // Ngón tay to hơn con trỏ chuột: cho các vùng bấm rộng rãi hơn mặc định.
        visualDensity: VisualDensity.comfortable,
      ),
      home: const CongVao(),
    );
  }
}

/// Quyết định vào thẳng app hay bắt đăng nhập, sau khi thử khôi phục phiên cũ.
class CongVao extends ConsumerStatefulWidget {
  const CongVao({super.key});

  @override
  ConsumerState<CongVao> createState() => _CongVaoState();
}

class _CongVaoState extends ConsumerState<CongVao> {
  bool _dangKhoiPhuc = true;
  StreamSubscription<Map<String, String>>? _thongBaoSub;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      await ref.read(xacThucProvider.notifier).khoiPhucPhien();
      if (mounted) setState(() => _dangKhoiPhuc = false);
    });

    // Bấm vào thông báo đơn hàng -> mở đúng màn chi tiết, nếu đã đăng nhập. Đây là nơi DUY NHẤT
    // biết cả hạ tầng thông báo (ThongBaoDay) và màn hình nghiệp vụ (ManChiTietDon).
    _thongBaoSub = ThongBaoDay.onMoTuThongBao.listen((duLieu) {
      final donHangId = int.tryParse(duLieu['donHangId'] ?? '');
      if (donHangId == null || !ref.read(xacThucProvider).daDangNhap) return;
      navigatorKeyToanCuc.currentState
          ?.push(MaterialPageRoute(builder: (_) => ManChiTietDon(id: donHangId)));
    });
  }

  @override
  void dispose() {
    _thongBaoSub?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (_dangKhoiPhuc) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    return ref.watch(xacThucProvider).daDangNhap ? const ManChinh() : const ManDangNhap();
  }
}
