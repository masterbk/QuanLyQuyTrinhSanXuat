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
  late final TextEditingController _quyDoiGam;
  late String _loaiSanPham;
  int? _donViTinhHnCId;
  bool _daKhopDonVi = false;
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
    // Hàng của cơ sở gần như luôn là hàng Việt Nam, và HanoiCheck yêu cầu khai xuất xứ -> điền sẵn.
    _quocGia = TextEditingController(text: sp?.quocGia ?? 'Việt Nam');
    _moTa = TextEditingController(text: sp?.moTa ?? '');
    _quyDoiGam = TextEditingController(text: sp?.quyDoiGam == null ? '' : _boSo0(sp!.quyDoiGam!));
    _donViTinhHnCId = sp?.donViTinhHnCId;
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
    _quyDoiGam.dispose();
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
      // null = không khai -> máy chủ giữ nguyên cấu hình đơn vị tính đang lưu.
      donViTinhHnCId: _donViTinhHnCId,
      quyDoiGam: _donViTinhHnCId == null
          ? null
          : double.tryParse(_quyDoiGam.text.trim().replaceAll(',', '.')),
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

  /// Ô "Đơn vị tính" dạng chữ - dùng khi chưa tải được danh mục đơn vị tính của HanoiCheck
  /// (cơ sở chưa bật đồng bộ, hoặc danh mục chưa tải về) để vẫn khai được đơn vị cho tồn kho.
  Widget _oDonViTinhChu({bool dangTaiDanhMuc = false}) => TextFormField(
        controller: _donViTinh,
        decoration: InputDecoration(
            labelText: 'Đơn vị tính',
            helperText: dangTaiDanhMuc
                ? 'Đang tải danh mục đơn vị tính...'
                : 'vd kg, cái, quả. Dùng cho tồn kho.',
            helperMaxLines: 2,
            border: const OutlineInputBorder()),
      );

  /// Dropdown có tìm kiếm trên danh mục đơn vị tính của HanoiCheck. Chọn xong gán cả measure_id
  /// (gửi HanoiCheck) và tên đơn vị (hiển thị tồn kho) để hai chỗ không lệch nhau.
  Widget _oDonViTinh(List<DonViTinhHnC> ds) {
    if (ds.isEmpty) return _oDonViTinhChu();

    // Lần đầu dựng: chưa có measure_id thì khớp theo TÊN đang lưu (dữ liệu cũ khai bằng chữ).
    if (!_daKhopDonVi) {
      _daKhopDonVi = true;
      if (_donViTinhHnCId == null) {
        final ten = _donViTinh.text.trim().toLowerCase();
        for (final d in ds) {
          if (d.ten.trim().toLowerCase() == ten) {
            _donViTinhHnCId = d.id;
            break;
          }
        }
      }
    }

    final daChon = ds.any((d) => d.id == _donViTinhHnCId) ? _donViTinhHnCId : null;
    final conChuaKhop = daChon == null && _donViTinh.text.trim().isNotEmpty;

    return DropdownMenu<int>(
      initialSelection: daChon,
      enableFilter: true,
      requestFocusOnTap: true,
      expandedInsets: EdgeInsets.zero,
      label: const Text('Đơn vị tính'),
      helperText: conChuaKhop
          ? 'Đang lưu "${_donViTinh.text.trim()}" - chưa khớp danh mục HanoiCheck, chọn lại giúp tôi.'
          : 'Gõ để tìm. Dùng cho tồn kho và gửi HanoiCheck.',
      inputDecorationTheme: const InputDecorationTheme(border: OutlineInputBorder()),
      dropdownMenuEntries: [for (final d in ds) DropdownMenuEntry(value: d.id, label: d.ten)],
      onSelected: (v) => setState(() {
        _donViTinhHnCId = v;
        if (v != null) {
          for (final d in ds) {
            if (d.id == v) {
              _donViTinh.text = d.ten;
              break;
            }
          }
        }
      }),
    );
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
            ref.watch(dsDonViTinhProvider).when(
                  loading: () => _oDonViTinhChu(dangTaiDanhMuc: true),
                  error: (e, _) => _oDonViTinhChu(),
                  data: _oDonViTinh,
                ),
            if (_donViTinhHnCId != null) ...[
              const SizedBox(height: 14),
              TextFormField(
                controller: _quyDoiGam,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                decoration: const InputDecoration(
                    labelText: 'Quy đổi ra gam',
                    helperText: '1 đơn vị tính bằng bao nhiêu gam, vd Kg = 1000. Để trống nếu không khai.',
                    helperMaxLines: 2,
                    border: OutlineInputBorder()),
              ),
            ],
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
              // HanoiCheck: thực phẩm chỉ gắn quy trình loại thực phẩm, bỏ quy trình món ăn.
              data: (tatCa) => DropdownButtonFormField<String>(
                initialValue: tatCa.any((q) => !q.laMonAn && q.maQuyTrinh == _maQuyTrinh) ? _maQuyTrinh : null,
                isExpanded: true,
                decoration: const InputDecoration(
                    labelText: 'Quy trình sản xuất',
                    helperText: 'Bắt buộc với thành phẩm đồng bộ HanoiCheck.', border: OutlineInputBorder()),
                items: [
                  const DropdownMenuItem(value: null, child: Text('(không chọn)')),
                  ...tatCa.where((q) => !q.laMonAn)
                      .map((q) => DropdownMenuItem(value: q.maQuyTrinh, child: Text('${q.maQuyTrinh} — ${q.tenQuyTrinh}'))),
                ],
                onChanged: (v) => setState(() => _maQuyTrinh = v),
              ),
            ),
            if (hanoiCheckBat && laThanhPham) ...[
              const SizedBox(height: 14),
              TextFormField(
                controller: _maLoaiSp,
                maxLines: 1,
                decoration: const InputDecoration(
                    labelText: 'Mã loại thực phẩm',
                    // v2.7.1: danh mục quyết định đơn vị tính gửi HanoiCheck, nên đổi danh mục cũng là đổi đơn
                    // vị tính - HnC không cho đổi khi thực phẩm đang nằm trong đơn trường chưa xác nhận.
                    helperText: 'Danh mục thực phẩm chuẩn HanoiCheck. Đổi danh mục có thể đổi đơn vị tính '
                        'nên không đổi được khi thực phẩm đang trong đơn chờ trường xác nhận.',
                    helperMaxLines: 3,
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
