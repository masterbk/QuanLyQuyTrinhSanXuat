import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';

/// Tạo lệnh nhập nguyên liệu từ các đơn đã chọn: gộp nguyên liệu (món ăn + thực phẩm) kèm tồn, gợi ý nhập phần
/// thiếu; mỗi nguyên liệu tạo một lô rồi nhập kho.
class ManNhapNguyenLieuDon extends ConsumerStatefulWidget {
  final List<int> donIds;
  const ManNhapNguyenLieuDon({super.key, required this.donIds});

  @override
  ConsumerState<ManNhapNguyenLieuDon> createState() => _ManNhapNguyenLieuDonState();
}

class _Dong {
  final NhuCauNguyenLieu nc;
  double nhap;
  _Dong(this.nc) : nhap = nc.goiYNhap;
}

class _ManNhapNguyenLieuDonState extends ConsumerState<ManNhapNguyenLieuDon> {
  bool _dangTai = true;
  bool _dangLuu = false;
  String? _loi;
  List<_Dong> _dong = [];
  List<MucChonDm> _kho = [];
  List<MucChonDm> _ncc = [];
  String? _maKho;
  String? _maNcc;
  DateTime _ngayNhap = DateTime.now();
  DateTime? _hanDung;

  @override
  void initState() {
    super.initState();
    _tai();
  }

  Future<void> _tai() async {
    setState(() { _dangTai = true; _loi = null; });
    try {
      final kho = ref.read(khoDonProvider);
      final nc = await kho.nhuCauNguyenLieu(widget.donIds);
      final dsKho = await kho.khoDanhMuc();
      final dsNcc = await kho.nccDanhMuc();
      if (!mounted) return;
      setState(() {
        _dong = nc.map((e) => _Dong(e)).toList();
        _kho = dsKho;
        _ncc = dsNcc;
        if (dsKho.length == 1) _maKho = dsKho.first.ma;
        _dangTai = false;
      });
    } catch (e) {
      if (mounted) setState(() { _loi = e is LoiApi ? e.thongBao : 'Lỗi tải: $e'; _dangTai = false; });
    }
  }

