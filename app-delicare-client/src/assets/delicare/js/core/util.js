/* Tiện ích định dạng, icon, nhãn cột */
(function () {
  const pad = n => String(n).padStart(2, '0');
  const UTIL = {
    uuid() { return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => { const r = Math.random() * 16 | 0; return (c === 'x' ? r : (r & 3 | 8)).toString(16); }); },
    iso(d) { return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes()); },
    esc(s) { return s === null || s === undefined ? '' : String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c])); },
    num(v, dg) { if (v === null || v === undefined || v === '') return ''; const n = Number(v); if (isNaN(n)) return UTIL.esc(v); return n.toLocaleString('vi-VN', { maximumFractionDigits: dg === undefined ? 2 : dg }); },
    money(v) { if (v === null || v === undefined || v === '') return ''; return Number(v).toLocaleString('vi-VN', { maximumFractionDigits: 0 }) + ' ₫'; },
    // UI-2R fix (D-80 d): thẻ chỉ số gọn – tiền ≥ 10 triệu rút gọn "34,65 tr ₫" (≥ 1 tỷ: "1,25 tỷ ₫"), title hiện số đầy đủ. Trả HTML.
    moneyShort(v) { if (v === null || v === undefined || v === '') return ''; const n = Number(v), a = Math.abs(n), full = UTIL.money(n); if (a < 1e7) return full; const num = (n / (a >= 1e9 ? 1e9 : 1e6)).toLocaleString('vi-VN', { maximumFractionDigits: 2 }), unit = a >= 1e9 ? 'tỷ ₫' : 'tr ₫'; return `<span title="${full}">${num}<span class="u"> ${unit}</span></span>`; },
    pct(v, dg = 1) { if (v === null || v === undefined || isNaN(v)) return '–'; return Number(v).toLocaleString('vi-VN', { maximumFractionDigits: dg }) + '%'; },
    date(v) { if (!v) return ''; const s = String(v); const m = s.match(/^(\d{4})-(\d{2})-(\d{2})/); return m ? m[3] + '/' + m[2] + '/' + m[1] : s; },
    dt(v) { if (!v) return ''; const s = String(v); const m = s.match(/^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})/); return m ? m[3] + '/' + m[2] + ' ' + m[4] + ':' + m[5] : UTIL.date(s); },
    time(v) { if (!v) return ''; const m = String(v).match(/(\d{2}):(\d{2})/); return m ? m[1] + ':' + m[2] : v; },
    day(v) { return v ? String(v).slice(0, 10) : ''; },
    addDays(ymd, n) { const d = new Date(ymd + 'T00:00'); d.setDate(d.getDate() + n); return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()); },
    sum(arr, f) { return arr.reduce((a, r) => a + (Number(typeof f === 'function' ? f(r) : r[f]) || 0), 0); },
    group(arr, f) { const o = {}; arr.forEach(r => { const k = typeof f === 'function' ? f(r) : r[f]; (o[k] = o[k] || []).push(r); }); return o; },
    defVal(def) {
      if (!def) return null; let v = def.replace(/^\(+|\)+$/g, '');
      if (v.startsWith("N'")) return v.slice(2, -1); if (v.startsWith("'")) { v = v.slice(1, -1); return v === '00000000-0000-0000-0000-000000000000' ? null : v; }
      if (/getdate|sysdatetime|sysutcdatetime/i.test(v)) return UTIL.iso(new Date()); if (/newid/i.test(v)) return null;
      const n = Number(v); return isNaN(n) ? null : n;
    },
    initials(s) { return (s || '?').split(/\s+/).slice(-2).map(x => x[0]).join('').toUpperCase(); },
    /** UI-6.10: tên file không dấu từ tên màn (vd "Diem-giao-hang"). */
    fileName(s) { return String(s || 'DELICARE').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').replace(/[^A-Za-z0-9]+/g, '-').replace(/^-|-$/g, ''); },
    csv(rows, cols, filename) {
      const lines = [cols.map(c => '"' + String(c.label).replace(/<[^>]*>/g, '').replace(/"/g, '""') + '"').join(',')]; // UI-6.10 (D-88): tiêu đề cột chỉ tiếng Việt, bỏ tên cột kỹ thuật
      rows.forEach(r => lines.push(cols.map(c => '"' + String(c.text ? c.text(r) : (r[c.k] ?? '')).replace(/"/g, '""') + '"').join(',')));
      const blob = new Blob(['﻿' + lines.join('\n')], { type: 'text/csv;charset=utf-8' });
      const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = filename; document.body.appendChild(a); a.click(); setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 500);
    }
  };
  const P = {
    // UI-2R (D-69 b): biểu tượng dòng cốt lõi của khối thông tin.
    phone: '<path d="M5 4h4l2 5-2.5 1.5a11 11 0 0 0 5 5L15 13l5 2v4a2 2 0 0 1-2 2A16 16 0 0 1 3 6a2 2 0 0 1 2-2"/>', mail: '<path d="M3 7a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><path d="m3 7 9 6 9-6"/>',
    user: '<circle cx="12" cy="7" r="4"/><path d="M6 21v-2a4 4 0 0 1 4-4h4a4 4 0 0 1 4 4v2"/>', star: '<path d="m12 17.8-6.2 3.2 1.2-6.9-5-4.9 6.9-1L12 2l3.1 6.2 6.9 1-5 4.9 1.2 6.9z"/>',
    tag: '<path d="M3 12V4a1 1 0 0 1 1-1h8l9 9-9 9z"/><circle cx="7.5" cy="7.5" r="1.5"/>', clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 3"/>',
    copy: '<rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2"/>', lock: '<rect x="5" y="11" width="14" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/>',
    chevron: '<path d="m6 9 6 6 6-6"/>',
    home: '<rect x="3" y="3" width="8" height="8" rx="1"/><rect x="13" y="3" width="8" height="5" rx="1"/><rect x="13" y="10" width="8" height="11" rx="1"/><rect x="3" y="13" width="8" height="8" rx="1"/>',
    building: '<path d="M3 21h18M5 21V7l7-4 7 4v14M9 21v-5h6v5M9 9h.01M15 9h.01M9 12h.01M15 12h.01"/>',
    clipboard: '<rect x="5" y="4" width="14" height="17" rx="2"/><path d="M9 4V2h6v2M9 9h6M9 13h6M9 17h3"/>',
    calendar: '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M16 3v4M8 3v4M3 10h18"/>',
    utensils: '<path d="M7 3v7M4 3v4a3 3 0 0 0 6 0V3M7 10v11M17 3c-2 3-2 7 0 9h3V3M18 12v9"/>',
    factory: '<path d="M3 21V9l6 3V9l6 3V5l6 3v13zM7 17h.01M12 17h.01M17 17h.01"/>',
    chef: '<path d="M6 12a4 4 0 1 1 2-7 5 5 0 0 1 9 2 3.5 3.5 0 1 1 1 6H6zM6 12v8h12v-8M9 16h6"/>',
    check: '<rect x="5" y="4" width="14" height="17" rx="2"/><path d="M9 4V2h6v2M8 12l2 2 5-5"/>',
    package: '<path d="m12 3 9 5-9 5-9-5 9-5zM3 8v9l9 5 9-5V8M12 13v9"/>',
    warehouse: '<path d="M3 21V8l9-5 9 5v13M3 10h18M7 21v-7h10v7"/>',
    down: '<path d="M12 3v12m-5-5 5 5 5-5M4 21h16"/>', up: '<path d="M12 15V3m-5 5 5-5 5 5M4 21h16"/>',
    truck: '<path d="M3 6h12v12H3zM15 10h4l3 4v4h-7M7 18a2 2 0 1 0 4 0m6 0a2 2 0 1 0 4 0"/>',
    pin: '<path d="M20 10c0 5-8 11-8 11S4 15 4 10a8 8 0 1 1 16 0z"/><circle cx="12" cy="10" r="2.5"/>',
    route: '<circle cx="6" cy="18" r="2"/><circle cx="18" cy="6" r="2"/><path d="M8 18h4a3 3 0 0 0 3-3V9a3 3 0 0 1 3-3"/>',
    recycle: '<path d="M7 19H4l3-5M17 19h3l-3-5M12 4l-2 4h4z"/><path d="M7 14 12 6l5 8"/>',
    calc: '<rect x="4" y="3" width="16" height="18" rx="2"/><path d="M8 7h8M8 12h2m4 0h2M8 16h2m4 0h2"/>',
    chart: '<path d="M3 3v18h18M7 14l4-4 4 3 6-8"/>',
    database: '<ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v14c0 1.7 3.6 3 8 3s8-1.3 8-3V5M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3"/>',
    users: '<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8M22 21v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8"/>',
    shield: '<path d="M12 22s8-4 8-11V5l-8-3-8 3v6c0 7 8 11 8 11zM9 12l2 2 4-4"/>',
    history: '<path d="M3 12a9 9 0 1 0 2.6-6.4L3 8M3 3v5h5M12 7v5l3 2"/>',
    settings: '<circle cx="12" cy="12" r="3"/><path d="M12 2v3M12 19v3M4.2 4.2l2.1 2.1M17.7 17.7l2.1 2.1M2 12h3M19 12h3M4.2 19.8l2.1-2.1M17.7 6.3l2.1-2.1"/>',
    bell: '<path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4"/>',
    help: '<circle cx="12" cy="12" r="9"/><path d="M9.6 9a2.5 2.5 0 1 1 4.3 1.7c-1.1 1-1.9 1.4-1.9 3M12 17.5h.01"/>',
    search: '<circle cx="11" cy="11" r="7"/><path d="m20 20-4-4"/>',
    menu: '<path d="M4 6h16M4 12h16M4 18h16"/>', panel: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M9 4v16"/>',
    plus: '<path d="M12 5v14M5 12h14"/>', edit: '<path d="M12 20h9M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z"/>', eye: '<path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z"/><circle cx="12" cy="12" r="3"/>',
    trash: '<path d="M3 6h18M8 6V4h8v2M6 6l1 14h10l1-14"/>', x: '<path d="M18 6 6 18M6 6l12 12"/>', ok: '<path d="m5 12 5 5L20 7"/>',
    alert: '<path d="m12 3 10 18H2L12 3zM12 9v5m0 3h.01"/>', export: '<path d="M12 3v12M7 8l5-5 5 5M4 15v5h16v-5"/>', link: '<path d="M10 13a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-1 1M14 11a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l1-1"/>',
    flow: '<circle cx="5" cy="6" r="2"/><circle cx="19" cy="6" r="2"/><circle cx="12" cy="18" r="2"/><path d="M7 6h10M6 8l5 8M18 8l-5 8"/>',
    thermo: '<path d="M14 14.8V4a2 2 0 0 0-4 0v10.8a4 4 0 1 0 4 0z"/>', bot: '<rect x="4" y="8" width="16" height="12" rx="3"/><path d="M12 4v4M9 13h.01M15 13h.01M9 17h6"/>',
    msg: '<path d="M21 12a8 8 0 0 1-12 7l-6 2 2-6A8 8 0 1 1 21 12z"/>', lot: '<path d="M4 7h16v13H4zM8 7V4h8v3M9 12h6"/>', approve: '<path d="M9 11l3 3 8-8M20 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/>',
    reset: '<path d="M3 12a9 9 0 1 0 3-6.7L3 8M3 3v5h5"/>',
    // UI-1 (D-64): thẻ khách hàng – tích tròn (đang hoạt động), văn bản (hợp đồng).
    'check-circle': '<circle cx="12" cy="12" r="9"/><path d="M8 12.5l2.7 2.7L16 9.8"/>',
    // FE-E.2: in mã QR.
    print: '<path d="M6 9V3h12v6M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="7"/>',
    file: '<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5M9 13h6M9 17h6"/>', play: '<path d="M6 4l14 8-14 8z"/>', layers: '<path d="m12 2 10 6-10 6L2 8z"/><path d="m2 16 10 6 10-6M2 12l10 6 10-6"/>', key: '<circle cx="8" cy="15" r="4"/><path d="m11 12 9-9M17 6l3 3"/>'
  };
  UTIL.icon = (n, cls) => `<svg class="i ${cls || ''}" viewBox="0 0 24 24" aria-hidden="true">${P[n] || P.clipboard}</svg>`;
  // MENU-5 (D-79): 14 nhóm theo vai trò – Việc của tôi, Phê duyệt, Mua hàng, Kho, Sản xuất, Giá thành, Nhật ký & Tích hợp (tên cũ giữ để DB chưa chạy script vẫn có biểu tượng).
  UTIL.GROUP_ICON = { 'Việc của tôi': 'user', 'Phê duyệt': 'approve', 'Mua hàng': 'down', 'Kho': 'warehouse', 'Sản xuất': 'factory', 'Giá thành': 'calc', 'Nhật ký & Tích hợp': 'history', 'Điều hành': 'home', 'Kinh doanh': 'building', 'Thực đơn & Món ăn': 'utensils', 'Mua hàng & Kho': 'warehouse', 'Sản xuất & Giá thành': 'factory', 'Chất lượng & ATTP': 'shield', 'Đóng gói & Giao hàng': 'truck', 'Nhân sự': 'users', 'Tích hợp & Chỉ số': 'bot', 'Quản trị hệ thống': 'settings', 'Database Architecture': 'database' };
  // Icon riêng cho từng mục menu con (khác icon của nhóm cha). Màn chưa khai báo ở đây → rơi về SCREEN_ICON theo tiền tố.
  UTIL.MENU_ICON = {
    'dash-exec': 'chart', 'flow-e2e': 'flow',
    'crm-customer': 'users', 'crm-contact': 'msg', 'crm-location': 'pin', 'crm-price-policy': 'calc', 'crm-contract': 'link', 'crm-forecast': 'calendar', 'crm-order': 'clipboard', 'crm-order-week': 'calendar', 'crm-reconciliation': 'check', 'crm-billing': 'calc',
    'menu-dish-cat': 'layers', 'menu-dish': 'chef', 'menu-recipe': 'clipboard', 'menu-menu': 'calendar', 'menu-assignment': 'link', 'menu-change-request': 'recycle',
    'scm-supplier': 'building', 'scm-ingredient': 'package', 'scm-ingredient-cat': 'layers', 'scm-supplier-material': 'link', 'scm-uom': 'calc', 'scm-uom-conversion': 'recycle', 'scm-warehouse': 'home', 'scm-zone': 'panel', 'scm-location': 'pin', 'scm-inv-policy': 'settings',
    'scm-requirement': 'chart', 'scm-pr': 'edit', 'scm-po': 'clipboard', 'scm-gr': 'down', 'scm-lot': 'lot', 'scm-inv-txn': 'history', 'scm-inv-balance': 'eye', 'scm-transfer': 'route', 'scm-adjustment': 'edit', 'scm-issue': 'up', 'scm-reservation': 'key', 'scm-stockcount': 'check', 'scm-consumption-var': 'alert',
    'cost-standard': 'calc', 'prod-line': 'route', 'prod-workstation': 'panel', 'prod-plan': 'calendar', 'prod-order': 'clipboard', 'prod-batch': 'chef', 'prod-output': 'package', 'cost-actual': 'chart', 'cost-meal': 'utensils', 'prod-waste': 'trash',
    'qc-checklist': 'clipboard', 'qc-inspection': 'eye', 'qc-checklist-result': 'check', 'qc-temperature': 'thermo', 'qc-cleaning': 'recycle', 'qc-cleaning-result': 'ok', 'qc-supplier-issue': 'alert',
    'log-vehicle': 'key', 'log-delivery-order': 'clipboard', 'log-packing-order': 'package', 'log-route': 'route', 'log-loading': 'up', 'log-execution': 'play', 'log-pod': 'check', 'log-container-return': 'recycle', 'log-return-inspection': 'eye', 'log-packaging-ledger': 'history', 'log-packing-result': 'layers', 'log-gps': 'pin', 'log-complaint': 'msg',
    'hr-shift': 'calendar', 'hr-shift-assign': 'panel', 'hr-attendance': 'check', 'hr-leave': 'export', 'hr-swap': 'recycle', 'wf-inbox': 'approve',
    'int-channel': 'link', 'int-bot': 'play', 'int-identity': 'key', 'int-inbound': 'down', 'int-outbound': 'up', 'int-log': 'history', 'kpi-definition': 'settings', 'kpi-daily': 'chart', 'kpi-monthly': 'calendar', 'kpi-refresh': 'reset',
    'adm-company': 'building', 'adm-orgunit': 'flow', 'adm-employee': 'users', 'adm-person': 'panel', 'adm-user': 'key', 'adm-permission': 'shield', 'adm-usergroup': 'layers', 'adm-objectmenu': 'menu', 'adm-position': 'up', 'adm-admin-unit': 'pin', 'adm-document': 'clipboard', 'adm-audit': 'history', 'adm-access-log': 'eye', 'adm-merge-conflict': 'alert', 'adm-reference': 'link',
    'dbx-explorer': 'search', 'dbx-gap': 'alert'
  };
  UTIL.SCREEN_ICON = { crm: 'building', menu: 'utensils', scm: 'warehouse', prod: 'factory', cost: 'calc', qc: 'shield', log: 'truck', hr: 'users', wf: 'approve', int: 'bot', kpi: 'chart', adm: 'settings', dbx: 'database', dash: 'home', flow: 'flow' };
  // Nhãn tiếng Việt cho cột (hiển thị kèm tên cột SQL)
  UTIL.LABEL = {
    Code: 'Mã', Name: 'Tên', FullName: 'Họ tên', DisplayName: 'Tên hiển thị', Status: 'Trạng thái', Note: 'Ghi chú', Description: 'Mô tả', Remark: 'Ghi chú', Address: 'Địa chỉ', Phone: 'Điện thoại', PhoneNumber: 'Điện thoại', Email: 'Email', TaxCode: 'Mã số thuế',
    CompanyId: 'Công ty', OrganizationUnitId: 'Đơn vị', ParentId: 'Cấp cha', ManagerId: 'Quản lý', PositionId: 'Chức vụ', UnitType: 'Loại đơn vị', Gender: 'Giới tính', HireDate: 'Ngày vào làm', DateOfBirth: 'Ngày sinh',
    CustomerId: 'Khách hàng', CustomerType: 'Loại khách hàng', CIF: 'Mã CIF', Segment: 'Phân khúc', Source: 'Nguồn', BranchId: 'Chi nhánh', DepartmentId: 'Phòng ban', ContactPersonName: 'Người liên hệ', ContactPersonPhone: 'SĐT liên hệ', LegalName: 'Tên pháp lý', Industry: 'Ngành',
    ContractId: 'Hợp đồng', ContractNumber: 'Số hợp đồng', SignedDate: 'Ngày ký', EffectiveFrom: 'Hiệu lực từ', EffectiveTo: 'Hiệu lực đến', PaymentTerm: 'Điều khoản thanh toán', ContractValue: 'Giá trị HĐ', MealType: 'Loại suất', MealName: 'Tên suất', ServiceType: 'Hình thức', UnitPrice: 'Đơn giá', UnitId: 'Đơn vị tính',
    CustomerDeliveryLocationId: 'Điểm giao', DeliveryLocationId: 'Điểm giao', DeliveryWindowFrom: 'Giờ nhận từ', DeliveryWindowTo: 'Giờ nhận đến', ContactName: 'Người nhận', ContactPhone: 'SĐT người nhận', Latitude: 'Vĩ độ', Longitude: 'Kinh độ', DeliveryInstructions: 'Hướng dẫn giao',
    OrderId: 'Đơn hàng', OrderNumber: 'Số đơn', OrderDate: 'Ngày đặt', MealDate: 'Ngày ăn', DeliveryDate: 'Ngày giao', TotalQuantity: 'Tổng suất', TotalAmount: 'Thành tiền', OrderLineId: 'Dòng đơn', Quantity: 'Số lượng', LineAmount: 'Thành tiền', ShiftId: 'Ca', MenuId: 'Thực đơn', ContractMealId: 'Loại suất HĐ',
    DishId: 'Món ăn', DishCategoryId: 'Nhóm món', MealCategory: 'Phân loại', StandardPortion: 'Khẩu phần chuẩn', StandardCost: 'Giá thành chuẩn', StandardServingSize: 'Định lượng/suất', DefaultUnitId: 'ĐVT mặc định', RecipeId: 'Công thức', VersionNo: 'Phiên bản', VersionCode: 'Mã phiên bản',
    YieldPercent: 'Tỷ lệ thành phẩm %', YieldQuantity: 'Sản lượng chuẩn', YieldUnitId: 'ĐVT sản lượng', StandardOutputQty: 'Sản lượng chuẩn', ApprovedBy: 'Người duyệt', ApprovedAt: 'Thời điểm duyệt', LockedAt: 'Khóa lúc', LockedBy: 'Người khóa', IsActive: 'Đang dùng', Instructions: 'Hướng dẫn chế biến',
    IngredientId: 'Nguyên liệu', IngredientCategoryId: 'Nhóm nguyên liệu', BaseUnitId: 'ĐVT cơ sở', PurchaseUomId: 'ĐVT mua', IssueUomId: 'ĐVT xuất', StorageType: 'Loại bảo quản', ShelfLifeDays: 'Hạn dùng (ngày)', StandardWastePercent: 'Hao hụt chuẩn %', LotControl: 'Quản lý lô', ExpiryControl: 'Quản lý HSD', QcRequired: 'Bắt buộc QC', MinimumStock: 'Tồn tối thiểu', SafetyStock: 'Tồn an toàn', StorageCondition: 'Điều kiện bảo quản',
    SupplierId: 'Nhà cung cấp', SupplierType: 'Loại NCC', SupplierMaterialCode: 'Mã hàng NCC', MinimumOrderQuantity: 'MOQ', LeadTimeDays: 'Lead time (ngày)', DeliveryFrequency: 'Tần suất giao', IsPreferred: 'NCC ưu tiên', QualityRequirement: 'Yêu cầu chất lượng',
    WarehouseId: 'Kho', WarehouseType: 'Loại kho', StorageZoneId: 'Khu vực', StorageLocationId: 'Vị trí', TemperatureMin: 'Nhiệt độ min', TemperatureMax: 'Nhiệt độ max', TemperatureRange: 'Dải nhiệt', LocationType: 'Loại vị trí', MaximumStock: 'Tồn tối đa', ReorderPoint: 'Điểm đặt hàng', CriticalLevel: 'Mức nguy cấp', UseFefo: 'Xuất FEFO', ExpiryWarningDays: 'Cảnh báo HSD (ngày)',
    MaterialLotId: 'Lô', LotNumber: 'Số lô', ManufactureDate: 'Ngày SX', ExpiryDate: 'Hạn sử dụng', ReceivedDate: 'Ngày nhận', UnitCost: 'Đơn giá vốn', QcStatus: 'Kết quả QC', QualityStatus: 'Chất lượng',
    OnHandQuantity: 'Tồn thực', ReservedQuantity: 'Đã giữ', AvailableQuantity: 'Khả dụng', UpdatedDate: 'Cập nhật', LastCalculatedDate: 'Tính lúc', TransactionNumber: 'Số giao dịch', TransactionType: 'Loại giao dịch', TransactionDate: 'Ngày giao dịch', ReferenceType: 'Loại chứng từ', ReferenceId: 'Chứng từ', QuantityIn: 'Nhập', QuantityOut: 'Xuất', Reason: 'Lý do',
    RequestNumber: 'Số đề nghị', RequestDate: 'Ngày đề nghị', RequiredDate: 'Ngày cần', RequestedByEmployeeId: 'Người đề nghị', PurchaseRequestId: 'Đề nghị mua', PurchaseOrderId: 'Đơn mua', PurchaseOrderNumber: 'Số PO', ExpectedDeliveryDate: 'Ngày giao dự kiến',
    GoodsReceiptId: 'Phiếu nhập', ReceiptNumber: 'Số phiếu nhập', ReceiptDate: 'Ngày nhập', ReceivedQuantity: 'SL nhận', AcceptedQuantity: 'SL đạt', RejectedQuantity: 'SL loại', GoodsReceiptLineId: 'Dòng nhập',
    InspectionNumber: 'Số phiếu QC', InspectionDate: 'Ngày kiểm', InspectorEmployeeId: 'Người kiểm', Result: 'Kết quả', Temperature: 'Nhiệt độ', Weight: 'Khối lượng', Appearance: 'Cảm quan', Packaging: 'Bao bì', Criterion: 'Tiêu chí',
    ProductionPlanId: 'Kế hoạch SX', PlanNumber: 'Số kế hoạch', ProductionDate: 'Ngày sản xuất', PlanDate: 'Ngày lập', PlannedQuantity: 'SL kế hoạch', ActualQuantity: 'SL thực tế', ProductionOrderId: 'Lệnh SX', ProductionOrderNumber: 'Số lệnh', ProductionLineId: 'Dây chuyền', PlannedQty: 'SL kế hoạch', ActualQty: 'SL thực tế',
    PlannedStartDate: 'Bắt đầu KH', ActualStartDate: 'Bắt đầu TT', ActualEndDate: 'Kết thúc TT', ResponsibleEmployeeId: 'Phụ trách', ProductionBatchId: 'Mẻ SX', BatchNumber: 'Số mẻ', KitchenWorkstationId: 'Trạm', StartDate: 'Bắt đầu', EndDate: 'Kết thúc', AcceptedQty: 'SL đạt', RejectedQty: 'SL loại', WasteQty: 'Hao hụt',
    ProducedQuantity: 'SL sản xuất', MaterialCost: 'Chi phí NVL', LaborCost: 'Nhân công', OverheadCost: 'Chi phí chung', WasteCost: 'Chi phí hao hụt', OtherCost: 'Chi phí khác', TotalCost: 'Tổng chi phí', CurrencyCode: 'Tiền tệ', CalculatedAt: 'Tính lúc', CostPerMeal: 'Giá thành/suất', TotalActualCost: 'Tổng giá thành', AcceptedMealQuantity: 'Suất được nhận', Revenue: 'Doanh thu', GrossMargin: 'Lãi gộp', CostPerServing: 'Giá thành/suất',
    WasteType: 'Loại hao hụt', QuantityMeals: 'Số suất', QuantityKg: 'Khối lượng (kg)', Disposition: 'Xử lý', OccurredAt: 'Thời điểm', RecordedBy: 'Người ghi',
    VehicleId: 'Phương tiện', LicensePlate: 'Biển số', VehicleType: 'Loại xe', Capacity: 'Sức chứa', DriverId: 'Tài xế', RouteNo: 'Số tuyến', PlannedStart: 'Xuất phát KH', PlannedEnd: 'Kết thúc KH', ActualStart: 'Xuất phát TT', ActualEnd: 'Kết thúc TT',
    DeliveryRouteId: 'Tuyến', DeliveryOrderId: 'Đơn giao', DeliveryNumber: 'Số đơn giao', PlannedDeliveryTime: 'Giờ giao KH', ActualDeliveryTime: 'Giờ giao TT', SequenceNo: 'Thứ tự', PlannedArrival: 'Đến KH', ActualArrival: 'Đến TT', PlannedMealQty: 'Suất KH', DeliveredMealQty: 'Suất giao', ShortageQty: 'Thiếu', StopStatus: 'Trạng thái điểm',
    DeliveredQuantity: 'SL giao', ReceiverName: 'Người nhận', ReceiverPhone: 'SĐT nhận', ReceivedAt: 'Nhận lúc', SignatureUrl: 'Chữ ký', PhotoUrl: 'Ảnh', ConfirmedAt: 'Xác nhận lúc', EventType: 'Sự kiện', EventAt: 'Thời điểm', TemperatureC: 'Nhiệt độ °C',
    ReturnDate: 'Ngày thu hồi', ExpectedQuantity: 'SL phải thu', ReturnedQuantity: 'SL thu được', DamagedQuantity: 'SL hỏng', InspectionResult: 'Kết quả kiểm', DamagedQty: 'Hỏng', MissingQty: 'Thiếu', ContainerReturnId: 'Phiếu thu hồi', ReturnInspectionId: 'Phiếu kiểm',
    ComplaintNo: 'Số phản ánh', SourceChannel: 'Kênh', CategoryCode: 'Phân loại', Severity: 'Mức độ', SlaDueAt: 'Hạn SLA', ComplaintStatus: 'Trạng thái', AssignedTo: 'Phụ trách', ResolvedAt: 'Xử lý xong', Resolution: 'Kết quả xử lý',
    EmployeeId: 'Nhân viên', WorkDate: 'Ngày làm', AssignmentStatus: 'Trạng thái ca', AssignedBy: 'Người phân', StartTime: 'Giờ bắt đầu', EndTime: 'Giờ kết thúc', FromDate: 'Từ ngày', ToDate: 'Đến ngày', LeaveTypeCode: 'Loại nghỉ', RequestStatus: 'Trạng thái', SubmittedAt: 'Gửi lúc', RequestNo: 'Số yêu cầu',
    BusinessType: 'Loại nghiệp vụ', BusinessId: 'Bản ghi nghiệp vụ', RequestedBy: 'Người yêu cầu', RequestedAt: 'Yêu cầu lúc', CurrentStepNo: 'Bước hiện tại', PriorityCode: 'Ưu tiên', DueAt: 'Hạn', StepNo: 'Bước', ApproverUserId: 'Người duyệt', ApproverRoleCode: 'Vai trò duyệt', DecisionStatus: 'Quyết định', DecidedAt: 'Quyết định lúc', DecisionNote: 'Ý kiến',
    ChannelCode: 'Mã kênh', ChannelName: 'Tên kênh', ProviderCode: 'Nhà cung cấp', ChannelStatus: 'Trạng thái', BotCode: 'Mã bot', BotName: 'Tên bot', BotStatus: 'Trạng thái', MessageType: 'Loại tin', ProcessingStatus: 'Xử lý', MessageStatus: 'Trạng thái gửi',
    MetricCode: 'Mã KPI', MetricName: 'Tên KPI', DomainCode: 'Nhóm', UnitCode: 'Đơn vị', MetricValue: 'Giá trị', TargetValue: 'Mục tiêu', VarianceValue: 'Chênh lệch', SnapshotDate: 'Ngày', SnapshotMonth: 'Tháng', DataQualityStatus: 'Chất lượng DL',
    UserName: 'Tên đăng nhập', PersonId: 'Cá nhân', IsAdmin: 'Quản trị', UserGroupId: 'Nhóm người dùng', ObjectMenuId: 'Chức năng', MetaLink: 'Đường dẫn', IconName: 'Icon', LevelShow: 'Cấp', OrderShow: 'Thứ tự',
    PlannedMealQuantity: 'Suất KH', ForecastDate: 'Ngày dự báo', ForecastQuantity: 'SL dự báo', RequiredQuantity: 'Nhu cầu', ExpectedIncomingQuantity: 'Hàng đang về', PurchaseQuantity: 'Cần mua', RequirementNo: 'Số nhu cầu',
    IssueNumber: 'Số phiếu xuất', IssueDate: 'Ngày xuất', MaterialIssueId: 'Phiếu xuất', RequiredQty: 'SL cần', CountNumber: 'Số phiếu KK', CountType: 'Loại KK', CountDate: 'Ngày KK', SystemQuantity: 'Tồn sổ', PhysicalQuantity: 'Tồn thực tế', VarianceQuantity: 'Chênh lệch', VarianceReason: 'Lý do chênh lệch', StockCountId: 'Phiếu KK',
    PackingOrderNumber: 'Số lệnh đóng', PackingDate: 'Ngày đóng', PackingOrderId: 'Lệnh đóng gói', PackedQuantity: 'SL đã đóng', PackagingType: 'Loại bao bì', ProductionOutputId: 'Kết quả SX',
    MeasurementType: 'Loại đo', MinAllowedC: 'Min °C', MaxAllowedC: 'Max °C', IsWithinRange: 'Trong ngưỡng', MeasuredAt: 'Đo lúc', DeviceCode: 'Thiết bị', CorrectiveAction: 'Hành động khắc phục', EvidenceUrl: 'Bằng chứng',
    IssueType: 'Loại sự cố', IssueStatus: 'Trạng thái', ReportedAt: 'Báo cáo lúc', OwnerId: 'Phụ trách', ClosedAt: 'Đóng lúc', ChecklistId: 'Checklist', ChecklistItemId: 'Tiêu chí', ResultCode: 'Kết quả', ActualValue: 'Giá trị đo', CheckedBy: 'Người kiểm', CheckedAt: 'Kiểm lúc', CheckDate: 'Ngày kiểm', WorkstationId: 'Trạm',
    CleaningBatchNumber: 'Số mẻ vệ sinh', ContainerQuantity: 'SL khay', QcResult: 'Kết quả QC', ItemCode: 'Mã tiêu chí', ItemName: 'Tiêu chí', IsCritical: 'Trọng yếu', ExpectedValue: 'Giá trị chuẩn', FrequencyCode: 'Tần suất', AreaCode: 'Khu vực', SortOrder: 'Thứ tự',
    CapacityMealPerBatch: 'Công suất/mẻ', MinutesPerBatch: 'Phút/mẻ', AreaName: 'Khu vực', RadiusMeters: 'Bán kính (m)', ConversionFactor: 'Hệ số', FromUnitId: 'Từ ĐVT', ToUnitId: 'Sang ĐVT', Symbol: 'Ký hiệu', UomType: 'Loại ĐVT', DecimalPrecision: 'Số lẻ',
    DayOfWeek: 'Thứ', PortionQty: 'Khẩu phần', StandardQuantity: 'Định lượng', IsRequired: 'Bắt buộc', MenuType: 'Loại thực đơn', OriginalDishId: 'Món gốc', RequestedDishId: 'Món đề nghị', WastePercent: 'Hao hụt sơ chế %', ExpectedWasteRate: 'Hao hụt chế biến %', PreparationNote: 'Ghi chú sơ chế',
    TransactionAt: 'Thời điểm', ReferenceNo: 'Chứng từ', ConditionCode: 'Tình trạng', OrderedQuantity: 'SL đặt', ProducedQty: 'SL sản xuất', PackedQty: 'SL đóng gói', VarianceQty: 'Chênh lệch', VariancePercent: 'Chênh lệch %', FinalAmount: 'Giá trị chốt', ReconciliationDate: 'Ngày đối soát', ProofOfDeliveryId: 'POD',
    LoadedQty: 'SL xếp', LoadingTime: 'Giờ xếp', LoadingDate: 'Ngày xếp', VehicleCode: 'Mã xe', DispatchedAt: 'Xuất phát', DepartedAt: 'Rời bếp', ArrivedAt: 'Đến nơi', DeliveredAt: 'Giao xong', DriverName: 'Tài xế',
    IdChange: 'Bản ghi', Action: 'Hành động', EntityName: 'Bảng', Changes: 'Nội dung thay đổi', IPv4: 'Địa chỉ IP', PcName: 'Tên máy', FileName: 'Tên tệp', FileType: 'Loại tệp', FileSize: 'Dung lượng',
    TheoreticalQty: 'Lý thuyết', StandardQuantity2: '', VarianceCost: 'Giá trị chênh lệch', NameVN: 'Tên', FullNameVN: 'Tên đầy đủ', AdministrativeProvinceId: 'Tỉnh/TP', AdministrativeDistrictId: 'Quận/Huyện', AdministrativeUnitId: 'Cấp hành chính', AdministrativeRegionId: 'Vùng'
  };
  // FIX-1.5 (D-88): cột kỹ thuật của màn tích hợp / nhật ký / chỉ số – không hiện tên cột tiếng Anh.
  Object.entries({
    IntegrationChannelId: 'Kênh nhắn tin', LinkedAt: 'Liên kết lúc', LastSeenAt: 'Hoạt động gần nhất', IdentityStatus: 'Trạng thái liên kết', RequestAt: 'Gửi lúc', ResponseAt: 'Phản hồi lúc',
    HttpStatus: 'Mã phản hồi', DirectionCode: 'Chiều', OperationCode: 'Thao tác', ErrorCode: 'Mã lỗi', ErrorMessage: 'Nội dung lỗi', LogStatus: 'Trạng thái', JobCode: 'Job', RunId: 'Lượt chạy',
    StartedAt: 'Bắt đầu', CompletedAt: 'Kết thúc', RunStatus: 'Trạng thái chạy', RowsProcessed: 'Số dòng xử lý', DurationSeconds: 'Thời gian (giây)', SourceWatermark: 'Mốc dữ liệu',
    CorrelationId: 'Mã liên kết', IdempotencyKey: 'Khóa chống trùng', MessageType: 'Loại tin', ReceivedAt: 'Nhận lúc', ProcessingStatus: 'Trạng thái xử lý', QueuedAt: 'Vào hàng đợi',
    SentAt: 'Gửi lúc', MessageStatus: 'Trạng thái gửi', RetryCount: 'Số lần thử lại', LastError: 'Lỗi gần nhất', ExternalUserId: 'Mã người dùng trên kênh', ExternalMessageId: 'Mã tin trên kênh',
    ExternalConversationId: 'Mã hội thoại', RecipientExternalId: 'Người nhận trên kênh', ProviderMessageId: 'Mã tin của nhà cung cấp', PayloadHash: 'Mã băm nội dung', PayloadJson: 'Nội dung',
  }).forEach(([k, v]) => { if (!UTIL.LABEL[k]) UTIL.LABEL[k] = v; });
  UTIL.colLabel = c => UTIL.LABEL[c] || c.replace(/([a-z])([A-Z])/g, '$1 $2');
  window.UTIL = UTIL;
})();
