import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../loi/api.dart';
import '../lenh_san_xuat/kho_du_lieu.dart' show khoProvider, nhanSuProvider;
import '../lenh_san_xuat/mo_hinh.dart' show ThanhPham, chuoiNgay, soGon;
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';

final _ngayVn = DateFormat('dd/MM/yyyy');

/// Một dòng hàng đang nhập trên form.
class _DongHang {
  String? maThanhPham;
  final TextEditingController soLuong;
  final TextEditingController donGia;

  _DongHang({this.maThanhPham, String soLuong = '', String donGia = '0'})
      : soLuong = TextEditingController(text: soLuong),
        donGia = TextEditingController(text: donGia);

  factory _DongHang.tu(DongDonHang d) => _DongHang(
        maThanhPham: d.maThanhPham,
        soLuong: soGon(d.soLuong),
        donGia: d.donGia.toStringAsFixed(0),
      );

  double get soLuongSo => double.tryParse(soLuong.text.replaceAll(',', '.')) ?? 0;
  double get donGiaSo => double.tryParse(donGia.text.replaceAll(',', '.')) ?? 0;
  double get thanhTien => soLuongSo * donGiaSo;

  void dispose() {
    soLuong.dispose();
    donGia.dispose();
  }
}

/// Tạo đơn mới (don = null) hoặc sửa đơn chưa xuất kho. Việc của quản lý/nhập liệu.
class ManSuaDon extends ConsumerStatefulWidget {
  final DonHangBan? don;

  const ManSuaDon({super.key, this.don});

  @override
  ConsumerState<ManSuaDon> createState() => _ManSuaDonState();
}

