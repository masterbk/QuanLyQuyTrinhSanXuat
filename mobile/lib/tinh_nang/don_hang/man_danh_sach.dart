import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../loi/api.dart';
import '../xac_thuc/xac_thuc.dart';
import 'kho_du_lieu.dart';
import 'man_chi_tiet.dart';
import 'man_sua.dart';
import 'man_xuat_kho.dart';
import 'mo_hinh.dart';
import 'quet_don.dart';
import 'xac_nhan_giao.dart';

final _ngayVn = DateFormat('dd/MM/yyyy');

/// Danh sách đơn hàng cho nhân viên giao hàng: nhận đơn đã xuất kho và xác nhận đã giao.
class ManDanhSachDon extends ConsumerStatefulWidget {
  const ManDanhSachDon({super.key});

  @override
  ConsumerState<ManDanhSachDon> createState() => _ManDanhSachDonState();
}

class _ManDanhSachDonState extends ConsumerState<ManDanhSachDon> {
  final _cuon = ScrollController();

  @override
  void initState() {
    super.initState();
    _cuon.addListener(() {
      // Gần chạm đáy thì lấy thêm trang sau.
      if (_cuon.position.pixels >= _cuon.position.maxScrollExtent - 300) {
        ref.read(danhSachDonProvider.notifier).taiThem();
      }
    });
  }

  @override
  void dispose() {
    _cuon.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final ds = ref.watch(danhSachDonProvider);
    final loc = ref.watch(locDonProvider);
    final coQuyenNhapLieu = ref.watch(xacThucProvider).nguoiDung?.coQuyenNhapLieu ?? false;

    return Scaffold(
      body: Column(
        children: [
          Row(
            children: [
              Expanded(
                child: _BoLoc(
                  dangChon: loc,
                  coQuyenNhapLieu: coQuyenNhapLieu,
                  khiChon: (v) => ref.read(locDonProvider.notifier).dat(v),
                ),
              ),
              if (coQuyenNhapLieu)
                Padding(
                  padding: const EdgeInsets.only(right: 12),
                  child: IconButton.filledTonal(
                    tooltip: 'Tạo đơn hàng',
                    icon: const Icon(Icons.add),
                    onPressed: () async {
                      final tb = await Navigator.push<String>(
                          context, MaterialPageRoute(builder: (_) => const ManSuaDon()));
                      if (tb == null || !context.mounted) return;
                      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
                      ref.read(danhSachDonProvider.notifier).taiLai();
                    },
                  ),
                ),
            ],
          ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: () => ref.read(danhSachDonProvider.notifier).taiLai(),
              child: ds.when(
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (e, _) => _Loi(
                  thongBao: e is LoiApi ? e.thongBao : 'Không tải được danh sách: $e',
                  thuLai: () => ref.read(danhSachDonProvider.notifier).taiLai(),
                ),
                data: (ds) => ds.isEmpty
                    ? _Rong(loc: loc)
                    : ListView.separated(
                        controller: _cuon,
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.fromLTRB(12, 4, 12, 88),
                        itemCount: ds.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 8),
                        itemBuilder: (c, i) => TheDonHang(don: ds[i]),
                      ),
              ),
            ),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () async {
          await Navigator.push(context, MaterialPageRoute(builder: (_) => const ManQuetDonHang()));
          if (context.mounted) ref.read(danhSachDonProvider.notifier).taiLai();
        },
        icon: const Icon(Icons.qr_code_scanner),
        label: const Text('Quét QR đơn'),
      ),
    );
  }
}

/// Thẻ một đơn: thông tin giao + nút Nhận đơn / Xác nhận đã giao.
class TheDonHang extends ConsumerWidget {
  final DonHangBan don;

  /// true khi dùng trong màn chi tiết (bỏ phần bấm mở chi tiết).
  final bool trongChiTiet;

