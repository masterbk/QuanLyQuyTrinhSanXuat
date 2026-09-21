import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../loi/api.dart';
import '../../loi/gio_viet_nam.dart';
import '../lenh_san_xuat/mo_hinh.dart' show soGon;
import '../qr/man_qr.dart';
import '../xac_thuc/xac_thuc.dart';
import 'kho_du_lieu.dart';
import 'man_danh_sach.dart';
import 'man_sua.dart';
import 'man_xuat_kho.dart';
import 'mo_hinh.dart';

final _ngayVn = DateFormat('dd/MM/yyyy');
final _gioVn = DateFormat('dd/MM/yyyy HH:mm');

final _chiTietDonProvider = FutureProvider.autoDispose
    .family((ref, int id) => ref.watch(khoDonProvider).chiTiet(id));

/// Chi tiết đơn: hàng hoá kèm lô đã xuất, thông tin giao và các nút thao tác.
class ManChiTietDon extends ConsumerWidget {
  final int id;

  const ManChiTietDon({super.key, required this.id});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final chiTiet = ref.watch(_chiTietDonProvider(id));
    final coQuyenNhapLieu = ref.watch(xacThucProvider).nguoiDung?.coQuyenNhapLieu ?? false;

    return Scaffold(
      appBar: AppBar(
        title: Text(chiTiet.value?.maDonHang ?? 'Chi tiết đơn'),
        actions: [
          if (chiTiet.value != null)
            IconButton(
              tooltip: 'Mã QR',
              icon: const Icon(Icons.qr_code_2),
              onPressed: () => Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (_) => ManXemQr(
                    tieuDe: 'Mã QR đơn hàng ${chiTiet.value!.maDonHang}',
                    tenFile: 'QR-${chiTiet.value!.maDonHang}.png',
                    taiAnh: () => ref.read(khoDonProvider).qrPng(id),
                  ),
                ),
              ),
            ),
          if (coQuyenNhapLieu && chiTiet.value != null)
            _MenuHanhDong(don: chiTiet.value!, id: id),
        ],
      ),
      body: chiTiet.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Text(e is LoiApi ? e.thongBao : 'Không tải được đơn: $e', textAlign: TextAlign.center),
          ),
        ),
        data: (d) => RefreshIndicator(
          onRefresh: () async => ref.invalidate(_chiTietDonProvider(id)),
          child: ListView(
            padding: const EdgeInsets.fromLTRB(12, 12, 12, 24),
            children: [
              TheDonHang(don: d, trongChiTiet: true),
              const SizedBox(height: 12),
              if ((d.ghiChu ?? '').isNotEmpty) _Muc('Ghi chú', d.ghiChu!),
              if (d.daHuy && (d.lyDoHuy ?? '').isNotEmpty) _Muc('Lý do huỷ', d.lyDoHuy!),
              _Muc('Kho xuất', d.maKho),
              if (d.ngayGiao != null) _Muc('Hẹn giao', _ngayVn.format(d.ngayGiao!)),
              if (d.thoiGianGiaoUtc != null) _Muc('Đã giao lúc', _gioVn.format(gioVietNam(d.thoiGianGiaoUtc!))),
              const SizedBox(height: 12),
              Text('Hàng hoá (${d.dong.length})', style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 4),
              for (final dong in d.dong)
                Card(
                  margin: const EdgeInsets.only(top: 8),
                  child: Padding(
                    padding: const EdgeInsets.all(12),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('${dong.tenHienThi} ×${soGon(dong.soLuong)}',
                            style: Theme.of(context).textTheme.titleSmall),
                        for (final lo in dong.xuatLo)
                          Padding(
                            padding: const EdgeInsets.only(top: 4),
                            child: Text(
                              'Lô ${lo.maLo} · ${soGon(lo.soLuong)}'
                              '${lo.hanSuDung == null ? '' : ' · HSD ${_ngayVn.format(lo.hanSuDung!)}'}',
                              style: Theme.of(context).textTheme.bodySmall,
                            ),
                          ),
                      ],
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Menu "⋮" cho quản lý/nhập liệu: Sửa/Xuất kho luôn/Huỷ/Xoá - đúng điều kiện hiện của web.
class _MenuHanhDong extends ConsumerWidget {
  final DonHangBan don;
  final int id;

  const _MenuHanhDong({required this.don, required this.id});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (!don.choXacNhan && !don.daXacNhan && !don.choGiaoHang && !don.dangGiao) return const SizedBox.shrink();

    Future<void> lamMoi() async => ref.invalidate(_chiTietDonProvider(id));

    Future<void> sua() async {
      final tb = await Navigator.push<String>(
          context, MaterialPageRoute(builder: (_) => ManSuaDon(don: don)));
      if (tb == null || !context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
      await lamMoi();
    }

    Future<void> xuatKhoLuon() async {
      final tb = await Navigator.push<String>(
          context, MaterialPageRoute(builder: (_) => ManXuatKho(don: don)));
      if (tb == null || !context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
      await lamMoi();
    }

    Future<void> huy() async {
      final lyDo = await showDialog<String>(
        context: context,
        builder: (c) => _HopThoaiHuy(don: don),
      );
      if (lyDo == null || lyDo.trim().isEmpty || !context.mounted) return;
      try {
        final tb = await ref.read(khoDonProvider).huyDon(don.id, lyDo.trim());
        if (!context.mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
        await lamMoi();
      } on LoiApi catch (e) {
        if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.thongBao)));
      }
    }

    Future<void> xoa() async {
      final dongY = await showDialog<bool>(
        context: context,
        builder: (c) => AlertDialog(
          title: const Text('Xoá đơn hàng'),
          content: Text('Xoá đơn "${don.maDonHang}"? Không thể hoàn tác.'),
          actions: [
            TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Huỷ')),
            FilledButton(onPressed: () => Navigator.pop(c, true), child: const Text('Xoá')),
          ],
        ),
      );
      if (dongY != true || !context.mounted) return;
      try {
        final tb = await ref.read(khoDonProvider).xoaDon(don.id);
        if (!context.mounted) return;
        Navigator.pop(context, tb);
      } on LoiApi catch (e) {
        if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.thongBao)));
      }
    }

    return PopupMenuButton<String>(
      onSelected: (v) => switch (v) {
        'sua' => sua(),
        'xuat-kho' => xuatKhoLuon(),
        'huy' => huy(),
        'xoa' => xoa(),
        _ => null,
      },
      itemBuilder: (c) => [
        if (don.choXacNhan || don.daXacNhan) ...[
          const PopupMenuItem(value: 'sua', child: Text('Sửa đơn')),
          if (don.choXacNhan) const PopupMenuItem(value: 'xuat-kho', child: Text('Xuất kho luôn')),
        ],
        const PopupMenuItem(value: 'huy', child: Text('Huỷ đơn')),
        if ((don.choXacNhan || don.daXacNhan) && don.nguon == 'NoiBo')
          const PopupMenuItem(value: 'xoa', child: Text('Xoá đơn')),
      ],
    );
  }
}

