import '../xac_thuc/xac_thuc.dart';
import 'tham_gia.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../loi/api.dart';
import 'kho_du_lieu.dart';
import 'man_chi_tiet.dart';
import 'man_sua.dart';
import 'mo_hinh.dart';

final _ngayVn = DateFormat('dd/MM/yyyy');

class ManDanhSachLenh extends ConsumerStatefulWidget {
  const ManDanhSachLenh({super.key});

  @override
  ConsumerState<ManDanhSachLenh> createState() => _ManDanhSachLenhState();
}

class _ManDanhSachLenhState extends ConsumerState<ManDanhSachLenh> {
  final _cuon = ScrollController();

  @override
  void initState() {
    super.initState();
    _cuon.addListener(() {
      // Gần chạm đáy thì lấy thêm trang sau.
      if (_cuon.position.pixels >= _cuon.position.maxScrollExtent - 300) {
        ref.read(danhSachLenhProvider.notifier).taiThem();
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
    final ds = ref.watch(danhSachLenhProvider);
    final loc = ref.watch(locTrangThaiProvider);
    final cuaToi = ref.watch(locCuaToiProvider);
    final thamGiaDuoc = ref.watch(xacThucProvider).nguoiDung?.coTheThamGiaLenh ?? false;

    return Scaffold(
      body: Column(
        children: [
          _BoLoc(
            dangChon: loc,
            khiChon: (v) => ref.read(locTrangThaiProvider.notifier).dat(v),
            cuaToi: thamGiaDuoc ? cuaToi : null,
            khiDoiCuaToi: (v) => ref.read(locCuaToiProvider.notifier).dat(v),
          ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: () => ref.read(danhSachLenhProvider.notifier).taiLai(),
              child: ds.when(
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (e, _) => _Loi(
                  thongBao: e is LoiApi ? e.thongBao : 'Không tải được danh sách: $e',
                  thuLai: () => ref.read(danhSachLenhProvider.notifier).taiLai(),
                ),
                data: (ds) => ds.isEmpty
                    ? _Rong(loc: loc)
                    : ListView.separated(
                        controller: _cuon,
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.fromLTRB(12, 4, 12, 88),
                        itemCount: ds.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 8),
                        itemBuilder: (c, i) => _TheLenh(lenh: ds[i]),
                      ),
              ),
            ),
          ),
        ],
      ),
      floatingActionButton: thamGiaDuoc
          ? Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                FloatingActionButton.small(
                    heroTag: 'tao-lenh', tooltip: 'Tạo lệnh', onPressed: _taoLenh, child: const Icon(Icons.add)),
                const SizedBox(height: 12),
                FloatingActionButton.extended(
                  heroTag: 'quet-ma-lenh',
                  onPressed: _quetMaLenh,
                  icon: const Icon(Icons.qr_code_scanner),
                  label: const Text('Quét mã lệnh'),
                ),
              ],
            )
          : FloatingActionButton.extended(
              onPressed: _taoLenh,
              icon: const Icon(Icons.add),
              label: const Text('Tạo lệnh'),
            ),
    );
  }

  Future<void> _taoLenh() async {
    final xong = await Navigator.push<bool>(context, MaterialPageRoute(builder: (_) => const ManSuaLenh()));
    if (xong == true && mounted) ref.read(danhSachLenhProvider.notifier).taiLai();
  }

  Future<void> _quetMaLenh() async {
    await Navigator.push(context, MaterialPageRoute(builder: (_) => const ManQuetMaLenh()));
    if (mounted) ref.read(danhSachLenhProvider.notifier).taiLai();
  }
}

class _BoLoc extends StatelessWidget {
  final String? dangChon;
  final ValueChanged<String?> khiChon;

  /// null = không hiện ô "Của tôi" (chỉ nhân viên sản xuất mới có).
  final bool? cuaToi;
  final ValueChanged<bool> khiDoiCuaToi;

  const _BoLoc({required this.dangChon, required this.khiChon, this.cuaToi, required this.khiDoiCuaToi});

  static const _muc = <({String? ma, String ten})>[
    (ma: null, ten: 'Tất cả'),
    (ma: 'MoiTao', ten: 'Mới tạo'),
    (ma: 'HoanThanh', ten: 'Hoàn thành'),
    (ma: 'DaHuy', ten: 'Đã huỷ'),
  ];

  @override
  Widget build(BuildContext context) => SizedBox(
        height: 50,
        child: ListView.separated(
          scrollDirection: Axis.horizontal,
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
          itemCount: _muc.length + (cuaToi == null ? 0 : 1),
          separatorBuilder: (_, _) => const SizedBox(width: 8),
          itemBuilder: (c, i) {
            if (cuaToi != null) {
              if (i == 0) {
                return FilterChip(label: const Text('Của tôi'), selected: cuaToi!, onSelected: khiDoiCuaToi);
              }
              i--;
            }
            return ChoiceChip(
              label: Text(_muc[i].ten),
              selected: dangChon == _muc[i].ma,
              onSelected: (_) => khiChon(_muc[i].ma),
            );
          },
        ),
      );
}

