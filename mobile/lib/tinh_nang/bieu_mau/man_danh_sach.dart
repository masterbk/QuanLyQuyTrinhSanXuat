import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import 'kho_du_lieu.dart';
import 'man_nhap_phieu.dart';
import 'mo_hinh.dart';

/// Danh sách biểu mẫu kiểm soát nhân viên được phép điền; chạm vào một mẫu để nhập phiếu.
class ManDanhSachBieuMau extends ConsumerWidget {
  const ManDanhSachBieuMau({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final ds = ref.watch(bieuMauProvider);
    return Scaffold(
      body: ds.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => _Loi(
          thongBao: e is LoiApi ? e.thongBao : 'Không tải được biểu mẫu: $e',
          thuLai: () => ref.invalidate(bieuMauProvider),
        ),
        data: (list) => list.isEmpty
            ? _Rong()
            : RefreshIndicator(
                onRefresh: () async => ref.invalidate(bieuMauProvider),
                child: ListView.separated(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(12, 8, 12, 24),
                  itemCount: list.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 8),
                  itemBuilder: (c, i) => _The(mau: list[i]),
                ),
              ),
      ),
    );
  }
}

class _The extends ConsumerWidget {
  final BieuMau mau;
  const _The({required this.mau});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Card(
      margin: EdgeInsets.zero,
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () async {
          final tb = await Navigator.push<String>(
              context, MaterialPageRoute(builder: (_) => ManNhapPhieu(mau: mau)));
          if (tb != null && context.mounted) {
            ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
          }
        },
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(
            children: [
              CircleAvatar(
                backgroundColor: Theme.of(context).colorScheme.secondaryContainer,
                child: Icon(_bieuTuong(mau.boCuc), color: Theme.of(context).colorScheme.onSecondaryContainer),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(mau.ten, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
                    const SizedBox(height: 2),
                    Text(mau.maHieu, style: Theme.of(context).textTheme.bodySmall),
                    if ((mau.tanSuat ?? '').isNotEmpty)
                      Padding(
                        padding: const EdgeInsets.only(top: 2),
                        child: Text('Tần suất: ${mau.tanSuat}', style: Theme.of(context).textTheme.bodySmall),
                      ),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right),
            ],
          ),
        ),
      ),
    );
  }

  static IconData _bieuTuong(String boCuc) => switch (boCuc) {
        'Checklist' => Icons.checklist,
        'NhieuDongTuDo' => Icons.table_rows,
        _ => Icons.event_note,
      };
}

class _Rong extends StatelessWidget {
  @override
  Widget build(BuildContext context) => ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(24, 80, 24, 24),
        children: [
          Icon(Icons.assignment_outlined, size: 56, color: Theme.of(context).hintColor),
          const SizedBox(height: 12),
          Text('Chưa có biểu mẫu nào cho bạn',
              textAlign: TextAlign.center, style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 4),
          Text('Quản trị cơ sở khai báo biểu mẫu trên web (mục Biểu mẫu kiểm soát) và gán quyền cho bạn.',
              textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodySmall),
        ],
      );
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
          Center(child: FilledButton(onPressed: thuLai, child: const Text('Thử lại'))),
        ],
      );
}
