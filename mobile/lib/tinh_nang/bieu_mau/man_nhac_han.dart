import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';

/// Danh sách thiết bị đến/quá hạn (hiệu chuẩn, bảo dưỡng). Chọn khoảng ngày để lọc.
class ManNhacHan extends ConsumerStatefulWidget {
  const ManNhacHan({super.key});

  @override
  ConsumerState<ManNhacHan> createState() => _ManNhacHanState();
}

class _ManNhacHanState extends ConsumerState<ManNhacHan> {
  int _soNgay = 30;

  @override
  Widget build(BuildContext context) {
    final ds = ref.watch(nhacHanProvider(_soNgay));
    return Scaffold(
      appBar: AppBar(
        title: const Text('Nhắc hạn thiết bị'),
        actions: [
          PopupMenuButton<int>(
            tooltip: 'Khoảng thời gian',
            initialValue: _soNgay,
            onSelected: (v) => setState(() => _soNgay = v),
            itemBuilder: (_) => const [
              PopupMenuItem(value: 7, child: Text('7 ngày tới')),
              PopupMenuItem(value: 30, child: Text('30 ngày tới')),
              PopupMenuItem(value: 60, child: Text('60 ngày tới')),
              PopupMenuItem(value: 90, child: Text('90 ngày tới')),
            ],
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12),
              child: Row(children: [Text('$_soNgay ngày'), const Icon(Icons.arrow_drop_down)]),
            ),
          ),
        ],
      ),
      body: ds.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => _loi(e),
        data: (list) => RefreshIndicator(
          onRefresh: () async => ref.invalidate(nhacHanProvider(_soNgay)),
          child: list.isEmpty
              ? ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(24, 80, 24, 24),
                  children: [
                    Icon(Icons.check_circle, size: 56, color: Colors.green.shade400),
                    const SizedBox(height: 12),
                    Text('Không có thiết bị nào đến hạn trong $_soNgay ngày tới.',
                        textAlign: TextAlign.center, style: Theme.of(context).textTheme.titleMedium),
                  ],
                )
              : ListView.separated(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(12, 12, 12, 24),
                  itemCount: list.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 8),
                  itemBuilder: (_, i) => _the(list[i]),
                ),
        ),
      ),
    );
  }

  Widget _the(NhacHan n) {
    final (mau, chu) = n.quaHan
        ? (Colors.red.shade100, Colors.red.shade900)
        : (Colors.orange.shade100, Colors.orange.shade900);
    final conLai = n.soNgayConLai < 0
        ? 'Quá hạn ${-n.soNgayConLai} ngày'
        : n.soNgayConLai == 0
            ? 'Đến hạn hôm nay'
            : 'Còn ${n.soNgayConLai} ngày';
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(color: mau, borderRadius: BorderRadius.circular(8)),
              child: Icon(n.quaHan ? Icons.warning : Icons.schedule, color: chu),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(n.nhan, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
                  const SizedBox(height: 2),
                  Text('${n.tenBieuMau} • ${n.tenTruong}', style: Theme.of(context).textTheme.bodySmall),
                  const SizedBox(height: 2),
                  Text('Hạn: ${_ngay(n.han)}', style: Theme.of(context).textTheme.bodySmall),
                ],
              ),
            ),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(color: mau, borderRadius: BorderRadius.circular(20)),
              child: Text(conLai, style: TextStyle(color: chu, fontWeight: FontWeight.w600, fontSize: 12)),
            ),
          ],
        ),
      ),
    );
  }

  static String _ngay(DateTime d) =>
      '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')}/${d.year}';

  Widget _loi(Object e) => ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(24, 80, 24, 24),
        children: [
          Text(e is LoiApi ? e.thongBao : 'Không tải được nhắc hạn: $e', textAlign: TextAlign.center),
          const SizedBox(height: 12),
          Center(child: FilledButton(
              onPressed: () => ref.invalidate(nhacHanProvider(_soNgay)), child: const Text('Thử lại'))),
        ],
      );
}
