import 'dart:io' show Platform;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'xac_thuc.dart';

class ManDangNhap extends ConsumerStatefulWidget {
  const ManDangNhap({super.key});

  @override
  ConsumerState<ManDangNhap> createState() => _ManDangNhapState();
}

class _ManDangNhapState extends ConsumerState<ManDangNhap> {
  final _form = GlobalKey<FormState>();
  final _mayChu = TextEditingController();
  final _email = TextEditingController();
  final _matKhau = TextEditingController();
  bool _hienMatKhau = false;
  bool _dangGui = false;

  @override
  void initState() {
    super.initState();
    _dienLaiThongTinCu();
  }

  /// Lần sau mở app chỉ phải gõ mật khẩu.
  Future<void> _dienLaiThongTinCu() async {
    final luuTru = ref.read(luuTruProvider);
    final mayChu = await luuTru.mayChu();
    final email = await luuTru.email();
    if (!mounted) return;
    setState(() {
      _mayChu.text = mayChu ?? '';
      _email.text = email ?? '';
    });
  }

  @override
  void dispose() {
    _mayChu.dispose();
    _email.dispose();
    _matKhau.dispose();
    super.dispose();
  }

  Future<void> _dangNhap() async {
    if (!_form.currentState!.validate()) return;
    setState(() => _dangGui = true);
    await ref.read(xacThucProvider.notifier).dangNhap(
          mayChu: _mayChu.text,
          email: _email.text,
          matKhau: _matKhau.text,
          thietBi: _tenThietBi(),
        );
    if (mounted) setState(() => _dangGui = false);
  }

  String _tenThietBi() {
    try {
      return Platform.operatingSystem == 'android' ? 'Điện thoại Android' : Platform.operatingSystem;
    } catch (_) {
      return 'Thiết bị';
    }
  }

  @override
  Widget build(BuildContext context) {
    final tt = ref.watch(xacThucProvider);

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Form(
                key: _form,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Icon(Icons.bakery_dining,
                        size: 64, color: Theme.of(context).colorScheme.primary),
                    const SizedBox(height: 12),
                    Text('Quản lý sản xuất',
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.headlineSmall
                            ?.copyWith(fontWeight: FontWeight.bold)),
                    const SizedBox(height: 4),
                    Text('Đơn hàng và lệnh sản xuất',
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.bodyMedium
                            ?.copyWith(color: Theme.of(context).hintColor)),
                    const SizedBox(height: 28),
                    TextFormField(
                      controller: _mayChu,
                      keyboardType: TextInputType.url,
                      autocorrect: false,
                      decoration: const InputDecoration(
                        labelText: 'Địa chỉ máy chủ',
                        hintText: 'https://quanly.tenmiencuaban.vn',
                        prefixIcon: Icon(Icons.dns_outlined),
                        border: OutlineInputBorder(),
                      ),
                      validator: (v) {
                        final s = (v ?? '').trim();
                        if (s.isEmpty) return 'Nhập địa chỉ máy chủ';
                        if (!s.startsWith('http://') && !s.startsWith('https://')) {
                          return 'Địa chỉ phải bắt đầu bằng http:// hoặc https://';
                        }
                        return null;
                      },
                    ),
                    const SizedBox(height: 14),
                    TextFormField(
                      controller: _email,
                      keyboardType: TextInputType.emailAddress,
                      autocorrect: false,
                      decoration: const InputDecoration(
                        labelText: 'Email',
                        prefixIcon: Icon(Icons.person_outline),
                        border: OutlineInputBorder(),
                      ),
                      validator: (v) => (v ?? '').trim().isEmpty ? 'Nhập email' : null,
                    ),
                    const SizedBox(height: 14),
                    TextFormField(
                      controller: _matKhau,
                      obscureText: !_hienMatKhau,
                      onFieldSubmitted: (_) => _dangGui ? null : _dangNhap(),
                      decoration: InputDecoration(
                        labelText: 'Mật khẩu',
                        prefixIcon: const Icon(Icons.lock_outline),
                        border: const OutlineInputBorder(),
                        suffixIcon: IconButton(
                          icon: Icon(_hienMatKhau ? Icons.visibility_off : Icons.visibility),
                          onPressed: () => setState(() => _hienMatKhau = !_hienMatKhau),
                        ),
                      ),
                      validator: (v) => (v ?? '').isEmpty ? 'Nhập mật khẩu' : null,
                    ),
                    if (tt.loi != null) ...[
                      const SizedBox(height: 16),
                      Container(
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: Theme.of(context).colorScheme.errorContainer,
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: Row(children: [
                          const Icon(Icons.error_outline, size: 20),
                          const SizedBox(width: 8),
                          Expanded(child: Text(tt.loi!)),
                        ]),
                      ),
                    ],
                    const SizedBox(height: 24),
                    FilledButton(
                      onPressed: _dangGui ? null : _dangNhap,
                      style: FilledButton.styleFrom(
                          padding: const EdgeInsets.symmetric(vertical: 16)),
                      child: _dangGui
                          ? const SizedBox(
                              height: 20, width: 20,
                              child: CircularProgressIndicator(strokeWidth: 2))
                          : const Text('Đăng nhập'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
