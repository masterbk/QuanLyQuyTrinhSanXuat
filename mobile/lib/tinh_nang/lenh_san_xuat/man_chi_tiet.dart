import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../loi/api.dart';
import 'hoan_thanh.dart';
import 'kho_du_lieu.dart';
import 'man_sua.dart';
import 'mo_hinh.dart';

final _ngayVn = DateFormat('dd/MM/yyyy');
final _gioVn = DateFormat('dd/MM/yyyy HH:mm');

final _chiTietProvider =
    FutureProvider.autoDispose.family<LenhSanXuat, int>((ref, id) => ref.watch(khoLenhProvider).chiTiet(id));

class ManChiTietLenh extends ConsumerStatefulWidget {
  final int id;

  const ManChiTietLenh({super.key, required this.id});

  @override
  ConsumerState<ManChiTietLenh> createState() => _ManChiTietLenhState();
}

class _ManChiTietLenhState extends ConsumerState<ManChiTietLenh> {
  bool _coThayDoi = false;

  Future<void> _hoanThanh(LenhSanXuat l) async {
    final tb = await showDialog<String>(
      context: context,
      barrierDismissible: false,
      builder: (_) => HopThoaiHoanThanh(lenh: l),
    );
    if (tb != null && mounted) {
      _coThayDoi = true;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
      ref.invalidate(_chiTietProvider(widget.id));
    }
  }

