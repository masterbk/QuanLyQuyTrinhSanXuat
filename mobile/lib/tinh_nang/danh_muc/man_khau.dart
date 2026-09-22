import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import '../xac_thuc/xac_thuc.dart';
import 'kho_du_lieu.dart';
import 'mo_hinh.dart';

/// Danh mục Khâu sản xuất: mã do hệ thống cấp, không sửa được - chỉ sửa tên/ghi chú.
class ManKhauSanXuat extends ConsumerWidget {
  const ManKhauSanXuat({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final ds = ref.watch(dsKhauProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Khâu sản xuất')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _moForm(context, ref, null),
        icon: const Icon(Icons.add),
        label: const Text('Thêm khâu'),
      ),
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(dsKhauProvider),
        child: ds.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => _Loi(
            thongBao: e is LoiApi ? e.thongBao : 'Không tải được danh sách: $e',
            thuLai: () => ref.invalidate(dsKhauProvider),
          ),
          data: (khau) => khau.isEmpty
              ? ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(24, 80, 24, 24),
                  children: const [
                    Icon(Icons.linear_scale_outlined, size: 56, color: Colors.grey),
                    SizedBox(height: 12),
                    Text('Chưa có khâu sản xuất nào.', textAlign: TextAlign.center),
                  ],
                )
              : ListView.separated(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(8, 8, 8, 88),
                  itemCount: khau.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 4),
                  itemBuilder: (c, i) => _TheKhau(khau: khau[i]),
                ),
        ),
      ),
    );
  }

  void _moForm(BuildContext context, WidgetRef ref, Khau? k) =>
      showDialog<void>(context: context, builder: (_) => _HopKhau(khau: k));
}

class _TheKhau extends ConsumerWidget {
  final Khau khau;

  const _TheKhau({required this.khau});

  @override
  Widget build(BuildContext context, WidgetRef ref) => Card(
        margin: EdgeInsets.zero,
        child: ListTile(
          title: Text(khau.tenKhau, style: const TextStyle(fontWeight: FontWeight.bold)),
          subtitle: Text([khau.maKhau, if (khau.ghiChu?.isNotEmpty ?? false) khau.ghiChu].join(' · ')),
          trailing: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              IconButton(
                icon: const Icon(Icons.edit_outlined),
                onPressed: () => showDialog<void>(context: context, builder: (_) => _HopKhau(khau: khau)),
              ),
              IconButton(
                icon: Icon(Icons.delete_outline, color: Theme.of(context).colorScheme.error),
                onPressed: () => _xacNhanXoa(context, ref, khau),
              ),
            ],
          ),
        ),
      );

  Future<void> _xacNhanXoa(BuildContext context, WidgetRef ref, Khau k) async {
    final dongY = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('Xác nhận xoá'),
        content: Text('Xoá khâu "${k.tenKhau}" (${k.maKhau})?'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Huỷ')),
          FilledButton(onPressed: () => Navigator.pop(c, true), child: const Text('Xoá')),
        ],
      ),
    );
    if (dongY != true) return;
    try {
      final tb = await ref.read(danhMucQuanLyProvider).xoaKhau(k.id);
      ref.invalidate(dsKhauProvider);
      if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
    } on LoiApi catch (e) {
      if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.thongBao)));
    }
  }
}

class _HopKhau extends ConsumerStatefulWidget {
  final Khau? khau;

  const _HopKhau({this.khau});

  @override
  ConsumerState<_HopKhau> createState() => _HopKhauState();
}

class _HopKhauState extends ConsumerState<_HopKhau> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _ten;
  late final TextEditingController _ghiChu;
  late bool _dongBoHnC;
  bool _dangLuu = false;

  @override
  void initState() {
    super.initState();
    _ten = TextEditingController(text: widget.khau?.tenKhau ?? '');
    _ghiChu = TextEditingController(text: widget.khau?.ghiChu ?? '');
    _dongBoHnC = widget.khau?.dongBoHnC ?? true;
  }

  @override
  void dispose() {
    _ten.dispose();
    _ghiChu.dispose();
    super.dispose();
  }

  Future<void> _luu() async {
    if (!_form.currentState!.validate()) return;
    setState(() => _dangLuu = true);
    try {
      final tb = await ref.read(danhMucQuanLyProvider).luuKhau(
            id: widget.khau?.id,
            dongBoHnC: _dongBoHnC,
            tenKhau: _ten.text.trim(),
            ghiChu: _ghiChu.text.trim().isEmpty ? null : _ghiChu.text.trim(),
          );
      ref.invalidate(dsKhauProvider);
      if (!mounted) return;
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
    final hanoiCheckBat = ref.watch(hanoiCheckBatProvider).maybeWhen(data: (v) => v, orElse: () => false);
    return AlertDialog(
      title: Text(widget.khau == null ? 'Thêm khâu sản xuất' : 'Sửa khâu sản xuất'),
      content: Form(
        key: _form,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (widget.khau != null) ...[
                Align(
                  alignment: Alignment.centerLeft,
                  child: Text('Mã khâu: ${widget.khau!.maKhau}', style: Theme.of(context).textTheme.bodySmall),
                ),
                const SizedBox(height: 8),
              ],
              TextFormField(
                controller: _ten,
                autofocus: true,
                decoration: const InputDecoration(labelText: 'Tên khâu *', border: OutlineInputBorder()),
                validator: (v) => (v ?? '').trim().isEmpty ? 'Vui lòng nhập tên khâu' : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _ghiChu,
                maxLines: 2,
                decoration: const InputDecoration(labelText: 'Ghi chú', border: OutlineInputBorder()),
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
      ),
      actions: [
        TextButton(onPressed: _dangLuu ? null : () => Navigator.pop(context), child: const Text('Huỷ')),
        FilledButton(onPressed: _dangLuu ? null : _luu, child: const Text('Lưu')),
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
