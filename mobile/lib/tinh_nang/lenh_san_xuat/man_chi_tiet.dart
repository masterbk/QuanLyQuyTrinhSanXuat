import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../loi/api.dart';
import '../../loi/gio_viet_nam.dart';
import '../qr/man_qr.dart';
import '../xac_thuc/xac_thuc.dart';
import 'hoan_thanh.dart';
import 'kho_du_lieu.dart';
import 'man_sua.dart';
import 'mo_hinh.dart';
import 'tham_gia.dart';

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
    final tb = await Navigator.push<String>(context, MaterialPageRoute(builder: (_) => ManHoanThanh(lenh: l)));
    if (tb != null && mounted) {
      _coThayDoi = true;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
      ref.invalidate(_chiTietProvider(widget.id));
    }
  }

  Future<void> _huy(LenhSanXuat l) async {
    // Chỉ cảnh báo HanoiCheck khi cơ sở đang bật đồng bộ (tắt thì lô không được gửi đi).
    final hanoiCheckBat = await ref.read(hanoiCheckBatProvider.future);
    if (!mounted) return;
    final lyDo = TextEditingController();
    final loDongBo =
        hanoiCheckBat ? l.sanPham.map((s) => s.maLoDaTao).whereType<String>().toList() : const <String>[];
    final dongY = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('Huỷ lệnh'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Hệ thống ghi bút toán đảo: trả lại nguyên liệu và thu hồi thành phẩm ở các lô '
                '${l.sanPham.map((s) => s.maLoThanhPham).join(', ')}.'),
            if (loDongBo.isNotEmpty) ...[
              const SizedBox(height: 8),
              Text('Lô ${loDongBo.join(', ')} đã tạo để đồng bộ HanoiCheck: nếu đã gửi đi thì phải xử lý '
                  'thủ công bên HanoiCheck.',
                  style: TextStyle(color: Theme.of(c).colorScheme.error)),
            ],
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
    ref.watch(hanoiCheckBatProvider); // tải sẵn công tắc HanoiCheck cho hộp thoại huỷ
    final chiTiet = ref.watch(_chiTietProvider(widget.id));
    final tenNguoi = {for (final n in ref.watch(nhanSuProvider).value ?? const <NhanSu>[]) n.maNhanSu: n.hoTen};

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) Navigator.pop(context, _coThayDoi);
      },
      child: Scaffold(
        appBar: AppBar(
          title: Text(chiTiet.value?.maLenh ?? 'Chi tiết lệnh'),
          actions: [
            if (chiTiet.value != null)
              IconButton(
                tooltip: 'Mã QR',
                icon: const Icon(Icons.qr_code_2),
                onPressed: () => Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (_) => ManXemQr(
                      tieuDe: 'Mã QR lệnh sản xuất ${chiTiet.value!.maLenh}',
                      tenFile: 'QR-${chiTiet.value!.maLenh}.png',
                      taiAnh: () => ref.read(khoLenhProvider).qrPng(widget.id),
                    ),
                  ),
                ),
              ),
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
              _Muc('Kho', l.maKho),
              _Muc('Ngày sản xuất', _ngayVn.format(l.ngaySanXuat)),
              if (l.ghiChu != null && l.ghiChu!.isNotEmpty) _Muc('Ghi chú', l.ghiChu!),
              if (l.thoiGianHoanThanhUtc != null)
                _Muc('Hoàn thành lúc', _gioVn.format(gioVietNam(l.thoiGianHoanThanhUtc!))),
              if (l.thoiGianHuyUtc != null) ...[
                _Muc('Huỷ lúc', _gioVn.format(gioVietNam(l.thoiGianHuyUtc!))),
                if (l.lyDoHuy != null) _Muc('Lý do huỷ', l.lyDoHuy!),
              ],
              const SizedBox(height: 12),
              Text('Sản phẩm (${l.sanPham.length})', style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 8),
              for (final s in l.sanPham) _TheSanPham(sanPham: s, tenNguoi: tenNguoi),
              if (l.thamGia.isNotEmpty) ...[
                const SizedBox(height: 8),
                Text('Nhân viên đã tham gia (${l.thamGia.length})', style: Theme.of(context).textTheme.titleSmall),
                const SizedBox(height: 4),
                for (final t in l.thamGia) _Muc(t.hoTen, 'từ ${_gioVn.format(gioVietNam(t.thoiGianUtc))}'),
              ],
              const SizedBox(height: 16),
              if (l.moiTao) ...[
                if (l.sanPham.any((s) => s.khau.isEmpty))
                  Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: Text('Có sản phẩm chưa khai khâu - bấm Sửa để khai trước khi hoàn thành.',
                        style: TextStyle(color: Theme.of(context).colorScheme.error)),
                  ),
                if (l.sanPham.any((s) => s.khau.any((k) => k.nguoiThucHien.isEmpty)))
                  Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: Text('Còn khâu chưa có người thực hiện: nhân viên sản xuất quét mã QR của lệnh để tham gia, '
                        'hoặc chọn người khi hoàn thành.',
                        style: TextStyle(color: Theme.of(context).colorScheme.secondary)),
                  ),
                if (ref.watch(xacThucProvider).nguoiDung?.coTheThamGiaLenh ?? false) ...[
                  FilledButton.tonalIcon(
                    onPressed: () async {
                      final xong = await Navigator.push<bool>(
                          context, MaterialPageRoute(builder: (_) => ManThamGia(lenh: l)));
                      if (xong == true && mounted) {
                        _coThayDoi = true;
                        ref.invalidate(_chiTietProvider(widget.id));
                      }
                    },
                    style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 14)),
                    icon: const Icon(Icons.group_add_outlined),
                    label: const Text('Chọn khâu tôi tham gia'),
                  ),
                  const SizedBox(height: 8),
                ],
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

