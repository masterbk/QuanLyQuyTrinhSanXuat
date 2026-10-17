import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'tinh_nang/bieu_mau/man_danh_sach.dart';
import 'tinh_nang/xac_thuc/man_doi_mat_khau.dart';
import 'tinh_nang/danh_muc/man_danh_sach.dart';
import 'tinh_nang/don_hang/man_danh_sach.dart';
import 'tinh_nang/kho_noi_bo/man_danh_sach.dart';
import 'tinh_nang/lenh_san_xuat/man_danh_sach.dart';
import 'tinh_nang/suat_an/man_suat_an.dart';
import 'tinh_nang/xac_thuc/xac_thuc.dart';

/// Khung chính sau khi đăng nhập: Lệnh sản xuất, Kho, Đơn hàng, Suất ăn, Danh mục, Biểu mẫu
/// - chỉ hiện mục người dùng có quyền và cơ sở được mở module.
class ManChinh extends ConsumerStatefulWidget {
  const ManChinh({super.key});

  @override
  ConsumerState<ManChinh> createState() => _ManChinhState();
}

/// Vai trò được dùng từng mục (khớp AppRoles phía máy chủ; người nhiều vai trò thấy gộp).
const _quyenLenhSanXuat = {'TenantAdmin', 'TenantStaff', 'TenantSanXuat'};
const _quyenKhoNoiBo = {'TenantAdmin', 'TenantStaff', 'TenantSanXuat'};
const _quyenDonHang = {'TenantAdmin', 'TenantStaff', 'TenantGiaoHang'};
const _quyenDanhMuc = {'TenantAdmin', 'TenantStaff'};
const _quyenBieuMau = {'TenantAdmin', 'TenantStaff', 'TenantBieuMau'};
/// Suất ăn chỉ để XEM nên mở cho mọi vai trò của cơ sở: bếp cần biết nấu gì, giao hàng cần biết bao nhiêu suất.
const _quyenSuatAn = {'TenantAdmin', 'TenantStaff', 'TenantSanXuat', 'TenantGiaoHang', 'TenantBieuMau'};

class _ManChinhState extends ConsumerState<ManChinh> {
  int _tab = 0;

  @override
  Widget build(BuildContext context) {
    final nd = ref.watch(xacThucProvider).nguoiDung;
    final vaiTro = nd?.vaiTro ?? const <String>[];
    // Chức năng (menu) quản trị nền tảng mở cho cơ sở - chồng lên vai trò. Máy chủ trả kèm cả mã
    // module cũ nên bản app này chạy được với máy chủ cũ lẫn mới (null = máy chủ rất cũ = mở hết).
    bool coModule(String ma) => nd?.coChucNang(ma) ?? false;
    final muc = <({String ten, Widget man, NavigationDestination nut})>[
      if (vaiTro.any(_quyenLenhSanXuat.contains) && coModule('LenhSanXuat'))
        (
          ten: 'Lệnh sản xuất',
          man: const ManDanhSachLenh(),
          nut: const NavigationDestination(icon: Icon(Icons.factory_outlined),
                                           selectedIcon: Icon(Icons.factory), label: 'Lệnh sản xuất'),
        ),
      if (vaiTro.any(_quyenKhoNoiBo.contains) && coModule('TonKho'))
        (
          ten: 'Kho',
          man: const ManKhoNoiBo(),
          nut: const NavigationDestination(icon: Icon(Icons.inventory_2_outlined),
                                           selectedIcon: Icon(Icons.inventory_2), label: 'Kho'),
        ),
      if (vaiTro.any(_quyenDonHang.contains) && coModule('DonHangBan'))
        (
          ten: 'Đơn hàng',
          man: const ManDanhSachDon(),
          nut: const NavigationDestination(icon: Icon(Icons.receipt_long_outlined),
                                           selectedIcon: Icon(Icons.receipt_long), label: 'Đơn hàng'),
        ),
      if (vaiTro.any(_quyenSuatAn.contains) && coModule('ThucDon'))
        (
          ten: 'Suất ăn',
          man: const ManSuatAn(),
          nut: const NavigationDestination(icon: Icon(Icons.restaurant_outlined),
                                           selectedIcon: Icon(Icons.restaurant), label: 'Suất ăn'),
        ),
      if (vaiTro.any(_quyenDanhMuc.contains))
        (
          ten: 'Danh mục',
          man: const ManDanhMuc(),
          nut: const NavigationDestination(icon: Icon(Icons.category_outlined),
                                           selectedIcon: Icon(Icons.category), label: 'Danh mục'),
        ),
      if (vaiTro.any(_quyenBieuMau.contains) && coModule('NhapPhieu'))
        (
          ten: 'Biểu mẫu',
          man: const ManDanhSachBieuMau(),
          nut: const NavigationDestination(icon: Icon(Icons.assignment_outlined),
                                           selectedIcon: Icon(Icons.assignment), label: 'Biểu mẫu'),
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
              if (v == 'doimk') {
                await Navigator.push(context, MaterialPageRoute(builder: (_) => const ManDoiMatKhau()));
                return;
              }
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
                    if ((nd?.tenCoSo ?? nd?.maCoSo) != null)
                      Text('Cơ sở: ${nd!.tenCoSo ?? nd.maCoSo}', style: Theme.of(context).textTheme.bodySmall),
                  ],
                ),
              ),
              const PopupMenuDivider(),
              const PopupMenuItem(value: 'doimk', child: Text('Đổi mật khẩu')),
              const PopupMenuItem(value: 'thoat', child: Text('Đăng xuất')),
            ],
          ),
        ],
      ),
      body: muc.isEmpty
          ? const _ChuaLam('Tài khoản chưa được phân quyền, hoặc cơ sở chưa được mở chức năng này', Icons.lock_outline)
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
