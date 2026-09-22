import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import '../xac_thuc/xac_thuc.dart';
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';

/// Thêm mới (quyTrinh = null) hoặc sửa Quy trình sản xuất, kèm chọn + sắp thứ tự các khâu.
class ManSuaQuyTrinh extends ConsumerStatefulWidget {
  final QuyTrinhQuanLy? quyTrinh;

  const ManSuaQuyTrinh({super.key, this.quyTrinh});

  @override
  ConsumerState<ManSuaQuyTrinh> createState() => _ManSuaQuyTrinhState();
}

class _ManSuaQuyTrinhState extends ConsumerState<ManSuaQuyTrinh> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _maQuyTrinh;
  late final TextEditingController _tenQuyTrinh;
  late final TextEditingController _maDanhMucThucPham;
  late List<QuyTrinhKhauDong> _danhSachKhau;
  late bool _dongBoHnC;
  bool _dangLuu = false;

  bool get _laSua => widget.quyTrinh != null;

  @override
  void initState() {
    super.initState();
    final qt = widget.quyTrinh;
    _maQuyTrinh = TextEditingController(text: qt?.maQuyTrinh ?? '');
    _tenQuyTrinh = TextEditingController(text: qt?.tenQuyTrinh ?? '');
    _maDanhMucThucPham = TextEditingController(text: qt?.maDanhMucThucPham?.toString() ?? '');
    _danhSachKhau = List.of(qt?.danhSachKhau ?? const []);
    _dongBoHnC = qt?.dongBoHnC ?? true;
  }

  @override
  void dispose() {
    _maQuyTrinh.dispose();
    _tenQuyTrinh.dispose();
    _maDanhMucThucPham.dispose();
    super.dispose();
  }

  void _themKhau(Khau k) {
    if (_danhSachKhau.any((d) => d.maKhau == k.maKhau)) return;
    setState(() => _danhSachKhau.add(QuyTrinhKhauDong(
        maKhau: k.maKhau, tenKhau: k.tenKhau, thuTu: _danhSachKhau.length + 1)));
  }

  Future<void> _luu() async {
    if (!_form.currentState!.validate()) return;
    if (_danhSachKhau.isEmpty) {
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('Quy trình phải có ít nhất một khâu sản xuất.')));
      return;
    }

    setState(() => _dangLuu = true);
    try {
      final tb = await ref.read(danhMucQuanLyProvider).luuQuyTrinh(
            id: widget.quyTrinh?.id,
            dongBoHnC: _dongBoHnC,
            maQuyTrinh: _maQuyTrinh.text.trim(),
            tenQuyTrinh: _tenQuyTrinh.text.trim(),
            maDanhMucThucPham: int.tryParse(_maDanhMucThucPham.text.trim()),
            danhSachMaKhau: _danhSachKhau.map((k) => k.maKhau).toList(),
          );
      ref.invalidate(dsQuyTrinhProvider);
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
    final khau = ref.watch(dsKhauProvider);

    return Scaffold(
      appBar: AppBar(title: Text(_laSua ? 'Sửa quy trình' : 'Thêm quy trình')),
      body: Form(
        key: _form,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
          children: [
            TextFormField(
              controller: _maQuyTrinh,
              decoration: const InputDecoration(
                  labelText: 'Mã quy trình *', helperText: 'Khoá nghiệp vụ, vd QT001.', border: OutlineInputBorder()),
              validator: (v) => (v ?? '').trim().isEmpty ? 'Vui lòng nhập mã quy trình' : null,
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _tenQuyTrinh,
              decoration: const InputDecoration(labelText: 'Tên quy trình *', border: OutlineInputBorder()),
              validator: (v) => (v ?? '').trim().isEmpty ? 'Vui lòng nhập tên' : null,
            ),
            if (hanoiCheckBat) ...[
              const SizedBox(height: 14),
              TextFormField(
                controller: _maDanhMucThucPham,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(
                    labelText: 'Mã danh mục thực phẩm (HanoiCheck)',
                    helperText: 'Không bắt buộc - để trống nếu chưa rõ.', border: OutlineInputBorder()),
              ),
            ],
            const Divider(height: 32),
            Row(
              children: [
                Expanded(
                  child: Text('Các khâu (${_danhSachKhau.length})', style: Theme.of(context).textTheme.titleMedium),
                ),
              ],
            ),
            const SizedBox(height: 4),
            Text('Kéo để đổi thứ tự thực hiện.', style: Theme.of(context).textTheme.bodySmall),
            const SizedBox(height: 8),
            if (_danhSachKhau.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 8),
                child: Text('Chưa chọn khâu nào.'),
              )
            else
              ReorderableListView.builder(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                itemCount: _danhSachKhau.length,
                onReorderItem: (oldIndex, newIndex) => setState(() {
                  final k = _danhSachKhau.removeAt(oldIndex);
                  _danhSachKhau.insert(newIndex, k);
                }),
                itemBuilder: (context, i) {
                  final k = _danhSachKhau[i];
                  return Card(
                    key: ValueKey(k.maKhau),
                    margin: const EdgeInsets.only(bottom: 6),
                    child: ListTile(
                      leading: CircleAvatar(radius: 14, child: Text('${i + 1}')),
                      title: Text(k.tenHienThi),
                      subtitle: Text(k.maKhau),
                      trailing: IconButton(
                        icon: Icon(Icons.close, color: Theme.of(context).colorScheme.error),
                        onPressed: () => setState(() => _danhSachKhau.removeAt(i)),
                      ),
                    ),
                  );
                },
              ),
            const SizedBox(height: 8),
            khau.when(
              loading: () => const LinearProgressIndicator(),
              error: (e, _) => _LoiNho('Lỗi tải khâu sản xuất', () => ref.invalidate(dsKhauProvider)),
              data: (ds) {
                final chuaChon = ds.where((k) => !_danhSachKhau.any((d) => d.maKhau == k.maKhau)).toList();
                if (chuaChon.isEmpty) return const Text('Đã dùng hết khâu sản xuất đang có.');
                return DropdownButtonFormField<Khau>(
                  key: ValueKey(_danhSachKhau.length),
                  initialValue: null,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Thêm khâu', border: OutlineInputBorder()),
                  items: chuaChon.map((k) => DropdownMenuItem(value: k, child: Text(k.tenKhau))).toList(),
                  onChanged: (v) {
                    if (v != null) _themKhau(v);
                  },
                );
              },
            ),
            if (hanoiCheckBat) ...[
              const SizedBox(height: 14),
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
              : Text(_laSua ? 'Lưu thay đổi' : 'Thêm quy trình'),
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
