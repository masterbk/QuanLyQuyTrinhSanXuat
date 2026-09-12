import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'tinh_nang/lenh_san_xuat/man_danh_sach.dart';
import 'tinh_nang/xac_thuc/xac_thuc.dart';

/// Khung chính sau khi đăng nhập: 2 mục đúng phạm vi app - Lệnh sản xuất và Đơn hàng.
class ManChinh extends ConsumerStatefulWidget {
  const ManChinh({super.key});

  @override
  ConsumerState<ManChinh> createState() => _ManChinhState();
}

class _ManChinhState extends ConsumerState<ManChinh> {
  int _tab = 0;

  @override
  Widget build(BuildContext context) {
    final nd = ref.watch(xacThucProvider).nguoiDung;

    return Scaffold(
      appBar: AppBar(
        title: Text(_tab == 0 ? 'Lệnh sản xuất' : 'Đơn hàng'),
        actions: [
          PopupMenuButton<String>(
            tooltip: 'Tài khoản',
            icon: const Icon(Icons.account_circle_outlined),
            onSelected: (v) async {
              if (v == 'thoat') {
                final dongY = await showDialog<bool>(
                  context: context,
                  builder: (c) => AlertDialog(
                    title: const Text('Đăng xuất'),
                    content: const Text('Đăng xuất khỏi ứng dụng?'),
                    actions: [
                      TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Huỷ')),
                      FilledButton(onPressed: () => Navigator.pop(c, true), child: const Text('Đăng xuất')),
                    ],
                  ),
                );
                if (dongY == true) await ref.read(xacThucProvider.notifier).dangXuat();
              }
            },
            itemBuilder: (c) => [
              PopupMenuItem(
                enabled: false,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(nd?.tenHienThi ?? '', style: const TextStyle(fontWeight: FontWeight.bold)),
                    if (nd?.maCoSo != null)
                      Text('Cơ sở: ${nd!.maCoSo}', style: Theme.of(context).textTheme.bodySmall),
                  ],
                ),
              ),
              const PopupMenuDivider(),
              const PopupMenuItem(value: 'thoat', child: Text('Đăng xuất')),
            ],
          ),
        ],
      ),
      body: IndexedStack(
        index: _tab,
        children: const [
          ManDanhSachLenh(),
          _ChuaLam('Đơn hàng', Icons.receipt_long_outlined),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _tab,
        onDestinationSelected: (i) => setState(() => _tab = i),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.factory_outlined),
                                selectedIcon: Icon(Icons.factory), label: 'Lệnh sản xuất'),
          NavigationDestination(icon: Icon(Icons.receipt_long_outlined),
                                selectedIcon: Icon(Icons.receipt_long), label: 'Đơn hàng'),
        ],
      ),
    );
  }
}

/// Chỗ giữ sẵn cho hai màn nghiệp vụ sẽ làm ở bước sau.
class _ChuaLam extends StatelessWidget {
  final String ten;
  final IconData bieuTuong;

  const _ChuaLam(this.ten, this.bieuTuong);

  @override
  Widget build(BuildContext context) => Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(bieuTuong, size: 56, color: Theme.of(context).hintColor),
            const SizedBox(height: 12),
            Text(ten, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 4),
            Text('Đang xây dựng', style: Theme.of(context).textTheme.bodySmall),
          ],
        ),
      );
}
