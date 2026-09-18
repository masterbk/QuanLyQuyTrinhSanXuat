import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../loi/api.dart';
import '../../loi/gio_viet_nam.dart';
import '../xac_thuc/xac_thuc.dart';
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';
import 'o_khau.dart';

final _ngayVn = DateFormat('dd/MM/yyyy');

/// Một dòng sản phẩm đang nhập trên form.
class _DongSp {
  String? maThanhPham;
  String? maQuyTrinh;
  final TextEditingController soLuong;
  final TextEditingController maLo;
  DateTime? hanSuDung;
  List<KhauSua> khau;

  _DongSp({this.maThanhPham, this.maQuyTrinh, String soLuong = '', String maLo = '', this.hanSuDung, List<KhauSua>? khau})
      : soLuong = TextEditingController(text: soLuong),
        maLo = TextEditingController(text: maLo),
        khau = khau ?? [];

  factory _DongSp.tu(SanPhamLenh s) => _DongSp(
        maThanhPham: s.maThanhPham,
        maQuyTrinh: s.maQuyTrinh.isEmpty ? null : s.maQuyTrinh,
        soLuong: soGon(s.soLuong),
        maLo: s.maLoThanhPham,
        hanSuDung: s.hanSuDung,
        khau: ([...s.khau]..sort((a, b) => a.thuTu.compareTo(b.thuTu))).map(KhauSua.tuKhauLenh).toList(),
      );

  double get soLuongSo => double.tryParse(soLuong.text.replaceAll(',', '.')) ?? 0;

  void dispose() {
    soLuong.dispose();
    maLo.dispose();
  }
}

/// Tạo lệnh mới (lenh = null) hoặc sửa lệnh chưa hoàn thành. Một lệnh gồm nhiều sản phẩm; mỗi sản
/// phẩm chọn quy trình và khai cơ sở + người thực hiện cho từng khâu.
class ManSuaLenh extends ConsumerStatefulWidget {
  final LenhSanXuat? lenh;

  const ManSuaLenh({super.key, this.lenh});

  @override
  ConsumerState<ManSuaLenh> createState() => _ManSuaLenhState();
}

