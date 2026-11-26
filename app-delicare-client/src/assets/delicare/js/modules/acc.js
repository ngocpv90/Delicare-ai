/* ACC-1 (D-131 b, c – anh chốt 2026-11-26): nhóm menu "Kế toán" – thanh chuyển nhanh 5 mục ở đầu mỗi màn (Chứng từ kế toán · Bảng kê thanh toán ·
 * Khớp mã AMIS · Đối chiếu với AMIS · Khóa kỳ) như mô phỏng docs/frontend-handoff/assets/acc-misa/.
 * Khớp mã AMIS (#/acc-amis-code): AMIS là nguồn mã chuẩn – nhận danh mục bằng tệp Excel xuất từ AMIS (API: ACC-7), 4 thẻ chỉ số, tab theo loại danh mục,
 * lọc Cần xử lý (mặc định) / Chưa có trên AMIS / Lệch thông tin / Đã khớp / Chỉ có trên AMIS (ẩn mặc định), ghép mã = đổi mã Delicare theo mã AMIS,
 * xuất danh sách mã cần tạo trên AMIS. API: AmisCatalog/GetMatch · ImportExcel · Remap · ExportMissing.
 * Chứng từ kế toán (ACC-2), Đối chiếu với AMIS (ACC-8), Khóa kỳ (ACC-4): màn tạm tới khi làm mục tương ứng. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const NAV = [['acc-voucher', 'Chứng từ kế toán'], ['crm-billing', 'Bảng kê thanh toán'], ['acc-amis-code', 'Khớp mã AMIS'], ['acc-amis-recon', 'Đối chiếu với AMIS'], ['acc-period', 'Khóa kỳ']];
  const FN = APP.FN || {};
  const def = (id, name) => { if (!FN[id]) FN[id] = { extra: true, ph: 'ACC', grp: 'Kế toán', fn: name, id, name, types: ['LS'], table: null, rel: [], st: 'MAPPED', note: '' }; };
  NAV.forEach(([id, name]) => { if (id !== 'crm-billing') def(id, name); });

  /** Thanh chuyển nhanh 5 mục (bỏ mục không có quyền xem). */
  APP.accNav = current => `<div class="acc-nav mb">${NAV.filter(([id]) => Store.perm(id).access !== false)
    .map(([id, name]) => `<a href="#/${id}" class="acc-nav-i ${id === current ? 'active' : ''}">${esc(name)}</a>`).join('')}</div>`;

  // ---------- màn tạm (ACC-2 / ACC-4 / ACC-8) ----------
  const soon = (id, task, what) => APP.register(id, (el, f) => {
    el.innerHTML = APP.header(f || FN[id], '', { noNote: true }) + APP.accNav(id)
      + `<div class="notice info">${I('help')}<div><b>Đang xây dựng (${task}).</b> ${esc(what)}</div></div>`;
  });
  soon('acc-amis-recon', 'ACC-8', 'Đối chiếu 8 chỉ tiêu Delicare ↔ AMIS theo kỳ: doanh thu, số suất theo thành phẩm, nhập mua, xuất nguyên liệu, bao bì, thành phẩm khác, tồn cuối kỳ.');

  // ---------- Bảng kê thanh toán: thêm thanh chuyển nhanh ----------
  const billing = APP.SCREENS['crm-billing'];
  if (billing) APP.register('crm-billing', (el, f, p) => {
    const r = billing(el, f, p);
    const put = () => { if (el.isConnected && !el.querySelector('.acc-nav')) { const h = el.querySelector('.page-heading'); const box = document.createElement('div'); box.innerHTML = APP.accNav('crm-billing'); const nav = box.firstElementChild; if (h) h.after(nav); else el.prepend(nav); } };
    put(); setTimeout(put, 0);
    return r;
  });

  // ---------- Khớp mã AMIS ----------
  const KINDS = [['material', 'Vật tư hàng hóa'], ['product', 'Thành phẩm theo loại suất'], ['unit', 'Đơn vị tính'], ['customer', 'Khách hàng'], ['supplier', 'Nhà cung cấp'], ['warehouse', 'Kho']];
  const FILTERS = [['need', 'Cần xử lý'], ['MISSING_ON_AMIS', 'Chưa có trên AMIS'], ['MISMATCH', 'Lệch thông tin'], ['MATCHED', 'Đã khớp'], ['ONLY_ON_AMIS', 'Chỉ có trên AMIS']];
  const COLOR = { MATCHED: 'green', MISSING_ON_AMIS: 'red', NOT_CONFIGURED: 'red', NAME_MISMATCH: 'orange', UNIT_MISMATCH: 'orange', ONLY_ON_AMIS: '', WAITING: '' };
  const st = { kind: 'material', filter: 'need', q: '' };
  const isNeed = s => ['MISSING_ON_AMIS', 'NAME_MISMATCH', 'UNIT_MISMATCH', 'NOT_CONFIGURED'].includes(s);
  const pass = (r, flt) => flt === 'need' ? isNeed(r.status) : flt === 'MISMATCH' ? ['NAME_MISMATCH', 'UNIT_MISMATCH'].includes(r.status)
    : flt === 'MISSING_ON_AMIS' ? ['MISSING_ON_AMIS', 'NOT_CONFIGURED'].includes(r.status) : r.status === flt;
  const when = d => (d ? U.date(d).slice(0, 5) + ' ' + String(d).slice(11, 16) : '–');

  function kpis(m) {
    return UI.kpis([
      UI.kpi('Đã khớp', U.num(m.matched || 0), `trên ${U.num(m.total || 0)} bản ghi Delicare đang dùng`, 'check', 'green'),
      UI.kpi('Chưa có trên AMIS', U.num(m.missing || 0), 'chặn gửi chứng từ có mã này', 'alert', m.missing ? 'red' : 'green'),
      UI.kpi('Lệch thông tin', U.num(m.mismatch || 0), 'cùng mã khác tên / ĐVT', 'edit', m.mismatch ? 'orange' : 'green'),
      UI.kpi('Lần nhận gần nhất', m.hasSnapshot ? when(m.receivedAt) : '–', m.hasSnapshot ? `${U.num(m.amisRecords || 0)} bản ghi AMIS` : 'chưa nhận – nhập tệp Excel xuất từ AMIS', 'down', m.hasSnapshot ? 'blue' : 'orange'),
    ]);
  }

  function table(box, m, rerender) {
    const canEdit = Store.perm('acc-amis-code').edit !== false;
    const rows = (m.listRow || []).filter(r => pass(r, st.filter)).filter(r => !st.q || [r.code, r.name, r.amisCode, r.amisName].some(v => (v || '').toLowerCase().includes(st.q)));
    const counts = Object.fromEntries(FILTERS.map(([k]) => [k, (m.listRow || []).filter(r => pass(r, k)).length]));
    const chips = FILTERS.map(([k, l]) => `<button class="chip ${st.filter === k ? 'active' : ''}" data-flt="${k}">${esc(l)} (${U.num(counts[k])})</button>`).join('');
    const unitDiff = (m.listRow || []).filter(r => r.status === 'UNIT_MISMATCH').length;
    box.innerHTML = `<div class="spread mb" style="flex-wrap:wrap;gap:8px"><div class="chips">${chips}</div><input class="input" id="acQ" placeholder="Tìm mã / tên…" style="max-width:220px" value="${esc(st.q)}"></div>`
      + (unitDiff ? `<div class="notice info mb">${I('help')}<div>${U.num(unitDiff)} dòng khác ĐVT – chứng từ gửi AMIS dùng ĐVT của AMIS (không đổi ĐVT trong Delicare).</div></div>` : '')
      + (rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Mã Delicare</th><th>Tên trên Delicare</th><th>ĐVT</th><th>Mã AMIS</th><th>Tên trên AMIS</th><th>ĐVT AMIS</th><th>Kết quả</th><th>Xử lý</th></tr></thead><tbody>
        ${rows.map((r, i) => `<tr><td>${esc(r.code || '–')}</td><td>${esc(r.name || '–')}</td><td>${esc(r.unit || '')}</td><td>${esc(r.amisCode || '–')}</td><td>${esc(r.amisName || (r.status === 'MISSING_ON_AMIS' ? 'chưa có' : ''))}</td><td>${esc(r.amisUnit || '')}</td>
          <td>${UI.tag(r.statusName || r.status, COLOR[r.status] || '')}${r.note ? `<div class="small muted">${esc(r.note)}</div>` : ''}</td>
          <td>${r.canRemap && canEdit && ['MISSING_ON_AMIS', 'NAME_MISMATCH'].includes(r.status) ? `<div class="row-flex" style="gap:6px;flex-wrap:nowrap"><input class="input sm" list="acSug${i}" data-code="${i}" placeholder="${esc(r.listSuggestion && r.listSuggestion[0] ? 'gợi ý: ' + r.listSuggestion[0].code : 'Gõ mã AMIS…')}" style="width:150px"><datalist id="acSug${i}">${(r.listSuggestion || []).map(s => `<option value="${esc(s.code)}">${esc(s.name || '')}</option>`).join('')}</datalist><button class="btn sm" data-map="${i}">Ghép</button></div>` : r.status === 'NOT_CONFIGURED' ? '<span class="small muted">khai ở cấu hình</span>' : '–'}</td></tr>`).join('')}
        </tbody></table></div>` : `<div class="muted small">${m.hasSnapshot ? 'Không có dòng nào' : 'Chưa nhận danh mục từ AMIS – bấm "Nhập file Excel xuất từ AMIS".'}</div>`);
    box.querySelectorAll('[data-flt]').forEach(b => b.onclick = () => { st.filter = b.dataset.flt; table(box, m, rerender); });
    const q = box.querySelector('#acQ'); q.oninput = () => { st.q = q.value.trim().toLowerCase(); const pos = q.selectionStart; table(box, m, rerender); const n = box.querySelector('#acQ'); n.focus(); n.setSelectionRange(pos, pos); };
    box.querySelectorAll('[data-map]').forEach(b => b.onclick = () => {
      const r = rows[+b.dataset.map], inp = box.querySelector(`[data-code="${b.dataset.map}"]`), code = (inp.value || (r.listSuggestion && r.listSuggestion[0] && r.listSuggestion[0].code) || '').trim();
      if (!code) { UI.toast('Gõ hoặc chọn mã AMIS để ghép', 'warn'); return; }
      UI.confirm(`Đổi mã Delicare <b>${esc(r.code)}</b> (${esc(r.name || '')}) thành mã AMIS <b>${esc(code)}</b>? Mọi chứng từ dùng bản ghi này sẽ mang mã mới.`, () =>
        Store.act('AmisCatalog/Remap', { json: { kind: st.kind, id: r.id, amisCode: code }, tables: [st.kind === 'customer' ? 'Customer' : st.kind === 'supplier' ? 'Supplier' : 'Ingredient'] })
          .then(x => { if (x.ok) { UI.toast(esc((x.res && x.res.responseMessage) || 'Đã ghép mã'), 'ok'); rerender(); } }));
    });
  }

  function importBox(rerender) {
    UI.modal({ title: 'Nhập danh mục từ tệp Excel xuất từ AMIS', body: `<div class="small muted mb">Trên AMIS: Danh mục › (Vật tư hàng hóa / Đơn vị tính / Khách hàng / Nhà cung cấp / Kho) › Xuất khẩu ra Excel. Mỗi lần nhập 1 loại – thay bản chụp cũ của loại đó.</div>
        <label class="small">Loại danh mục</label><select class="input mb" id="acKind"><option value="">Tự nhận theo tệp</option>${KINDS.filter(([k]) => k !== 'product').map(([k, l]) => `<option value="${k}">${esc(l)}</option>`).join('')}</select>
        <label class="small">Tệp (.xlsx)</label><input type="file" class="input" id="acFile" accept=".xlsx">`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="acGo">Nhập</button>',
      onMount: (m, close) => m.querySelector('#acGo').onclick = () => {
        const file = m.querySelector('#acFile').files[0]; if (!file) { UI.toast('Chọn tệp Excel', 'warn'); return; }
        const fd = new FormData(); fd.append('file', file); const k = m.querySelector('#acKind').value; if (k) fd.append('kind', k);
        Store.act('AmisCatalog/ImportExcel', { json: fd }).then(x => {
          if (!x.ok) return; close(); const d = x.data || {};
          UI.toast(esc((x.res && x.res.responseMessage) || 'Đã nhận danh mục') + ((d.listWarning || []).length ? ' · ' + esc(d.listWarning.join(' ')) : ''), 'ok');
          if (d.kind) st.kind = d.kind; st.filter = 'need'; rerender();
        });
      } });
  }

  APP.register('acc-amis-code', (el, f, p) => {
    f = f || FN['acc-amis-code'];
    if (p && KINDS.some(([k]) => k === p.kind)) st.kind = p.kind;
    const perm = Store.perm('acc-amis-code'), rerender = () => APP.SCREENS['acc-amis-code'](el, f, {});
    el.innerHTML = APP.header(f, `<button class="btn" id="acApi">${I('link')} Nhận từ AMIS (API)</button>${perm.imp !== false ? `<button class="btn" id="acImp">${I('down')} Nhập file Excel xuất từ AMIS</button>` : ''}${perm.exp !== false ? `<button class="btn primary" id="acExp">${I('export')} Xuất danh sách mã cần tạo</button>` : ''}`, { noNote: true })
      + APP.accNav('acc-amis-code')
      + `<div class="small muted mb">AMIS là nguồn mã chuẩn. Delicare nhận danh mục vật tư, ĐVT, khách, NCC, kho từ AMIS và khớp với dữ liệu Delicare theo mã. Mã chỉ có ở Delicare được gom vào danh sách để kế toán tạo trên AMIS trước khi gửi chứng từ.</div>
      <div id="acHead"><div class="muted small">Đang tải…</div></div>
      <div class="card panel"><div class="tabs mb" id="acTabs"></div><div id="acBody"><div class="muted small">Đang tải…</div></div><div id="acCfg" class="mt"></div></div>`;
    el.querySelector('#acApi').onclick = () => UI.toast('Tính năng chưa được nâng cấp – kết nối API AMIS làm ở ACC-7 khi có tài liệu API + thông tin kết nối.', 'warn');
    const imp = el.querySelector('#acImp'); if (imp) imp.onclick = () => importBox(rerender);
    const exp = el.querySelector('#acExp'); if (exp) exp.onclick = () => Store.download('AmisCatalog/ExportMissing');
    Store.query('AmisCatalog/GetMatch', { kind: st.kind }).then(x => {
      if (!el.isConnected) return;
      const hd = el.querySelector('#acHead'), body = el.querySelector('#acBody');
      if (!x.ok || !x.data) { hd.innerHTML = ''; body.innerHTML = `<div class="muted small">${esc(x.message || 'Không tải được danh mục')}</div>`; return; }
      const m = x.data, cnt = Object.fromEntries((m.listKind || []).map(k => [k.kind, k]));
      hd.innerHTML = kpis(m);
      el.querySelector('#acTabs').innerHTML = KINDS.map(([k, l]) => `<button class="tab ${st.kind === k ? 'active' : ''}" data-k="${k}">${esc(l)} (${U.num((cnt[k] || {}).total || 0)})${(cnt[k] || {}).needsAction ? ` <span class="tag red">${U.num(cnt[k].needsAction)}</span>` : ''}</button>`).join('');
      el.querySelectorAll('[data-k]').forEach(b => b.onclick = () => { st.kind = b.dataset.k; st.filter = 'need'; rerender(); });
      table(body, m, rerender);
      const cfg = el.querySelector('#acCfg');
      if (st.kind === 'product') cfg.innerHTML = `<div class="notice info">${I('help')}<div>Thành phẩm theo buổi / loại suất khớp bằng cấu hình <b>Integration:Misa:ProductByMeal</b> (IT khai trên máy chủ) – mã AMIS phải có trong danh sách vật tư hàng hóa đã nhận.</div></div>`;
      else if (st.kind === 'warehouse') cfg.innerHTML = `<div class="notice info">${I('help')}<div>Kho Delicare → kho AMIS khai ở cấu hình <b>Integration:Misa:WarehouseMap</b>. Nhiều kho cùng 1 mã AMIS thì chuyển kho nội bộ giữa chúng không gửi sang AMIS.</div></div>`;
    });
  });
})();

/* ACC-2 (D-131 d, e): Kế toán › Chứng từ kế toán (#/acc-voucher) – như mô phỏng chung-tu-ke-toan.html: kỳ tháng ◀ ▶, dải 3 bước, 4 thẻ chỉ số,
 * tab Cần xử lý (mặc định) · Bán hàng · Mua hàng · Kho & sản xuất · Bảng công · Tất cả; trạng thái Sẵn sàng · Chờ nghiệp vụ gốc · Chờ lập trên AMIS · Đã lập · Lỗi ·
 * Đã sửa – cần điều chỉnh; dòng lỗi có nút đi thẳng tới chỗ sửa; chọn nhiều → Tải file Excel mẫu AMIS / Đánh dấu đã lập / Gửi sang AMIS (chưa nâng cấp – ACC-7).
 * API: AccountingVoucher/GetMonth · ExportExcel · MarkCreated. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc, FN = APP.FN || {};
  const TABS = [['need', 'Cần xử lý'], ['sale', 'Bán hàng'], ['purchase', 'Mua hàng'], ['stock', 'Kho & sản xuất'], ['timesheet', 'Bảng công'], ['all', 'Tất cả']];
  const COLOR = { READY: 'green', PENDING_SOURCE: '', WAITING_AMIS: 'blue', CREATED_ON_AMIS: 'green', ERROR: 'red', CHANGED: 'orange' };
  const LABEL = { READY: 'Sẵn sàng', PENDING_SOURCE: 'Chờ nghiệp vụ gốc', WAITING_AMIS: 'Chờ lập trên AMIS', CREATED_ON_AMIS: 'Đã lập trên AMIS', ERROR: 'Lỗi', CHANGED: 'Đã sửa – cần điều chỉnh' };
  const ym = d => d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0');
  const shift = (m, n) => { const [y, mo] = m.split('-').map(Number); return ym(new Date(y, mo - 1 + n, 1)); };
  const st = { month: null, tab: 'need', sel: new Set() };
  const money = v => (v === null || v === undefined ? '–' : U.money(v));
  const CANCELLED = 'Nghiệp vụ gốc đã hủy sau khi gửi';

  function detail(v) {
    const lines = v.listLine || [];
    UI.modal({ title: esc(v.typeName + ' ' + v.number), size: 'lg',
      body: `<div class="small muted mb">${esc(U.date(v.date))} · ${esc([v.objectCode, v.objectName].filter(Boolean).join(' · '))} · ${esc(v.source || '')}</div>`
        + (lines.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Mã hàng</th><th>Tên</th><th>ĐVT</th><th class="num">Số lượng</th><th class="num">Đơn giá</th><th class="num">Thành tiền</th><th>Đối tượng THCP / lý do</th></tr></thead><tbody>`
          + lines.map(l => `<tr><td>${esc(l.code || '–')}</td><td>${esc(l.name || '')}</td><td>${esc(l.unit || '')}</td><td class="num">${U.num(l.quantity)}</td><td class="num">${money(l.unitPrice)}</td><td class="num">${money(l.amount)}</td><td>${esc(l.target || '')}</td></tr>`).join('')
          + '</tbody></table></div>' : '<div class="muted small">Không có dòng hàng.</div>')
        + (v.note ? `<div class="small mt">${esc(v.note)}</div>` : ''),
      foot: '<button class="btn" data-close>Đóng</button>' });
  }

  APP.register('acc-voucher', (el, f, p) => {
    f = f || FN['acc-voucher'];
    if (!st.month) st.month = ym(new Date());
    if (p && /^\d{4}-\d{2}$/.test(p.month || '')) st.month = p.month;
    if (p && TABS.some(([k]) => k === p.tab)) st.tab = p.tab;
    const perm = Store.perm('acc-voucher'), rerender = () => APP.SCREENS['acc-voucher'](el, f, {});
    const [y, mo] = st.month.split('-');
    const legend = Object.keys(LABEL).map(s => UI.tag(LABEL[s], COLOR[s])).join(' ');
    el.innerHTML = APP.header(f, '', { noNote: true }) + APP.accNav('acc-voucher')
      + '<div class="small muted mb">Delicare tự lập chứng từ từ nghiệp vụ đã xong (đối soát, nhận hàng, xuất kho, đóng gói, kiểm kê). Kế toán chỉ xử lý dòng báo lỗi, rồi gửi sang AMIS (tải file Excel đúng mẫu AMIS để nhập; tự động qua kết nối khi có API). Hạch toán, hóa đơn, công nợ làm trên AMIS.</div>'
      + `<div class="card panel mb"><div class="fw-week"><button class="btn sm" id="avPrev" aria-label="Tháng trước">◀</button><span>Tháng ${mo}/${y}</span><button class="btn sm" id="avNext" aria-label="Tháng sau">▶</button></div><div id="avSteps" class="mt"></div></div>`
      + '<div id="avHead"><div class="muted small">Đang tải…</div></div>'
      + `<div class="card panel"><div class="tabs mb" id="avTabs"></div><div class="small muted mb">${legend}</div><div id="avBody"><div class="muted small">Đang tải…</div></div><div id="avBar" class="mt"></div></div>`;
    el.querySelector('#avPrev').onclick = () => { st.month = shift(st.month, -1); st.sel.clear(); rerender(); };
    el.querySelector('#avNext').onclick = () => { st.month = shift(st.month, 1); st.sel.clear(); rerender(); };
    Store.query('AccountingVoucher/GetMonth', { month: st.month, tab: st.tab }).then(x => {
      if (!el.isConnected) return;
      const hd = el.querySelector('#avHead'), body = el.querySelector('#avBody');
      if (!x.ok || !x.data) { hd.innerHTML = ''; body.innerHTML = `<div class="muted small">${esc(x.message || 'Không tải được chứng từ')}</div>`; return; }
      const m = x.data, list = m.listVoucher || [], tc = m.tabCount || {};
      el.querySelector('#avSteps').innerHTML = '<div class="acc-steps">'
        + `<div class="acc-step"><b>1</b><div><b>Xử lý dòng cần sửa</b><div class="small muted">Mỗi dòng lỗi có nút đi thẳng tới chỗ sửa</div><button class="btn sm mt" data-tab="need">Xem ${U.num(m.needsAction || 0)} dòng cần xử lý</button></div></div>`
        + `<div class="acc-step"><b>2</b><div><b>Gửi sang AMIS</b><div class="small muted">Chọn chứng từ sẵn sàng → tải file Excel mẫu AMIS (gửi API: ACC-7)</div><button class="btn sm mt" id="avAllReady">Chọn tất cả sẵn sàng (${U.num(m.readyCount || 0)})</button></div></div>`
        + '<div class="acc-step"><b>3</b><div><b>Lập chứng từ trên AMIS</b><div class="small muted">Nhập file vào AMIS rồi đánh dấu đã lập; khóa kỳ ở mục Khóa kỳ</div></div></div></div>'
        + (m.hasAmisCatalog ? '' : `<div class="notice warn mt">${I('alert')}<div>Chưa nhận danh mục AMIS – mọi chứng từ báo Lỗi tới khi nhập danh mục ở <a href="#/acc-amis-code">Khớp mã AMIS</a>.</div></div>`);
      hd.innerHTML = UI.kpis([
        UI.kpi('Doanh thu sẽ gửi', U.moneyShort(m.saleAmount || 0), `${U.num(m.saleCount || 0)} phiếu bán tháng · chưa VAT`, 'chart', 'green'),
        UI.kpi('Xuất NVL cho sản xuất', U.moneyShort(m.issueAmount || 0), `${U.num(m.issueCount || 0)} phiếu theo buổi`, 'up', 'blue'),
        UI.kpi('Đã lập trên AMIS', `${U.num(m.createdCount || 0)} / ${U.num(m.total || 0)}`, 'chứng từ của kỳ', 'check', 'green'),
        UI.kpi('Cần xử lý', U.num(m.needsAction || 0), `${U.num(m.errorCount || 0)} lỗi · ${U.num(m.changedCount || 0)} sửa sau khi gửi`, 'alert', m.needsAction ? 'red' : 'green'),
      ]);
      el.querySelector('#avTabs').innerHTML = TABS.map(([k, l]) => `<button class="tab ${st.tab === k ? 'active' : ''}" data-tab="${k}">${esc(l)} (${U.num(tc[k] || 0)})</button>`).join('');
      el.querySelectorAll('[data-tab]').forEach(b => b.onclick = () => { st.tab = b.dataset.tab; rerender(); });
      const selectable = v => ['READY', 'WAITING_AMIS', 'CHANGED', 'CREATED_ON_AMIS'].includes(v.status) && v.type !== 'TIMESHEET' && v.source !== CANCELLED;
      const cell = v => (v.amount !== null && v.amount !== undefined ? money(v.amount) : v.quantity ? U.num(v.quantity) + ' suất' : '–');
      const fix = v => (v.fixLink && v.fixLabel && ['ERROR', 'PENDING_SOURCE', 'READY'].includes(v.status) ? `<a class="btn sm" href="${esc(v.fixLink)}">${esc(v.fixLabel)}</a> ` : '');
      body.innerHTML = list.length ? '<div class="table-wrap"><table class="table dc-table"><thead><tr><th><input type="checkbox" id="avAll"></th><th>Loại chứng từ · nguồn</th><th>Số Delicare</th><th>Ngày</th><th>Đối tượng</th><th class="num">Giá trị (chưa VAT)</th><th>Trạng thái</th><th>Ghi chú</th><th></th></tr></thead><tbody>'
        + list.map((v, i) => `<tr><td>${selectable(v) ? `<input type="checkbox" data-sel="${i}" ${st.sel.has(v.key) ? 'checked' : ''}>` : ''}</td><td>${esc(v.typeName)}<div class="small muted">${esc(v.source || '')}</div></td><td><b>${esc(v.number)}</b></td><td>${esc(U.date(v.date))}</td>`
          + `<td>${esc([v.objectCode, v.objectName].filter(Boolean).join(' · '))}</td><td class="num">${cell(v)}</td>`
          + `<td>${UI.tag(v.statusName, COLOR[v.status] || '')}</td><td class="small">${esc(v.note || '')}</td><td style="white-space:nowrap">${fix(v)}<button class="btn sm" data-view="${i}">Xem</button></td></tr>`).join('')
        + '</tbody></table></div>' : '<div class="muted small">Không có chứng từ nào trong tab này.</div>';
      const bar = el.querySelector('#avBar');
      const paintBar = () => {
        const picked = list.filter(v => st.sel.has(v.key)), sum = picked.reduce((s, v) => s + (v.amount || 0), 0), dis = st.sel.size ? '' : 'disabled';
        bar.innerHTML = `<div class="spread" style="flex-wrap:wrap;gap:8px"><span class="small">Đã chọn <b>${U.num(st.sel.size)}</b> chứng từ${picked.length ? ' · ' + money(sum) : ''}</span><div class="row-flex" style="gap:8px">`
          + (perm.exp !== false ? `<button class="btn" id="avXls" ${dis}>${I('export')} Tải file Excel mẫu AMIS</button>` : '')
          + (perm.edit !== false ? `<button class="btn" id="avMark" ${dis}>${I('check')} Đánh dấu đã lập trên AMIS</button>` : '')
          + `<button class="btn primary" id="avSend" ${dis}>${I('link')} Gửi sang AMIS</button></div></div>`
          + '<div class="small muted mt">Tự gửi hằng đêm: Tắt (bật sau khi có kết nối API AMIS – ACC-7).</div>';
        const xls = bar.querySelector('#avXls'); if (xls) xls.onclick = () => Store.download('AccountingVoucher/ExportExcel', null, { month: st.month, listKey: [...st.sel] }).then(r => { if (r.ok) { st.sel.clear(); rerender(); } });
        const mark = bar.querySelector('#avMark'); if (mark) mark.onclick = () => Store.act('AccountingVoucher/MarkCreated', { json: { month: st.month, listKey: [...st.sel] } }).then(r => { if (r.ok) { UI.toast(esc((r.res && r.res.responseMessage) || 'Đã đánh dấu'), 'ok'); st.sel.clear(); rerender(); } });
        bar.querySelector('#avSend').onclick = () => UI.toast('Tính năng chưa được nâng cấp – gửi qua API AMIS làm ở ACC-7 khi có tài liệu API + thông tin kết nối. Dùng "Tải file Excel mẫu AMIS".', 'warn');
      };
      paintBar();
      body.querySelectorAll('[data-sel]').forEach(cb => cb.onchange = () => { const k = list[+cb.dataset.sel].key; if (cb.checked) st.sel.add(k); else st.sel.delete(k); paintBar(); });
      const all = body.querySelector('#avAll');
      if (all) all.onchange = () => { list.forEach(v => { if (selectable(v)) { if (all.checked) st.sel.add(v.key); else st.sel.delete(v.key); } }); body.querySelectorAll('[data-sel]').forEach(cb => { cb.checked = all.checked; }); paintBar(); };
      body.querySelectorAll('[data-view]').forEach(b => b.onclick = () => detail(list[+b.dataset.view]));
      const ar = el.querySelector('#avAllReady');
      if (ar) ar.onclick = () => Store.query('AccountingVoucher/GetMonth', { month: st.month, tab: 'all' }).then(z => {
        ((z.ok && z.data && z.data.listVoucher) || []).filter(v => v.status === 'READY' && v.type !== 'TIMESHEET').forEach(v => st.sel.add(v.key));
        st.tab = 'all'; rerender();
      });
    });
  });
})();