  Future<void> _luu() async {
    if (_maKho == null || _maKho!.isEmpty) { _bao('Vui lòng chọn kho nhập.'); return; }
    final dong = _dong.where((d) => d.nhap > 0)
        .map((d) => {'maNguyenLieu': d.nc.maNguyenLieu, 'soLuong': d.nhap}).toList();
    if (dong.isEmpty) { _bao('Không có nguyên liệu nào có số nhập > 0.'); return; }

    setState(() => _dangLuu = true);
    try {
      final tb = await ref.read(khoDonProvider).taoLoNhapNguyenLieu(
          maKho: _maKho!, maNccDauVao: _maNcc, ngayNhap: _ngayNhap, hanDung: _hanDung, dong: dong);
      if (!mounted) return;
      Navigator.pop(context, tb);
    } on LoiApi catch (e) {
      _bao(e.thongBao);
    } catch (_) {
      _bao('Không tạo được lệnh nhập. Vui lòng thử lại.');
    } finally {
      if (mounted) setState(() => _dangLuu = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Tạo lệnh nhập nguyên liệu')),
      body: _dangTai
          ? const Center(child: CircularProgressIndicator())
          : _loi != null
              ? _loiView()
              : _dong.isEmpty
                  ? _rong()
                  : ListView(
                      padding: const EdgeInsets.fromLTRB(16, 12, 16, 100),
                      children: [
                        Text('Số cần = công thức/định mức × số lượng (cộng dồn các đơn). Gợi ý nhập = phần còn thiếu so '
                            'với tồn - sửa lại nếu muốn.', style: Theme.of(context).textTheme.bodySmall),
                        const SizedBox(height: 12),
                        _oKhoNcc(),
                        const SizedBox(height: 12),
                        ..._dong.map(_theDong),
                      ],
                    ),
      bottomNavigationBar: (_dangTai || _loi != null || _dong.isEmpty)
          ? null
          : Padding(
              padding: EdgeInsets.fromLTRB(16, 8, 16, MediaQuery.of(context).padding.bottom + 12),
              child: FilledButton.icon(
                onPressed: _dangLuu ? null : _luu,
                style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 14)),
                icon: _dangLuu
                    ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Icon(Icons.inventory_2),
                label: const Text('Tạo lô & nhập kho'),
              ),
            ),
    );
  }

  Widget _oKhoNcc() => Column(children: [
        DropdownButtonFormField<String>(
          initialValue: _maKho,
          isExpanded: true,
          decoration: const InputDecoration(labelText: 'Kho nhập *', border: OutlineInputBorder(), isDense: true),
          items: _kho.map((k) => DropdownMenuItem(value: k.ma, child: Text('${k.ten} (${k.ma})'))).toList(),
          onChanged: _dangLuu ? null : (v) => setState(() => _maKho = v),
        ),
        const SizedBox(height: 10),
        DropdownButtonFormField<String>(
          initialValue: _maNcc,
          isExpanded: true,
          decoration: const InputDecoration(labelText: 'Nhà cung ứng (tuỳ chọn)', border: OutlineInputBorder(), isDense: true),
          items: [
            const DropdownMenuItem<String>(value: null, child: Text('— Không —')),
            ..._ncc.map((n) => DropdownMenuItem(value: n.ma, child: Text(n.ten))),
          ],
          onChanged: _dangLuu ? null : (v) => setState(() => _maNcc = v),
        ),
        const SizedBox(height: 10),
        Row(children: [
          Expanded(child: _oNgay('Ngày nhập', _ngayNhap, (d) => setState(() => _ngayNhap = d))),
          const SizedBox(width: 10),
          Expanded(child: _oNgay('Hạn dùng', _hanDung, (d) => setState(() => _hanDung = d), xoaDuoc: true)),
        ]),
      ]);

  Widget _oNgay(String nhan, DateTime? gt, void Function(DateTime) chon, {bool xoaDuoc = false}) => InkWell(
        onTap: _dangLuu
            ? null
            : () async {
                final d = await showDatePicker(
                    context: context, initialDate: gt ?? DateTime.now(),
                    firstDate: DateTime(2020), lastDate: DateTime(2100));
                if (d != null) chon(d);
              },
        child: InputDecorator(
          decoration: InputDecoration(labelText: nhan, border: const OutlineInputBorder(), isDense: true,
              suffixIcon: const Icon(Icons.calendar_today, size: 18)),
          child: Text(gt == null ? '—' : '${gt.day.toString().padLeft(2, '0')}/${gt.month.toString().padLeft(2, '0')}/${gt.year}'),
        ),
      );

  Widget _theDong(_Dong d) {
    final thieu = d.nc.ton < d.nc.can;
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(d.nc.tenNguyenLieu, style: const TextStyle(fontWeight: FontWeight.w600)),
          Text(d.nc.maNguyenLieu, style: Theme.of(context).textTheme.bodySmall),
          const SizedBox(height: 6),
          Row(children: [
            Expanded(child: Text('Cần: ${_so(d.nc.can)} ${d.nc.donViTinh ?? ''}',
                style: Theme.of(context).textTheme.bodySmall)),
            Expanded(child: Text('Tồn: ${_so(d.nc.ton)}',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(color: thieu ? Colors.red : null))),
          ]),
          const SizedBox(height: 8),
          TextFormField(
            key: ValueKey('nhap_${d.nc.maNguyenLieu}'),
            initialValue: _so(d.nhap),
            enabled: !_dangLuu,
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            decoration: InputDecoration(labelText: 'Nhập', border: const OutlineInputBorder(), isDense: true,
                suffixText: d.nc.donViTinh),
            onChanged: (v) => d.nhap = double.tryParse(v.replaceAll(',', '.')) ?? 0,
          ),
        ]),
      ),
    );
  }

  static String _so(double v) => v == v.roundToDouble() ? v.toInt().toString() : v.toString();

  Widget _rong() => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Text('Các đơn đã chọn không tính được nguyên liệu (món chưa có công thức / thành phẩm chưa có định mức).',
              textAlign: TextAlign.center),
        ),
      );

  Widget _loiView() => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Text(_loi ?? '', textAlign: TextAlign.center),
            const SizedBox(height: 12),
            FilledButton(onPressed: _tai, child: const Text('Thử lại')),
          ]),
        ),
      );

  void _bao(String s) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(s)));
}