  const TheDonHang({super.key, required this.don, this.trongChiTiet = false});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final nguoiDung = ref.watch(xacThucProvider).nguoiDung;
    final maToi = nguoiDung?.maNhanSu;
    final cuaToi = don.cuaToi(maToi);
    final nhanDuoc = don.choGiaoHang;
    final giaoDuoc = don.dangGiao && cuaToi;
    // Xác nhận/Xuất kho: việc của quản lý/nhập liệu, không phải shipper.
    final coQuyenNhapLieu = nguoiDung?.coQuyenNhapLieu ?? false;
    final xacNhanDuoc = don.choXacNhan && coQuyenNhapLieu;
    final xuatKhoDuoc = don.daXacNhan && coQuyenNhapLieu;

    Future<void> chay(Future<String> Function() viec) async {
      try {
        final tb = await viec();
        if (!context.mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
        ref.read(danhSachDonProvider.notifier).taiLai();
      } on LoiApi catch (e) {
        if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.thongBao)));
      }
    }

    Future<void> xacNhanGiao() async {
      final tb = await Navigator.push<String>(
          context, MaterialPageRoute(builder: (_) => ManXacNhanGiao(don: don)));
      if (tb == null || !context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
      ref.read(danhSachDonProvider.notifier).taiLai();
    }

    Future<void> xuatKho() async {
      final tb = await Navigator.push<String>(
          context, MaterialPageRoute(builder: (_) => ManXuatKho(don: don)));
      if (tb == null || !context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
      ref.read(danhSachDonProvider.notifier).taiLai();
    }

    final noiDung = Padding(
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(don.maDonHang, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                    color: _mau(context, don).withValues(alpha: 0.15), borderRadius: BorderRadius.circular(20)),
                child: Text(don.trangThaiHienThi,
                    style: TextStyle(color: _mau(context, don), fontSize: 12, fontWeight: FontWeight.w600)),
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text(don.tenKhachHang ?? '', style: Theme.of(context).textTheme.bodyLarge),
          if ((don.diaChiGiao ?? '').isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(top: 2),
              child: Row(
                children: [
                  const Icon(Icons.place_outlined, size: 16),
                  const SizedBox(width: 4),
                  Expanded(child: Text(don.diaChiGiao!, style: Theme.of(context).textTheme.bodySmall)),
                ],
              ),
            ),
          const SizedBox(height: 6),
          Text(don.tomTat, style: Theme.of(context).textTheme.bodySmall),
          const SizedBox(height: 8),
          Wrap(spacing: 16, runSpacing: 4, children: [
            if (don.ngayGiao != null) _Dong(Icons.event_outlined, 'Hẹn ${_ngayVn.format(don.ngayGiao!)}'),
            if (!don.chuaCoNguoiGiao)
              _Dong(Icons.person_outline, cuaToi ? 'Bạn nhận' : 'Người giao: ${don.tenNguoiGiao ?? don.maNguoiGiao}'),
          ]),
          if (xacNhanDuoc || xuatKhoDuoc || nhanDuoc || giaoDuoc) ...[
            const SizedBox(height: 10),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                if (xacNhanDuoc)
                  FilledButton.icon(
                    onPressed: () => chay(() => ref.read(khoDonProvider).xacNhan(don.id)),
                    icon: const Icon(Icons.check_circle_outline, size: 18),
                    label: const Text('Xác nhận'),
                  ),
                if (xacNhanDuoc && (xuatKhoDuoc || nhanDuoc || giaoDuoc)) const SizedBox(width: 8),
                if (xuatKhoDuoc)
                  FilledButton.icon(
                    onPressed: xuatKho,
                    icon: const Icon(Icons.local_shipping_outlined, size: 18),
                    label: const Text('Xuất kho'),
                  ),
                if (xuatKhoDuoc && (nhanDuoc || giaoDuoc)) const SizedBox(width: 8),
                if (nhanDuoc)
                  FilledButton.tonalIcon(
                    onPressed: () => chay(() => ref.read(khoDonProvider).nhanDon(don.id)),
                    icon: const Icon(Icons.local_shipping_outlined, size: 18),
                    label: const Text('Nhận đơn'),
                  ),
                if (nhanDuoc && giaoDuoc) const SizedBox(width: 8),
                if (giaoDuoc)
                  FilledButton.icon(
                    onPressed: xacNhanGiao,
                    icon: const Icon(Icons.done_all, size: 18),
                    label: const Text('Đã giao'),
                  ),
              ],
            ),
          ],
        ],
      ),
    );

    return Card(
      margin: EdgeInsets.zero,
      child: trongChiTiet
          ? noiDung
          : InkWell(
              borderRadius: BorderRadius.circular(12),
              onTap: () async {
                final tb = await Navigator.push<String>(
                    context, MaterialPageRoute(builder: (_) => ManChiTietDon(id: don.id)));
                if (!context.mounted) return;
                if (tb != null) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
                ref.read(danhSachDonProvider.notifier).taiLai();
              },
              child: noiDung,
            ),
    );
  }

  static Color _mau(BuildContext c, DonHangBan d) => d.daGiao
      ? Colors.green
      : d.dangGiao
          ? Theme.of(c).colorScheme.primary
          : d.choGiaoHang
              ? Colors.orange
              : Theme.of(c).hintColor;
}

