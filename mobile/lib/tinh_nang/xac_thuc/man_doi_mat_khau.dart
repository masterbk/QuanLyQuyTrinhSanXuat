import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../loi/api.dart';
import 'xac_thuc.dart';

/// Kiểm tra phía app trước khi gửi (máy chủ vẫn kiểm lại đầy đủ). Trả lỗi đầu tiên hoặc null.
String? kiemTraDoiMatKhau(String hienTai, String moi, String nhapLai) {
  if (hienTai.isEmpty) return 'Vui lòng nhập mật khẩu hiện tại.';
  if (moi.length < 8) return 'Mật khẩu mới phải có ít nhất 8 ký tự.';
  if (moi == hienTai) return 'Mật khẩu mới phải khác mật khẩu hiện tại.';
  if (moi != nhapLai) return 'Mật khẩu nhập lại không khớp.';
  return null;
}

/// Người dùng tự đổi mật khẩu.
class ManDoiMatKhau extends ConsumerStatefulWidget {
  const ManDoiMatKhau({super.key});

  @override
  ConsumerState<ManDoiMatKhau> createState() => _ManDoiMatKhauState();
}

class _ManDoiMatKhauState extends ConsumerState<ManDoiMatKhau> {
  final _hienTai = TextEditingController();
  final _moi = TextEditingController();
  final _nhapLai = TextEditingController();
  bool _hien = false;
  bool _dangLuu = false;
  String? _loi;

  @override
  void dispose() {
    _hienTai.dispose();
    _moi.dispose();
    _nhapLai.dispose();
    super.dispose();
  }

  Future<void> _luu() async {
    final loi = kiemTraDoiMatKhau(_hienTai.text, _moi.text, _nhapLai.text);
    if (loi != null) {
      setState(() => _loi = loi);
      return;
    }
    setState(() { _dangLuu = true; _loi = null; });
    try {
      final tb = await ref.read(xacThucProvider.notifier).doiMatKhau(hienTai: _hienTai.text, moi: _moi.text);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(tb)));
      Navigator.pop(context);
    } on LoiApi catch (e) {
      if (mounted) setState(() => _loi = e.thongBao);
    } catch (_) {
      if (mounted) setState(() => _loi = 'Không đổi được mật khẩu. Vui lòng thử lại.');
    } finally {
      if (mounted) setState(() => _dangLuu = false);
    }
  }

  Widget _o(TextEditingController c, String nhan, {String? goiY}) => Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: TextField(
          controller: c,
          obscureText: !_hien,
          enabled: !_dangLuu,
          decoration: InputDecoration(labelText: nhan, helperText: goiY, helperMaxLines: 2,
              border: const OutlineInputBorder()),
        ),
      );

  @override
  Widget build(BuildContext context) {
    final nd = ref.watch(xacThucProvider).nguoiDung;
    return Scaffold(
      appBar: AppBar(title: const Text('Đổi mật khẩu')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (nd != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 16),
              child: Text('Tài khoản: ${nd.tenHienThi}', style: Theme.of(context).textTheme.bodyMedium),
            ),
          _o(_hienTai, 'Mật khẩu hiện tại'),
          _o(_moi, 'Mật khẩu mới',
              goiY: 'Tối thiểu 8 ký tự, gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt (vd @, #, !).'),
          _o(_nhapLai, 'Nhập lại mật khẩu mới'),
          CheckboxListTile(
            value: _hien,
            onChanged: (v) => setState(() => _hien = v ?? false),
            title: const Text('Hiện mật khẩu'),
            contentPadding: EdgeInsets.zero,
            controlAffinity: ListTileControlAffinity.leading,
          ),
          if (_loi != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: Text(_loi!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ),
          FilledButton(
            onPressed: _dangLuu ? null : _luu,
            child: _dangLuu
                ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Đổi mật khẩu'),
          ),
          const SizedBox(height: 12),
          Text('Sau khi đổi, máy này vẫn đăng nhập; các thiết bị khác phải đăng nhập lại bằng mật khẩu mới.',
              style: Theme.of(context).textTheme.bodySmall),
        ],
      ),
    );
  }
}
