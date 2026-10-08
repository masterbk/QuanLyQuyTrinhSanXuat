// Khung ký tay (chuột trên máy tính, ngón tay/bút trên màn cảm ứng) cho màn Nhập phiếu biểu mẫu.
// Dùng Pointer Events để gộp chuột + cảm ứng; xuất ảnh PNG nền trong suốt.
window.chuKy = (function () {
    const ds = {};

    function batDau(id) {
        const c = document.getElementById(id);
        if (!c) return;
        // Khớp độ phân giải thật với kích thước hiển thị (nét không bị mờ/lệch trên màn hình độ phân giải cao).
        const tyLe = window.devicePixelRatio || 1;
        const kt = c.getBoundingClientRect();
        c.width = kt.width * tyLe;
        c.height = kt.height * tyLe;
        const g = c.getContext('2d');
        g.scale(tyLe, tyLe);
        g.lineWidth = 2.5;
        g.lineCap = 'round';
        g.lineJoin = 'round';
        g.strokeStyle = '#1a237e';
        const tt = { ve: false, coNet: false };
        ds[id] = tt;

        const diem = e => { const r = c.getBoundingClientRect(); return { x: e.clientX - r.left, y: e.clientY - r.top }; };
        c.style.touchAction = 'none';   // không cuộn trang khi ký bằng tay
        c.onpointerdown = e => { tt.ve = true; c.setPointerCapture(e.pointerId); const p = diem(e); g.beginPath(); g.moveTo(p.x, p.y); };
        c.onpointermove = e => { if (!tt.ve) return; const p = diem(e); g.lineTo(p.x, p.y); g.stroke(); tt.coNet = true; };
        c.onpointerup = c.onpointercancel = () => { tt.ve = false; };
    }

    function xoa(id) {
        const c = document.getElementById(id);
        if (!c) return;
        c.getContext('2d').clearRect(0, 0, c.width, c.height);
        if (ds[id]) ds[id].coNet = false;
    }

    // Trả base64 PNG (không kèm tiền tố data:) hoặc null nếu chưa ký.
    function layAnh(id) {
        const c = document.getElementById(id);
        if (!c || !ds[id] || !ds[id].coNet) return null;
        return c.toDataURL('image/png').split(',')[1];
    }

    return { batDau, xoa, layAnh };
})();
