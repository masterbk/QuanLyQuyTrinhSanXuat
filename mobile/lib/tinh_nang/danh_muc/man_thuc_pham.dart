import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import 'kho_du_lieu.dart';
import 'man_dinh_muc.dart';
import 'man_sua_thuc_pham.dart';
import 'mo_hinh.dart';

/// Danh mục Thực phẩm/SKU: nguyên liệu + thành phẩm. Thành phẩm có thêm nút "Định mức".
class ManThucPham extends ConsumerWidget {
  const ManThucPham({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final ds = ref.watch(dsThucPhamProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Thực phẩm / SKU')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const ManSuaThucPham())),
        icon: const Icon(Icons.add),
        label: const Text('Thêm'),
      ),
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(dsThucPhamProvider),
        child: ds.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => _Loi(
            thongBao: e is LoiApi ? e.thongBao : 'Không tải được danh sách: $e',
            thuLai: () => ref.invalidate(dsThucPhamProvider),
          ),
          data: (sp) => sp.isEmpty
              ? ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(24, 80, 24, 24),
                  children: const [
                    Icon(Icons.fastfood_outlined, size: 56, color: Colors.grey),
                    SizedBox(height: 12),
                    Text('Chưa có thực phẩm nào.', textAlign: TextAlign.center),
                  ],
                )
              : ListView.separated(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(8, 8, 8, 88),
                  itemCount: sp.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 4),
                  itemBuilder: (c, i) => _TheSanPham(sp: sp[i]),
                ),
        ),
      ),
    );
  }
}

class _TheSanPham extends ConsumerWidget {
  final SanPham sp;

  const _TheSanPham({required this.sp});

  @override
  Widget build(BuildContext context, WidgetRef ref) => Card(
        margin: EdgeInsets.zero,
        child: ListTile(
          title: Text(sp.tenSanPham, style: const TextStyle(fontWeight: FontWeight.bold)),
          subtitle: Text('${sp.maSanPham}${sp.donViTinh?.isNotEmpty ?? false ? ' · ${sp.donViTinh}' : ''}'
              '${sp.maQuyTrinh?.isNotEmpty ?? false ? ' · QT ${sp.maQuyTrinh}' : ''}'),
          leading: CircleAvatar(
            backgroundColor: (sp.laThanhPham ? Theme.of(context).colorScheme.primary : Colors.orange)
                .withValues(alpha: 0.15),
            child: Icon(sp.laThanhPham ? Icons.inventory_2_outlined : Icons.grass_outlined,
                color: sp.laThanhPham ? Theme.of(context).colorScheme.primary : Colors.orange.shade800, size: 20),
          ),
          trailing: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (sp.laThanhPham)
                IconButton(
                  tooltip: 'Định mức',
                  icon: const Icon(Icons.science_outlined),
                  onPressed: () =>
                      Navigator.push(context, MaterialPageRoute(builder: (_) => ManDinhMuc(sanPham: sp))),
                ),
              IconButton(
                tooltip: 'Sửa',
                icon: const Icon(Icons.edit_outlined),
                onPressed: () => Navigator.push(
                    context, MaterialPageRoute(builder: (_) => ManSuaThucPham(sanPham: sp))),
              ),
              IconButton(
                tooltip: 'Xoá',
                icon: Icon(Icons.delete_outline, color: Theme.of(context).colorScheme.error),
                onPressed: () => _xacNhanXoa(context, ref, sp),
              ),
            ],
          ),
        ),
      );

  Future<void> _xacNhanXoa(BuildContext context, WidgetRef ref, SanPham sp) async {
    final dongY = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('Xác nhận xoá'),
        content: Text('Xoá thực phẩm "${sp.tenSanPham}" (${sp.maSanPham})?'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Huỷ')),
          FilledButton(onPressed: () => Navigator.pop(c, true), child: const Text('Xoá')),
        ],
      ),
    );
    if (dongY != true) return;
    try {
      final tb = await ref.read(danhMucQuanLyProvider).xoaThucPham(sp.id);
      ref.invalidate(dsThucPhamProvider);
      if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
    } on LoiApi catch (e) {
      if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.thongBao)));
    }
  }
}

class _Loi extends StatelessWidget {
  final String thongBao;
  final VoidCallback thuLai;

  const _Loi({required this.thongBao, required this.thuLai});

  @override
  Widget build(BuildContext context) => ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(24, 80, 24, 24),
        children: [
          Text(thongBao, textAlign: TextAlign.center),
          const SizedBox(height: 12),
          Center(child: FilledButton.tonal(onPressed: thuLai, child: const Text('Thử lại'))),
        ],
      );
}
