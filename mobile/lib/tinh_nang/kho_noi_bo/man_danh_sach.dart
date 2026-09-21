import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../loi/api.dart';
import '../../loi/gio_viet_nam.dart';
import '../lenh_san_xuat/mo_hinh.dart' show soGon;
import '../xac_thuc/xac_thuc.dart';
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';

final _ngayVn = DateFormat('dd/MM/yyyy');
final _gioVn = DateFormat('dd/MM/yyyy HH:mm');

/// Kho nội bộ: xem tồn theo lô + kiểm kê/điều chỉnh (mọi người có quyền sản xuất), nhập nguyên liệu (chỉ
/// quản trị/nhân viên nhập liệu). Nghiệp vụ vận hành nội bộ, KHÔNG đồng bộ HanoiCheck.
class ManKhoNoiBo extends ConsumerWidget {
  const ManKhoNoiBo({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final coQuyenNhapLieu = ref.watch(xacThucProvider).nguoiDung?.coQuyenNhapLieu ?? false;
    final soTab = coQuyenNhapLieu ? 2 : 1;

    return DefaultTabController(
      length: soTab,
      child: Scaffold(
        body: Column(
          children: [
            Material(
              color: Theme.of(context).colorScheme.surface,
              child: TabBar(
                tabs: [
                  const Tab(text: 'Tồn kho'),
                  if (coQuyenNhapLieu) const Tab(text: 'Nhập nguyên liệu'),
                ],
              ),
            ),
            Expanded(
              child: TabBarView(
                children: [
                  const _TabTonKho(),
                  if (coQuyenNhapLieu) const _TabNhap(),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TabTonKho extends ConsumerWidget {
  const _TabTonKho();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final ton = ref.watch(tonKhoProvider);
    final coQuyenNhapLieu = ref.watch(xacThucProvider).nguoiDung?.coQuyenNhapLieu ?? false;

    return RefreshIndicator(
      onRefresh: () async => ref.invalidate(tonKhoProvider),
      child: ton.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => _Loi(
          thongBao: e is LoiApi ? e.thongBao : 'Không tải được tồn kho: $e',
          thuLai: () => ref.invalidate(tonKhoProvider),
        ),
        data: (ds) => ds.isEmpty
            ? ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.fromLTRB(24, 80, 24, 24),
                children: const [
                  Icon(Icons.inventory_2_outlined, size: 56, color: Colors.grey),
                  SizedBox(height: 12),
                  Text('Chưa có tồn kho.', textAlign: TextAlign.center),
                ],
              )
            : ListView.separated(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.fromLTRB(12, 12, 12, 24),
                itemCount: ds.length,
                separatorBuilder: (_, _) => const SizedBox(height: 8),
                itemBuilder: (c, i) => _TheTonKho(ton: ds[i], coQuyenNhapLieu: coQuyenNhapLieu),
              ),
      ),
    );
  }
}

class _TheTonKho extends StatelessWidget {
  final TonKho ton;
  final bool coQuyenNhapLieu;

  const _TheTonKho({required this.ton, required this.coQuyenNhapLieu});

  /// "Đã hết hạn" / "Sắp hết hạn" (trong 30 ngày) / null nếu còn hạn lâu.
  static String? _canhBao(DateTime hsd) {
    final homNay = bayGioVietNam();
    final ngayHan = DateTime(hsd.year, hsd.month, hsd.day);
    final ngayNay = DateTime(homNay.year, homNay.month, homNay.day);
    if (ngayHan.isBefore(ngayNay)) return 'Đã hết hạn';
    if (!ngayHan.isAfter(ngayNay.add(const Duration(days: 30)))) return 'Sắp hết hạn';
    return null;
  }

  @override
  Widget build(BuildContext context) {
    final canhBao = ton.hanSuDung == null ? null : _canhBao(ton.hanSuDung!);
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(ton.tenSanPham,
                            style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                        decoration: BoxDecoration(
                          color: (ton.laNguyenLieu ? Colors.orange : Theme.of(context).colorScheme.primary)
                              .withValues(alpha: 0.15),
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Text(ton.laNguyenLieu ? 'Nguyên liệu' : 'Thành phẩm',
                            style: TextStyle(
                                fontSize: 11,
                                color: ton.laNguyenLieu ? Colors.orange.shade800 : Theme.of(context).colorScheme.primary)),
                      ),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text('Kho ${ton.tenKho} · Lô ${ton.maLo}', style: Theme.of(context).textTheme.bodySmall),
                  const SizedBox(height: 6),
                  Row(
                    children: [
                      Text('${soGon(ton.soLuongTon)} ${ton.donViTinh ?? ''}',
                          style: Theme.of(context).textTheme.titleMedium),
                      if (ton.hanSuDung != null) ...[
                        const SizedBox(width: 8),
                        Text('HSD ${_ngayVn.format(ton.hanSuDung!)}', style: Theme.of(context).textTheme.bodySmall),
                      ],
                      if (canhBao != null) ...[
                        const SizedBox(width: 6),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 1),
                          decoration: BoxDecoration(
                              color: Colors.red.withValues(alpha: 0.15), borderRadius: BorderRadius.circular(20)),
                          child: Text(canhBao, style: TextStyle(fontSize: 11, color: Colors.red.shade800)),
                        ),
                      ],
                    ],
                  ),
                ],
              ),
            ),
            if (coQuyenNhapLieu)
              IconButton(
                tooltip: 'Kiểm kê / Điều chỉnh tồn',
                icon: const Icon(Icons.fact_check_outlined),
                onPressed: () => showDialog<void>(context: context, builder: (_) => _HopDieuChinh(ton: ton)),
              ),
          ],
        ),
      ),
    );
  }
}

