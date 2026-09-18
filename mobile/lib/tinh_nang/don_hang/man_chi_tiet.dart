import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../loi/api.dart';
import '../../loi/gio_viet_nam.dart';
import '../lenh_san_xuat/mo_hinh.dart' show soGon;
import 'kho_du_lieu.dart';
import 'man_danh_sach.dart';

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

    return Scaffold(
      appBar: AppBar(title: Text(chiTiet.value?.maDonHang ?? 'Chi tiết đơn')),
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