class _TheSanPham extends StatelessWidget {
  final SanPhamLenh sanPham;
  final Map<String, String> tenNguoi;

  const _TheSanPham({required this.sanPham, required this.tenNguoi});

  @override
  Widget build(BuildContext context) {
    final s = sanPham;
    final khau = [...s.khau]..sort((a, b) => a.thuTu.compareTo(b.thuTu));
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('${s.tenHienThi} ×${soGon(s.soLuong)}',
                style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
            const SizedBox(height: 4),
            _Muc('Lô thành phẩm', s.maLoThanhPham),
            if (s.hanSuDung != null) _Muc('Hạn dùng', _ngayVn.format(s.hanSuDung!)),
            _Muc('Quy trình', s.tenQuyTrinh ?? (s.maQuyTrinh.isEmpty ? '—' : s.maQuyTrinh)),
            if (s.maLoDaTao != null) _Muc('Lô đồng bộ HnC', s.maLoDaTao!),
            if (khau.isNotEmpty) ...[
              const SizedBox(height: 6),
              Text('Khâu', style: Theme.of(context).textTheme.titleSmall),
              for (final k in khau)
                Padding(
                  padding: const EdgeInsets.only(top: 6),
                  child: Text.rich(TextSpan(children: [
                    TextSpan(text: '${k.thuTu}. ${k.tenHienThi}', style: const TextStyle(fontWeight: FontWeight.w600)),
                    TextSpan(
                      text: ' — ${k.tenCoSo ?? k.maCoSo}\n'
                          '${k.nguoiThucHien.map((m) => tenNguoi[m] ?? m).join(', ')}',
                      style: TextStyle(color: Theme.of(context).hintColor),
                    ),
                  ])),
                ),
            ],
            if (s.anh.isNotEmpty) ...[
              const SizedBox(height: 10),
              SizedBox(
                height: 96,
                child: ListView.separated(
                  scrollDirection: Axis.horizontal,
                  itemCount: s.anh.length,
                  separatorBuilder: (_, _) => const SizedBox(width: 8),
                  itemBuilder: (c, i) => ClipRRect(
                    borderRadius: BorderRadius.circular(8),
                    child: Image.network(
                      s.anh[i].duongDan,
                      width: 96, height: 96, fit: BoxFit.cover,
                      errorBuilder: (c, e, st) => Container(
                        width: 96, height: 96,
                        color: Theme.of(c).colorScheme.surfaceContainerHighest,
                        child: const Icon(Icons.broken_image_outlined),
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ],
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
        padding: const EdgeInsets.symmetric(vertical: 5),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(
              width: 130,
              child: Text(nhan, style: TextStyle(color: Theme.of(context).hintColor)),
            ),
            Expanded(child: Text(giaTri, style: const TextStyle(fontWeight: FontWeight.w500))),
          ],
        ),
      );
}