class _HopDieuChinh extends ConsumerStatefulWidget {
  final TonKho ton;

  const _HopDieuChinh({required this.ton});

  @override
  ConsumerState<_HopDieuChinh> createState() => _HopDieuChinhState();
}

class _HopDieuChinhState extends ConsumerState<_HopDieuChinh> {
  final _soThucTe = TextEditingController();
  final _lyDo = TextEditingController();
  bool _dangLuu = false;

  @override
  void initState() {
    super.initState();
    _soThucTe.text = soGon(widget.ton.soLuongTon);
  }

  @override
  void dispose() {
    _soThucTe.dispose();
    _lyDo.dispose();
    super.dispose();
  }

  double get _thucTe => double.tryParse(_soThucTe.text.replaceAll(',', '.')) ?? widget.ton.soLuongTon;
  double get _chenh => _thucTe - widget.ton.soLuongTon;

  Future<void> _luu() async {
    setState(() => _dangLuu = true);
    try {
      final tb = await ref.read(khoNoiBoProvider).dieuChinh(
            maSanPham: widget.ton.maSanPham,
            maKho: widget.ton.maKho,
            maLo: widget.ton.maLo,
            soLuongThucTe: _thucTe,
            lyDo: _lyDo.text.trim().isEmpty ? null : _lyDo.text.trim(),
          );
      if (!mounted) return;
      ref.invalidate(tonKhoProvider);
      Navigator.pop(context);
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
    } on LoiApi catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.thongBao)));
    } finally {
      if (mounted) setState(() => _dangLuu = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final chenh = _chenh;
    final dvt = widget.ton.donViTinh ?? '';
    return AlertDialog(
      title: const Text('Kiểm kê / Điều chỉnh tồn'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('${widget.ton.tenSanPham} — Kho ${widget.ton.tenKho} — Lô ${widget.ton.maLo}',
                style: const TextStyle(fontWeight: FontWeight.bold)),
            const SizedBox(height: 4),
            Text('Tồn theo sổ hiện tại: ${soGon(widget.ton.soLuongTon)} $dvt',
                style: Theme.of(context).textTheme.bodySmall),
            const SizedBox(height: 12),
            TextField(
              controller: _soThucTe,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: const InputDecoration(
                  labelText: 'Số tồn thực tế đếm được', border: OutlineInputBorder(), isDense: true),
              onChanged: (_) => setState(() {}),
            ),
            const SizedBox(height: 8),
            Text(
              'Chênh lệch: ${chenh > 0 ? '+' : ''}${soGon(chenh)} $dvt'
              '${chenh == 0 ? '' : (chenh > 0 ? ' (thừa/tăng)' : ' (hao hụt/giảm)')}',
              style: TextStyle(
                  fontWeight: FontWeight.bold, color: chenh == 0 ? null : (chenh > 0 ? Colors.green[700] : Colors.red[700])),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _lyDo,
              decoration: const InputDecoration(
                  labelText: 'Lý do', helperText: 'Vd: hao hụt sản xuất, vỡ hỏng, đếm lại.', border: OutlineInputBorder()),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: _dangLuu ? null : () => Navigator.pop(context), child: const Text('Huỷ')),
        FilledButton(onPressed: _dangLuu ? null : _luu, child: const Text('Lưu điều chỉnh')),
      ],
    );
  }
}

class _TabNhap extends ConsumerStatefulWidget {
  const _TabNhap();

  @override
  ConsumerState<_TabNhap> createState() => _TabNhapState();
}

class _TabNhapState extends ConsumerState<_TabNhap> {
  final _maLo = TextEditingController();
  final _soLuong = TextEditingController();
  final _ghiChu = TextEditingController();
  String? _maSanPham;
  String? _maKho;
  String? _maNcc;
  DateTime? _hanSuDung;
  bool _dangLuu = false;

  @override
  void dispose() {
    _maLo.dispose();
    _soLuong.dispose();
    _ghiChu.dispose();
    super.dispose();
  }

