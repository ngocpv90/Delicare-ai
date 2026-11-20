/* UI-2 (D-66) – Màn khách hàng 360 cả trang: #/…/crm-customer-360?id=<CustomerId>.
 * Mở khi bấm dòng ở danh sách khách hàng; mở từ màn khác vẫn là ngăn xem nhanh + nút "Mở hồ sơ đầy đủ" (modules/sales.js).
 * Quyền = quyền xem crm-customer (Store.perm alias). Mỗi tab chỉ hiện khi có quyền xem màn tương ứng, tải dữ liệu khi mở lần đầu. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const ID = 'crm-customer-360';
  APP.FN[ID] = { ph: 'P1', grp: 'Kinh doanh & Đơn hàng', fn: 'Hồ sơ khách hàng 360', id: ID, name: 'Hồ sơ khách hàng 360', types: ['DT'], table: null, rel: [], st: 'MAPPED', note: '' };

  // Thành phần chung (modules/detail360.js – UI-3 tách từ UI-2).
  const { pas, failBox, loading, none, can, initials, dmy, table, bindRows, filterBar, statusOpts, run, demoNote } = APP.D360;
  const statusBadge = (t, col, v, name) => (v ? UI.badge(v, t, col).replace(esc(UI.statusVi(t, col, v)), esc(name || UI.statusVi(t, col, v))) : '');

  APP.register(ID, (el, f, params) => {
    const id = params && params.id;
    const local = id && Store.get('Customer', id);
    if (!id) { el.innerHTML = APP.header(f, '', { noNote: true }) + failBox('Chưa chọn khách hàng – mở từ danh sách khách hàng.'); return; }
    // Vẽ khung ngay, dữ liệu máy chủ điền sau (GetOverview 1 lần cho phần đầu + thẻ + tab Tổng quan).
    el.innerHTML = `<div class="page-head"><div><div class="breadcrumb small"><a href="javascript:void 0" data-back>${I('down')} Danh sách khách hàng</a> / <span data-c360code>${esc(local ? local.Code || '' : '')}</span></div></div></div>
      <div data-c360head>${local ? headHtml({ customer: pas(local) }, true) : loading}</div>
      <div class="kpis" data-c360kpi>${APP.customerKpis(null)}</div>
      <div class="tabs c360-tabs" data-c360tabs></div><div data-c360pane></div>`;
    el.querySelector('[data-back]').onclick = () => APP.go('crm-customer');
    const ov = Store.isLive ? Store.query('Customer/GetOverview', { id }) : Promise.resolve({ ok: true, data: demoOverview(id) });
    ov.then(x => {
      if (!el.isConnected) return;
      if (!x.ok || !x.data) { el.querySelector('[data-c360head]').innerHTML = failBox(x.message); return; }
      const d = x.data;
      el.querySelector('[data-c360head]').innerHTML = headHtml(d, false);
      el.querySelector('[data-c360code]').textContent = (d.customer && d.customer.code) || '';
      el.querySelector('[data-c360kpi]').innerHTML = APP.customerKpis(d);
      el.querySelectorAll('[data-c360kpi] [data-go]').forEach(b => b.onclick = () => APP.go(b.dataset.go));
      bindHead(el, d, id);
      buildTabs(el, d, id);
    });
  });

  // ---------- Phần đầu (D-66 b) ----------
  function headHtml(d, partial) {
    const c = d.customer ? (d.customer.Code !== undefined ? d.customer : pas(d.customer)) : {};
    const ind = String(c.CustomerType || '').toUpperCase() === 'INDIVIDUAL';
    const name = c.DisplayName || c.Name || '';
    const meta = [c.Code, c.Industry, d.branchName && 'Chi nhánh ' + d.branchName, d.userAssignName && 'Phụ trách: ' + d.userAssignName].filter(Boolean).map(esc).join(' · ');
    const contact = [c.Address && `<span>${I('pin')}${esc(c.Address)}</span>`, c.PhoneNumber && `<span>${I('msg')}${esc(c.PhoneNumber)}</span>`,
      ind ? c.IdentityNumber && `<span>CCCD ${esc(c.IdentityNumber)}</span>` : c.TaxCode && `<span>MST ${esc(c.TaxCode)}</span>`].filter(Boolean).join('');
    const p = Store.perm('crm-customer');
    const btn = (act, icon, label, ok) => ok ? `<button class="btn ${act === 'order' ? 'primary' : ''}" data-h="${act}">${I(icon)} ${label}</button>` : '';
    return `<div class="card panel"><div class="c360-head">
      <div class="c360-avatar ${ind ? 'ind' : 'biz'}">${esc(initials(name))}</div>
      <div class="c360-id"><h1>${esc(name)} ${UI.tag(ind ? 'Cá nhân' : 'Doanh nghiệp', ind ? 'blue' : 'green')} ${c.Status ? UI.badge(c.Status, 'Customer', 'Status') : ''}</h1>
        <div class="c360-meta">${meta}</div><div class="c360-meta">${contact}</div></div>
      ${partial ? '' : `<div class="c360-actions">${btn('edit', 'edit', 'Sửa', p.edit !== false)}${btn('order', 'plus', 'Tạo đơn', can('crm-order', 'add'))}${btn('complaint', 'msg', 'Ghi phản ánh', can('log-complaint', 'add'))}
        <button class="btn" data-h="more" aria-label="Thêm thao tác">⋯</button>
        <div class="c360-more hide" data-hmore>${can('crm-contract', 'add') ? '<button data-h="contract">Tạo hợp đồng</button>' : ''}<button data-h="print">In hồ sơ</button>${p.edit !== false && c.Status !== 'Inactive' ? '<button data-h="inactive">Ngừng hoạt động</button>' : ''}</div></div>`}
    </div></div>`;
  }
  function bindHead(el, d, id) {
    const box = el.querySelector('[data-c360head]');
    const more = box.querySelector('[data-hmore]');
    box.querySelectorAll('[data-h]').forEach(b => b.onclick = e => {
      const act = b.dataset.h;
      if (act === 'more') { e.stopPropagation(); more.classList.toggle('hide'); return; }
      if (more) more.classList.add('hide');
      const row = Store.get('Customer', id) || pas(d.customer);
      if (act === 'edit') UI.form('Customer', row, { title: 'Khách hàng', onSave: () => APP.refresh() });
      else if (act === 'order') APP.go('crm-order?customer=' + encodeURIComponent(id));
      else if (act === 'complaint') APP.go('log-complaint?customer=' + encodeURIComponent(id));
      else if (act === 'contract') APP.go('crm-contract?customer=' + encodeURIComponent(id));
      else if (act === 'print') window.print();
      else if (act === 'inactive') UI.confirm(`Ngừng hoạt động khách <b>${esc(row.DisplayName || row.Name || '')}</b>? Khách ngừng hoạt động không tạo đơn / hợp đồng mới được.`, () => {
        Store.save('Customer', 'Update', Object.assign({}, row, { Status: 'Inactive' })).then(r => { if (!r.ok) { UI.toast(esc(r.message || 'Không lưu được'), 'err'); return; } UI.toast('Đã ngừng hoạt động khách hàng', 'ok'); APP.refresh(); });
      }, { danger: true, ok: 'Ngừng hoạt động' });
    });
    document.addEventListener('click', () => more && more.classList.add('hide'), { once: true });
  }

  // ---------- Hàng tab (D-66 c) – chỉ tab có quyền xem màn tương ứng ----------
  function buildTabs(el, d, id) { APP.D360.buildTabs(el.querySelector('[data-c360tabs]'), el.querySelector('[data-c360pane]'), TABS, d, id); }

  // ---------- Tab Tổng quan (D-66 d) ----------
  function overview(box, d, id) {
    const c = pas(d.customer), ind = String(c.CustomerType || '').toUpperCase() === 'INDIVIDUAL';
    const rows = list => `<div class="c360-rows">${list.filter(x => x[1]).map(([l, v]) => `<div class="r"><label>${esc(l)}</label><span>${v}</span></div>`).join('') || none}</div>`;
    const info = ind
      ? [['Họ tên', esc(c.Name)], ['Giới tính', esc(c.Gender)], ['Ngày sinh', dmy(c.DateOfBirth)], ['CCCD', esc([c.IdentityNumber, c.IdentityDate && 'cấp ' + U.date(c.IdentityDate), c.IdentityPlace].filter(Boolean).join(' · '))],
        ['Nghề nghiệp', esc(c.Occupation)], ['Điện thoại', esc(c.PhoneNumber)], ['Email', esc(c.Email)], ['MST cá nhân', esc(c.TaxCode)], ['Nguồn', esc(c.Source)], ['Ghi chú', esc(c.Note)]]
      : [['Tên pháp lý', esc(c.LegalName || c.Name)], ['Tên giao dịch', esc(c.DisplayName)], ['Mã số thuế', esc(c.TaxCode)], ['Người đại diện pháp luật', esc(c.LegalRepresentativeName)],
        ['Người liên hệ', esc([c.ContactPersonName, c.ContactPersonPhone].filter(Boolean).join(' · '))], ['Ngành / quy mô', esc([c.Industry, c.CompanySize].filter(Boolean).join(' · '))],
        ['Phân khúc / nguồn', esc([c.Segment, c.Source].filter(Boolean).join(' · '))], ['Email', esc(c.Email)], ['Ghi chú', esc(c.Note)]];
    const locs = (d.listLocation || []).map(pas), contacts = (d.listContact || []).map(pas);
    const locHtml = locs.map(l => `<div class="r"><label>${esc(l.Name || l.Code || '')}${l.DeliveryWindowFrom ? ' · ' + esc(U.time(l.DeliveryWindowFrom)) + '–' + esc(U.time(l.DeliveryWindowTo)) : ''}</label><span>${esc(l.Address || '')}</span></div>`).join('');
    const ctHtml = contacts.map(x => `<div class="r"><label>${esc(x.FullName || '')}${x.IsPrimary ? ' · liên hệ chính' : ''}</label><span>${esc(x.Phone || x.Email || '')}</span></div>`).join('');
    const today = Store.ctx.date;
    const active = (d.listContract || []).filter(x => x.status === 'Active' && U.day(x.startDate || x.effectiveFrom || '') <= today && (!(x.endDate || x.effectiveTo) || U.day(x.endDate || x.effectiveTo) >= today));
    const contractHtml = active.length ? active.map(x => `<div class="mb"><div class="spread"><b>${esc(x.contractNumber || '')}</b>${UI.badge(x.status, 'Contract', 'Status')}</div>
        <div class="small muted">${dmy(x.startDate)} – ${x.endDate ? dmy(x.endDate) : 'không hạn'} · ${(x.listMeal || []).length} loại suất</div>
        <div class="small">${(x.listMeal || []).map(m => `${esc(m.mealName || m.mealType || '')} ${m.unitPrice ? U.money(m.unitPrice) : ''}`).join(' · ')}</div>
        <div class="row-flex mt"><button class="btn xs" data-ct="${esc(x.id)}">${I('eye')} Xem</button>${can('crm-contract', 'add') ? `<button class="btn xs" data-renew>Gia hạn</button>` : ''}</div></div>`).join('') : none;
    const alerts = d.listAlert || [];
    box.innerHTML = `${alerts.length ? `<div class="c360-alert">${I('alert')}<div><b>Cần chú ý</b><ul>${alerts.map(a => `<li class="sev-${esc(a.severity)}">${a.screen && can(a.screen) ? `<a data-go="${esc(a.screen)}">${esc(a.message)}</a>` : esc(a.message)}</li>`).join('')}</ul></div></div>` : ''}
      <div class="c360-grid">
        <div class="card panel"><div class="spread"><h3>Thông tin khách hàng</h3>${Store.perm('crm-customer').edit !== false ? `<button class="btn xs" data-edit>${I('edit')} Sửa</button>` : ''}</div>${rows(info)}</div>
        <div class="card panel"><h3>Điểm giao và liên hệ</h3><div class="c360-rows">${locHtml || '<div class="muted small">Chưa có điểm giao</div>'}${ctHtml}</div>
          <div class="row-flex mt">${can('crm-location', 'add') ? `<button class="btn xs" data-add="CustomerDeliveryLocation">${I('plus')} Thêm điểm giao</button>` : ''}${can('crm-contact', 'add') ? `<button class="btn xs" data-add="CustomerContact">${I('plus')} Thêm liên hệ</button>` : ''}</div></div>
        <div class="card panel"><h3>Hợp đồng hiện hành</h3>${contractHtml}</div>
      </div>
      <div class="c360-grid two">
        <div class="card panel"><h3>Suất đặt – giao – nhận 30 ngày</h3><div data-trend>${loading}</div></div>
        <div class="card panel"><div class="spread"><h3>Đơn hàng gần đây</h3><div class="row-flex"><select class="select" data-ost style="width:auto"><option value="">Mọi trạng thái</option>${['Draft', 'Submitted', 'Confirmed', 'Locked', 'InProduction', 'Delivered', 'Reconciled', 'Cancelled'].map(s => `<option value="${s}">${esc(UI.statusVi('Order', 'Status', s))}</option>`).join('')}</select>${can('crm-order') ? `<a href="javascript:void 0" data-go="crm-order" class="small">Xem tất cả</a>` : ''}</div></div><div data-orders></div></div>
      </div>`;
    box.querySelectorAll('[data-go]').forEach(a => a.onclick = () => APP.go(a.dataset.go));
    const edit = box.querySelector('[data-edit]'); if (edit) edit.onclick = () => UI.form('Customer', Store.get('Customer', id) || c, { title: 'Khách hàng', onSave: () => APP.refresh() });
    box.querySelectorAll('[data-add]').forEach(b => b.onclick = () => UI.form(b.dataset.add, null, { title: b.dataset.add === 'CustomerContact' ? 'Người liên hệ' : 'Điểm giao', defaults: { CustomerId: id }, onSave: () => APP.refresh() }));
    box.querySelectorAll('[data-ct]').forEach(b => b.onclick = () => APP.openDetail('Contract', b.dataset.ct, APP.FN['crm-contract']));
    box.querySelectorAll('[data-renew]').forEach(b => b.onclick = () => APP.go('crm-contract?customer=' + encodeURIComponent(id)));
    // Biểu đồ 30 ngày (Customer/GetTrend).
    const trendBox = box.querySelector('[data-trend]');
    (Store.isLive ? Store.query('Customer/GetTrend', { id }) : Promise.resolve({ ok: false, message: 'Chế độ DEMO không có số liệu xu hướng' })).then(x => {
      if (!trendBox.isConnected) return;
      if (!x.ok || !x.data) { trendBox.innerHTML = failBox(x.message); return; }
      const days = x.data.listDay || [];
      const labels = days.map(r => U.date(r.date).slice(0, 5));
      const S = [{ name: 'Đặt', values: days.map(r => r.ordered || 0), color: '#91AF42' }, { name: 'Giao', values: days.map(r => r.delivered || 0), color: '#4C83B6' }, { name: 'Nhận', values: days.map(r => r.accepted || 0), color: '#F07825' }];
      const mix = (x.data.listMealType || []).map(m => `${esc(m.mealType)} ${U.num(m.quantity)}`).join(' · ');
      trendBox.innerHTML = days.some(r => r.ordered || r.delivered || r.accepted) ? `${UI.lines(labels, S, { min: 0, h: 220 })}<div class="c360-legend">${S.map(s => `<span><i style="background:${s.color}"></i>${s.name}</span>`).join('')}</div>${mix ? `<div class="small muted mt">Cơ cấu loại suất: ${mix}</div>` : ''}` : none;
    });
    // Đơn hàng gần đây – lọc trạng thái (Order/GetAll theo khách).
    const ordBox = box.querySelector('[data-orders]'), sel = box.querySelector('[data-ost]');
    const loadOrders = () => {
      ordBox.innerHTML = loading;
      const q = Store.isLive ? Store.query('Order/GetAll', { CustomerId: id, Status: sel.value, Page: 1, PageSize: 8, SortField: 'mealDate', SortOrder: 'descend' })
        : Promise.resolve({ ok: true, list: Store.by('Order', 'CustomerId', id).filter(o => !sel.value || o.Status === sel.value).sort((a, b) => String(b.MealDate).localeCompare(a.MealDate)).slice(0, 8).map(o => ({ id: o.Id, orderNumber: o.OrderNumber, mealDate: o.MealDate, totalQuantity: o.TotalQuantity, status: o.Status })) });
      q.then(x => {
        if (!ordBox.isConnected) return;
        if (!x.ok) { ordBox.innerHTML = failBox(x.message); return; }
        const list = x.list || [];
        ordBox.innerHTML = table(list, [{ label: 'Số đơn', html: o => `<b>${esc(o.orderNumber || '')}</b>` }, { label: 'Ngày ăn', html: o => dmy(o.mealDate) },
          { label: 'Suất', num: true, html: o => U.num(o.totalQuantity || 0) }, { label: 'Trạng thái', html: o => UI.badge(o.status, 'Order', 'Status') }], true);
        bindRows(ordBox, list, o => APP.openDetail('Order', o.id));
      });
    };
    sel.onchange = loadOrders; loadOrders();
  }

  // Chế độ DEMO: dựng dữ liệu hồ sơ từ Store (không có máy chủ).
  function demoOverview(id) {
    const c = Store.get('Customer', id) || {};
    const lc = o => { const r = {}; Object.entries(o || {}).forEach(([k, v]) => { r[k.charAt(0).toLowerCase() + k.slice(1)] = v; }); return r; };
    const contracts = Store.by('Contract', 'CustomerId', id).map(x => Object.assign(lc(x), { startDate: x.EffectiveFrom, endDate: x.EffectiveTo, listMeal: Store.by('ContractMeal', 'ContractId', x.Id).map(lc) }));
    const cpl = Store.by('CustomerComplaint', 'CustomerId', id);
    return {
      customer: lc(c), listContact: Store.by('CustomerContact', 'CustomerId', id).map(lc), listLocation: Store.by('CustomerDeliveryLocation', 'CustomerId', id).map(lc), listContract: contracts,
      activeContractCount: contracts.filter(x => x.status === 'Active').length, avgMealsPerDay7d: APP.customerAvgMeals7d ? APP.customerAvgMeals7d(new Set([id])) : 0,
      reconciledRevenue: 0, reconciledOrderCount: 0, openComplaintCount: cpl.filter(x => !['Resolved', 'Closed', 'Rejected'].includes(x.ComplaintStatus)).length, complaintCount: cpl.length, listAlert: [],
    };
  }

  // ---------- Các tab còn lại (D-66 e) – mỗi tab gọi API đã có, lọc theo khách ----------
  const live = () => Store.isLive;
  const iso = d => U.day(d);
  const addD = (d, n) => U.addDays(d, n);

  function contractsTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    filterBar(box, { dates: false, statuses: statusOpts('Contract', 'Status', ['Draft', 'PendingApproval', 'Active', 'Expired', 'Terminated']) }, (st, body) => {
      body.innerHTML = loading;
      run(body, Store.query('Contract/GetAll', { CustomerId: id, Status: st.status, Page: 1, PageSize: 100 }), x => {
        const rows = x.list || [];
        body.innerHTML = table(rows, [{ label: 'Số hợp đồng', html: r => `<b>${esc(r.contractNumber || '')}</b>` }, { label: 'Tên', html: r => esc(r.name || '') },
          { label: 'Hiệu lực', html: r => `${dmy(r.effectiveFrom || r.startDate)} – ${r.effectiveTo || r.endDate ? dmy(r.effectiveTo || r.endDate) : 'không hạn'}` },
          { label: 'Giá trị', num: true, html: r => (r.contractValue ? U.money(r.contractValue) : '') }, { label: 'Trạng thái', html: r => UI.badge(r.status, 'Contract', 'Status') }], true);
        bindRows(body, rows, r => APP.openDetail('Contract', r.id, APP.FN['crm-contract']));
      });
    });
  }
  function ordersTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    const today = Store.ctx.date;
    filterBar(box, { from: addD(today, -30), to: addD(today, 7), statuses: statusOpts('Order', 'Status', ['Draft', 'Submitted', 'Confirmed', 'Locked', 'InProduction', 'Delivered', 'Reconciled', 'Cancelled']) }, (st, body) => {
      body.innerHTML = loading;
      run(body, Store.query('Order/GetAll', { CustomerId: id, FromMealDate: st.from, ToMealDate: st.to, Status: st.status, Page: 1, PageSize: 200, SortField: 'mealDate', SortOrder: 'descend' }), x => {
        const rows = x.list || [];
        body.innerHTML = `<div class="small muted mb">${U.num(x.res && x.res.totalRecords || rows.length)} đơn · ${U.num(U.sum(rows, 'totalQuantity'))} suất · ${U.money(U.sum(rows, 'totalAmount'))}</div>` + table(rows, [
          { label: 'Số đơn', html: r => `<b>${esc(r.orderNumber || '')}</b>` }, { label: 'Ngày ăn', html: r => dmy(r.mealDate) }, { label: 'Điểm giao', html: r => esc(r.deliveryLocationName || '') },
          { label: 'Suất', num: true, html: r => U.num(r.totalQuantity || 0) }, { label: 'Thành tiền', num: true, html: r => U.money(r.totalAmount || 0) }, { label: 'Trạng thái', html: r => UI.badge(r.status, 'Order', 'Status') }], true);
        bindRows(body, rows, r => APP.openDetail('Order', r.id));
      });
    });
  }
  function deliveryTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    filterBar(box, { dates: false, statuses: statusOpts('DeliveryOrder', 'Status', ['Planned', 'InTransit', 'Delivered', 'Failed', 'Cancelled']) }, (st, body) => {
      body.innerHTML = loading;
      run(body, Store.query('DeliveryOrder/GetList', { customerId: id, status: st.status }), x => {
        const rows = (x.list || []).sort((a, b) => String((b.deliveryOrder || {}).deliveryDate).localeCompare((a.deliveryOrder || {}).deliveryDate)).slice(0, 200);
        body.innerHTML = table(rows, [{ label: 'Đơn giao', html: r => `<b>${esc((r.deliveryOrder || {}).deliveryNumber || '')}</b>` }, { label: 'Ngày giao', html: r => dmy((r.deliveryOrder || {}).deliveryDate) },
          { label: 'Đơn hàng', html: r => esc(r.orderNumber || '') }, { label: 'Điểm giao', html: r => esc(r.locationName || '') }, { label: 'Chuyến', html: r => esc(r.routeNo || '') },
          { label: 'Suất', num: true, html: r => U.num(r.mealQuantity || 0) }, { label: 'Trạng thái', html: r => UI.tag(r.statusName || (r.deliveryOrder || {}).status || '', 'blue') }], true);
        bindRows(body, rows, r => APP.openDetail('DeliveryOrder', (r.deliveryOrder || {}).id));
      });
    });
  }
  function reconTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    const today = Store.ctx.date;
    filterBar(box, { from: addD(today, -60), to: today }, (st, body) => {
      body.innerHTML = loading;
      run(body, Store.query('OrderReconciliation/GetList', { customerId: id, from: st.from, to: st.to, page: 1, pageSize: 200 }), x => {
        const rows = (x.data && x.data.listItem) || [];
        body.innerHTML = `<div class="small muted mb">${U.num(rows.length)} đơn · nhận ${U.num(U.sum(rows, 'acceptedQuantity'))} suất · ${U.money(U.sum(rows, 'finalAmount'))}</div>` + table(rows, [
          { label: 'Đơn hàng', html: r => `<b>${esc(r.orderNumber || '')}</b>` }, { label: 'Ngày ăn', html: r => dmy(r.mealDate) }, { label: 'Đặt', num: true, html: r => U.num(r.orderedQuantity || 0) },
          { label: 'Giao', num: true, html: r => U.num(r.deliveredQuantity || 0) }, { label: 'Nhận', num: true, html: r => U.num(r.acceptedQuantity || 0) },
          { label: 'Lệch %', num: true, html: r => (r.variancePercent === null || r.variancePercent === undefined ? '' : U.num(r.variancePercent, 1) + '%') },
          { label: 'Thành tiền', num: true, html: r => U.money(r.finalAmount || 0) }, { label: 'Trạng thái', html: r => UI.tag(r.statusName || r.status || '', 'green') }], true);
        bindRows(body, rows, r => APP.openDetail('Order', r.orderId));
      });
    });
  }
  function packagingTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    box.innerHTML = `<h3>Số dư khay / hộp ở khách</h3><div data-bal>${loading}</div><h3 class="mt">Lần thu hồi</h3><div data-ret>${loading}</div>`;
    const bal = box.querySelector('[data-bal]'), ret = box.querySelector('[data-ret]');
    run(bal, Store.query('PackagingLedger/GetBalance', { customerId: id }), x => {
      const rows = (x.data && x.data.listItem) || [];
      bal.innerHTML = table(rows, [{ label: 'Loại', html: r => esc(r.packagingType || '') }, { label: 'Đã giao', num: true, html: r => U.num(r.outQuantity || 0) },
        { label: 'Đã thu', num: true, html: r => U.num(r.inQuantity || 0) }, { label: 'Hỏng', num: true, html: r => U.num(r.damagedInQuantity || 0) }, { label: 'Thất thoát', num: true, html: r => U.num(r.lossQuantity || 0) },
        { label: 'Còn ở khách', num: true, html: r => `<b>${U.num(r.outstanding || 0)}</b>` },
        { label: 'Tỷ lệ thất thoát', num: true, html: r => (r.lossRatePct === null || r.lossRatePct === undefined ? '' : `<span class="${r.isOverThreshold ? 'bad' : ''}">${U.num(r.lossRatePct, 1)}%</span> / ${U.num(r.lossThresholdPct, 1)}%`) }]);
    });
    run(ret, Store.query('ContainerReturn/GetList', { customerId: id, page: 1, pageSize: 100 }), x => {
      const rows = (x.data && x.data.listItem) || [];
      ret.innerHTML = table(rows, [{ label: 'Ngày thu', html: r => dmy((r.return || {}).returnDate) }, { label: 'Đơn giao', html: r => esc(r.deliveryNumber || '') }, { label: 'Loại', html: r => esc(r.packagingType || '') },
        { label: 'Phải thu', num: true, html: r => U.num((r.return || {}).expectedQuantity || 0) }, { label: 'Thu về', num: true, html: r => U.num((r.return || {}).returnedQuantity || 0) },
        { label: 'Hỏng', num: true, html: r => U.num((r.return || {}).damagedQuantity || 0) }, { label: 'Trạng thái', html: r => UI.tag(r.statusName || '', 'blue') }, { label: 'Kiểm tra', html: r => esc(r.inspectionResultName || '') }]);
    });
  }
  function complaintTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    filterBar(box, { dates: false, statuses: statusOpts('CustomerComplaint', 'ComplaintStatus', ['Open', 'InProgress', 'Resolved', 'Closed', 'Rejected']) }, (st, body) => {
      body.innerHTML = loading;
      run(body, Store.query('CustomerComplaint/GetList', { customerId: id, status: st.status, page: 1, pageSize: 200 }), x => {
        const rows = (x.data && x.data.listItem) || [];
        body.innerHTML = table(rows, [{ label: 'Số', html: r => `<b>${esc((r.complaint || {}).complaintNo || '')}</b>` }, { label: 'Nhận lúc', html: r => U.dt ? U.dt((r.complaint || {}).receivedAt) : dmy((r.complaint || {}).receivedAt) },
          { label: 'Mức độ', html: r => esc(r.severityName || '') }, { label: 'Nội dung', html: r => esc(String((r.complaint || {}).description || '').slice(0, 90)) },
          { label: 'Hạn xử lý', html: r => `${U.dt ? U.dt((r.complaint || {}).slaDueAt) : dmy((r.complaint || {}).slaDueAt)}${r.isOverdue ? ' <span class="bad">quá hạn</span>' : ''}` },
          { label: 'Người xử lý', html: r => esc(r.assignedToName || '') }, { label: 'Trạng thái', html: r => UI.tag(r.statusName || '', r.isOverdue ? 'red' : 'orange') }], true);
        bindRows(body, rows, r => APP.openDetail('CustomerComplaint', (r.complaint || {}).id));
      });
    });
  }
  function forecastTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    const today = Store.ctx.date, to = addD(today, 13);
    box.innerHTML = `<h3>Dự báo suất 14 ngày tới</h3><div data-fc>${loading}</div><h3 class="mt">Thực đơn được gán</h3><div data-ma>${loading}</div>`;
    const fc = box.querySelector('[data-fc]'), ma = box.querySelector('[data-ma]');
    run(fc, Store.query('Forecast/GetMatrix', { from: today, to, customerId: id }), x => {
      const m = x.data || {}, dates = m.dates || [], row = (m.rows || [])[0];
      if (!row || !dates.length) { fc.innerHTML = none; return; }
      const v = dt => { const k = Object.keys(row.values || {}).find(k => iso(k) === iso(dt)); return k ? row.values[k] : null; };
      fc.innerHTML = UI.bars(dates.map(dt => U.date(dt).slice(0, 5)), [{ name: 'Dự báo', values: dates.map(dt => v(dt) || 0), color: '#91AF42' }], { h: 180 })
        + `<div class="small muted">Tổng ${U.num(row.total || 0)} suất</div>`;
    });
    run(ma, Store.query('MenuAssignment/GetAll', { CustomerId: id, Page: 1, PageSize: 100 }), x => {
      const rows = x.list || [];
      ma.innerHTML = table(rows, [{ label: 'Thực đơn', html: r => `<b>${esc(r.menuName || '')}</b>` }, { label: 'Loại suất', html: r => esc(r.mealType || '') }, { label: 'Điểm giao', html: r => esc(r.deliveryLocationName || 'Mọi điểm') },
        { label: 'Hiệu lực', html: r => `${dmy(r.effectiveFrom)} – ${r.effectiveTo ? dmy(r.effectiveTo) : 'không hạn'}` }, { label: 'Trạng thái', html: r => UI.tag(r.status || '', 'green') }]);
    });
  }
  const documentsTab = (box, d, id) => APP.D360.documentsTab('Customer', Store.perm('crm-customer').edit !== false)(box, d, id);
  const historyTab = APP.D360.historyTab('Customer');

  const TABS = [
    { label: 'Tổng quan', render: overview },
    { label: 'Hợp đồng và giá', screen: 'crm-contract', render: contractsTab },
    { label: 'Đơn hàng', screen: 'crm-order', render: ordersTab },
    { label: 'Giao nhận', screen: 'log-delivery-order', render: deliveryTab },
    { label: 'Đối soát', screen: 'crm-reconciliation', render: reconTab },
    { label: 'Khay hộp', screen: 'log-container-return', render: packagingTab },
    { label: 'Phản ánh', screen: 'log-complaint', render: complaintTab },
    { label: 'Dự báo và thực đơn', screen: 'crm-forecast', render: forecastTab },
    { label: 'Tài liệu', render: documentsTab },
    { label: 'Lịch sử', render: historyTab },
  ];
})();
