import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'tinh_nang/don_hang/man_danh_sach.dart';
import 'tinh_nang/lenh_san_xuat/man_danh_sach.dart';
import 'tinh_nang/xac_thuc/xac_thuc.dart';

/// Khung chính sau khi đăng nhập: Lệnh sản xuất và Đơn hàng, chỉ hiện mục người dùng có quyền.
class ManChinh extends ConsumerStatefulWidget {
  const ManChinh({super.key});

  @override
  ConsumerState<ManChinh> createState() => _ManChinhState();
}

/// Vai trò được dùng từng mục (khớp AppRoles phía máy chủ; người nhiều vai trò thấy gộp).
const _quyenLenhSanXuat = {'TenantAdmin', 'TenantStaff', 'TenantSanXuat'};
const _quyenDonHang = {'TenantAdmin', 'TenantStaff', 'TenantGiaoHang'};

class _ManChinhState extends ConsumerState<ManChinh> {
  int _tab = 0;

  @override
  Widget build(BuildContext context) {
    final nd = ref.watch(xacThucProvider).nguoiDung;
    final vaiTro = nd?.vaiTro ?? const <String>[];
    final muc = <({String ten, Widget man, NavigationDestination nut})>[
      if (vaiTro.any(_quyenLenhSanXuat.contains))
        (
          ten: 'Lệnh sản xuất',
          man: const ManDanhSachLenh(),
          nut: const NavigationDestination(icon: Icon(Icons.factory_outlined),
                                           selectedIcon: Icon(Icons.factory), label: 'Lệnh sản xuất'),
        ),
      if (vaiTro.any(_quyenDonHang.contains))
        (
          ten: 'Đơn hàng',
          man: const ManDanhSachDon(),
          nut: const NavigationDestination(icon: Icon(Icons.receipt_long_outlined),
                                           selectedIcon: Icon(Icons.receipt_long), label: 'Đơn hàng'),
        ),
    ];
    final tab = muc.isEmpty ? 0 : _tab.clamp(0, muc.length - 1);

    return Scaffold(
      appBar: AppBar(
        title: Text(muc.isEmpty ? 'Quản lý sản xuất' : muc[tab].ten),
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
      body: muc.isEmpty
          ? const _ChuaLam('Tài khoản chưa được phân quyền dùng ứng dụng', Icons.lock_outline)
          : IndexedStack(index: tab, children: [for (final m in muc) m.man]),
      // NavigationBar cần ít nhất 2 mục: chỉ một quyền thì không hiện thanh chuyển mục.
      bottomNavigationBar: muc.length < 2
          ? null
          : NavigationBar(
              selectedIndex: tab,
              onDestinationSelected: (i) => setState(() => _tab = i),
              destinations: [for (final m in muc) m.nut],
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
            if (bieuTuong != Icons.lock_outline)
              Text('Đang xây dựng', style: Theme.of(context).textTheme.bodySmall),
          ],
        ),
      );
}
