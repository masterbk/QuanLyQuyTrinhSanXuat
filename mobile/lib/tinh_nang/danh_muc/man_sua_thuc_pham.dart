import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import '../xac_thuc/xac_thuc.dart';
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';

/// Thêm mới (sanPham = null) hoặc sửa Thực phẩm/SKU.
class ManSuaThucPham extends ConsumerStatefulWidget {
  final SanPham? sanPham;

  const ManSuaThucPham({super.key, this.sanPham});

  @override
  ConsumerState<ManSuaThucPham> createState() => _ManSuaThucPhamState();
}

class _ManSuaThucPhamState extends ConsumerState<ManSuaThucPham> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _maSanPham;
  late final TextEditingController _tenSanPham;
  late final TextEditingController _donViTinh;
  late final TextEditingController _tonToiThieu;
  late final TextEditingController _maLoaiSp;
  late final TextEditingController _maThucPhamChuan;
  late final TextEditingController _gtin;
  late final TextEditingController _quocGia;
  late final TextEditingController _moTa;
  late String _loaiSanPham;
  String? _maQuyTrinh;
  late bool _dongBoHnC;
  bool _dangLuu = false;

  bool get _laSua => widget.sanPham != null;

  @override
  void initState() {
    super.initState();
    final sp = widget.sanPham;
    _maSanPham = TextEditingController(text: sp?.maSanPham ?? '');
    _tenSanPham = TextEditingController(text: sp?.tenSanPham ?? '');
    _donViTinh = TextEditingController(text: sp?.donViTinh ?? '');
    _tonToiThieu = TextEditingController(text: sp?.tonToiThieu == null ? '' : _boSo0(sp!.tonToiThieu!));
    _maLoaiSp = TextEditingController(text: sp?.maLoaiSp ?? '');
    _maThucPhamChuan = TextEditingController(text: sp?.maThucPhamChuan ?? '');
    _gtin = TextEditingController(text: sp?.gtin ?? '');
    _quocGia = TextEditingController(text: sp?.quocGia ?? '');
    _moTa = TextEditingController(text: sp?.moTa ?? '');
    _loaiSanPham = sp?.loaiSanPham ?? 'ThanhPham';
    _maQuyTrinh = sp?.maQuyTrinh;
    _dongBoHnC = sp?.dongBoHnC ?? true;
  }

  @override
  void dispose() {
    _maSanPham.dispose();
    _tenSanPham.dispose();
    _donViTinh.dispose();
    _tonToiThieu.dispose();
    _maLoaiSp.dispose();
    _maThucPhamChuan.dispose();
    _gtin.dispose();
    _quocGia.dispose();
    _moTa.dispose();
    super.dispose();
  }

  static String _boSo0(double v) {
    final s = v.toStringAsFixed(3);
    return s.contains('.') ? s.replaceAll(RegExp(r'0+$'), '').replaceAll(RegExp(r'\.$'), '') : s;
  }

  Future<void> _luu() async {
    if (!_form.currentState!.validate()) return;
    setState(() => _dangLuu = true);
    final sp = SanPham(
      id: widget.sanPham?.id ?? 0,
      dongBoHnC: _dongBoHnC,
      maSanPham: _maSanPham.text.trim(),
      tenSanPham: _tenSanPham.text.trim(),
      loaiSanPham: _loaiSanPham,
      donViTinh: _donViTinh.text.trim().isEmpty ? null : _donViTinh.text.trim(),
      tonToiThieu: double.tryParse(_tonToiThieu.text.trim().replaceAll(',', '.')),
      maLoaiSp: _maLoaiSp.text.trim(),
      maThucPhamChuan: _maThucPhamChuan.text.trim().isEmpty ? null : _maThucPhamChuan.text.trim(),
      gtin: _gtin.text.trim().isEmpty ? null : _gtin.text.trim(),
      quocGia: _quocGia.text.trim().isEmpty ? null : _quocGia.text.trim(),
      moTa: _moTa.text.trim().isEmpty ? null : _moTa.text.trim(),
      maQuyTrinh: _maQuyTrinh,
    );
    try {
      final tb = await ref.read(danhMucQuanLyProvider).luuThucPham(sp);
      ref.invalidate(dsThucPhamProvider);
      if (!mounted) return;
      Navigator.pop(context, tb);
    } on LoiApi catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.thongBao)));
    } finally {
      if (mounted) setState(() => _dangLuu = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final hanoiCheckBat = ref.watch(hanoiCheckBatProvider).maybeWhen(data: (v) => v, orElse: () => false);
    final laThanhPham = _loaiSanPham == 'ThanhPham';
    final quyTrinh = ref.watch(dsQuyTrinhProvider);

    return Scaffold(
      appBar: AppBar(title: Text(_laSua ? 'Sửa thực phẩm' : 'Thêm thực phẩm')),
      body: Form(
        key: _form,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
          children: [
            TextFormField(
              controller: _maSanPham,
              decoration: const InputDecoration(
                  labelText: 'Mã SKU *', helperText: 'Khoá nghiệp vụ, vd SP001.', border: OutlineInputBorder()),
              validator: (v) => (v ?? '').trim().isEmpty ? 'Vui lòng nhập mã SKU' : null,
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _tenSanPham,
              decoration: const InputDecoration(labelText: 'Tên thực phẩm *', border: OutlineInputBorder()),
              validator: (v) => (v ?? '').trim().isEmpty ? 'Vui lòng nhập tên' : null,
            ),
            const SizedBox(height: 14),
            DropdownButtonFormField<String>(
              initialValue: _loaiSanPham,
              decoration: const InputDecoration(labelText: 'Loại', border: OutlineInputBorder()),
              items: const [
                DropdownMenuItem(value: 'ThanhPham', child: Text('Thành phẩm (để bán)')),
                DropdownMenuItem(value: 'NguyenLieu', child: Text('Nguyên liệu (đầu vào)')),
              ],
              onChanged: (v) => setState(() => _loaiSanPham = v ?? 'ThanhPham'),
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _donViTinh,
              decoration: const InputDecoration(
                  labelText: 'Đơn vị tính', helperText: 'vd kg, cái, quả. Dùng cho tồn kho.',
                  border: OutlineInputBorder()),
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _tonToiThieu,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: const InputDecoration(
                  labelText: 'Tồn tối thiểu', helperText: 'Cảnh báo khi tồn dưới mức này. Để trống nếu không cần.',
                  border: OutlineInputBorder()),
            ),
            const SizedBox(height: 14),
            quyTrinh.when(
              loading: () => const LinearProgressIndicator(),
              error: (e, _) => _LoiNho('Lỗi tải quy trình', () => ref.invalidate(dsQuyTrinhProvider)),
              data: (ds) => DropdownButtonFormField<String>(
                initialValue: ds.any((q) => q.maQuyTrinh == _maQuyTrinh) ? _maQuyTrinh : null,
                isExpanded: true,
                decoration: const InputDecoration(
                    labelText: 'Quy trình sản xuất',
                    helperText: 'Bắt buộc với thành phẩm đồng bộ HanoiCheck.', border: OutlineInputBorder()),
                items: [
                  const DropdownMenuItem(value: null, child: Text('(không chọn)')),
                  ...ds.map((q) => DropdownMenuItem(value: q.maQuyTrinh, child: Text('${q.maQuyTrinh} — ${q.tenQuyTrinh}'))),
                ],
                onChanged: (v) => setState(() => _maQuyTrinh = v),
              ),
            ),
            if (hanoiCheckBat && laThanhPham) ...[
              const SizedBox(height: 14),
              TextFormField(
                controller: _maLoaiSp,
                decoration: const InputDecoration(
                    labelText: 'Mã loại thực phẩm', helperText: 'Mã danh mục thực phẩm chuẩn HanoiCheck, vd THIT.',
                    border: OutlineInputBorder()),
                validator: (v) => (_dongBoHnC && (v ?? '').trim().isEmpty)
                    ? 'Vui lòng nhập mã loại thực phẩm'
                    : null,
              ),
            ],
            const SizedBox(height: 14),
            TextFormField(
              controller: _gtin,
              decoration: const InputDecoration(labelText: 'Mã vạch GTIN', border: OutlineInputBorder()),
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _maThucPhamChuan,
              decoration: const InputDecoration(
                  labelText: 'Mã thực phẩm chuẩn quốc gia', helperText: 'Nếu có, vd VN-0001.',
                  border: OutlineInputBorder()),
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _quocGia,
              decoration: const InputDecoration(labelText: 'Quốc gia xuất xứ', border: OutlineInputBorder()),
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _moTa,
              maxLines: 2,
              decoration: const InputDecoration(labelText: 'Mô tả', border: OutlineInputBorder()),
            ),
            if (hanoiCheckBat) ...[
              const SizedBox(height: 4),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Đồng bộ HanoiCheck'),
                value: _dongBoHnC,
                onChanged: (v) => setState(() => _dongBoHnC = v),
              ),
            ],
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
              : Text(_laSua ? 'Lưu thay đổi' : 'Thêm thực phẩm'),
        ),
      ),
    );
  }
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
