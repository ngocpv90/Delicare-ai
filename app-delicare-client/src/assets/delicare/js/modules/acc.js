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
  soon('acc-voucher', 'ACC-2', 'Chứng từ bán hàng tháng, mua hàng, xuất nguyên liệu theo buổi, nhập / xuất thành phẩm, kho khác, bảng công – tải Excel mẫu AMIS / gửi API.');
  soon('acc-amis-recon', 'ACC-8', 'Đối chiếu 8 chỉ tiêu Delicare ↔ AMIS theo kỳ: doanh thu, số suất theo thành phẩm, nhập mua, xuất nguyên liệu, bao bì, thành phẩm khác, tồn cuối kỳ.');
  soon('acc-period', 'ACC-4', 'Khóa kỳ kế toán: 5 điều kiện trước khi khóa, Kế toán khóa, COO duyệt mở khóa; kỳ đã khóa chặn sửa nghiệp vụ trong kỳ.');

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
