import 'package:flutter/material.dart';

import 'man_khau.dart';
import 'man_quy_trinh.dart';
import 'man_thuc_pham.dart';

/// Cửa vào Quản lý danh mục: Thực phẩm/SKU (+ Định mức), Khâu sản xuất, Quy trình sản xuất.
/// Chỉ quản trị/nhân viên nhập liệu thấy mục này (xem điều kiện ở man_chinh.dart).
class ManDanhMuc extends StatelessWidget {
  const ManDanhMuc({super.key});

  @override
  Widget build(BuildContext context) => ListView(
        padding: const EdgeInsets.all(12),
        children: [
          _TheDanhMuc(
            icon: Icons.fastfood_outlined,
            tieuDe: 'Thực phẩm / SKU',
            moTa: 'Nguyên liệu, thành phẩm và định mức (công thức) nguyên liệu.',
            onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const ManThucPham())),
          ),
          _TheDanhMuc(
            icon: Icons.linear_scale_outlined,
            tieuDe: 'Khâu sản xuất',
            moTa: 'Danh mục các khâu (vd sơ chế, đóng gói) dùng cho quy trình và lô sản xuất.',
            onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const ManKhauSanXuat())),
          ),
          _TheDanhMuc(
            icon: Icons.route_outlined,
            tieuDe: 'Quy trình sản xuất',
            moTa: 'Chuỗi khâu có thứ tự, gắn cho thành phẩm khi lập lệnh sản xuất.',
            onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const ManQuyTrinh())),
          ),
        ],
      );
}

class _TheDanhMuc extends StatelessWidget {
  final IconData icon;
  final String tieuDe;
  final String moTa;
  final VoidCallback onTap;

  const _TheDanhMuc({required this.icon, required this.tieuDe, required this.moTa, required this.onTap});

  @override
  Widget build(BuildContext context) => Card(
        margin: const EdgeInsets.only(bottom: 10),
        child: ListTile(
          leading: CircleAvatar(child: Icon(icon)),
          title: Text(tieuDe, style: const TextStyle(fontWeight: FontWeight.bold)),
          subtitle: Text(moTa),
          trailing: const Icon(Icons.chevron_right),
          onTap: onTap,
        ),
      );
}
