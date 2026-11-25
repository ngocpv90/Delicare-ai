/* UI-6.33 (D-114):
 * A. "Sự cố nhà cung cấp" (qc-supplier-issue): chip trạng thái có số đếm (mặc định chưa đóng), quá hạn lên đầu, cột mô tả ngắn, nút bước tiếp trên dòng
 *    (Nhận xử lý / Đã xử lý – chọn hướng xử lý / Đóng / Mở lại), lọc NCC · Loại · Mức độ · kỳ, ngăn chi tiết (mô tả, chứng từ, lịch sử), 4 chỉ số;
 *    form lập tay gọn (NCC, Loại trong danh mục, Mức độ 3 nút, Mô tả, chứng từ của NCC) – không ô trạng thái / cách xử lý / ngày.
 * B. "Bộ kiểm tra ATTP": form bộ gọn (mã tự sinh từ tên, khu vực / tần suất chọn danh mục, phiên bản tự sinh), nhãn "Bộ hệ thống", nút "Tạo phiên bản mới"
 *    – dùng trong tab "Bộ kiểm tra" của màn Kiểm tra theo ca / mẻ (qc-shift.js gọi APP.qcChecklistSets).
 * Chế độ DEMO giữ màn cũ. Tải SAU qc.js, qc-shift.js. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const st = { status: 'open', q: '', supplier: '', type: '', severity: '', period: 'all' };
  const STATUS = [['Open', 'Mới', 'blue'], ['InProgress', 'Đang xử lý', 'orange'], ['Resolved', 'Đã xử lý', 'green'], ['Closed', 'Đóng', '']];
  const SEV = [['High', 'Cao', 'red'], ['Medium', 'Trung bình', 'orange'], ['Low', 'Thấp', '']];
  const ref = n => (Store.refList ? Store.refList(n) : []) || [];
  const refName = (n, c) => (ref(n).find(x => x.code === c) || {}).name || c || '';
  const sevTag = s => { const x = SEV.find(v => v[0] === s); return x ? UI.tag(x[1], x[2]) : esc(s || ''); };
  const stTag = s => { const x = STATUS.find(v => v[0] === s); return x ? UI.tag(x[1], x[2]) : esc(s || ''); };
  const TABLES = ['SupplierIssue'];

  // ================= A. Sự cố nhà cung cấp =================
  const base = APP.SCREENS['qc-supplier-issue'];
  APP.register('qc-supplier-issue', (el, f, p) => {
    if (!Store.isLive) return base(el, f, p);
    if (p && p.supplier) st.supplier = p.supplier;
    const perm = Store.perm('qc-supplier-issue');
    el.innerHTML = APP.header(f, perm.add !== false ? `<button class="btn primary" id="siNew">${I('plus')} Lập sự cố</button>` : '') + '<div id="siBody"><div class="card panel small muted">Đang tải…</div></div>';
    const nb = el.querySelector('#siNew'); if (nb) nb.onclick = () => issueForm(null, () => APP.SCREENS['qc-supplier-issue'](el, f, {}));
    Promise.all([Store.ensure('Supplier'), Store.loadRefLists ? Store.loadRefLists() : Promise.resolve()]).then(() => draw(el, f), () => draw(el, f));
  });
  function draw(el, f) {
    const body = el.querySelector('#siBody'); if (!body) return;
    const re = () => draw(el, f), perm = Store.perm('qc-supplier-issue'), t = U.day(Store.ctx.date || new Date());
    const from = st.period === '30d' ? U.addDays(t, -29) : st.period === '90d' ? U.addDays(t, -89) : null;
    Promise.all([
      Store.query('SupplierIssue/GetList', { Page: 1, PageSize: 500, SupplierId: st.supplier || null, IssueType: st.type || null, Severity: st.severity || null, SearchText: st.q || null, From: from }),
      Store.query('SupplierIssue/GetSummary', { from: U.addDays(t, -29), to: t }),
    ]).then(([x, y]) => {
      if (!body.isConnected) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được sự cố')}</div>`; return; }
      const d = x.data || {}, all = d.listItem || [], counts = d.statusCounts || {}, top = ((y.ok && (y.list && y.list.length ? y.list : Array.isArray(y.data) ? y.data : [])) || [])[0];
      const rows = all.filter(i => { const s = (i.issue || {}).issueStatus; return st.status === 'open' ? s !== 'Closed' : st.status === 'all' ? true : s === st.status; });
      const notClosed = ['Open', 'InProgress', 'Resolved'].reduce((a, k) => a + (counts[k] || 0), 0), new30 = all.filter(i => (i.issue || {}).reportedAt >= U.addDays(t, -29)).length;
      body.innerHTML = `<div class="kpis">${UI.kpi('Chưa đóng', U.num(notClosed), '', 'alert', 'orange')}${UI.kpi('Quá hạn', U.num(d.overdueCount || 0), d.overdueCount ? '<span class="bad">xếp đầu danh sách</span>' : 'Không có', 'clock', 'red')}${UI.kpi('Mới trong 30 ngày', U.num(new30), '', 'plus', 'blue')}${UI.kpi('NCC nhiều sự cố nhất (30 ngày)', top ? U.num(top.total) : '–', top ? esc(top.supplierName || '') : 'Không có', 'truck', 'purple')}</div>
        <div class="card panel mb"><div class="row-flex" style="flex-wrap:wrap;gap:8px">
          ${[['open', 'Chưa đóng', notClosed], ...STATUS.map(s => [s[0], s[1], counts[s[0]] || 0]), ['all', 'Tất cả', Object.values(counts).reduce((a, b) => a + b, 0)]].map(([k, l, n]) => `<button class="chip ${st.status === k ? 'active' : ''}" data-st="${k}">${l} · ${U.num(n)}</button>`).join('')}</div>
          <div class="row-flex mt" style="flex-wrap:wrap;gap:8px"><input class="input" type="search" id="siQ" placeholder="Tìm mô tả, loại" value="${esc(st.q)}" style="width:220px">
            <select class="select" id="siSup" style="width:auto"><option value="">Mọi nhà cung cấp</option>${Store.rows('Supplier').map(s => `<option value="${s.Id}" ${st.supplier === s.Id ? 'selected' : ''}>${esc(Store.label('Supplier', s))}</option>`).join('')}</select>
            <select class="select" id="siType" style="width:auto"><option value="">Mọi loại</option>${ref('SupplierIssueType').map(r => `<option value="${esc(r.code)}" ${st.type === r.code ? 'selected' : ''}>${esc(r.name)}</option>`).join('')}</select>
            <select class="select" id="siSev" style="width:auto"><option value="">Mọi mức độ</option>${SEV.map(s => `<option value="${s[0]}" ${st.severity === s[0] ? 'selected' : ''}>${s[1]}</option>`).join('')}</select>
            ${[['all', 'Mọi thời gian'], ['30d', '30 ngày'], ['90d', '90 ngày']].map(([k, l]) => `<button class="chip ${st.period === k ? 'active' : ''}" data-pd="${k}">${l}</button>`).join('')}</div></div>
        <div class="card panel">${rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Nhà cung cấp</th><th>Mô tả</th><th>Loại</th><th>Mức độ</th><th>Phiếu nhập / đơn mua</th><th>Hạn xử lý</th><th>Phụ trách</th><th>Trạng thái</th><th></th></tr></thead><tbody>${rows.map((i, n) => { const s = i.issue || {}; return `<tr data-i="${n}" style="cursor:pointer">
            <td><b>${esc(i.supplierName || '')}</b></td><td title="${esc(s.description || '')}"><span style="display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;max-width:320px">${esc(s.description || '')}</span></td>
            <td>${esc(i.issueTypeName || s.issueType || '')}</td><td>${sevTag(s.severity)}</td>
            <td class="small">${i.goodsReceiptNumber ? `<a href="${APP.href('scm-gr-detail?id=' + s.goodsReceiptId)}">${esc(i.goodsReceiptNumber)}</a>` : ''}${i.purchaseOrderNumber ? `<br><a href="${APP.href('scm-po-detail?id=' + s.purchaseOrderId)}">${esc(i.purchaseOrderNumber)}</a>` : ''}</td>
            <td>${U.date(s.dueAt)}${i.isOverdue ? ' ' + UI.tag('Quá hạn', 'red') : ''}</td><td class="small">${esc(i.ownerName || '')}</td><td>${stTag(s.issueStatus)}${i.resolutionTypeName ? `<br><span class="small muted">${esc(i.resolutionTypeName)}</span>` : ''}</td>
            <td class="actions">${perm.edit !== false ? (i.allowedTransitions || []).map(to => `<button class="btn xs ${to === 'Resolved' ? 'orange' : ''}" data-to="${to}" data-n="${n}">${to === 'InProgress' && s.issueStatus === 'Resolved' ? 'Mở lại' : to === 'InProgress' ? 'Nhận xử lý' : to === 'Resolved' ? 'Đã xử lý' : 'Đóng'}</button>`).join(' ') : ''}</td></tr>`; }).join('')}</tbody></table></div>` : '<div class="empty">Không có sự cố nào khớp bộ lọc.</div>'}</div>`;
      body.querySelectorAll('[data-st]').forEach(b => b.onclick = () => { st.status = b.dataset.st; re(); });
      body.querySelectorAll('[data-pd]').forEach(b => b.onclick = () => { st.period = b.dataset.pd; re(); });
      let tq; body.querySelector('#siQ').oninput = e => { clearTimeout(tq); tq = setTimeout(() => { st.q = e.target.value.trim(); re(); }, 400); };
      body.querySelector('#siSup').onchange = e => { st.supplier = e.target.value; re(); };
      body.querySelector('#siType').onchange = e => { st.type = e.target.value; re(); };
      body.querySelector('#siSev').onchange = e => { st.severity = e.target.value; re(); };
      body.querySelectorAll('[data-to]').forEach(b => b.onclick = e => { e.stopPropagation(); transit(rows[+b.dataset.n], b.dataset.to, re); });
      body.querySelectorAll('tr[data-i]').forEach(tr => tr.onclick = e => { if (e.target.closest('a,button')) return; drawer(rows[+tr.dataset.i], re); });
    });
  }
  function transit(i, to, done) {
    const s = i.issue || {}, reopen = to === 'InProgress' && s.issueStatus === 'Resolved';
    if (to !== 'Resolved' && !reopen) {
      return UI.confirm(to === 'InProgress' ? `Nhận xử lý sự cố của <b>${esc(i.supplierName || '')}</b>? Bạn là người phụ trách.` : `Đóng sự cố của <b>${esc(i.supplierName || '')}</b>?`,
        () => Store.act('SupplierIssue/Transition', { form: { Id: s.id, To: to }, tables: TABLES }).then(r => { if (r.ok) { UI.toast(to === 'Closed' ? 'Đã đóng sự cố' : 'Đã nhận xử lý', 'ok'); done(); } }), { ok: to === 'Closed' ? 'Đóng' : 'Nhận xử lý' });
    }
    UI.modal({
      title: reopen ? 'Mở lại sự cố' : 'Đã xử lý sự cố', sub: esc(i.supplierName || '') + ' · ' + esc(i.issueTypeName || ''), size: 'md',
      body: `${reopen ? '' : `<div class="field"><label>Hướng xử lý <span class="req">*</span></label><div class="row-flex" style="flex-wrap:wrap;gap:6px">${ref('SupplierIssueResolutionType').map(r => `<button type="button" class="chip" data-rt="${esc(r.code)}">${esc(r.name)}</button>`).join('')}</div></div>`}
        <div class="field mt"><label>${reopen ? 'Lý do mở lại' : 'Ghi chú xử lý'} <span class="req">*</span></label><textarea class="input" rows="3" id="trN"></textarea></div>`,
      foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="trGo">${reopen ? 'Mở lại' : 'Đã xử lý'}</button>`,
      onMount: (m, close) => {
        let rt = '';
        m.querySelectorAll('[data-rt]').forEach(b => b.onclick = () => { rt = b.dataset.rt; m.querySelectorAll('[data-rt]').forEach(x => x.classList.toggle('active', x === b)); });
        m.querySelector('#trGo').onclick = () => {
          const note = m.querySelector('#trN').value.trim();
          if (!reopen && !rt) return UI.toast('Chọn hướng xử lý', 'err');
          if (note.length < 3) return UI.toast('Ghi chú bắt buộc', 'err');
          Store.act('SupplierIssue/Transition', { form: { Id: s.id, To: to, ResolutionType: rt || null, Resolution: note }, tables: TABLES }).then(r => { if (r.ok) { UI.toast(reopen ? 'Đã mở lại sự cố' : 'Đã ghi xử lý', 'ok'); close(); done(); } });
        };
      }
    });
  }
  function drawer(i, done) {
    const s = i.issue || {};
    UI.drawer({
      title: esc(i.supplierName || ''), badge: stTag(s.issueStatus), width: 560,
      tabs: [
        { label: 'Chi tiết', html: `<div style="display:grid;grid-template-columns:140px 1fr;gap:6px 12px">${[['Loại', esc(i.issueTypeName || '')], ['Mức độ', sevTag(s.severity)], ['Báo lúc', U.dt ? U.dt(s.reportedAt) : U.date(s.reportedAt)], ['Hạn xử lý', U.date(s.dueAt) + (i.isOverdue ? ' ' + UI.tag('Quá hạn', 'red') : '')],
          ['Phụ trách', esc(i.ownerName || '–')], ['Phiếu nhập', i.goodsReceiptNumber ? `<a href="${APP.href('scm-gr-detail?id=' + s.goodsReceiptId)}">${esc(i.goodsReceiptNumber)}</a>` : '–'], ['Đơn mua', i.purchaseOrderNumber ? `<a href="${APP.href('scm-po-detail?id=' + s.purchaseOrderId)}">${esc(i.purchaseOrderNumber)}</a>` : '–'],
          ['Mô tả', esc(s.description || '')], ['Hướng xử lý', esc(i.resolutionTypeName || '–')], ['Ghi chú xử lý', esc(i.resolutionNote || s.resolution || '–')], ['Đóng lúc', s.closedAt ? U.date(s.closedAt) : '–']]
          .map(([k, v]) => `<div class="muted small">${k}</div><div>${v}</div>`).join('')}</div>` },
        { label: 'Lịch sử', html: pane => { UI.history('SupplierIssue', s.id, pane); return '<div class="small muted">Đang tải…</div>'; } },
      ],
      foot: ['Open', 'InProgress'].includes(s.issueStatus) && Store.perm('qc-supplier-issue').edit !== false ? '<button class="btn" data-ed>Sửa</button>' : '',
      onMount: (d, close) => { const b = d.parentNode.querySelector('[data-ed]'); if (b) b.onclick = () => { close(); issueForm(i, done); }; }
    });
  }
  // Form lập / sửa: NCC, Loại (danh mục), Mức độ, Mô tả, chứng từ của NCC; trạng thái / xử lý / ngày do máy chủ.
  function issueForm(i, done) {
    const s = (i && i.issue) || {}, editing = !!s.id;
    Promise.all([Store.ensure('GoodsReceipt'), Store.ensure('PurchaseOrder')]).then(() => {
      let sev = s.severity || 'Medium';
      const docs = sup => `<option value="">Không gắn</option>` + Store.rows('GoodsReceipt').filter(r => r.SupplierId === sup).slice(-60).reverse().map(r => `<option value="gr:${r.Id}" ${s.goodsReceiptId === r.Id ? 'selected' : ''}>Phiếu nhập ${esc(r.ReceiptNumber || '')}</option>`).join('')
        + Store.rows('PurchaseOrder').filter(r => r.SupplierId === sup).slice(-60).reverse().map(r => `<option value="po:${r.Id}" ${s.purchaseOrderId === r.Id && !s.goodsReceiptId ? 'selected' : ''}>Đơn mua ${esc(r.PurchaseOrderNumber || '')}</option>`).join('');
      UI.modal({
        title: editing ? 'Sửa sự cố nhà cung cấp' : 'Lập sự cố nhà cung cấp', size: 'md',
        body: `<div class="field"><label>Nhà cung cấp <span class="req">*</span></label><select class="select" id="ifSup" ${editing ? 'disabled' : ''}><option value="">Chọn nhà cung cấp</option>${Store.rows('Supplier').map(r => `<option value="${r.Id}" ${(s.supplierId || st.supplier) === r.Id ? 'selected' : ''}>${esc(Store.label('Supplier', r))}</option>`).join('')}</select></div>
          <div class="field mt"><label>Loại <span class="req">*</span></label><select class="select" id="ifType"><option value="">Chọn loại</option>${ref('SupplierIssueType').map(r => `<option value="${esc(r.code)}" ${s.issueType === r.code ? 'selected' : ''}>${esc(r.name)}</option>`).join('')}</select></div>
          <div class="field mt"><label>Mức độ</label><div class="row-flex" style="gap:6px">${SEV.map(v => `<button type="button" class="chip ${sev === v[0] ? 'active' : ''}" data-sv="${v[0]}">${v[1]}</button>`).join('')}</div></div>
          <div class="field mt"><label>Mô tả <span class="req">*</span></label><textarea class="input" rows="3" id="ifDesc">${esc(s.description || '')}</textarea></div>
          <div class="field mt"><label>Phiếu nhập / đơn mua</label><select class="select" id="ifDoc">${docs(s.supplierId || st.supplier)}</select></div>
          <div class="small muted mt">${editing ? 'Hạn xử lý: ' + U.date(s.dueAt) : 'Trạng thái Mới; hạn xử lý tính tự động từ lúc lập.'}</div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="ifGo">Lưu</button>',
        onMount: (m, close) => {
          m.querySelectorAll('[data-sv]').forEach(b => b.onclick = () => { sev = b.dataset.sv; m.querySelectorAll('[data-sv]').forEach(x => x.classList.toggle('active', x === b)); });
          m.querySelector('#ifSup').onchange = e => { m.querySelector('#ifDoc').innerHTML = docs(e.target.value); };
          m.querySelector('#ifGo').onclick = () => {
            const sup = m.querySelector('#ifSup').value, type = m.querySelector('#ifType').value, desc = m.querySelector('#ifDesc').value.trim(), doc = m.querySelector('#ifDoc').value;
            if (!sup) return UI.toast('Chọn nhà cung cấp', 'err');
            if (!type) return UI.toast('Chọn loại sự cố', 'err');
            if (desc.length < 3) return UI.toast('Nhập mô tả sự cố', 'err');
            const gr = doc.startsWith('gr:') ? doc.slice(3) : null, grRow = gr && Store.get('GoodsReceipt', gr);
            const form = { SupplierId: sup, IssueType: type, Severity: sev, Description: desc, GoodsReceiptId: gr || '', PurchaseOrderId: doc.startsWith('po:') ? doc.slice(3) : (grRow && grRow.PurchaseOrderId) || '' };
            if (editing) Object.assign(form, { Id: s.id, ReportedAt: s.reportedAt, DueAt: s.dueAt, IssueStatus: s.issueStatus });
            Store.act(editing ? 'SupplierIssue/Update' : 'SupplierIssue/Insert', { form, method: editing ? 'PUT' : 'POST', tables: TABLES }).then(r => { if (r.ok) { UI.toast(editing ? 'Đã lưu sự cố' : 'Đã lập sự cố – trạng thái Mới', 'ok'); close(); done(); } });
          };
        }
      });
    });
  }

  // ================= B. Bộ kiểm tra ATTP =================
  // Bộ hệ thống (khớp máy chủ FoodSafetyRules.IsSystem: mã có giờ hạn, vệ sinh khay, công đoạn / lưu mẫu, SystemChecklistCodes) – chỉ để hiện nhãn; máy chủ chặn.
  const SYSTEM = new Set(['PREP', 'PREP_AREA_CLEAN', 'BLAST_CHILL', 'KITCHEN_CLEAN', 'VS-KHAY', 'PRD_STAGE', 'PRD_BATCH', 'QA_FG']);
  const OFFSET = { PREP: '150 phút trước giờ xuất bếp', PREP_AREA_CLEAN: '90 phút trước giờ xuất bếp', BLAST_CHILL: '20 phút trước giờ xuất bếp', KITCHEN_CLEAN: '90 phút sau giờ xuất bếp' };
  const baseForm = UI.form;
  UI.form = (t, row, opt = {}) => {
    if (t !== 'FoodSafetyChecklist' || !Store.isLive || opt.fields) return baseForm(t, row, opt);
    const sys = row && SYSTEM.has(row.Code);
    const fields = ['Name', 'AreaCode', 'FrequencyCode', 'EffectiveFrom', 'EffectiveTo', 'Note'].concat(sys ? [] : ['Status']);
    // Mã tự sinh từ tên (BKT_…) khi thêm; sửa giữ mã. Phiên bản do máy chủ.
    const defaults = Object.assign({}, opt.defaults || {}, row ? {} : { Code: 'BKT', VersionNo: 1, Status: 'Active' });
    const close = baseForm(t, row, Object.assign({}, opt, { fields, defaults, optional: ['EffectiveFrom', 'EffectiveTo', 'Note'] }));
    if (!row) setTimeout(() => {
      const all = document.querySelectorAll('[name="Name"]'), name = all[all.length - 1];
      if (name) name.addEventListener('input', () => { defaults.Code = ('BKT_' + Store.userCode(name.value.trim().replace(/\s+/g, '_'))).slice(0, 40); });
    }, 50);
    return close;
  };
  APP.qcChecklistSets = el => {
    const canAdd = Store.perm('qc-checklist').add !== false;
    APP.rowExtra(el, tr => {
      const r = Store.get('FoodSafetyChecklist', tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td || td.dataset.v633) return; td.dataset.v633 = '1';
      const nameCell = tr.querySelector('td'); if (nameCell && SYSTEM.has(r.Code)) nameCell.insertAdjacentHTML('beforeend', ' ' + UI.tag('Bộ hệ thống', 'blue'));
      if (SYSTEM.has(r.Code)) td.querySelectorAll('[data-act="del"]').forEach(b => b.remove());
      if (!canAdd) return;
      const b = document.createElement('button'); b.className = 'btn xs'; b.textContent = 'Tạo phiên bản mới';
      b.onclick = e => { e.stopPropagation(); newVersion(r); }; td.insertBefore(b, td.firstChild);
    });
    const h = el.querySelector('.page-heading');
    if (h && !el.querySelector('#ckDue')) h.insertAdjacentHTML('afterend', `<div class="notice info mb" id="ckDue">${I('info')}<div>Hạn làm: Sơ chế ${OFFSET.PREP}; Vệ sinh khu sơ chế ${OFFSET.PREP_AREA_CLEAN}; Làm lạnh nhanh ${OFFSET.BLAST_CHILL}; Vệ sinh bếp ${OFFSET.KITCHEN_CLEAN}; bộ tự tạo – trong ca (hạn = hết ca). Bộ hệ thống không đổi mã / ngừng / xóa – sửa bằng nút «Tạo phiên bản mới».</div></div>`);
  };
  function newVersion(r) {
    const tomorrow = U.addDays(U.day(Store.ctx.date || new Date()), 1);
    UI.modal({ title: 'Tạo phiên bản mới – ' + esc(r.Name || r.Code || ''), sub: `Phiên bản hiện tại ${U.num(r.VersionNo || 1)} – chép toàn bộ tiêu chí sang phiên bản mới; bản cũ hết hiệu lực trước ngày áp dụng.`, size: 'sm',
      body: `<div class="field"><label>Áp dụng từ ngày <span class="req">*</span></label><input type="date" class="input" id="nvFrom" min="${tomorrow}" value="${tomorrow}"></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="nvGo">Tạo phiên bản</button>',
      onMount: (m, close) => m.querySelector('#nvGo').onclick = () => Store.act('FoodSafetyChecklist/NewVersion', { form: { id: r.Id, effectiveFrom: m.querySelector('#nvFrom').value }, tables: ['FoodSafetyChecklist', 'FoodSafetyChecklistItem'] })
        .then(x => { if (x.ok) { UI.toast(esc(x.message || 'Đã tạo phiên bản mới'), 'ok'); close(); APP.refresh(); } }) });
  }
})();