  Future<void> _nhap() async {
    if (_maSanPham == null || _maKho == null || _maLo.text.trim().isEmpty) {
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('Vui lòng chọn nguyên liệu, kho và nhập mã lô.')));
      return;
    }
    final soLuong = double.tryParse(_soLuong.text.replaceAll(',', '.')) ?? 0;
    if (soLuong <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Số lượng nhập phải lớn hơn 0.')));
      return;
    }

    setState(() => _dangLuu = true);
    try {
      final tb = await ref.read(khoNoiBoProvider).nhap(
            maSanPham: _maSanPham!,
            maKho: _maKho!,
            maLo: _maLo.text.trim(),
            soLuong: soLuong,
            hanSuDung: _hanSuDung,
            maNccDauVao: _maNcc,
            ghiChu: _ghiChu.text.trim().isEmpty ? null : _ghiChu.text.trim(),
          );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
      setState(() {
        _maLo.clear();
        _soLuong.clear();
        _ghiChu.clear();
        _hanSuDung = null;
      });
      ref.invalidate(tonKhoProvider);
      ref.invalidate(lichSuKhoProvider);
    } on LoiApi catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.thongBao)));
    } finally {
      if (mounted) setState(() => _dangLuu = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final nguyenLieu = ref.watch(nguyenLieuProvider);
    final kho = ref.watch(khoDanhMucProvider);
    final ncc = ref.watch(nccDauVaoProvider);
    final lichSu = ref.watch(lichSuKhoProvider);

    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
      children: [
        nguyenLieu.when(
          loading: () => const LinearProgressIndicator(),
          error: (e, _) => Text(e is LoiApi ? e.thongBao : 'Lỗi tải nguyên liệu: $e'),
          data: (ds) => ds.isEmpty
              ? const Text('Chưa có nguyên liệu nào - khai ở màn Thực phẩm/SKU trước.')
              : DropdownButtonFormField<String>(
                  initialValue: _maSanPham,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Nguyên liệu', border: OutlineInputBorder()),
                  items: ds.map((n) => DropdownMenuItem(value: n.maSanPham, child: Text(n.tenSanPham))).toList(),
                  onChanged: (v) => setState(() => _maSanPham = v),
                ),
        ),
        const SizedBox(height: 12),
        kho.when(
          loading: () => const LinearProgressIndicator(),
          error: (e, _) => Text(e is LoiApi ? e.thongBao : 'Lỗi tải kho: $e'),
          data: (ds) => DropdownButtonFormField<String>(
            initialValue: _maKho,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'Kho nhập', border: OutlineInputBorder()),
            items: ds.map((k) => DropdownMenuItem(value: k.maKho, child: Text(k.tenKho))).toList(),
            onChanged: (v) => setState(() => _maKho = v),
          ),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _maLo,
          decoration: const InputDecoration(labelText: 'Mã lô', border: OutlineInputBorder()),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _soLuong,
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          decoration: const InputDecoration(labelText: 'Số lượng', border: OutlineInputBorder()),
        ),
        const SizedBox(height: 12),
        InkWell(
          onTap: () async {
            final now = DateTime.now();
            final chon = await showDatePicker(
              context: context,
              initialDate: _hanSuDung ?? now,
              firstDate: DateTime(now.year - 1),
              lastDate: DateTime(now.year + 5),
            );
            if (chon != null) setState(() => _hanSuDung = chon);
          },
          child: InputDecorator(
            decoration: const InputDecoration(labelText: 'Hạn sử dụng', border: OutlineInputBorder()),
            child: Text(_hanSuDung == null ? 'Không bắt buộc' : _ngayVn.format(_hanSuDung!)),
          ),
        ),
        const SizedBox(height: 12),
        ncc.when(
          loading: () => const LinearProgressIndicator(),
          error: (e, _) => const SizedBox.shrink(),
          data: (ds) => DropdownButtonFormField<String>(
            initialValue: _maNcc,
            isExpanded: true,
            decoration: const InputDecoration(
                labelText: 'Nhà cung ứng', helperText: 'Không bắt buộc.', border: OutlineInputBorder()),
            items: [
              const DropdownMenuItem(value: null, child: Text('(không chọn)')),
              ...ds.map((n) => DropdownMenuItem(value: n.maNccDauVao, child: Text(n.ten))),
            ],
            onChanged: (v) => setState(() => _maNcc = v),
          ),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _ghiChu,
          decoration: const InputDecoration(labelText: 'Ghi chú', border: OutlineInputBorder()),
        ),
        const SizedBox(height: 16),
        FilledButton.icon(
          onPressed: _dangLuu ? null : _nhap,
          style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 14)),
          icon: const Icon(Icons.add_box_outlined),
          label: const Text('Nhập kho'),
        ),
        const SizedBox(height: 20),
        const Divider(),
        Text('Nhập gần đây', style: Theme.of(context).textTheme.titleSmall),
        const SizedBox(height: 8),
        lichSu.when(
          loading: () => const Center(child: Padding(padding: EdgeInsets.all(16), child: CircularProgressIndicator())),
          error: (e, _) => Text(e is LoiApi ? e.thongBao : 'Lỗi tải lịch sử: $e'),
          data: (ds) => ds.isEmpty
              ? const Text('Chưa có giao dịch.')
              : Column(
                  children: [
                    for (final g in ds)
                      ListTile(
                        dense: true,
                        contentPadding: EdgeInsets.zero,
                        title: Text('${g.tenHienThi} × ${soGon(g.soLuong)} (Lô ${g.maLo})'),
                        subtitle: Text('${g.tenLoai} · ${_gioVn.format(gioVietNam(g.thoiGianUtc))}'),
                      ),
                  ],
                ),
        ),
      ],
    );
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