/* ACC-4 (D-131 h): Kế toán › Khóa kỳ (#/acc-period) – như mô phỏng khoa-ky.html: kỳ tháng ◀ ▶, 5 điều kiện (Đạt / còn việc + nút Xem / Xử lý),
 * hạn khóa, nút Khóa kỳ (đủ điều kiện, kỳ đã hết), bảng các kỳ (Đã khóa – Xin mở khóa → COO duyệt), chứng từ điều chỉnh ĐC- của kỳ đã khóa.
 * API: AccountingPeriod/GetOverview · Lock · RequestUnlock. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc, FN = APP.FN || {};
  const st = { month: null };
  const ym = d => d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0');
  const shift = (m, n) => { const [y, mo] = m.split('-').map(Number); return ym(new Date(y, mo - 1 + n, 1)); };
  const label = m => { const [y, mo] = m.split('-'); return `Tháng ${mo}/${y}`; };

  function askUnlock(month, done) {
    UI.modal({ title: 'Xin mở khóa kỳ ' + label(month).toLowerCase(), body: '<div class="small muted mb">Gửi COO duyệt (Phê duyệt › Mở khóa kỳ). Sau khi mở, sửa nghiệp vụ rồi khóa lại; mọi lần khóa / mở được ghi lịch sử.</div><textarea class="input" id="apRe" rows="3" placeholder="Lý do mở khóa (bắt buộc)…"></textarea>',
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="apReOk">Gửi duyệt</button>',
      onMount: (m, close) => m.querySelector('#apReOk').onclick = () => { const r = m.querySelector('#apRe').value.trim(); if (!r) { UI.toast('Nhập lý do mở khóa', 'warn'); return; }
        Store.act('AccountingPeriod/RequestUnlock', { json: { month, reason: r } }).then(x => { if (x.ok) { close(); UI.toast(esc((x.res && x.res.responseMessage) || 'Đã gửi duyệt'), 'ok'); done(); } }); } });
  }

  APP.register('acc-period', (el, f, p) => {
    f = f || FN['acc-period'];
    if (!st.month) st.month = shift(ym(new Date()), -1);
    if (p && /^\d{4}-\d{2}$/.test(p.month || '')) st.month = p.month;
    const perm = Store.perm('acc-period'), rerender = () => APP.SCREENS['acc-period'](el, f, {});
    el.innerHTML = APP.header(f, '', { noNote: true }) + APP.accNav('acc-period')
      + '<div class="small muted mb">Khóa kỳ khi mọi chứng từ của tháng đã lập trên AMIS. Sau khi khóa, Delicare chặn hủy / đảo phiếu kho, ghi sổ kiểm kê, sửa / chốt / mở lại đối soát có ngày trong tháng đó. Cần sửa thì lập chứng từ điều chỉnh vào kỳ đang mở.</div>'
      + `<div class="card panel mb"><div class="fw-week"><button class="btn sm" id="apPrev" aria-label="Tháng trước">◀</button><span>${label(st.month)}</span><button class="btn sm" id="apNext" aria-label="Tháng sau">▶</button></div></div>`
      + '<div id="apBody"><div class="muted small">Đang tải…</div></div>';
    el.querySelector('#apPrev').onclick = () => { st.month = shift(st.month, -1); rerender(); };
    el.querySelector('#apNext').onclick = () => { st.month = shift(st.month, 1); rerender(); };
    Store.query('AccountingPeriod/GetOverview', { month: st.month }).then(x => {
      if (!el.isConnected) return;
      const box = el.querySelector('#apBody');
      if (!x.ok || !x.data) { box.innerHTML = `<div class="muted small">${esc(x.message || 'Không tải được kỳ kế toán')}</div>`; return; }
      const d = x.data, conds = d.listCondition || [];
      const head = d.locked
        ? `<div class="notice ok mb">${I('key')}<div><b>${label(d.month)} đã khóa</b>${d.lockedBy ? ' – ' + esc(d.lockedBy) : ''}${d.lockedAt ? ' · ' + U.dt(d.lockedAt) : ''}.${d.unlockPending ? ' Đang có yêu cầu mở khóa chờ COO duyệt.' : ''}</div>${!d.unlockPending && perm.edit !== false ? '<button class="btn sm" id="apUn">Xin mở khóa</button>' : ''}</div>`
        : `<div class="card panel mb"><div class="spread mb"><b>Điều kiện khóa kỳ ${label(d.month).toLowerCase()}</b><span class="small muted">Hạn khóa: ${U.date(d.dueDate).slice(0, 5)} · ${d.remaining ? 'còn ' + d.remaining + '/' + conds.length + ' việc' : 'đủ điều kiện'}</span></div>`
          + conds.map(c => `<div class="spread" style="border-bottom:1px solid var(--line);padding:8px 0"><span>${c.ok ? I('check') : I('alert')} ${esc(c.title)}</span><span class="row-flex" style="gap:8px">${UI.tag(c.detail || (c.ok ? 'Đạt' : ''), c.ok ? 'green' : 'orange')}${!c.ok && c.link ? `<a class="btn sm" href="${esc(c.link)}">${esc(c.actionLabel || 'Xem')}</a>` : ''}</span></div>`).join('')
          + `<div class="spread mt"><span class="small muted">${!d.ended ? 'Kỳ chưa kết thúc – khóa sau ngày cuối tháng.' : d.remaining ? `Còn ${d.remaining} việc chưa xong ở trên – bấm "Xem" để làm tiếp.` : 'Đủ điều kiện – Kế toán khóa kỳ.'}</span>${perm.edit !== false ? `<button class="btn primary" id="apLock" ${d.canLock ? '' : 'disabled'}>${I('key')} Khóa kỳ ${label(d.month).slice(6)}</button>` : ''}</div>`
          + '<div class="small muted mt">Kế toán khóa kỳ. Mở khóa phải gửi duyệt COO, nêu lý do; mọi lần khóa / mở ghi lịch sử. Bot nhắc Kế toán từ ngày 5 nếu kỳ trước chưa khóa.</div></div>';
      const periods = d.listPeriod || [], adj = d.listAdjustment || [];
      box.innerHTML = head
        + '<div class="card panel mb"><div class="panel-title mb">Các kỳ</div><div class="table-wrap"><table class="table dc-table"><thead><tr><th>Kỳ</th><th>Trạng thái</th><th>Khóa bởi</th><th>Thời điểm</th><th></th></tr></thead><tbody>'
        + periods.map(r => `<tr><td><a href="javascript:void 0" data-m="${r.month}">${label(r.month)}</a></td><td>${r.locked ? UI.tag('Đã khóa', 'green') : UI.tag('Đang mở', '')}${r.unlockPending ? ' ' + UI.tag('Chờ duyệt mở khóa', 'orange') : ''}</td><td>${esc(r.by || '–')}</td><td>${r.at ? U.dt(r.at) : '–'}</td><td>${r.locked && !r.unlockPending && perm.edit !== false ? `<button class="btn xs" data-un="${r.month}">Xin mở khóa</button>` : ''}</td></tr>`).join('')
        + '</tbody></table></div></div>'
        + '<div class="card panel"><div class="panel-title">Chứng từ điều chỉnh</div><div class="small muted mb">Hệ thống tự lập khi nghiệp vụ gốc đã gửi sang AMIS của kỳ đã khóa bị hủy / sửa; Kế toán kiểm rồi gửi như chứng từ thường (ghi vào kỳ đang mở).</div>'
        + (adj.length ? '<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Số điều chỉnh</th><th>Chứng từ gốc</th><th>Kỳ gốc</th><th>Lý do</th><th class="num">Giá trị</th><th>Ghi vào kỳ</th></tr></thead><tbody>'
          + adj.map(a => `<tr><td><b>${esc(a.number)}</b></td><td>${esc((a.sourceNumber || '') + ' · ' + (a.sourceType || ''))}</td><td>${esc(a.sourceMonth)} (đã khóa)</td><td class="small">${esc(a.reason || '')}</td><td class="num">${a.difference !== null && a.difference !== undefined ? U.money(a.difference) : '–'}</td><td>${esc(a.writeTo)}</td></tr>`).join('')
          + '</tbody></table></div>' : '<div class="muted small">Không có chứng từ điều chỉnh.</div>') + '</div>';
      box.querySelectorAll('[data-m]').forEach(a => a.onclick = () => { st.month = a.dataset.m; rerender(); });
      box.querySelectorAll('[data-un]').forEach(b => b.onclick = () => askUnlock(b.dataset.un, rerender));
      const un = box.querySelector('#apUn'); if (un) un.onclick = () => askUnlock(d.month, rerender);
      const lk = box.querySelector('#apLock');
      if (lk) lk.onclick = () => UI.confirm(`Khóa kỳ kế toán <b>${label(d.month).toLowerCase()}</b>? Sau khi khóa, mọi hủy / đảo / sửa nghiệp vụ có ngày trong tháng bị chặn; mở lại phải COO duyệt.`,
        note => Store.act('AccountingPeriod/Lock', { json: { month: d.month, reason: note || null } }).then(r => { if (r.ok) { UI.toast(esc((r.res && r.res.responseMessage) || 'Đã khóa kỳ'), 'ok'); rerender(); } }), { input: 'Ghi chú (tùy chọn)' });
    });
  });
})();

/* ACC-5 (D-131 i – anh chốt 2026-11-26): Kho › Trả hàng NCC (#/scm-supplier-return) – như mô phỏng tra-hang-ncc.html: phiếu "TH-…",
 * 1 Thủ kho lập (chọn lô còn tồn, số trả ≤ tồn, lý do – lô chuyển Tạm giữ) → 2 Mua hàng xác nhận NCC đồng ý / không nhận → 3 Thủ kho ghi sổ khi NCC nhận hàng
 * → 4 Kế toán: chứng từ "Trả lại hàng mua" ở Chứng từ kế toán. Danh sách theo trạng thái, chi tiết có lịch sử. Rộng 390px: bảng cuộn ngang, hộp thoại 1 cột.
 * API: SupplierReturn/GetList · GetDetail · GetLots · Create · Confirm · Post · Cancel. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc, FN = APP.FN || {};
  if (!FN['scm-supplier-return']) FN['scm-supplier-return'] = { extra: true, ph: 'ACC', grp: 'Kho', fn: 'Trả hàng NCC', id: 'scm-supplier-return', name: 'Trả hàng NCC', types: ['LS'], table: null, rel: [], st: 'MAPPED', note: '' };
  const COLOR = { RETURN_PENDING: 'orange', RETURN_AGREED: 'blue', RETURN_POSTED: 'green', RETURN_REFUSED: 'red', RETURN_CANCELLED: '' };
  const TABS = [['open', 'Đang mở'], ['RETURN_POSTED', 'Đã trả'], ['RETURN_REFUSED', 'NCC không nhận'], ['RETURN_CANCELLED', 'Đã hủy'], ['all', 'Tất cả']];
  const st = { tab: 'open', q: '' };
  const money = v => (v === null || v === undefined ? '–' : U.money(v));
  const has = v => v !== null && v !== undefined;
  const done = (r, msg, cb) => { if (r.ok) { UI.toast(esc((r.res && r.res.responseMessage) || msg), 'ok'); cb(); } };

  function steps(cur) {
    const S = [['RETURN_PENDING', 'Thủ kho lập', 'lô tạm giữ'], ['RETURN_AGREED', 'Mua hàng xác nhận NCC', 'đồng ý / không nhận'], ['RETURN_POSTED', 'Thủ kho ghi sổ', 'khi NCC nhận hàng'], ['ACC', 'Kế toán', 'chứng từ Trả lại hàng mua']];
    const at = S.findIndex(s => s[0] === cur);
    return '<div class="acc-steps four">' + S.map((s, i) => `<div class="acc-step ${cur && i <= at ? 'done' : ''}"><b>${i + 1}</b><div><b>${esc(s[1])}</b><div class="small muted">${esc(s[2])}</div></div></div>`).join('') + '</div>';
  }

  function createDlg(reasons, after) {
    let lots = [], pick = null;
    UI.modal({ title: 'Lập phiếu trả hàng NCC', size: 'lg',
      body: '<div class="small muted mb">Chọn lô còn tồn (tìm theo mã lô, nguyên liệu, NCC hoặc số phiếu nhận). Lập xong lô chuyển <b>Tạm giữ</b> – không xuất cho sản xuất – và Mua hàng được báo để hỏi NCC.</div>'
        + `<div class="row-flex mb" style="gap:8px;flex-wrap:wrap"><input class="input" id="srQ" placeholder="Mã lô / nguyên liệu / NCC / phiếu nhận…" style="flex:1;min-width:200px"><button class="btn" id="srFind">${I('search')} Tìm lô</button></div>`
        + '<div id="srLots" class="mb"></div><div id="srForm"></div>',
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="srOk" disabled>Lập phiếu trả</button>',
      onMount: (m, close) => {
        const q = m.querySelector('#srQ'), box = m.querySelector('#srLots'), form = m.querySelector('#srForm'), ok = m.querySelector('#srOk');
        const paintForm = () => {
          if (!pick) { form.innerHTML = ''; ok.disabled = true; return; }
          const unit = esc(pick.unitName || '');
          form.innerHTML = '<div class="card panel"><div class="sr-grid small">'
            + `<div>Nguyên liệu: <b>${esc([pick.ingredientCode, pick.ingredientName].filter(Boolean).join(' · '))}</b></div><div>Lô: <b>${esc(pick.lotNo || '–')}</b>${pick.expiryDate ? ' · HSD ' + U.date(pick.expiryDate) : ''}</div>`
            + `<div>NCC: <b>${esc(pick.supplierName || '–')}</b></div><div>Phiếu nhận: ${esc(pick.goodsReceiptNumber || '–')}</div>`
            + `<div>Đã nhập: ${has(pick.receivedQuantity) ? U.num(pick.receivedQuantity) : '–'} ${unit}</div><div>Còn tồn: <b>${U.num(pick.onHand)}</b> ${unit} · ${esc(pick.warehouseName || '')}</div>`
            + `<div>Đơn giá: ${money(pick.unitCost)}</div><div>Thành tiền: <b id="srAmt">–</b></div></div>`
            + `<div class="sr-grid mt"><label class="small">Số trả (${unit})<input class="input" id="srQty" type="number" min="0" step="any" max="${pick.onHand}"></label>`
            + `<label class="small">Lý do<select class="input" id="srRs"><option value="">– Chọn lý do –</option>${reasons.map(r => `<option value="${esc(r.code)}">${esc(r.label)}</option>`).join('')}</select></label></div>`
            + '<label class="small mt" style="display:block">Ghi chú (tình trạng hàng, đã báo NCC…)<textarea class="input" id="srNote" rows="2"></textarea></label>'
            + '<div class="small muted mt">Ảnh hàng lỗi: đính kèm ở Chất lượng › Sự cố NCC sau khi lập (phiếu trả hiện ở đó với loại "Trả hàng NCC").</div></div>';
          const qty = form.querySelector('#srQty');
          qty.oninput = () => { const v = +qty.value; form.querySelector('#srAmt').textContent = v > 0 && has(pick.unitCost) ? U.money(Math.round(v * pick.unitCost)) : '–'; };
          ok.disabled = false;
        };
        const find = () => Store.query('SupplierReturn/GetLots', { search: q.value.trim() }).then(x => {
          lots = (x.ok && x.data) || []; pick = null; paintForm();
          box.innerHTML = lots.length ? '<div class="table-wrap" style="max-height:240px;overflow:auto"><table class="table dc-table"><thead><tr><th></th><th>Nguyên liệu · lô</th><th>NCC · phiếu nhận</th><th class="num">Còn tồn</th></tr></thead><tbody>'
            + lots.map((l, i) => `<tr><td><input type="radio" name="srPick" data-i="${i}" aria-label="Chọn lô"></td><td>${esc(l.ingredientName || '')}<div class="small muted">${esc(l.lotNo || '–')}${l.lotStatus === 'OnHold' ? ' · Tạm giữ' : ''}</div></td><td>${esc(l.supplierName || 'Không có NCC')}<div class="small muted">${esc(l.goodsReceiptNumber || '')}</div></td><td class="num">${U.num(l.onHand)} ${esc(l.unitName || '')}</td></tr>`).join('')
            + '</tbody></table></div>' : `<div class="muted small">${esc(x.ok ? 'Không có lô còn tồn khớp từ khóa.' : (x.message || 'Không tải được lô'))}</div>`;
          box.querySelectorAll('[data-i]').forEach(r => r.onchange = () => { pick = lots[+r.dataset.i]; paintForm(); });
        });
        m.querySelector('#srFind').onclick = find;
        q.onkeydown = e => { if (e.key === 'Enter') find(); };
        find();
        ok.onclick = () => {
          if (!pick) return;
          const qty = +m.querySelector('#srQty').value, rs = m.querySelector('#srRs').value;
          if (!(qty > 0)) { UI.toast('Nhập số trả lớn hơn 0', 'warn'); return; }
          if (qty > pick.onHand) { UI.toast('Số trả vượt tồn của lô', 'warn'); return; }
          if (!rs) { UI.toast('Chọn lý do trả hàng', 'warn'); return; }
          Store.act('SupplierReturn/Create', { json: { materialLotId: pick.materialLotId, warehouseId: pick.warehouseId, quantity: qty, reason: rs, note: m.querySelector('#srNote').value.trim() || null } })
            .then(r => done(r, 'Đã lập phiếu trả', () => { close(); after(r.res && r.res.data && r.res.data.id); }));
        };
      } });
  }

  function detailDlg(id, perm, after) {
    Store.query('SupplierReturn/GetDetail', { id }).then(x => {
      if (!x.ok || !x.data) { UI.toast(esc(x.message || 'Không tải được phiếu'), 'warn'); return; }
      const d = x.data, edit = perm.edit !== false, unit = esc(d.unitName || '');
      const row = (k, v) => `<div><span class="muted">${k}:</span> ${v}</div>`;
      UI.modal({ title: 'Phiếu trả ' + esc(d.number), size: 'lg',
        body: steps(d.state === 'RETURN_REFUSED' || d.state === 'RETURN_CANCELLED' ? null : d.state)
          + `<div class="mt mb">${UI.tag(d.stateName, COLOR[d.state] || '')}</div>`
          + '<div class="sr-grid small">'
          + row('NCC', `<b>${esc([d.supplierCode, d.supplierName].filter(Boolean).join(' · '))}</b>`) + row('Phiếu nhận', esc(d.goodsReceiptNumber || '–'))
          + row('Nguyên liệu', esc([d.ingredientCode, d.ingredientName].filter(Boolean).join(' · '))) + row('Lô', esc(d.lotNo || '–') + ' · ' + esc(d.warehouseName || ''))
          + row('Đã nhập', has(d.receivedQuantity) ? U.num(d.receivedQuantity) + ' ' + unit : '–') + row('Còn tồn', U.num(d.onHand) + ' ' + unit)
          + row('Số trả', `<b>${U.num(d.quantity)} ${unit}</b>`) + row('Đơn giá · thành tiền', money(d.unitCost) + ' · <b>' + money(d.amount) + '</b>')
          + row('Lý do', esc(d.reasonName || '')) + row('Người lập', esc(d.createdByName || '–') + ' · ' + U.dt(d.reportedAt))
          + (d.note ? row('Ghi chú', esc(d.note)) : '') + (d.postedAt ? row('Ghi sổ', U.dt(d.postedAt)) : '') + '</div>'
          + '<div class="panel-title mt">Lịch sử</div>' + ((d.listHistory || []).map(h => `<div class="small" style="border-bottom:1px solid var(--line);padding:4px 0">${U.dt(h.at)} · <b>${esc(h.actionName)}</b>${h.by ? ' – ' + esc(h.by) : ''}${h.note ? `<div class="muted">${esc(h.note)}</div>` : ''}</div>`).join('') || '<div class="muted small">–</div>')
          + (d.state === 'RETURN_POSTED' ? `<div class="notice info mt">${I('link')}<div>Đã ghi sổ kho (Trả lại). Kế toán thấy chứng từ <b>Trả lại hàng mua</b> ở <a href="#/acc-voucher?tab=purchase">Chứng từ kế toán › Mua hàng</a>.</div></div>` : ''),
        foot: '<button class="btn" data-close>Đóng</button>'
          + (edit && d.canCancel ? '<button class="btn" id="sdCancel">Hủy phiếu</button>' : '')
          + (edit && d.canConfirm ? '<button class="btn" id="sdRefuse">NCC không nhận</button><button class="btn primary" id="sdAgree">NCC đồng ý</button>' : '')
          + (edit && d.canPost ? `<button class="btn primary" id="sdPost">${I('check')} Ghi sổ – NCC đã nhận hàng</button>` : ''),
        onMount: (m, close) => {
          const go = (path, json, msg) => Store.act('SupplierReturn/' + path, { json }).then(r => done(r, msg, () => { close(); after(); }));
          const b = s => m.querySelector(s);
          if (b('#sdAgree')) b('#sdAgree').onclick = () => UI.confirm('Xác nhận <b>NCC đồng ý nhận lại</b> hàng? Thủ kho được báo để giao trả và ghi sổ.', note => go('Confirm', { id, agree: true, note: note || null }, 'Đã xác nhận'), { input: 'Ghi chú (tùy chọn)' });
          if (b('#sdRefuse')) b('#sdRefuse').onclick = () => UI.confirm('NCC <b>không nhận</b> lại hàng? Lô sẽ bỏ tạm giữ.', note => { if (!note) { UI.toast('Ghi lý do NCC không nhận', 'warn'); return; } go('Confirm', { id, agree: false, note }, 'Đã ghi nhận'); }, { input: 'Lý do NCC không nhận (bắt buộc)' });
          if (b('#sdPost')) b('#sdPost').onclick = () => UI.confirm(`Ghi sổ xuất trả <b>${U.num(d.quantity)} ${unit}</b> lô ${esc(d.lotNo || '')} cho ${esc(d.supplierName || 'NCC')}? Tồn kho giảm ngay.`, note => go('Post', { id, note: note || null }, 'Đã ghi sổ'), { input: 'Ghi chú (người nhận của NCC, biên bản…)' });
          if (b('#sdCancel')) b('#sdCancel').onclick = () => UI.confirm('Hủy phiếu trả? Lô bỏ tạm giữ.', note => { if (!note) { UI.toast('Ghi lý do hủy', 'warn'); return; } go('Cancel', { id, note }, 'Đã hủy phiếu'); }, { input: 'Lý do hủy (bắt buộc)' });
        } });
    });
  }

  APP.register('scm-supplier-return', (el, f, p) => {
    f = f || FN['scm-supplier-return'];
    if (p && TABS.some(([k]) => k === p.tab)) st.tab = p.tab;
    const perm = Store.perm('scm-supplier-return'), rerender = () => APP.SCREENS['scm-supplier-return'](el, f, {});
    el.innerHTML = APP.header(f, '', { noNote: true })
      + '<div class="small muted mb">Trả lại NCC hàng đã nhập kho bị hỏng, sai quy cách, gần hết hạn hoặc giao thừa. Hàng bị loại ngay lúc nhận thì ghi ở phiếu nhận (số loại), không lập phiếu trả.</div>'
      + `<div class="card panel mb">${steps(null)}</div>`
      + '<div id="srHead"><div class="muted small">Đang tải…</div></div>'
      + `<div class="card panel"><div class="spread mb" style="flex-wrap:wrap;gap:8px"><div class="tabs" id="srTabs"></div><div class="row-flex" style="gap:8px;flex-wrap:wrap"><input class="input" id="srSearch" placeholder="Tìm số phiếu, NCC, nguyên liệu, lô…" value="${esc(st.q)}" style="min-width:180px">`
      + (perm.add !== false ? `<button class="btn primary" id="srNew">${I('plus')} Lập phiếu trả</button>` : '') + '</div></div><div id="srBody"><div class="muted small">Đang tải…</div></div></div>';
    const s = el.querySelector('#srSearch');
    s.onkeydown = e => { if (e.key === 'Enter') { st.q = s.value.trim(); rerender(); } };
    Store.query('SupplierReturn/GetList', { state: 'all', search: st.q }).then(x => {
      if (!el.isConnected) return;
      const head = el.querySelector('#srHead'), body = el.querySelector('#srBody');
      if (!x.ok || !x.data) { head.innerHTML = ''; body.innerHTML = `<div class="muted small">${esc(x.message || 'Không tải được phiếu trả')}</div>`; return; }
      const m = x.data, all = m.listData || [];
      head.innerHTML = UI.kpis([
        UI.kpi('Chờ NCC xác nhận', U.num(m.pending || 0), 'Mua hàng cần hỏi NCC', 'alert', m.pending ? 'orange' : 'green'),
        UI.kpi('Chờ giao trả', U.num(m.agreed || 0), 'NCC đã đồng ý – Thủ kho ghi sổ khi giao', 'up', m.agreed ? 'blue' : 'green'),
        UI.kpi('Đã trả tháng này', U.num(m.postedThisMonth || 0), money(m.postedAmountThisMonth || 0), 'check', 'green'),
      ]);
      const inTab = (v, k) => k === 'all' || (k === 'open' ? ['RETURN_PENDING', 'RETURN_AGREED'].includes(v.state) : v.state === k);
      el.querySelector('#srTabs').innerHTML = TABS.map(([k, l]) => `<button class="tab ${st.tab === k ? 'active' : ''}" data-tab="${k}">${esc(l)} (${U.num(all.filter(v => inTab(v, k)).length)})</button>`).join('');
      el.querySelectorAll('[data-tab]').forEach(b => b.onclick = () => { st.tab = b.dataset.tab; rerender(); });
      const list = all.filter(v => inTab(v, st.tab));
      body.innerHTML = list.length ? '<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Số phiếu · ngày</th><th>NCC</th><th>Nguyên liệu · lô</th><th class="num">Số trả</th><th class="num">Thành tiền</th><th>Lý do</th><th>Trạng thái</th><th></th></tr></thead><tbody>'
        + list.map((v, i) => `<tr><td><b>${esc(v.number)}</b><div class="small muted">${U.date(v.reportedAt)}</div></td><td>${esc(v.supplierName || '')}<div class="small muted">${esc(v.goodsReceiptNumber || '')}</div></td>`
          + `<td>${esc(v.ingredientName || '')}<div class="small muted">${esc(v.lotNo || '')}</div></td><td class="num">${U.num(v.quantity)} ${esc(v.unitName || '')}</td><td class="num">${money(v.amount)}</td><td class="small">${esc(v.reasonName || '')}</td>`
          + `<td>${UI.tag(v.stateName, COLOR[v.state] || '')}</td><td><button class="btn sm" data-v="${i}">${v.canConfirm || v.canPost ? 'Xử lý' : 'Xem'}</button></td></tr>`).join('')
        + '</tbody></table></div>' : '<div class="muted small">Không có phiếu trả nào.</div>';
      body.querySelectorAll('[data-v]').forEach(b => b.onclick = () => detailDlg(list[+b.dataset.v].id, perm, rerender));
      const nw = el.querySelector('#srNew');
      if (nw) nw.onclick = () => createDlg(m.listReason || [], nid => { rerender(); if (nid) detailDlg(nid, perm, rerender); });
      if (p && p.id) detailDlg(p.id, perm, rerender);
    });
  });
})();