class _BoLoc extends StatelessWidget {
  final LocDon dangChon;
  final bool coQuyenNhapLieu;
  final ValueChanged<LocDon> khiChon;

  const _BoLoc({required this.dangChon, required this.coQuyenNhapLieu, required this.khiChon});

  static const _mucCoBan = <({LocDon ma, String ten})>[
    (ma: LocDon.canGiao, ten: 'Cần giao'),
    (ma: LocDon.cuaToi, ten: 'Của tôi'),
  ];
  static const _mucQuanLy = <({LocDon ma, String ten})>[
    (ma: LocDon.choXacNhan, ten: 'Chờ xác nhận'),
    (ma: LocDon.canXuatKho, ten: 'Cần xuất kho'),
  ];
  static const _mucTatCa = (ma: LocDon.tatCa, ten: 'Tất cả');

  @override
  Widget build(BuildContext context) {
    final muc = [..._mucCoBan, if (coQuyenNhapLieu) ..._mucQuanLy, _mucTatCa];
    return SizedBox(
      height: 50,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        itemCount: muc.length,
        separatorBuilder: (_, _) => const SizedBox(width: 8),
        itemBuilder: (c, i) => ChoiceChip(
          label: Text(muc[i].ten),
          selected: dangChon == muc[i].ma,
          onSelected: (_) => khiChon(muc[i].ma),
        ),
      ),
    );
  }
}

class _Dong extends StatelessWidget {
  final IconData bieuTuong;
  final String chu;

  const _Dong(this.bieuTuong, this.chu);

  @override
  Widget build(BuildContext context) => Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(bieuTuong, size: 16, color: Theme.of(context).hintColor),
          const SizedBox(width: 4),
          Text(chu, style: Theme.of(context).textTheme.bodySmall),
        ],
      );
}

class _Rong extends StatelessWidget {
  final LocDon loc;

  const _Rong({required this.loc});

  @override
  Widget build(BuildContext context) => ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(24, 80, 24, 24),
        children: [
          Icon(Icons.local_shipping_outlined, size: 56, color: Theme.of(context).hintColor),
          const SizedBox(height: 12),
          Text(
            switch (loc) {
              LocDon.cuaToi => 'Bạn chưa nhận đơn nào',
              LocDon.choXacNhan => 'Không có đơn chờ xác nhận',
              LocDon.canXuatKho => 'Không có đơn cần xuất kho',
              _ => 'Không có đơn cần giao',
            },
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: 4),
          Text(
            switch (loc) {
              LocDon.choXacNhan => 'Đơn mới đặt từ trường sẽ xuất hiện ở đây để xác nhận.',
              LocDon.canXuatKho => 'Đơn đã xác nhận, chờ chọn lô để xuất kho sẽ xuất hiện ở đây.',
              _ => 'Đơn xuất hiện ở đây sau khi bộ phận kho xuất hàng.',
            },
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodySmall,
          ),
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