class _ManSuaDonState extends ConsumerState<ManSuaDon> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _diaChiGiao;
  late final TextEditingController _ghiChu;

  String? _maKhachHang;
  String? _maKho;
  String? _maNguoiGiao;
  late DateTime _ngayDat;
  DateTime? _ngayGiao;
  bool _dangLuu = false;
  late List<_DongHang> _dong;

  bool get _laSua => widget.don != null;

  @override
  void initState() {
    super.initState();
    final d = widget.don;
    _diaChiGiao = TextEditingController(text: d?.diaChiGiao ?? '');
    _ghiChu = TextEditingController(text: d?.ghiChu ?? '');
    _maKhachHang = d?.maKhachHang.isEmpty ?? true ? null : d!.maKhachHang;
    _maKho = d?.maKho.isEmpty ?? true ? null : d!.maKho;
    _maNguoiGiao = d?.maNguoiGiao;
    _ngayDat = d?.ngayDat ?? DateTime.now();
    _ngayGiao = d?.ngayGiao;
    _dong = d == null || d.dong.isEmpty ? [_DongHang(soLuong: '1')] : d.dong.map(_DongHang.tu).toList();
  }

  @override
  void dispose() {
    _diaChiGiao.dispose();
    _ghiChu.dispose();
    for (final d in _dong) {
      d.dispose();
    }
    super.dispose();
  }

  double get _tongTien => _dong.fold(0, (t, d) => t + d.thanhTien);

  String? _kiemTra() {
    if (_maKhachHang == null) return 'Chọn khách hàng.';
    if (_maKho == null) return 'Chọn kho xuất.';
    if (_dong.isEmpty) return 'Đơn phải có ít nhất một dòng hàng.';
    for (var i = 0; i < _dong.length; i++) {
      final d = _dong[i];
      final ten = 'Dòng ${i + 1}';
      if (d.maThanhPham == null) return '$ten: chọn thành phẩm.';
      if (d.soLuongSo <= 0) return '$ten: số lượng phải lớn hơn 0.';
      if (d.donGiaSo < 0) return '$ten: đơn giá không được âm.';
    }
    return null;
  }

  Future<void> _luu() async {
    if (!_form.currentState!.validate()) return;
    final loi = _kiemTra();
    if (loi != null) return _bao(loi);

    setState(() => _dangLuu = true);
    final than = {
      'maKhachHang': _maKhachHang,
      'maKho': _maKho,
      'ngayDat': chuoiNgay(_ngayDat),
      'ngayGiao': _ngayGiao == null ? null : chuoiNgay(_ngayGiao!),
      'diaChiGiao': _diaChiGiao.text.trim().isEmpty ? null : _diaChiGiao.text.trim(),
      'maNguoiGiao': _maNguoiGiao,
      'ghiChu': _ghiChu.text.trim().isEmpty ? null : _ghiChu.text.trim(),
      'dong': _dong
          .map((d) => {
                'maThanhPham': d.maThanhPham,
                'soLuong': d.soLuongSo,
                'donGia': d.donGiaSo,
              })
          .toList(),
    };

    try {
      final kho = ref.read(khoDonProvider);
      final tb = _laSua ? await kho.suaDon(widget.don!.id, than) : await kho.taoDon(than);
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
    final khachHang = ref.watch(khachHangProvider);
    final kho = ref.watch(khoProvider);
    final thanhPham = ref.watch(thanhPhamBanProvider);
    final nguoiGiao = ref.watch(nhanSuProvider);

    return Scaffold(
      appBar: AppBar(title: Text(_laSua ? 'Sửa đơn ${widget.don!.maDonHang}' : 'Tạo đơn hàng')),
      body: Form(
        key: _form,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
          children: [
            khachHang.when(
              loading: () => const LinearProgressIndicator(),
              error: (e, _) => _LoiNho('Lỗi tải khách hàng', () => ref.invalidate(khachHangProvider)),
              data: (ds) => DropdownButtonFormField<String>(
                initialValue: ds.any((k) => k.maKhachHang == _maKhachHang) ? _maKhachHang : null,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'Khách hàng *', border: OutlineInputBorder()),
                items: ds
                    .map((k) => DropdownMenuItem(value: k.maKhachHang, child: Text(k.tenKhachHang)))
                    .toList(),
                onChanged: (v) => setState(() {
                  _maKhachHang = v;
                  final kh = ds.where((k) => k.maKhachHang == v).firstOrNull;
                  if (kh != null && _diaChiGiao.text.trim().isEmpty) _diaChiGiao.text = kh.diaChi ?? '';
                }),
                validator: (v) => v == null ? 'Chọn khách hàng' : null,
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
                      decoration: const InputDecoration(labelText: 'Kho xuất *', border: OutlineInputBorder()),
                      items: ds.map((k) => DropdownMenuItem(value: k.maKho, child: Text(k.tenKho))).toList(),
                      onChanged: (v) => setState(() => _maKho = v),
                      validator: (v) => v == null ? 'Chọn kho' : null,
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: _OChonNgay(
                    nhan: 'Ngày đặt',
                    ngay: _ngayDat,
                    khiChon: (d) => setState(() => _ngayDat = d ?? _ngayDat),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 14),
            _OChonNgay(
              nhan: 'Ngày giao',
              ngay: _ngayGiao,
              choXoa: true,
              khiChon: (d) => setState(() => _ngayGiao = d),
            ),
            const SizedBox(height: 14),
            nguoiGiao.when(
              loading: () => const LinearProgressIndicator(),
              error: (e, _) => _LoiNho('Lỗi tải nhân sự', () => ref.invalidate(nhanSuProvider)),
              data: (ds) => DropdownButtonFormField<String>(
                initialValue: ds.any((n) => n.maNhanSu == _maNguoiGiao) ? _maNguoiGiao : null,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'Người giao', border: OutlineInputBorder()),
                items: [
                  const DropdownMenuItem(value: null, child: Text('(chưa chọn)')),
                  ...ds.map((n) => DropdownMenuItem(value: n.maNhanSu, child: Text(n.hoTen))),
                ],
                onChanged: (v) => setState(() => _maNguoiGiao = v),
              ),
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _diaChiGiao,
              maxLines: 2,
              decoration: const InputDecoration(
                  labelText: 'Địa chỉ giao', helperText: 'Trống thì lấy địa chỉ của khách hàng',
                  border: OutlineInputBorder()),
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _ghiChu,
              maxLines: 2,
              decoration: const InputDecoration(labelText: 'Ghi chú', border: OutlineInputBorder()),
            ),
            const Divider(height: 24),
            Row(
              children: [
                Expanded(
                  child: Text('Dòng hàng (${_dong.length})', style: Theme.of(context).textTheme.titleMedium),
                ),
                TextButton.icon(
                  onPressed: () => setState(() => _dong.add(_DongHang(soLuong: '1'))),
                  icon: const Icon(Icons.add, size: 18),
                  label: const Text('Thêm dòng'),
                ),
              ],
            ),
            if (thanhPham.hasError)
              _LoiNho('Không tải được thành phẩm', () => ref.invalidate(thanhPhamBanProvider)),
            if (thanhPham.isLoading) const LinearProgressIndicator(),
            for (var i = 0; i < _dong.length; i++) _theDong(i, _dong[i], thanhPham.value ?? const []),
            const SizedBox(height: 8),
            Text('Tổng: ${_tongTien.toStringAsFixed(0)} đ',
                textAlign: TextAlign.right, style: Theme.of(context).textTheme.titleMedium),
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
              : Text(_laSua ? 'Lưu thay đổi' : 'Tạo đơn'),
        ),
      ),
    );
  }

  Widget _theDong(int i, _DongHang d, List<ThanhPham> thanhPham) => Card(
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
                    onPressed: _dong.length <= 1 ? null : () => setState(() => _dong.removeAt(i).dispose()),
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
                onChanged: (v) => setState(() => d.maThanhPham = v),
                validator: (v) => v == null ? 'Chọn thành phẩm' : null,
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: TextFormField(
                      controller: d.soLuong,
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      decoration: const InputDecoration(labelText: 'Số lượng *', border: OutlineInputBorder()),
                      onChanged: (_) => setState(() {}),
                      validator: (v) {
                        final sl = double.tryParse((v ?? '').replaceAll(',', '.'));
                        return sl == null || sl <= 0 ? 'Phải > 0' : null;
                      },
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: TextFormField(
                      controller: d.donGia,
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      decoration: const InputDecoration(labelText: 'Đơn giá (đ)', border: OutlineInputBorder()),
                      onChanged: (_) => setState(() {}),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 6),
              Text('Thành tiền: ${d.thanhTien.toStringAsFixed(0)} đ',
                  textAlign: TextAlign.right, style: Theme.of(context).textTheme.bodySmall),
            ],
          ),
        ),
      );
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
