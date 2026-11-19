/* UI-3 (D-67) – Màn chi tiết nhà cung cấp cả trang: #/…/scm-supplier-360?id=<SupplierId>.
 * Mở khi bấm dòng ở danh sách NCC; mở từ màn khác vẫn là hộp Tổng quan + nút "Mở hồ sơ đầy đủ" (modules/scm.js).
 * Quyền = quyền xem scm-supplier (Store.perm alias). Tab hiện theo quyền xem màn tương ứng, tải khi mở lần đầu. Khung dùng chung: modules/detail360.js. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const ID = 'scm-supplier-360';
  APP.FN[ID] = { ph: 'P4', grp: 'Mua hàng & Kho', fn: 'Hồ sơ nhà cung cấp', id: ID, name: 'Hồ sơ nhà cung cấp', types: ['DT'], table: null, rel: [], st: 'MAPPED', note: '' };
  const { pas, failBox, loading, none, can, initials, dmy, pct, table, bindRows, filterBar, statusOpts, run, demoNote, alertBox, bindGo, meter } = APP.D360;
  const live = () => Store.isLive;
  const today = () => Store.ctx.date;
  const PO_ST = ['Draft', 'PendingApproval', 'Approved', 'Sent', 'PartiallyReceived', 'Received', 'Closed', 'Cancelled'];
  const GR_ST = ['QCPending', 'Accepted', 'PartiallyAccepted', 'Rejected', 'Posted', 'Cancelled'];
  const LOT_ST = ['Available', 'OnHold', 'Expired', 'Consumed'];
  const ISSUE_ST = ['Open', 'InProgress', 'Resolved', 'Closed'];
  const daysLate = v => { if (!v) return 0; const n = Math.round((new Date(today()) - new Date(U.day(v))) / 86400000); return n > 0 ? n : 0; };

  // 4 thẻ chỉ số (D-67 b, chuẩn thẻ D-64 c).
  APP.supplierKpis = d => UI.kpi('PO đang mở', d ? U.num(d.openPoCount || 0) : '–', d ? U.money(d.openPoAmount || 0) : '', 'clipboard', 'blue', can('scm-po') ? 'scm-po' : '')
    + UI.kpi('Giao đúng hạn 90 ngày', d ? pct(d.onTimeRate) : '–', d ? `${U.num(d.receiptCount90d || 0)} lần nhận` : '', 'truck', 'green')
    + UI.kpi('Hàng bị loại 90 ngày', d ? pct(d.rejectRate) : '–', d && d.lastReceiptDate ? 'gần nhất ' + dmy(d.lastReceiptDate) : '', 'alert', d && d.rejectRate > 0 ? 'red' : 'green')
    + UI.kpi('Sự cố đang mở', d ? U.num(d.openIssueCount || 0) : '–', d ? `/ ${U.num(d.issueCount || 0)}${d.overdueIssueCount ? ' · ' + U.num(d.overdueIssueCount) + ' quá hạn' : ''}` : '', 'shield', d && d.overdueIssueCount ? 'red' : 'orange', can('qc-supplier-issue') ? 'qc-supplier-issue' : '');

  APP.register(ID, (el, f, params) => {
    const id = params && params.id ? String(params.id).toLowerCase() : '';   // Guid từ máy chủ là chữ thường – so sánh nhất quán
    if (!id) { el.innerHTML = APP.header(f, '', { noNote: true }) + failBox('Chưa chọn nhà cung cấp – mở từ danh sách nhà cung cấp.'); return; }
    const local = Store.get('Supplier', id);
    el.innerHTML = `<div class="page-head"><div><div class="breadcrumb small"><a href="javascript:void 0" data-back>${I('down')} Danh sách nhà cung cấp</a> / <span data-s360code>${esc(local ? local.Code || '' : '')}</span></div></div></div>
      <div data-s360head>${local ? headHtml({ supplier: local }, true) : loading}</div>
      <div class="kpis" data-s360kpi>${APP.supplierKpis(null)}</div>
      <div class="tabs c360-tabs" data-s360tabs></div><div data-s360pane></div>`;
    el.querySelector('[data-back]').onclick = () => APP.go('scm-supplier');
    const ov = live() ? Store.query('Supplier/GetOverview', { id }) : Promise.resolve({ ok: true, data: demoOverview(id) });
    ov.then(x => {
      if (!el.isConnected) return;
      if (!x.ok || !x.data) { el.querySelector('[data-s360head]').innerHTML = failBox(x.message); return; }
      const d = x.data;
      el.querySelector('[data-s360head]').innerHTML = headHtml(d, false);
      el.querySelector('[data-s360code]').textContent = (d.supplier && (d.supplier.code || d.supplier.Code)) || '';
      el.querySelector('[data-s360kpi]').innerHTML = APP.supplierKpis(d);
      bindGo(el.querySelector('[data-s360kpi]'));
      bindHead(el, d, id);
      APP.D360.buildTabs(el.querySelector('[data-s360tabs]'), el.querySelector('[data-s360pane]'), TABS, d, id);
    });
  });

  // ---------- Phần đầu (D-67 b) – không có "Tạo PO" (PO chỉ lập từ đề nghị mua) ----------
  const sup = d => (d.supplier ? (d.supplier.Code !== undefined || d.supplier.Name !== undefined ? d.supplier : pas(d.supplier)) : {});
  function headHtml(d, partial) {
    const s = sup(d), p = Store.perm('scm-supplier');
    const meta = [s.Code, s.TaxCode && 'MST ' + s.TaxCode].filter(Boolean).map(esc).join(' · ');
    const contact = [s.ContactName && `<span>${I('users')}${esc([s.ContactName, s.ContactPhone].filter(Boolean).join(' · '))}</span>`, s.Phone && `<span>${I('msg')}${esc(s.Phone)}</span>`,
      s.Address && `<span>${I('pin')}${esc(s.Address)}</span>`, s.Email && `<span>${esc(s.Email)}</span>`].filter(Boolean).join('');
    const btn = (act, icon, label, ok, primary) => ok ? `<button class="btn ${primary ? 'primary' : ''}" data-h="${act}">${I(icon)} ${label}</button>` : '';
    return `<div class="card panel"><div class="c360-head">
      <div class="c360-avatar biz">${esc(initials(s.Name))}</div>
      <div class="c360-id"><h1>${esc(s.Name || '')} ${s.SupplierType ? UI.tag(s.SupplierType, 'blue') : ''} ${s.Status ? UI.tag(s.Status === 'Inactive' ? 'Ngừng giao dịch' : s.Status === 'Active' ? 'Đang giao dịch' : s.Status, s.Status === 'Inactive' ? 'gray' : 'green') : ''}</h1>
        <div class="c360-meta">${meta}</div><div class="c360-meta">${contact}</div></div>
      ${partial ? '' : `<div class="c360-actions">${btn('edit', 'settings', 'Sửa', p.edit !== false)}${btn('price', 'plus', 'Thêm giá NL', can('scm-supplier-material', 'add'), true)}${btn('issue', 'alert', 'Ghi sự cố', can('qc-supplier-issue', 'add'))}
        <button class="btn" data-h="more" aria-label="Thêm thao tác">⋯</button>
        <div class="c360-more hide" data-hmore><button data-h="print">In hồ sơ</button>${p.edit !== false && s.Status !== 'Inactive' ? '<button data-h="inactive">Ngừng giao dịch</button>' : ''}</div></div>`}
    </div></div>`;
  }
  const priceForm = (id, row) => UI.form('SupplierMaterial', row || null, { title: 'Giá nguyên liệu của NCC', defaults: row ? undefined : { SupplierId: id, IsPreferred: false, Status: 'Active', EffectiveFrom: today() }, onSave: () => APP.refresh() });
  function bindHead(el, d, id) {
    APP.D360.bindMore(el.querySelector('[data-s360head]'), act => {
      const row = Store.get('Supplier', id) || sup(d);
      if (act === 'edit') UI.form('Supplier', row, { title: 'Nhà cung cấp', onSave: () => APP.refresh() });
      else if (act === 'price') priceForm(id);
      else if (act === 'issue') UI.form('SupplierIssue', null, { title: 'Ghi sự cố nhà cung cấp', defaults: { SupplierId: id, IssueStatus: 'Open' }, onSave: () => APP.refresh() });
      else if (act === 'print') window.print();
      else if (act === 'inactive') UI.confirm(`Ngừng giao dịch với <b>${esc(row.Name || '')}</b>? NCC ngừng giao dịch không lập đơn mua mới được.`, () => {
        Store.save('Supplier', 'Update', Object.assign({}, row, { Status: 'Inactive' })).then(r => { if (!r.ok) { UI.toast(esc(r.message || 'Không lưu được'), 'err'); return; } UI.toast('Đã ngừng giao dịch nhà cung cấp', 'ok'); APP.refresh(); });
      }, { danger: true, ok: 'Ngừng giao dịch' });
    });
  }

  // ---------- Tab Tổng quan (D-67 d) ----------
  function overview(box, d, id) {
    const s = sup(d);
    const rows = list => `<div class="c360-rows">${list.filter(x => x[1]).map(([l, v]) => `<div class="r"><label>${esc(l)}</label><span>${v}</span></div>`).join('') || none}</div>`;
    const info = [['Tên NCC', esc(s.Name)], ['Mã', esc(s.Code)], ['Loại NCC', esc(s.SupplierType)], ['Mã số thuế', esc(s.TaxCode)], ['Người liên hệ', esc([s.ContactName, s.ContactPhone].filter(Boolean).join(' · '))],
      ['Điện thoại', esc(s.Phone)], ['Email', esc(s.Email)], ['Địa chỉ', esc(s.Address)], ['Ghi chú', esc(s.Note)]];
    const t = today();
    const current = (d.listMaterial || []).filter(m => (!m.effectiveFrom || U.day(m.effectiveFrom) <= t) && (!m.effectiveTo || U.day(m.effectiveTo) >= t))
      .sort((a, b) => (b.isPreferred - a.isPreferred) || String(a.ingredientName || '').localeCompare(b.ingredientName || ''));
    const matHtml = current.length ? `<div class="c360-rows">${current.slice(0, 12).map(m => `<div class="r"><label>${esc(m.ingredientName || '')}${m.isPreferred ? ' ' + UI.tag('Ưu tiên', 'green') : ''}</label><span><b>${U.money(m.unitPrice || 0)}</b>${m.effectiveTo ? `<span class="small muted"> · đến ${dmy(m.effectiveTo)}</span>` : ''}</span></div>`).join('')}</div>${current.length > 12 ? `<div class="small muted mt">… và ${current.length - 12} nguyên liệu khác (tab Bảng giá)</div>` : ''}` : '<div class="muted small">Chưa có giá đang hiệu lực</div>';
    const pos = (d.listOpenPurchaseOrder || []).map(p => (p.PurchaseOrderNumber !== undefined ? p : pas(p)));
    box.innerHTML = `${alertBox(d.listAlert)}
      <div class="c360-grid">
        <div class="card panel"><div class="spread"><h3>Thông tin nhà cung cấp</h3>${Store.perm('scm-supplier').edit !== false ? `<button class="btn xs" data-edit>${I('settings')} Sửa</button>` : ''}</div>${rows(info)}</div>
        <div class="card panel"><div class="spread"><h3>Nguyên liệu cung cấp</h3>${can('scm-supplier-material', 'add') ? `<button class="btn xs" data-price>${I('plus')} Thêm giá</button>` : ''}</div>${matHtml}</div>
        <div class="card panel"><h3>Đánh giá nhà cung cấp</h3>
          ${meter('Giao đúng hạn (90 ngày)', d.onTimeRate, d.receiptCount90d ? `${U.num(d.receiptCount90d)} lần nhận` : 'Chưa nhận hàng 90 ngày', 90)}
          ${meter('Đạt QC (90 ngày, theo số lượng)', d.qcPassRate, '', 95)}
          ${meter('Sự cố xử lý đúng hạn', d.issueOnTimePct, d.issueCount ? `${U.num(d.issueCount)} sự cố` : 'Chưa có sự cố', 90)}</div>
      </div>
      <div class="c360-grid two">
        <div class="card panel"><h3>Giá trị mua 6 tháng</h3><div data-trend>${loading}</div></div>
        <div class="card panel"><div class="spread"><h3>Đơn mua đang mở</h3>${can('scm-po') ? `<a href="javascript:void 0" data-go="scm-po" class="small">Xem tất cả</a>` : ''}</div><div data-pos></div></div>
      </div>`;
    bindGo(box);
    const edit = box.querySelector('[data-edit]'); if (edit) edit.onclick = () => UI.form('Supplier', Store.get('Supplier', id) || s, { title: 'Nhà cung cấp', onSave: () => APP.refresh() });
    const addPrice = box.querySelector('[data-price]'); if (addPrice) addPrice.onclick = () => priceForm(id);
    const posBox = box.querySelector('[data-pos]');
    posBox.innerHTML = table(pos, [{ label: 'Số PO', html: p => `<b>${esc(p.PurchaseOrderNumber || p.PONo || '')}</b>` },
      { label: 'Hẹn giao', html: p => { const exp = p.ExpectedDeliveryDate || p.ExpectedDate, late = ['Approved', 'Sent', 'PartiallyReceived'].includes(p.Status) ? daysLate(exp) : 0; return `${dmy(exp)}${late ? ` <span class="bad">trễ ${late} ngày</span>` : ''}`; } },
      { label: 'Giá trị', num: true, html: p => U.money(p.TotalAmount || 0) }, { label: 'Trạng thái', html: p => UI.badge(p.Status, 'PurchaseOrder', 'Status') }], true);
    bindRows(posBox, pos, p => APP.openDetail('PurchaseOrder', p.Id));
    // Biểu đồ giá trị mua theo tháng (Supplier/GetTrend): giá trị PO đặt và giá trị đã nhận (phiếu Đã ghi sổ).
    const trendBox = box.querySelector('[data-trend]');
    (live() ? Store.query('Supplier/GetTrend', { id }) : Promise.resolve({ ok: false, message: 'Chế độ DEMO không có số liệu xu hướng' })).then(x => {
      if (!trendBox.isConnected) return;
      if (!x.ok || !x.data) { trendBox.innerHTML = failBox(x.message); return; }
      const ms = x.data.listMonth || [];
      const mil = v => Math.round((v || 0) / 100000) / 10;   // triệu đồng, 1 chữ số thập phân – trục không bị cắt số
      const S = [{ name: 'Giá trị PO đặt (triệu đ)', values: ms.map(m => mil(m.poAmount)), color: '#4C83B6' }, { name: 'Đã nhận – ghi sổ (triệu đ)', values: ms.map(m => mil(m.receivedAmount)), color: '#91AF42' }];
      const labels = ms.map(m => { const v = U.day(m.month); return v.slice(5, 7) + '/' + v.slice(2, 4); });
      const rates = ms.filter(m => m.onTimePct !== null && m.onTimePct !== undefined).map(m => `${U.day(m.month).slice(5, 7)}: ${U.num(m.onTimePct, 0)}%`).join(' · ');
      trendBox.innerHTML = ms.some(m => m.poAmount || m.receivedAmount) ? `${UI.bars(labels, S, { h: 220, title: 'Giá trị mua 6 tháng' })}<div class="c360-legend">${S.map(v => `<span><i style="background:${v.color}"></i>${v.name}</span>`).join('')}</div>${rates ? `<div class="small muted mt">Đúng hạn theo tháng: ${rates}</div>` : ''}` : none;
    });
  }

  // Chế độ DEMO: dựng hồ sơ từ Store.
  function demoOverview(id) {
    const s = Store.get('Supplier', id) || {};
    const lc = o => { const r = {}; Object.entries(o || {}).forEach(([k, v]) => { r[k.charAt(0).toLowerCase() + k.slice(1)] = v; }); return r; };
    const mats = Store.by('SupplierMaterial', 'SupplierId', id).map(m => Object.assign(lc(m), { ingredientName: Store.label('Ingredient', m.IngredientId) }));
    const pos = Store.by('PurchaseOrder', 'SupplierId', id).filter(p => !['Received', 'Closed', 'Cancelled'].includes(p.Status));
    const iss = Store.by('SupplierIssue', 'SupplierId', id);
    return { supplier: s, listMaterial: mats, listOpenPurchaseOrder: pos, openPoCount: pos.length, openPoAmount: U.sum(pos, 'TotalAmount'), issueCount: iss.length,
      openIssueCount: iss.filter(x => ['Open', 'InProgress'].includes(x.IssueStatus)).length, listAlert: [] };
  }

  // ---------- Các tab còn lại (D-67 e) ----------
  function priceTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    const canAdd = can('scm-supplier-material', 'add'), canEdit = can('scm-supplier-material', 'edit');
    filterBar(box, { dates: false, extra: [['active', 'Đang hiệu lực'], ['all', 'Tất cả']], extraDefault: 'active', tools: canAdd ? `<span class="grow"></span><button class="btn primary" data-addp>${I('plus')} Thêm giá NL</button>` : '' }, (st, body) => {
      body.innerHTML = loading;
      const on = st.extra === 'active' ? today() : undefined;
      // So giá: giá đang hiệu lực thấp nhất của NCC khác cùng nguyên liệu.
      Promise.all([Store.query('SupplierMaterial/GetAll', { SupplierId: id, EffectiveOn: on, Page: 1, PageSize: 500 }), Store.query('SupplierMaterial/GetAll', { EffectiveOn: today(), Page: 1, PageSize: 1000 })]).then(([x, all]) => {
        if (!body.isConnected) return;
        if (!x.ok) { body.innerHTML = failBox(x.message); return; }
        const other = {};
        (all.ok ? all.list || [] : []).filter(r => String(r.supplierId).toLowerCase() !== id && r.unitPrice).forEach(r => { const o = other[r.ingredientId]; if (!o || r.unitPrice < o.unitPrice) other[r.ingredientId] = r; });
        const rows = (x.list || []).sort((a, b) => String(a.ingredientName || '').localeCompare(b.ingredientName || ''));
        body.innerHTML = table(rows, [{ label: 'Nguyên liệu', html: r => `<b>${esc(r.ingredientName || '')}</b>${r.isPreferred ? ' ' + UI.tag('Ưu tiên', 'green') : ''}` },
          { label: 'Đơn giá', num: true, html: r => U.money(r.unitPrice || 0) },
          { label: 'Thấp nhất NCC khác', num: true, html: r => { const o = other[r.ingredientId]; if (!o) return '<span class="muted">–</span>'; const cheaper = o.unitPrice < (r.unitPrice || 0); return `<span class="${cheaper ? 'bad' : ''}" title="${esc(o.supplierName || '')}">${U.money(o.unitPrice)}</span><span class="sub">${esc(o.supplierName || '')}</span>`; } },
          { label: 'Hiệu lực', html: r => `${dmy(r.effectiveFrom) || '…'} – ${r.effectiveTo ? dmy(r.effectiveTo) : 'không hạn'}` },
          { label: 'Đặt tối thiểu', num: true, html: r => (r.minimumOrderQuantity ? U.num(r.minimumOrderQuantity) : '') }, { label: 'Giao sau (ngày)', num: true, html: r => (r.leadTimeDays ?? '') },
          { label: '', html: r => (canEdit ? `${r.isPreferred ? '' : `<button class="btn xs" data-pref="${esc(r.id)}">Ưu tiên</button> `}<button class="btn xs" data-ep="${esc(r.id)}">Sửa</button>` : '') }], true);
        const byId = {}; rows.forEach(r => byId[r.id] = r);
        const entity = r => Object.assign(pas(r), Store.get('SupplierMaterial', r.id) || {});
        body.querySelectorAll('[data-ep]').forEach(b => b.onclick = () => priceForm(id, entity(byId[b.dataset.ep])));
        body.querySelectorAll('[data-pref]').forEach(b => b.onclick = () => {
          const r = byId[b.dataset.pref];
          UI.confirm(`Đặt <b>${esc(r.ingredientName || '')}</b> của NCC này là <b>ưu tiên</b>? Mỗi nguyên liệu chỉ có 1 NCC ưu tiên.`, () =>
            Store.save('SupplierMaterial', 'Update', Object.assign(entity(r), { IsPreferred: true })).then(x2 => { if (!x2.ok) { UI.toast(esc(x2.message || 'Không lưu được'), 'err'); return; } UI.toast('Đã đặt NCC ưu tiên', 'ok'); APP.refresh(); }), { ok: 'Đặt ưu tiên' });
        });
        bindRows(body, rows, r => APP.openDetail('SupplierMaterial', r.id));
      });
    });
    const add = box.querySelector('[data-addp]'); if (add) add.onclick = () => priceForm(id);
  }
  function poTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    filterBar(box, { from: U.addDays(today(), -90), to: today(), statuses: statusOpts('PurchaseOrder', 'Status', PO_ST) }, (st, body) => {
      body.innerHTML = loading;
      run(body, Store.query('PurchaseOrder/GetAll', { SupplierId: id, FromOrderDate: st.from, ToOrderDate: st.to, Status: st.status, Page: 1, PageSize: 200, SortField: 'orderDate', SortOrder: 'descend' }), x => {
        const rows = x.list || [];
        body.innerHTML = `<div class="small muted mb">${U.num(x.res && x.res.totalRecords || rows.length)} đơn mua · ${U.money(U.sum(rows, 'totalAmount'))}</div>` + table(rows, [
          { label: 'Số PO', html: r => `<b>${esc(r.purchaseOrderNumber || '')}</b>` }, { label: 'Ngày đặt', html: r => dmy(r.orderDate) },
          { label: 'Hẹn giao', html: r => { const late = ['Approved', 'Sent', 'PartiallyReceived'].includes(r.status) ? daysLate(r.expectedDeliveryDate) : 0; return `${dmy(r.expectedDeliveryDate)}${late ? ` <span class="bad">trễ ${late} ngày</span>` : ''}`; } },
          { label: 'Đề nghị mua', html: r => esc(r.purchaseRequestName || '') }, { label: 'Giá trị', num: true, html: r => U.money(r.totalAmount || 0) }, { label: 'Trạng thái', html: r => UI.badge(r.status, 'PurchaseOrder', 'Status') }], true);
        bindRows(body, rows, r => APP.openDetail('PurchaseOrder', r.id));
      });
    });
  }
  function receiptTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    filterBar(box, { from: U.addDays(today(), -90), to: today(), statuses: statusOpts('GoodsReceipt', 'Status', GR_ST) }, (st, body) => {
      body.innerHTML = loading;
      run(body, Store.query('GoodsReceipt/GetAll', { SupplierId: id, FromReceiptDate: st.from, ToReceiptDate: st.to, Status: st.status, Page: 1, PageSize: 200, SortField: 'receiptDate', SortOrder: 'descend' }), x => {
        const rows = x.list || [];
        body.innerHTML = table(rows, [{ label: 'Phiếu nhập', html: r => `<b>${esc(r.receiptNumber || '')}</b>` }, { label: 'Ngày nhận', html: r => dmy(r.receiptDate) },
          { label: 'Đơn mua', html: r => esc(r.purchaseOrderName || '') }, { label: 'Kho', html: r => esc(r.warehouseName || '') }, { label: 'Kết quả QC / trạng thái', html: r => UI.badge(r.status, 'GoodsReceipt', 'Status') }], true);
        bindRows(body, rows, r => APP.openDetail('GoodsReceipt', r.id));
      });
    });
  }
  function lotTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    filterBar(box, { dates: false, statuses: statusOpts('MaterialLot', 'Status', LOT_ST) }, (st, body) => {
      body.innerHTML = loading;
      run(body, Store.query('MaterialLot/GetAll', { SupplierId: id, Status: st.status, Page: 1, PageSize: 200, SortField: 'receivedDate', SortOrder: 'descend' }), x => {
        const rows = x.list || [];
        const soon = r => r.expiryDate && r.status === 'Available' && U.day(r.expiryDate) <= U.addDays(today(), 2);
        body.innerHTML = table(rows, [{ label: 'Số lô', html: r => `<b>${esc(r.lotNumber || '')}</b>` }, { label: 'Nguyên liệu', html: r => esc(r.ingredientName || '') },
          { label: 'Ngày nhận', html: r => dmy(r.receivedDate) }, { label: 'Hạn dùng', html: r => `${dmy(r.expiryDate)}${soon(r) ? ' <span class="bad">cận date</span>' : ''}` },
          { label: 'QC', html: r => { const q = r.qualityStatus || r.qcStatus || ''; return esc(({ DAT: 'Đạt', PASS: 'Đạt', KHONG_DAT: 'Không đạt', REJECT: 'Từ chối', HOLD: 'Tạm giữ' })[q] || q); } }, { label: 'Trạng thái', html: r => UI.badge(r.status, 'MaterialLot', 'Status') }], true);
        bindRows(body, rows, r => APP.openDetail('MaterialLot', r.id));
      });
    });
  }
  function issueTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    const canEdit = can('qc-supplier-issue', 'edit');
    const BTN = { InProgress: 'Nhận xử lý', Resolved: 'Đã xử lý', Closed: 'Đóng' };
    filterBar(box, { dates: false, statuses: statusOpts('SupplierIssue', 'IssueStatus', ISSUE_ST) }, (st, body) => {
      body.innerHTML = loading;
      run(body, Store.query('SupplierIssue/GetList', { SupplierId: id, Status: st.status, Page: 1, PageSize: 200 }), x => {
        const rows = (x.data && x.data.listItem) || [];
        body.innerHTML = table(rows, [{ label: 'Ghi nhận', html: r => esc(U.dt((r.issue || {}).reportedAt)) }, { label: 'Loại', html: r => esc((r.issue || {}).issueType || '') },
          { label: 'Mức độ', html: r => esc(r.severityName || (r.issue || {}).severity || '') }, { label: 'Nội dung', html: r => esc(String((r.issue || {}).description || '').slice(0, 90)) },
          { label: 'Chứng từ', html: r => esc([r.purchaseOrderNumber, r.goodsReceiptNumber].filter(Boolean).join(' · ')) },
          { label: 'Hạn xử lý', html: r => `${esc(U.dt((r.issue || {}).dueAt))}${r.isOverdue ? ' <span class="bad">quá hạn</span>' : ''}` }, { label: 'Phụ trách', html: r => esc(r.ownerName || '') },
          { label: 'Trạng thái', html: r => UI.tag(r.statusName || (r.issue || {}).issueStatus || '', r.isOverdue ? 'red' : 'orange') },
          { label: '', html: r => (canEdit && APP.supplierIssueTransit ? (r.allowedTransitions || []).map(to => `<button class="btn xs ${to === 'Resolved' ? 'orange' : ''}" data-to="${esc(to)}" data-iid="${esc((r.issue || {}).id)}">${to === 'InProgress' && (r.issue || {}).issueStatus === 'Resolved' ? 'Mở lại' : BTN[to] || esc(to)}</button>`).join(' ') : '') }], true);
        const byId = {}; rows.forEach(r => { if (r.issue) byId[r.issue.id] = r; });
        body.querySelectorAll('[data-to]').forEach(b => b.onclick = () => APP.supplierIssueTransit(pas(byId[b.dataset.iid].issue), b.dataset.to));
        bindRows(body, rows, r => APP.openDetail('SupplierIssue', (r.issue || {}).id));
      });
    });
  }

  const TABS = [
    { label: 'Tổng quan', render: overview },
    { label: 'Bảng giá nguyên liệu', screen: 'scm-supplier-material', render: priceTab },
    { label: 'Đơn mua', screen: 'scm-po', render: poTab },
    { label: 'Nhận hàng và QC', screen: 'scm-gr', render: receiptTab },
    { label: 'Lô nguyên liệu', screen: 'scm-lot', render: lotTab },
    { label: 'Sự cố', screen: 'qc-supplier-issue', render: issueTab },
    { label: 'Tài liệu', render: (box, d, id) => APP.D360.documentsTab('Supplier', Store.perm('scm-supplier').edit !== false)(box, d, id) },
    { label: 'Lịch sử', render: APP.D360.historyTab('Supplier') },
  ];
})();
