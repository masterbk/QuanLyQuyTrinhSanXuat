import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../loi/api.dart';
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';

final _ngayVn = DateFormat('dd/MM/yyyy');

/// Tạo lệnh mới (lenh = null) hoặc sửa lệnh chưa hoàn thành.
class ManSuaLenh extends ConsumerStatefulWidget {
  final LenhSanXuat? lenh;

  const ManSuaLenh({super.key, this.lenh});

  @override
  ConsumerState<ManSuaLenh> createState() => _ManSuaLenhState();
}

class _ManSuaLenhState extends ConsumerState<ManSuaLenh> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _maLenh;
  late final TextEditingController _soLuong;
  late final TextEditingController _maLo;
  late final TextEditingController _ghiChu;

  String? _maThanhPham;
  String? _maKho;
  late DateTime _ngaySanXuat;
  DateTime? _hanSuDung;
  bool _taoLoDongBo = false;
  bool _dangLuu = false;

  List<NguyenLieuCan>? _xemTruoc;
  bool _dangTinh = false;

  bool get _laSua => widget.lenh != null;

  @override
  void initState() {
    super.initState();
    final l = widget.lenh;
    _maLenh = TextEditingController(text: l?.maLenh ?? '');
    _soLuong = TextEditingController(text: l == null ? '' : soGon(l.soLuong));
    _maLo = TextEditingController(text: l?.maLoThanhPham ?? '');
    _ghiChu = TextEditingController(text: l?.ghiChu ?? '');
    _maThanhPham = l?.maThanhPham;
    _maKho = l?.maKho;
    _ngaySanXuat = l?.ngaySanXuat ?? DateTime.now();
    _hanSuDung = l?.hanSuDungThanhPham;
    _taoLoDongBo = l?.taoLoDongBo ?? false;
  }

  @override
  void dispose() {
    _maLenh.dispose();
    _soLuong.dispose();
    _maLo.dispose();
    _ghiChu.dispose();
    super.dispose();
  }

  Future<void> _tinhNguyenLieu() async {
    final sl = double.tryParse(_soLuong.text.replaceAll(',', '.')) ?? 0;
    if (_maThanhPham == null || _maKho == null || sl <= 0) {
      _bao('Chọn thành phẩm, kho và nhập số lượng trước.');
      return;
    }
    setState(() => _dangTinh = true);
    try {
      final ds = await ref.read(khoLenhProvider).nguyenLieuCan(_maThanhPham!, sl, _maKho!);
      setState(() => _xemTruoc = ds);
    } on LoiApi catch (e) {
      _bao(e.thongBao);
    } finally {
      if (mounted) setState(() => _dangTinh = false);
    }
  }

  Future<void> _luu() async {
    if (!_form.currentState!.validate()) return;
    if (_maThanhPham == null) return _bao('Chọn thành phẩm.');
    if (_maKho == null) return _bao('Chọn kho.');

    setState(() => _dangLuu = true);
    final than = {
      'maLenh': _maLenh.text.trim(),
      'maThanhPham': _maThanhPham,
      'soLuong': double.tryParse(_soLuong.text.replaceAll(',', '.')) ?? 0,
      'maKho': _maKho,
      'maLoThanhPham': _maLo.text.trim(),
      'hanSuDungThanhPham': _hanSuDung == null ? null : _chuoiNgay(_hanSuDung!),
      'ngaySanXuat': _chuoiNgay(_ngaySanXuat),
      'taoLoDongBo': _taoLoDongBo,
      'ghiChu': _ghiChu.text.trim().isEmpty ? null : _ghiChu.text.trim(),
    };

    try {
      final kho = ref.read(khoLenhProvider);
      final tb = _laSua ? await kho.sua(widget.lenh!.id, than) : await kho.tao(than);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
      Navigator.pop(context, true);
    } on LoiApi catch (e) {
      _bao(e.thongBao);
    } finally {
      if (mounted) setState(() => _dangLuu = false);
    }
  }

  /// API nhận ngày dạng yyyy-MM-dd (không kèm giờ).
  String _chuoiNgay(DateTime d) =>
      '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

  void _bao(String s) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(s)));

  @override
  Widget build(BuildContext context) {
    final thanhPham = ref.watch(thanhPhamProvider);
    final kho = ref.watch(khoProvider);

    return Scaffold(
      appBar: AppBar(title: Text(_laSua ? 'Sửa lệnh ${widget.lenh!.maLenh}' : 'Tạo lệnh sản xuất')),
      body: Form(
        key: _form,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
          children: [
            TextFormField(
              controller: _maLenh,
              textCapitalization: TextCapitalization.characters,
              decoration: const InputDecoration(
                labelText: 'Mã lệnh *',
                hintText: 'vd LSX-001',
                border: OutlineInputBorder(),
              ),
              validator: (v) => (v ?? '').trim().isEmpty ? 'Nhập mã lệnh' : null,
            ),
            const SizedBox(height: 14),
            thanhPham.when(
              loading: () => const LinearProgressIndicator(),
              error: (e, _) => _LoiNho('Không tải được danh sách thành phẩm',
                  () => ref.invalidate(thanhPhamProvider)),
              data: (ds) => DropdownButtonFormField<String>(
                initialValue: ds.any((t) => t.maSanPham == _maThanhPham) ? _maThanhPham : null,
                isExpanded: true,
                decoration: const InputDecoration(
                    labelText: 'Thành phẩm *', border: OutlineInputBorder()),
                items: ds
                    .map((t) => DropdownMenuItem(
                        value: t.maSanPham, child: Text('${t.tenSanPham} (${t.maSanPham})')))
                    .toList(),
                onChanged: (v) => setState(() {
                  _maThanhPham = v;
                  _xemTruoc = null;
                }),
                validator: (v) => v == null ? 'Chọn thành phẩm' : null,
              ),
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                Expanded(
                  child: TextFormField(
                    controller: _soLuong,
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    decoration: const InputDecoration(
                        labelText: 'Số lượng *', border: OutlineInputBorder()),
                    onChanged: (_) => setState(() => _xemTruoc = null),
                    validator: (v) {
                      final sl = double.tryParse((v ?? '').replaceAll(',', '.'));
                      if (sl == null || sl <= 0) return 'Số lượng phải lớn hơn 0';
                      return null;
                    },
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: kho.when(
                    loading: () => const LinearProgressIndicator(),
                    error: (e, _) => _LoiNho('Lỗi tải kho', () => ref.invalidate(khoProvider)),
                    data: (ds) => DropdownButtonFormField<String>(
                      initialValue: ds.any((k) => k.maKho == _maKho) ? _maKho : null,
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'Kho *', border: OutlineInputBorder()),
                      items: ds
                          .map((k) => DropdownMenuItem(value: k.maKho, child: Text(k.tenKho)))
                          .toList(),
                      onChanged: (v) => setState(() {
                        _maKho = v;
                        _xemTruoc = null;
                      }),
                      validator: (v) => v == null ? 'Chọn kho' : null,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _maLo,
              textCapitalization: TextCapitalization.characters,
              decoration: const InputDecoration(
                  labelText: 'Mã lô thành phẩm *', border: OutlineInputBorder()),
              validator: (v) => (v ?? '').trim().isEmpty ? 'Nhập mã lô thành phẩm' : null,
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                Expanded(
                  child: _OChonNgay(
                    nhan: 'Ngày sản xuất',
                    ngay: _ngaySanXuat,
                    khiChon: (d) => setState(() => _ngaySanXuat = d ?? _ngaySanXuat),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: _OChonNgay(
                    nhan: 'Hạn dùng',
                    ngay: _hanSuDung,
                    choXoa: true,
                    khiChon: (d) => setState(() => _hanSuDung = d),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _ghiChu,
              maxLines: 2,
              decoration: const InputDecoration(labelText: 'Ghi chú', border: OutlineInputBorder()),
            ),
            const SizedBox(height: 8),
            CheckboxListTile(
              contentPadding: EdgeInsets.zero,
              value: _taoLoDongBo,
              onChanged: (v) => setState(() => _taoLoDongBo = v ?? false),
              title: const Text('Tạo Lô sản xuất và đồng bộ HanoiCheck'),
              subtitle: const Text('Truy xuất lô nguyên liệu sang lô thành phẩm'),
            ),
            const Divider(height: 32),
            Row(
              children: [
                Expanded(
                  child: Text('Nguyên liệu cần',
                      style: Theme.of(context).textTheme.titleSmall),
                ),
                TextButton.icon(
                  onPressed: _dangTinh ? null : _tinhNguyenLieu,
                  icon: const Icon(Icons.calculate_outlined, size: 18),
                  label: const Text('Tính thử'),
                ),
              ],
            ),
            if (_dangTinh) const LinearProgressIndicator(),
            if (_xemTruoc != null) ..._bangXemTruoc(_xemTruoc!),
          ],
        ),
      ),
      bottomNavigationBar: Padding(
        padding: EdgeInsets.fromLTRB(16, 8, 16, MediaQuery.of(context).padding.bottom + 12),
        child: FilledButton(
          onPressed: _dangLuu ? null : _luu,
          style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 16)),
          child: _dangLuu
              ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
              : Text(_laSua ? 'Lưu thay đổi' : 'Tạo lệnh'),
        ),
      ),
    );
  }

  List<Widget> _bangXemTruoc(List<NguyenLieuCan> ds) {
    if (ds.isEmpty) {
      return [const Padding(padding: EdgeInsets.all(8), child: Text('Thành phẩm này chưa khai định mức.'))];
    }
    return ds.map((n) {
      final mau = n.du ? Colors.green.shade700 : Theme.of(context).colorScheme.error;
      return ListTile(
        dense: true,
        contentPadding: EdgeInsets.zero,
        leading: Icon(n.du ? Icons.check_circle_outline : Icons.error_outline, color: mau),
        title: Text(n.tenNguyenLieu),
        subtitle: Text('Cần ${soGon(n.can)} ${n.donViTinh ?? ''} · Tồn ${soGon(n.ton)}'),
        trailing: Text(n.du ? 'Đủ' : 'Thiếu', style: TextStyle(color: mau, fontWeight: FontWeight.w600)),
      );
    }).toList();
  }
}

class _OChonNgay extends StatelessWidget {
  final String nhan;
  final DateTime? ngay;
  final bool choXoa;
  final ValueChanged<DateTime?> khiChon;

  const _OChonNgay({required this.nhan, required this.ngay, required this.khiChon, this.choXoa = false});

  @override
  Widget build(BuildContext context) => InkWell(
        onTap: () async {
          final d = await showDatePicker(
            context: context,
            initialDate: ngay ?? DateTime.now(),
            firstDate: DateTime(2020),
            lastDate: DateTime(2100),
          );
          if (d != null) khiChon(d);
        },
        child: InputDecorator(
          decoration: InputDecoration(
            labelText: nhan,
            border: const OutlineInputBorder(),
            suffixIcon: choXoa && ngay != null
                ? IconButton(icon: const Icon(Icons.clear, size: 18), onPressed: () => khiChon(null))
                : const Icon(Icons.calendar_today, size: 18),
          ),
          child: Text(ngay == null ? '—' : _ngayVn.format(ngay!)),
        ),
      );
}

class _LoiNho extends StatelessWidget {
  final String chu;
  final VoidCallback thuLai;

  const _LoiNho(this.chu, this.thuLai);

  @override
  Widget build(BuildContext context) => Row(
        children: [
          Expanded(child: Text(chu, style: TextStyle(color: Theme.of(context).colorScheme.error))),
          TextButton(onPressed: thuLai, child: const Text('Thử lại')),
        ],
      );
}
