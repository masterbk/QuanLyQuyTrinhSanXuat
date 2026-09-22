import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import 'kho_du_lieu.dart';
import 'man_sua_quy_trinh.dart';
import 'mo_hinh.dart';

/// Danh mục Quy trình sản xuất: chuỗi khâu có thứ tự.
class ManQuyTrinh extends ConsumerWidget {
  const ManQuyTrinh({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final ds = ref.watch(dsQuyTrinhProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Quy trình sản xuất')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const ManSuaQuyTrinh())),
        icon: const Icon(Icons.add),
        label: const Text('Thêm quy trình'),
      ),
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(dsQuyTrinhProvider),
        child: ds.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => _Loi(
            thongBao: e is LoiApi ? e.thongBao : 'Không tải được danh sách: $e',
            thuLai: () => ref.invalidate(dsQuyTrinhProvider),
          ),
          data: (qt) => qt.isEmpty
              ? ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(24, 80, 24, 24),
                  children: const [
                    Icon(Icons.route_outlined, size: 56, color: Colors.grey),
                    SizedBox(height: 12),
                    Text('Chưa có quy trình sản xuất nào.', textAlign: TextAlign.center),
                  ],
                )
              : ListView.separated(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(8, 8, 8, 88),
                  itemCount: qt.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 4),
                  itemBuilder: (c, i) => _TheQuyTrinh(qt: qt[i]),
                ),
        ),
      ),
    );
  }
}

class _TheQuyTrinh extends ConsumerWidget {
  final QuyTrinhQuanLy qt;

  const _TheQuyTrinh({required this.qt});

  @override
  Widget build(BuildContext context, WidgetRef ref) => Card(
        margin: EdgeInsets.zero,
        child: ListTile(
          title: Text('${qt.maQuyTrinh} — ${qt.tenQuyTrinh}', style: const TextStyle(fontWeight: FontWeight.bold)),
          subtitle: Text(qt.tomTatKhau),
          isThreeLine: qt.tomTatKhau.length > 40,
          trailing: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              IconButton(
                tooltip: 'Sửa',
                icon: const Icon(Icons.edit_outlined),
                onPressed: () =>
                    Navigator.push(context, MaterialPageRoute(builder: (_) => ManSuaQuyTrinh(quyTrinh: qt))),
              ),
              IconButton(
                tooltip: 'Xoá',
                icon: Icon(Icons.delete_outline, color: Theme.of(context).colorScheme.error),
                onPressed: () => _xacNhanXoa(context, ref, qt),
              ),
            ],
          ),
        ),
      );

  Future<void> _xacNhanXoa(BuildContext context, WidgetRef ref, QuyTrinhQuanLy qt) async {
    final dongY = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('Xác nhận xoá'),
        content: Text('Xoá quy trình "${qt.tenQuyTrinh}" (${qt.maQuyTrinh})?'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Huỷ')),
          FilledButton(onPressed: () => Navigator.pop(c, true), child: const Text('Xoá')),
        ],
      ),
    );
    if (dongY != true) return;
    try {
      final tb = await ref.read(danhMucQuanLyProvider).xoaQuyTrinh(qt.id);
      ref.invalidate(dsQuyTrinhProvider);
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