class _ManSuaLenhState extends ConsumerState<ManSuaLenh> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _maLenh;
  late final TextEditingController _ghiChu;

  String? _maKho;
  late DateTime _ngaySanXuat;
  bool _taoLoDongBo = false;
  bool _dangLuu = false;
  late List<_DongSp> _dong;

  List<NguyenLieuCan>? _xemTruoc;
  bool _dangTinh = false;

  bool get _laSua => widget.lenh != null;

  @override
  void initState() {
    super.initState();
    final l = widget.lenh;
    _maLenh = TextEditingController(text: l?.maLenh ?? '');
    _ghiChu = TextEditingController(text: l?.ghiChu ?? '');
    _maKho = l?.maKho;
    _ngaySanXuat = l?.ngaySanXuat ?? bayGioVietNam();
    _taoLoDongBo = l?.taoLoDongBo ?? false;
    _dong = l == null || l.sanPham.isEmpty ? [_DongSp(soLuong: '1')] : l.sanPham.map(_DongSp.tu).toList();

    // Lệnh cũ (trước khi có khâu) chưa có dữ liệu khâu: dựng sẵn theo quy trình để người dùng khai.
    Future.microtask(() async {
      try {
        final ds = await ref.read(quyTrinhProvider.future);
        final coSo = await ref.read(coSoProvider.future);
        if (!mounted) return;
        setState(() {
          for (final d in _dong.where((d) => d.khau.isEmpty && d.maQuyTrinh != null)) {
            final qt = ds.where((q) => q.maQuyTrinh == d.maQuyTrinh).firstOrNull;
            if (qt != null) d.khau = dungKhauTheoQuyTrinh(qt, const [], coSoMacDinh: _coSoMacDinh(coSo));
          }
        });
      } on LoiApi {
        // Lỗi tải danh mục đã hiện ở ô chọn tương ứng.
      }
    });
  }

  @override
  void dispose() {
    _maLenh.dispose();
    _ghiChu.dispose();
    for (final d in _dong) {
      d.dispose();
    }
    super.dispose();
  }

  String? _coSoMacDinh(List<CoSo> ds) => ds.length == 1 ? ds.first.maCoSo : null;

  void _doiThanhPham(_DongSp d, String? ma, List<ThanhPham> thanhPham, List<QuyTrinh> quyTrinh) {
    setState(() {
      d.maThanhPham = ma;
      _xemTruoc = null;
      final macDinh = thanhPham.where((t) => t.maSanPham == ma).firstOrNull?.maQuyTrinh;
      final qt = quyTrinh.where((q) => q.maQuyTrinh == macDinh).firstOrNull;
      if (qt != null) _apQuyTrinh(d, qt);
    });
  }

  void _apQuyTrinh(_DongSp d, QuyTrinh qt) {
    d.maQuyTrinh = qt.maQuyTrinh;
    d.khau = dungKhauTheoQuyTrinh(qt, d.khau,
        coSoMacDinh: _coSoMacDinh(ref.read(coSoProvider).value ?? const []));
  }

  void _chepKhauDau(_DongSp d) => setState(() {
        final dau = d.khau.first;
        for (final k in d.khau.skip(1)) {
          k.maCoSo = dau.maCoSo;
          k.nguoi = [...dau.nguoi];
        }
      });

  Future<void> _tinhNguyenLieu() async {
    final dong = _dong
        .where((d) => d.maThanhPham != null && d.soLuongSo > 0)
        .map((d) => (maThanhPham: d.maThanhPham!, soLuong: d.soLuongSo))
        .toList();
    if (_maKho == null || dong.isEmpty) {
      _bao('Chọn kho và ít nhất một thành phẩm có số lượng trước.');
      return;
    }
    setState(() => _dangTinh = true);
    try {
      final ds = await ref.read(khoLenhProvider).nguyenLieuCan(dong, _maKho!);
      setState(() => _xemTruoc = ds);
    } on LoiApi catch (e) {
      _bao(e.thongBao);
    } finally {
      if (mounted) setState(() => _dangTinh = false);
    }
  }

  /// Kiểm tra phần form không bắt được (dropdown sản phẩm, khâu). Trả thông báo lỗi đầu tiên.
  String? _kiemTra() {
    if (_maKho == null) return 'Chọn kho.';
    if (_dong.isEmpty) return 'Lệnh phải có ít nhất một sản phẩm.';
    for (var i = 0; i < _dong.length; i++) {
      final d = _dong[i];
      final ten = 'Sản phẩm ${i + 1}';
      if (d.maThanhPham == null) return '$ten: chọn thành phẩm.';
      if (d.maQuyTrinh == null) return '$ten: chọn quy trình sản xuất.';
      if (d.khau.isEmpty) return '$ten: quy trình chưa có khâu.';
      for (final k in d.khau) {
        final loi = k.loiKhiLap;
        if (loi != null) return '$ten - khâu "${k.tenKhau}": $loi.';
      }
    }
    return null;
  }

  Future<void> _luu() async {
    if (!_form.currentState!.validate()) return;
    final loi = _kiemTra();
    if (loi != null) return _bao(loi);

    setState(() => _dangLuu = true);
    final than = {
      'maLenh': _maLenh.text.trim(),
      'maKho': _maKho,
      'ngaySanXuat': chuoiNgay(_ngaySanXuat),
      'taoLoDongBo': _taoLoDongBo,
      'ghiChu': _ghiChu.text.trim().isEmpty ? null : _ghiChu.text.trim(),
      'sanPham': _dong
          .map((d) => {
                'maThanhPham': d.maThanhPham,
                'soLuong': d.soLuongSo,
                'maLoThanhPham': d.maLo.text.trim(),
                'hanSuDung': d.hanSuDung == null ? null : chuoiNgay(d.hanSuDung!),
                'maQuyTrinh': d.maQuyTrinh,
                'khau': d.khau.map((k) => k.choLuu()).toList(),
              })
          .toList(),
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

  void _bao(String s) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(s)));

  @override
  Widget build(BuildContext context) {
    // Chưa biết (đang tải) thì chưa hiện ô tạo lô đồng bộ, tránh hiện rồi biến mất.
    final hanoiCheckBat = ref.watch(hanoiCheckBatProvider).maybeWhen(data: (v) => v, orElse: () => false);
    final kho = ref.watch(khoProvider);
    final thanhPham = ref.watch(thanhPhamProvider);
    final quyTrinh = ref.watch(quyTrinhProvider);
    // Tải sẵn để ô khâu có dữ liệu.
    ref.watch(coSoProvider);
    ref.watch(nhanSuProvider);

    return Scaffold(
      appBar: AppBar(title: Text(_laSua ? 'Sửa lệnh ${widget.lenh!.maLenh}' : 'Tạo lệnh sản xuất')),
      body: Form(
        key: _form,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
          children: [
            TextFormField(
              controller: _maLenh,
              readOnly: true,
              decoration: const InputDecoration(
                labelText: 'Mã lệnh',
                hintText: '(tự sinh khi lưu)',
                helperText: 'Hệ thống tự sinh theo ngày sản xuất, không sửa được',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                Expanded(
                  child: kho.when(
                    loading: () => const LinearProgressIndicator(),
                    error: (e, _) => _LoiNho('Lỗi tải kho', () => ref.invalidate(khoProvider)),
                    data: (ds) => DropdownButtonFormField<String>(
                      initialValue: ds.any((k) => k.maKho == _maKho) ? _maKho : null,
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'Kho *', border: OutlineInputBorder()),
                      items: ds.map((k) => DropdownMenuItem(value: k.maKho, child: Text(k.tenKho))).toList(),
                      onChanged: (v) => setState(() {
                        _maKho = v;
                        _xemTruoc = null;
                      }),
                      validator: (v) => v == null ? 'Chọn kho' : null,
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: _OChonNgay(
                    nhan: 'Ngày sản xuất',
                    ngay: _ngaySanXuat,
                    khiChon: (d) => setState(() => _ngaySanXuat = d ?? _ngaySanXuat),
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
            // Cơ sở tắt HanoiCheck thì dùng app như quản lý nội bộ: không có lô đồng bộ.
            if (hanoiCheckBat)
            CheckboxListTile(
              contentPadding: EdgeInsets.zero,
              value: _taoLoDongBo,
              onChanged: (v) => setState(() => _taoLoDongBo = v ?? false),
              title: const Text('Tạo Lô sản xuất và đồng bộ HanoiCheck'),
              subtitle: const Text('Mỗi sản phẩm sinh một lô, kèm khâu và người thực hiện'),
            ),
            const Divider(height: 24),
            Row(
              children: [
                Expanded(
                  child: Text('Sản phẩm (${_dong.length})', style: Theme.of(context).textTheme.titleMedium),
                ),
                TextButton.icon(
                  onPressed: () => setState(() => _dong.add(_DongSp(soLuong: '1'))),
                  icon: const Icon(Icons.add, size: 18),
                  label: const Text('Thêm sản phẩm'),
                ),
              ],
            ),
            if (thanhPham.hasError)
              _LoiNho('Không tải được thành phẩm', () => ref.invalidate(thanhPhamProvider)),
            if (quyTrinh.hasError)
              _LoiNho('Không tải được quy trình', () => ref.invalidate(quyTrinhProvider)),
            if (thanhPham.isLoading || quyTrinh.isLoading) const LinearProgressIndicator(),
            for (var i = 0; i < _dong.length; i++)
              _theSanPham(i, _dong[i], thanhPham.value ?? const [], quyTrinh.value ?? const []),
            const Divider(height: 32),
            Row(
              children: [
                Expanded(
                  child: Text('Nguyên liệu cần (cộng dồn)', style: Theme.of(context).textTheme.titleSmall),
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

  Widget _theSanPham(int i, _DongSp d, List<ThanhPham> thanhPham, List<QuyTrinh> quyTrinh) => Card(
        key: ObjectKey(d),
        margin: const EdgeInsets.only(bottom: 12),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(12, 4, 12, 12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text('Sản phẩm ${i + 1}', style: const TextStyle(fontWeight: FontWeight.bold)),
                  ),
                  IconButton(
                    tooltip: 'Bỏ sản phẩm',
                    icon: Icon(Icons.delete_outline, color: Theme.of(context).colorScheme.error),
                    onPressed: _dong.length <= 1
                        ? null
                        : () => setState(() {
                              _dong.removeAt(i).dispose();
                              _xemTruoc = null;
                            }),
                  ),
                ],
              ),
              DropdownButtonFormField<String>(
                initialValue: thanhPham.any((t) => t.maSanPham == d.maThanhPham) ? d.maThanhPham : null,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'Thành phẩm *', border: OutlineInputBorder()),
                items: thanhPham
                    .map((t) => DropdownMenuItem(value: t.maSanPham, child: Text('${t.tenSanPham} (${t.maSanPham})')))
                    .toList(),
                onChanged: (v) => _doiThanhPham(d, v, thanhPham, quyTrinh),
                validator: (v) => v == null ? 'Chọn thành phẩm' : null,
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                key: ValueKey('qt-${identityHashCode(d)}-${d.maQuyTrinh}'),
                initialValue: quyTrinh.any((q) => q.maQuyTrinh == d.maQuyTrinh) ? d.maQuyTrinh : null,
                isExpanded: true,
                decoration: const InputDecoration(
                    labelText: 'Quy trình sản xuất *',
                    helperText: 'Mặc định theo thành phẩm, đổi được',
                    border: OutlineInputBorder()),
                items: quyTrinh
                    .map((q) => DropdownMenuItem(value: q.maQuyTrinh, child: Text(q.tenQuyTrinh)))
                    .toList(),
                onChanged: (v) => setState(() {
                  final qt = quyTrinh.where((q) => q.maQuyTrinh == v).firstOrNull;
                  if (qt != null) _apQuyTrinh(d, qt);
                }),
                validator: (v) => v == null ? 'Chọn quy trình' : null,
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    flex: 2,
                    child: TextFormField(
                      controller: d.soLuong,
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      decoration: const InputDecoration(labelText: 'Số lượng *', border: OutlineInputBorder()),
                      onChanged: (_) => setState(() => _xemTruoc = null),
                      validator: (v) {
                        final sl = double.tryParse((v ?? '').replaceAll(',', '.'));
                        return sl == null || sl <= 0 ? 'Phải > 0' : null;
                      },
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    flex: 3,
                    child: TextFormField(
                      controller: d.maLo,
                      readOnly: true,
                      decoration: const InputDecoration(
                          labelText: 'Mã lô', hintText: '(tự sinh)', border: OutlineInputBorder()),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              _OChonNgay(
                nhan: 'Hạn dùng',
                ngay: d.hanSuDung,
                choXoa: true,
                khiChon: (v) => setState(() => d.hanSuDung = v),
              ),
              if (d.khau.isNotEmpty) ...[
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: Text('Khâu sản xuất (${d.khau.length})',
                          style: Theme.of(context).textTheme.titleSmall),
                    ),
                    if (d.khau.length > 1)
                      TextButton(onPressed: () => _chepKhauDau(d), child: const Text('Chép khâu đầu xuống')),
                  ],
                ),
                for (final k in d.khau) OKhau(batBuocNguoi: false, key: ObjectKey(k), khau: k, khiDoi: () => setState(() {})),
              ],
            ],
          ),
        ),
      );

  List<Widget> _bangXemTruoc(List<NguyenLieuCan> ds) {
    if (ds.isEmpty) {
      return [const Padding(padding: EdgeInsets.all(8), child: Text('Các thành phẩm chưa khai định mức.'))];
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
            initialDate: ngay ?? bayGioVietNam(),
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
