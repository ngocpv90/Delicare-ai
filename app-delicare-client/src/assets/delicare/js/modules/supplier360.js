/* UI-3 (D-67) – Màn chi tiết nhà cung cấp cả trang: #/…/scm-supplier-360?id=<SupplierId>.
 * Mở khi bấm dòng ở danh sách NCC; mở từ màn khác vẫn là hộp Tổng quan + nút "Mở hồ sơ đầy đủ" (modules/scm.js).
 * Quyền = quyền xem scm-supplier (Store.perm alias). Tab hiện theo quyền xem màn tương ứng, tải khi mở lần đầu. Khung dùng chung: modules/detail360.js. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const ID = 'scm-supplier-360';
  APP.FN[ID] = { ph: 'P4', grp: 'Mua hàng & Kho', fn: 'Nhà cung cấp', id: ID, name: 'Nhà cung cấp', types: ['DT'], table: null, rel: [], st: 'MAPPED', note: '' };
  const { pas, failBox, loading, none, can, initials, dmy, pct, table, bindRows, filterBar, statusOpts, run, demoNote, alertBox, bindGo, meter } = APP.D360;
  const live = () => Store.isLive;
  const today = () => Store.ctx.date;
  const PO_ST = ['Draft', 'PendingApproval', 'Approved', 'Sent', 'PartiallyReceived', 'Received', 'Closed', 'Cancelled'];
  const GR_ST = ['QCPending', 'Accepted', 'PartiallyAccepted', 'Rejected', 'Posted', 'Cancelled'];
  const LOT_ST = ['Available', 'OnHold', 'Expired', 'Consumed'];
  const ISSUE_ST = ['Open', 'InProgress', 'Resolved', 'Closed'];
  const daysLate = v => { if (!v) return 0; const n = Math.round((new Date(today()) - new Date(U.day(v))) / 86400000); return n > 0 ? n : 0; };

  // 4 thẻ chỉ số (D-67 b, chuẩn thẻ D-64 c).
  APP.supplierKpis = d => UI.kpi('PO đang mở', d ? U.num(d.openPoCount || 0) : '–', d ? '· ' + U.moneyShort(d.openPoAmount || 0) : '', 'clipboard', 'blue', can('scm-po') ? 'scm-po' : '')
    + UI.kpi('Giao đúng hạn 90 ngày', d ? pct(d.onTimeRate) : '–', d ? `${U.num(d.receiptCount90d || 0)} lần nhận` : '', 'truck', 'green')
    + UI.kpi('Hàng bị loại 90 ngày', d ? pct(d.rejectRate) : '–', d && d.lastReceiptDate ? 'gần nhất ' + dmy(d.lastReceiptDate) : '', 'alert', d && d.rejectRate > 0 ? 'red' : 'green')
    + UI.kpi('Sự cố đang mở', d ? U.num(d.openIssueCount || 0) : '–', d ? `/ ${U.num(d.issueCount || 0)}${d.overdueIssueCount ? ' · ' + U.num(d.overdueIssueCount) + ' quá hạn' : ''}` : '', 'shield', d && d.overdueIssueCount ? 'red' : 'orange', can('qc-supplier-issue') ? 'qc-supplier-issue' : '');

  APP.register(ID, (el, f, params) => {
    const id = params && params.id ? String(params.id).toLowerCase() : '';   // Guid từ máy chủ là chữ thường – so sánh nhất quán
    if (!id) { el.innerHTML = APP.header(f, '', { noNote: true }) + failBox('Chưa chọn nhà cung cấp – mở từ danh sách nhà cung cấp.'); return; }
    const local = Store.get('Supplier', id);
    el.innerHTML = `<div class="page-head"><div><div class="breadcrumb small"><a href="javascript:void 0" data-back>${I('down')} Danh sách nhà cung cấp</a> / <span data-s360code>${esc(local ? local.Code || '' : '')}</span></div></div></div>
      <!-- UI-2R fix (D-80 d): 1 khung bọc dòng tên + hàng chỉ số gọn (.kpis.compact) --><div class="card d360-top"><div data-s360head>${local ? headHtml({ supplier: local }, true) : loading}</div>
      <div class="kpis compact" data-s360kpi>${APP.supplierKpis(null)}</div></div>
      <div class="tabs c360-tabs" data-s360tabs></div><div data-s360pane></div>`;
    el.querySelector('[data-back]').onclick = () => APP.go('scm-supplier');
    const ov = live() ? Store.query('Supplier/GetOverview', { id }) : Promise.resolve({ ok: true, data: demoOverview(id) });
    // UI-2R: màn có thể được vẽ lại trước khi API trả về – chỉ lần vẽ mới nhất dựng tab (tránh 2 bộ tab chồng nhau).
    const tok = el.dataset.d360 = String(Date.now() + Math.random());
    ov.then(x => {
      if (!el.isConnected || el.dataset.d360 !== tok) return;
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
    // Thông tin phụ 1 dòng: mã · MST · người liên hệ · SĐT · email (bỏ ô trống; địa chỉ xem ở tab Tổng quan).
    return APP.D360.headRow({ avatar: initials(s.Name), avatarCls: 'biz', actions: !partial,
      title: `${esc(s.Name || '')} ${s.SupplierType ? UI.tag(s.SupplierType, 'blue') : ''} ${s.Status ? UI.tag(s.Status === 'Inactive' ? 'Ngừng giao dịch' : s.Status === 'Active' ? 'Đang giao dịch' : s.Status, s.Status === 'Inactive' ? 'gray' : 'green') : ''}`,
      meta: [esc(s.Code || ''), s.TaxCode && esc('MST ' + s.TaxCode), s.ContactName && esc('Liên hệ: ' + [s.ContactName, s.ContactPhone].filter(Boolean).join(' – ')),
        s.Phone && `${I('phone')} ${esc(s.Phone)}`, s.Email && esc(s.Email)],
      buttons: [{ act: 'edit', icon: 'settings', label: 'Sửa', ok: p.edit !== false }, { act: 'price', icon: 'plus', label: 'Thêm giá NL', ok: can('scm-supplier-material', 'add'), primary: true },
        { act: 'issue', icon: 'alert', label: 'Ghi sự cố', ok: can('qc-supplier-issue', 'add') }],
      more: ['<button data-h="print">In hồ sơ</button>', p.edit !== false && s.Status !== 'Inactive' && '<button data-h="inactive">Ngừng giao dịch</button>'] });
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

  // ---------- Tab Tổng quan (D-67 d → UI-2R, D-69 e–f): trái 1/3 khối thông tin rút gọn, phải 2/3 khối nghiệp vụ ----------
  const ST_VI = { Active: 'Đang giao dịch', Inactive: 'Ngừng giao dịch' };
  function infoCfg(d, id) {
    const s = sup(d), t = today();
    const join = (...a) => a.filter(Boolean).join(' · ');
    const mats = d.listMaterial || [];
    const current = mats.filter(m => (!m.effectiveFrom || U.day(m.effectiveFrom) <= t) && (!m.effectiveTo || U.day(m.effectiveTo) >= t));
    const ingCount = new Set(mats.map(m => m.ingredientId)).size, prefCount = new Set(current.filter(m => m.isPreferred).map(m => m.ingredientId)).size;
    const at = (dt, by) => join(dt && U.date(dt), by);
    const since = d.firstTradeDate ? U.day(d.firstTradeDate).slice(5, 7) + '/' + U.day(d.firstTradeDate).slice(0, 4) : '';
    return {
      key: 'supplier', title: 'Thông tin nhà cung cấp', canEdit: Store.perm('scm-supplier').edit !== false,
      onEdit: () => UI.form('Supplier', Store.get('Supplier', id) || s, { title: 'Nhà cung cấp', onSave: () => APP.refresh() }),
      core: [
        { icon: 'user', html: s.ContactName ? `<b>${esc(s.ContactName)}</b> <span class="ip-muted">· người liên hệ</span>` : '' },
        { icon: 'phone', html: [UI.tel(s.ContactPhone, 'liên hệ'), s.Phone !== s.ContactPhone ? UI.tel(s.Phone, 'công ty') : ''].filter(Boolean).join(' · ') },
        { icon: 'mail', html: UI.mail(s.Email) },
        { icon: 'pin', html: esc(s.Address) },
        { icon: 'tag', html: s.SupplierType || ingCount ? `${esc(s.SupplierType || 'Chưa phân loại')}${ingCount ? ` <span class="ip-muted">· ${U.num(ingCount)} nguyên liệu${prefCount ? ' · ' + U.num(prefCount) + ' ưu tiên' : ''}</span>` : ''}` : '' },
        { icon: 'truck', html: d.lastReceiptDate ? `Nhận hàng gần nhất ${dmy(d.lastReceiptDate)}${d.lastReceiptNumber ? ` <span class="ip-muted">· ${esc(d.lastReceiptNumber)}</span>` : ''}` : '' },
      ],
      groups: [
        { key: 'legal', title: 'Pháp lý', open: true, summary: s.TaxCode ? 'MST ' + s.TaxCode : '',
          rows: [['Tên đầy đủ', esc(s.Name)], ['Mã số thuế', s.TaxCode ? esc(s.TaxCode) + UI.copyBtn(s.TaxCode) : ''], ['Địa chỉ đăng ký', esc(s.Address)]] },
        { key: 'class', title: 'Phân loại', summary: join(s.SupplierType, ST_VI[s.Status] || s.Status),
          rows: [['Loại NCC', esc(s.SupplierType)], ['Trạng thái', esc(ST_VI[s.Status] || s.Status || '')], ['Bắt đầu giao dịch', esc(since)]] },
        { key: 'note', title: 'Ghi chú', html: esc(s.Note || ''), summary: String(s.Note || '').split('\n')[0].slice(0, 60) },
        { key: 'sys', title: 'Hệ thống', rows: [['Tạo lúc / bởi', esc(at(s.CreatedDate, d.createdByName))], ['Cập nhật gần nhất', esc(at(s.ModifiedDate, d.modifiedByName))]],
          summary: s.ModifiedDate ? 'Cập nhật ' + at(s.ModifiedDate, d.modifiedByName) : s.CreatedDate ? 'Tạo ' + at(s.CreatedDate, d.createdByName) : '' },
      ],
    };
  }
  function overview(box, d, id) {
    const t = today();
    const daysTo = v => Math.round((new Date(U.day(v)) - new Date(t)) / 86400000);
    const current = (d.listMaterial || []).filter(m => (!m.effectiveFrom || U.day(m.effectiveFrom) <= t) && (!m.effectiveTo || U.day(m.effectiveTo) >= t))
      .sort((a, b) => (b.isPreferred - a.isPreferred) || ((a.effectiveTo ? daysTo(a.effectiveTo) : 9999) - (b.effectiveTo ? daysTo(b.effectiveTo) : 9999)) || String(a.ingredientName || '').localeCompare(b.ingredientName || ''));
    const matHtml = current.length ? current.slice(0, 6).map(m => {
      const left = m.effectiveTo ? daysTo(m.effectiveTo) : null;
      const sub = [m.effectiveTo ? `Hiệu lực đến ${dmy(m.effectiveTo)}${left !== null && left <= 15 ? ` · <span class="dc-warn">còn ${left} ngày</span>` : ''}` : 'Không thời hạn',
        m.isLowest && m.supplierCount > 1 ? `thấp nhất trong ${m.supplierCount} NCC` : ''].filter(Boolean).join(' · ');
      return `<div class="dc-it"><div class="top"><b>${esc(m.ingredientName || '')}${m.isPreferred ? ' ' + UI.tag('Ưu tiên', 'green') : ''}</b><span class="ip-muted">${U.money(m.unitPrice || 0)}</span></div><div class="sub">${sub}</div></div>`;
    }).join('') + `<div class="ip-muted mt">${U.num(current.length)} giá đang hiệu lực${can('scm-supplier-material') ? ' · <a href="javascript:void 0" data-tabprice>Xem bảng giá</a>' : ''}</div>` : '<div class="muted small">Chưa có giá đang hiệu lực</div>';
    const pos = (d.listOpenPurchaseOrder || []).map(p => (p.PurchaseOrderNumber !== undefined ? p : pas(p)));
    const cfg = infoCfg(d, id);
    box.innerHTML = `<div class="dc-two">${UI.infoPanel(cfg)}<div class="dc-col">${alertBox(d.listAlert)}
      <div class="dc-pair">
        <div class="card panel"><div class="spread"><h3>Nguyên liệu cung cấp</h3>${can('scm-supplier-material', 'add') ? `<button class="btn xs" data-price>${I('plus')} Thêm giá</button>` : ''}</div>${matHtml}</div>
        <div class="card panel"><div class="spread"><h3>Đánh giá nhà cung cấp</h3><span class="ip-muted">90 ngày</span></div>
          ${meter('Giao đúng hạn', d.onTimeRate, '', 90)}
          ${meter('Đạt QC đầu vào', d.qcPassRate, '', 95)}
          ${meter('Sự cố xử lý đúng hạn', d.issueOnTimePct, '', 90)}
          <div class="ip-muted">${U.num(d.receiptCount90d || 0)} lần nhận hàng · ${U.num(d.issueCount || 0)} sự cố${d.openIssueCount ? ` (${U.num(d.openIssueCount)} đang mở)` : ''}</div></div>
      </div>
      <div class="card panel"><div class="spread"><h3>Giá trị mua 6 tháng</h3><div class="c360-legend" data-legend></div></div><div data-trend>${loading}</div></div>
      <div class="card panel"><div class="spread"><h3>Đơn mua đang mở</h3>${can('scm-po') ? `<button class="btn xs" data-go="scm-po">Xem tất cả</button>` : ''}</div><div data-pos></div></div>
    </div></div>`;
    UI.wireInfoPanel(box, cfg);
    bindGo(box);
    const addPrice = box.querySelector('[data-price]'); if (addPrice) addPrice.onclick = () => priceForm(id);
    const tabPrice = box.querySelector('[data-tabprice]'); if (tabPrice) tabPrice.onclick = () => { const b = [...document.querySelectorAll('[data-s360tabs] .tab')].find(x => /Bảng giá/.test(x.textContent)); if (b) b.click(); else APP.go('scm-supplier-material'); };
    const posBox = box.querySelector('[data-pos]');
    posBox.innerHTML = table(pos, [{ label: 'Số PO', html: p => `<b>${esc(p.PurchaseOrderNumber || p.PONo || '')}</b>` },
      { label: 'Hẹn giao', html: p => dmy(p.ExpectedDeliveryDate || p.ExpectedDate) },
      { label: 'Giá trị', num: true, html: p => U.money(p.TotalAmount || 0) },
      { label: 'Trạng thái', num: true, html: p => { const late = ['Approved', 'Sent', 'PartiallyReceived'].includes(p.Status) ? daysLate(p.ExpectedDeliveryDate || p.ExpectedDate) : 0; return late ? `<span class="badge red">Trễ ${late} ngày</span>` : UI.badge(p.Status, 'PurchaseOrder', 'Status'); } }], true);
    bindRows(posBox, pos, p => APP.openDetail('PurchaseOrder', p.Id));
    // Giá trị mua theo tháng (Supplier/GetTrend) – cột, tháng hiện tại đậm.
    const trendBox = box.querySelector('[data-trend]'), legend = box.querySelector('[data-legend]');
    (live() ? Store.query('Supplier/GetTrend', { id }) : Promise.resolve({ ok: false, message: 'Chế độ DEMO không có số liệu xu hướng' })).then(x => {
      if (!trendBox.isConnected) return;
      if (!x.ok || !x.data) { trendBox.innerHTML = failBox(x.message); return; }
      const ms = x.data.listMonth || [];
      if (!ms.some(m => m.poAmount || m.receivedAmount)) { trendBox.innerHTML = none; return; }
      const mil = v => Math.round((v || 0) / 100000) / 10;   // triệu đồng, 1 chữ số thập phân
      const S = [{ name: 'PO đặt (triệu đ)', values: ms.map(m => mil(m.poAmount)), color: '#4C83B6' }, { name: 'Đã nhận – ghi sổ (triệu đ)', values: ms.map(m => mil(m.receivedAmount)), color: '#91AF42' }];
      const labels = ms.map(m => { const v = U.day(m.month); return 'T' + (+v.slice(5, 7)); });
      const cur = ms[ms.length - 1];
      legend.innerHTML = S.map(v => `<span><i style="background:${v.color}"></i>${v.name}</span>`).join('');
      trendBox.innerHTML = `${UI.bars(labels, S, { h: 220, title: 'Giá trị mua 6 tháng', unit: '₫', labelHead: 'Tháng' })}<div class="ip-muted">${esc(labels[0] + ' → ' + labels[labels.length - 1])} · theo phiếu nhập đã ghi sổ · tháng này <b>${U.money(cur ? cur.receivedAmount || 0 : 0)}</b></div>`;
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
    { label: 'Tri thức', render: (box, d, id) => APP.D360.knowledgeTab('supplier')(box, d, id) }, // AI-17 (D-133)
    { label: 'Tài liệu', render: (box, d, id) => APP.D360.documentsTab('Supplier', Store.perm('scm-supplier').edit !== false)(box, d, id) },
    { label: 'Lịch sử', render: APP.D360.historyTab('Supplier') },
  ];
})();
