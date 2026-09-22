import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import '../kho_noi_bo/kho_du_lieu.dart' show nguyenLieuProvider;
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';

/// Một dòng định mức đang sửa trên form.
class _Dong {
  String? maNguyenLieu;
  final TextEditingController soLuong;
  final TextEditingController haoHut;

  _Dong({this.maNguyenLieu, String soLuong = '', String haoHut = '0'})
      : soLuong = TextEditingController(text: soLuong),
        haoHut = TextEditingController(text: haoHut);

  factory _Dong.tu(DinhMucDong d) => _Dong(
        maNguyenLieu: d.maNguyenLieu,
        soLuong: _boSo0(d.soLuong),
        haoHut: _boSo0(d.haoHutPhanTram),
      );

  double get soLuongSo => double.tryParse(soLuong.text.replaceAll(',', '.')) ?? 0;
  double get haoHutSo => double.tryParse(haoHut.text.replaceAll(',', '.')) ?? 0;

  void dispose() {
    soLuong.dispose();
    haoHut.dispose();
  }
}

String _boSo0(double v) {
  final s = v.toStringAsFixed(3);
  return s.contains('.') ? s.replaceAll(RegExp(r'0+$'), '').replaceAll(RegExp(r'\.$'), '') : s;
}

/// Định mức (công thức) nguyên liệu của một thành phẩm - mỗi lần lưu thay thế toàn bộ danh sách.
class ManDinhMuc extends ConsumerStatefulWidget {
  final SanPham sanPham;

  const ManDinhMuc({super.key, required this.sanPham});

  @override
  ConsumerState<ManDinhMuc> createState() => _ManDinhMucState();
}

class _ManDinhMucState extends ConsumerState<ManDinhMuc> {
  List<_Dong>? _dong;
  bool _dangLuu = false;
  late final Future<List<DinhMucDong>> _future =
      ref.read(danhMucQuanLyProvider).dinhMuc(widget.sanPham.id);

  @override
  void dispose() {
    for (final d in _dong ?? const <_Dong>[]) {
      d.dispose();
    }
    super.dispose();
  }

  Future<void> _luu() async {
    final dong = _dong!;
    final maDaChon = <String>{};
    for (final d in dong) {
      if (d.maNguyenLieu == null) {
        return _bao('Vui lòng chọn nguyên liệu cho mọi dòng, hoặc bỏ dòng trống.');
      }
      if (!maDaChon.add(d.maNguyenLieu!)) {
        return _bao('Nguyên liệu bị lặp trong định mức.');
      }
      if (d.soLuongSo <= 0) {
        return _bao('Định lượng mỗi nguyên liệu phải lớn hơn 0.');
      }
      if (d.haoHutSo < 0 || d.haoHutSo >= 100) {
        return _bao('Hao hụt (%) phải từ 0 đến dưới 100.');
      }
    }

    setState(() => _dangLuu = true);
    try {
      final tb = await ref.read(danhMucQuanLyProvider).luuDinhMuc(
            widget.sanPham.id,
            dong.map((d) => DinhMucDong(
                  maNguyenLieu: d.maNguyenLieu!,
                  soLuong: d.soLuongSo,
                  haoHutPhanTram: d.haoHutSo,
                )).toList(),
          );
      if (!mounted) return;
      Navigator.pop(context, tb);
    } on LoiApi catch (e) {
      _bao(e.thongBao);
    } finally {
      if (mounted) setState(() => _dangLuu = false);
    }
  }

  void _bao(String s) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(s)));

  @override
  Widget build(BuildContext context) {
    final nguyenLieu = ref.watch(nguyenLieuProvider);

    return Scaffold(
      appBar: AppBar(title: Text('Định mức — ${widget.sanPham.tenSanPham}')),
      body: FutureBuilder<List<DinhMucDong>>(
        future: _future,
        builder: (context, snap) {
          if (snap.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snap.hasError) {
            final e = snap.error;
            return Center(child: Text(e is LoiApi ? e.thongBao : 'Lỗi tải định mức: $e'));
          }
          _dong ??= (snap.data ?? const []).map(_Dong.tu).toList();
          final dong = _dong!;

          return nguyenLieu.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (e, _) => Center(
                child: Text(e is LoiApi ? e.thongBao : 'Lỗi tải danh mục nguyên liệu: $e')),
            data: (dsNguyenLieu) => ListView(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
              children: [
                Text(
                  'Lượng nguyên liệu cần cho 1 đơn vị thành phẩm'
                  '${widget.sanPham.donViTinh?.isNotEmpty ?? false ? ' (${widget.sanPham.donViTinh})' : ''}.',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
                const SizedBox(height: 12),
                if (dsNguyenLieu.isEmpty)
                  const Text('Chưa có nguyên liệu nào - khai ở màn Thực phẩm/SKU trước.')
                else ...[
                  for (var i = 0; i < dong.length; i++) _theDong(context, i, dong[i], dsNguyenLieu),
                  const SizedBox(height: 8),
                  OutlinedButton.icon(
                    onPressed: () => setState(() => dong.add(_Dong())),
                    icon: const Icon(Icons.add),
                    label: const Text('Thêm nguyên liệu'),
                  ),
                ],
              ],
            ),
          );
        },
      ),
      bottomNavigationBar: _dong == null
          ? null
          : Padding(
              padding: EdgeInsets.fromLTRB(16, 8, 16, MediaQuery.of(context).padding.bottom + 12),
              child: FilledButton(
                onPressed: _dangLuu ? null : _luu,
                style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 16)),
                child: _dangLuu
                    ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Text('Lưu định mức'),
              ),
            ),
    );
  }

  Widget _theDong(BuildContext context, int i, _Dong d, List<dynamic> dsNguyenLieu) => Card(
        key: ObjectKey(d),
        margin: const EdgeInsets.only(bottom: 12),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(12, 4, 12, 12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Expanded(child: Text('Dòng ${i + 1}', style: const TextStyle(fontWeight: FontWeight.bold))),
                  IconButton(
                    tooltip: 'Bỏ dòng',
                    icon: Icon(Icons.delete_outline, color: Theme.of(context).colorScheme.error),
                    onPressed: () => setState(() => _dong!.removeAt(i).dispose()),
                  ),
                ],
              ),
              DropdownButtonFormField<String>(
                initialValue: dsNguyenLieu.any((n) => n.maSanPham == d.maNguyenLieu) ? d.maNguyenLieu : null,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'Nguyên liệu *', border: OutlineInputBorder()),
                items: dsNguyenLieu
                    .map<DropdownMenuItem<String>>(
                        (n) => DropdownMenuItem(value: n.maSanPham as String, child: Text(n.tenSanPham as String)))
                    .toList(),
                onChanged: (v) => setState(() => d.maNguyenLieu = v),
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: TextFormField(
                      controller: d.soLuong,
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      decoration: const InputDecoration(labelText: 'Định lượng *', border: OutlineInputBorder()),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: TextFormField(
                      controller: d.haoHut,
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      decoration: const InputDecoration(labelText: 'Hao hụt (%)', border: OutlineInputBorder()),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      );
}