class _TheLenh extends ConsumerWidget {
  final LenhSanXuat lenh;

  const _TheLenh({required this.lenh});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final mau = _mauTrangThai(context, lenh);
    final nd = ref.watch(xacThucProvider).nguoiDung;
    final maToi = (nd?.maNhanSu ?? '').toLowerCase();
    final thamGiaDuoc = lenh.moiTao && (nd?.coTheThamGiaLenh ?? false);
    final daThamGia = maToi.isNotEmpty &&
        lenh.sanPham.any((s) => s.khau.any((k) => k.nguoiThucHien.any((m) => m.toLowerCase() == maToi)));

    Future<void> moThamGia() async {
      final xong = await Navigator.push<bool>(context, MaterialPageRoute(builder: (_) => ManThamGia(lenh: lenh)));
      if (xong == true && context.mounted) ref.read(danhSachLenhProvider.notifier).taiLai();
    }

    return Card(
      margin: EdgeInsets.zero,
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () async {
          final coDoi = await Navigator.push<bool>(
              context, MaterialPageRoute(builder: (_) => ManChiTietLenh(id: lenh.id)));
          if (coDoi == true) ref.read(danhSachLenhProvider.notifier).taiLai();
        },
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(lenh.maLenh,
                        style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                        color: mau.withValues(alpha: 0.15), borderRadius: BorderRadius.circular(20)),
                    child: Text(lenh.trangThaiHienThi,
                        style: TextStyle(color: mau, fontSize: 12, fontWeight: FontWeight.w600)),
                  ),
                ],
              ),
              const SizedBox(height: 6),
              Text(lenh.tomTat, style: Theme.of(context).textTheme.bodyLarge),
              const SizedBox(height: 8),
              Wrap(
                spacing: 16,
                runSpacing: 4,
                children: [
                  _Dong(Icons.inventory_2_outlined, '${lenh.sanPham.length} lô'),
                  _Dong(Icons.warehouse_outlined, lenh.maKho),
                  _Dong(Icons.event_outlined, _ngayVn.format(lenh.ngaySanXuat)),
                ],
              ),
              // Nhân viên sản xuất tham gia ngay từ danh sách (QR của lệnh chỉ là cách phụ).
              if (thamGiaDuoc) ...[
                const SizedBox(height: 10),
                Row(
                  children: [
                    if (daThamGia)
                      const Chip(
                        avatar: Icon(Icons.check_circle, size: 16),
                        label: Text('Đã tham gia'),
                        visualDensity: VisualDensity.compact,
                      ),
                    const Spacer(),
                    daThamGia
                        ? OutlinedButton.icon(
                            onPressed: moThamGia, icon: const Icon(Icons.tune, size: 18), label: const Text('Sửa khâu'))
                        : FilledButton.icon(
                            onPressed: moThamGia, icon: const Icon(Icons.group_add, size: 18), label: const Text('Tham gia')),
                  ],
                ),
              ],
            ],
          ),
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
          Icon(bieuTuong, size: 15, color: Theme.of(context).hintColor),
          const SizedBox(width: 4),
          Text(chu, style: Theme.of(context).textTheme.bodySmall),
        ],
      );
}

Color _mauTrangThai(BuildContext c, LenhSanXuat l) {
  if (l.hoanThanh) return Colors.green.shade700;
  if (l.daHuy) return Theme.of(c).hintColor;
  return Colors.blue.shade700;
}

class _Rong extends StatelessWidget {
  final String? loc;

  const _Rong({this.loc});

  @override
  Widget build(BuildContext context) => ListView(
        children: [
          const SizedBox(height: 120),
          Icon(Icons.inbox_outlined, size: 56, color: Theme.of(context).hintColor),
          const SizedBox(height: 12),
          Center(
            child: Text(loc == null ? 'Chưa có lệnh sản xuất nào' : 'Không có lệnh nào ở trạng thái này',
                style: Theme.of(context).textTheme.bodyMedium),
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
        children: [
          const SizedBox(height: 100),
          Icon(Icons.cloud_off, size: 56, color: Theme.of(context).colorScheme.error),
          const SizedBox(height: 12),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 32),
            child: Text(thongBao, textAlign: TextAlign.center),
          ),
          const SizedBox(height: 16),
          Center(child: FilledButton.tonal(onPressed: thuLai, child: const Text('Thử lại'))),
        ],
      );
}