class _HopThoaiHuy extends StatefulWidget {
  final DonHangBan don;

  const _HopThoaiHuy({required this.don});

  @override
  State<_HopThoaiHuy> createState() => _HopThoaiHuyState();
}

class _HopThoaiHuyState extends State<_HopThoaiHuy> {
  final _lyDo = TextEditingController();

  @override
  void dispose() {
    _lyDo.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: Text('Huỷ đơn ${widget.don.maDonHang}'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (widget.don.dangGiao || widget.don.choGiaoHang)
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: Text(
                  'Đơn đã xuất kho: huỷ sẽ trả hàng về đúng các lô đã xuất.',
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ),
            TextField(
              controller: _lyDo,
              maxLines: 2,
              decoration: const InputDecoration(labelText: 'Lý do huỷ *', border: OutlineInputBorder()),
              autofocus: true,
            ),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Đóng')),
          FilledButton(
            onPressed: () => Navigator.pop(context, _lyDo.text),
            child: const Text('Huỷ đơn'),
          ),
        ],
      );
}

class _Muc extends StatelessWidget {
  final String nhan;
  final String giaTri;

  const _Muc(this.nhan, this.giaTri);

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 3),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(width: 110, child: Text(nhan, style: Theme.of(context).textTheme.bodySmall)),
            Expanded(child: Text(giaTri, style: Theme.of(context).textTheme.bodyMedium)),
          ],
        ),
      );
}