  Future<void> _huy(LenhSanXuat l) async {
    final lyDo = TextEditingController();
    final dongY = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('Huỷ lệnh'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Hệ thống ghi bút toán đảo: trả lại nguyên liệu và thu hồi '
                '${soGon(l.soLuong)} thành phẩm ở lô ${l.maLoThanhPham}.'),
            const SizedBox(height: 12),
            TextField(
              controller: lyDo,
              autofocus: true,
              maxLines: 2,
              decoration: const InputDecoration(
                labelText: 'Lý do huỷ *',
                hintText: 'vd hỏng mẻ bánh',
                border: OutlineInputBorder(),
              ),
            ),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Đóng')),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: Theme.of(c).colorScheme.error),
            onPressed: () => Navigator.pop(c, true),
            child: const Text('Huỷ lệnh'),
          ),
        ],
      ),
    );
    if (dongY != true) return;
    if (lyDo.text.trim().isEmpty) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Vui lòng nhập lý do huỷ.')));
      }
      return;
    }
    await _chay(() => ref.read(khoLenhProvider).huy(l.id, lyDo.text.trim()));
  }

  Future<void> _xoa(LenhSanXuat l) async {
    final dongY = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('Xoá lệnh'),
        content: Text('Xoá lệnh ${l.maLenh}?'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Huỷ')),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: Theme.of(c).colorScheme.error),
            onPressed: () => Navigator.pop(c, true),
            child: const Text('Xoá'),
          ),
        ],
      ),
    );
    if (dongY != true) return;
    if (await _chay(() => ref.read(khoLenhProvider).xoa(l.id)) && mounted) {
      Navigator.pop(context, true);
    }
  }

  Future<bool> _chay(Future<String> Function() ham) async {
    try {
      final tb = await ham();
      if (!mounted) return true;
      _coThayDoi = true;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
      ref.invalidate(_chiTietProvider(widget.id));
      return true;
    } on LoiApi catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(e.thongBao), backgroundColor: Theme.of(context).colorScheme.error));
      }
      return false;
    }
  }

  @override
  Widget build(BuildContext context) {
    final chiTiet = ref.watch(_chiTietProvider(widget.id));

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) Navigator.pop(context, _coThayDoi);
      },
      child: Scaffold(
        appBar: AppBar(
          title: Text(chiTiet.value?.maLenh ?? 'Chi tiết lệnh'),
          actions: [
            if (chiTiet.value?.moiTao == true)
              IconButton(
                tooltip: 'Sửa',
                icon: const Icon(Icons.edit_outlined),
                onPressed: () async {
                  final xong = await Navigator.push<bool>(context,
                      MaterialPageRoute(builder: (_) => ManSuaLenh(lenh: chiTiet.value!)));
                  if (xong == true) {
                    _coThayDoi = true;
                    ref.invalidate(_chiTietProvider(widget.id));
                  }
                },
              ),
          ],
        ),
        body: chiTiet.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(e is LoiApi ? e.thongBao : '$e', textAlign: TextAlign.center),
                  const SizedBox(height: 12),
                  FilledButton.tonal(
                      onPressed: () => ref.invalidate(_chiTietProvider(widget.id)),
                      child: const Text('Thử lại')),
                ],
              ),
            ),
          ),
          data: (l) => ListView(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
            children: [
              _TheTrangThai(lenh: l),
              const SizedBox(height: 16),
              _Muc('Thành phẩm', l.tenHienThi),
              _Muc('Số lượng', soGon(l.soLuong)),
              _Muc('Kho', l.maKho),
              _Muc('Lô thành phẩm', l.maLoThanhPham),
              _Muc('Ngày sản xuất', _ngayVn.format(l.ngaySanXuat)),
              if (l.hanSuDungThanhPham != null)
                _Muc('Hạn dùng', _ngayVn.format(l.hanSuDungThanhPham!)),
              if (l.ghiChu != null && l.ghiChu!.isNotEmpty) _Muc('Ghi chú', l.ghiChu!),
              if (l.maLoDaTao != null) _Muc('Lô đồng bộ HanoiCheck', l.maLoDaTao!),
              if (l.thoiGianHoanThanhUtc != null)
                _Muc('Hoàn thành lúc', _gioVn.format(l.thoiGianHoanThanhUtc!.toLocal())),
              if (l.thoiGianHuyUtc != null) ...[
                _Muc('Huỷ lúc', _gioVn.format(l.thoiGianHuyUtc!.toLocal())),
                if (l.lyDoHuy != null) _Muc('Lý do huỷ', l.lyDoHuy!),
              ],
              if (l.anh.isNotEmpty) ...[
                const SizedBox(height: 16),
                Text('Ảnh lô sản phẩm', style: Theme.of(context).textTheme.titleSmall),
                const SizedBox(height: 8),
                SizedBox(
                  height: 110,
                  child: ListView.separated(
                    scrollDirection: Axis.horizontal,
                    itemCount: l.anh.length,
                    separatorBuilder: (_, _) => const SizedBox(width: 8),
                    itemBuilder: (c, i) => ClipRRect(
                      borderRadius: BorderRadius.circular(8),
                      child: Image.network(
                        l.anh[i].duongDan,
                        width: 110, height: 110, fit: BoxFit.cover,
                        errorBuilder: (c, e, s) => Container(
                          width: 110, height: 110,
                          color: Theme.of(c).colorScheme.surfaceContainerHighest,
                          child: const Icon(Icons.broken_image_outlined),
                        ),
                      ),
                    ),
                  ),
                ),
              ],
              const SizedBox(height: 24),
              if (l.moiTao) ...[
                FilledButton.icon(
                  onPressed: () => _hoanThanh(l),
                  style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 16)),
                  icon: const Icon(Icons.check_circle_outline),
                  label: const Text('Hoàn thành (kèm ảnh)'),
                ),
                const SizedBox(height: 8),
                OutlinedButton.icon(
                  onPressed: () => _xoa(l),
                  style: OutlinedButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
                  icon: const Icon(Icons.delete_outline),
                  label: const Text('Xoá lệnh'),
                ),
              ],
              if (l.hoanThanh)
                OutlinedButton.icon(
                  onPressed: () => _huy(l),
                  style: OutlinedButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
                  icon: const Icon(Icons.undo),
                  label: const Text('Huỷ lệnh (trả lại kho)'),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _TheTrangThai extends StatelessWidget {
  final LenhSanXuat lenh;

  const _TheTrangThai({required this.lenh});

  @override
  Widget build(BuildContext context) {
    final mau = lenh.hoanThanh
        ? Colors.green.shade700
        : lenh.daHuy
            ? Theme.of(context).hintColor
            : Colors.blue.shade700;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
          color: mau.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(12)),
      child: Row(
        children: [
          Icon(lenh.hoanThanh ? Icons.check_circle : (lenh.daHuy ? Icons.cancel : Icons.pending_outlined),
              color: mau),
          const SizedBox(width: 10),
          Text(lenh.trangThaiHienThi,
              style: TextStyle(color: mau, fontWeight: FontWeight.bold, fontSize: 16)),
        ],
      ),
    );
  }
}

class _Muc extends StatelessWidget {
  final String nhan;
  final String giaTri;

  const _Muc(this.nhan, this.giaTri);

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 7),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(
              width: 140,
              child: Text(nhan, style: TextStyle(color: Theme.of(context).hintColor)),
            ),
            Expanded(child: Text(giaTri, style: const TextStyle(fontWeight: FontWeight.w500))),
          ],
        ),
      );
}
