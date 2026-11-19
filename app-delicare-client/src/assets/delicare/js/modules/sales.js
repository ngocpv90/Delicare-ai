/* P1/P3 – Khách hàng 360°, Đơn hàng, Đối soát, Hợp đồng, Dự báo */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const ORDER_FLOW = ['Draft', 'Submitted', 'Confirmed', 'Locked', 'InProduction', 'Delivered', 'Reconciled'];
  // ---------- Tiện ích cục bộ cho chế độ API ----------
  // API trả camelCase → đổi sang tên cột PascalCase để dùng chung hàm vẽ với dữ liệu trong Store.
  const pas = o => { const r = {}; Object.entries(o || {}).forEach(([k, v]) => { r[k.charAt(0).toUpperCase() + k.slice(1)] = v; }); return r; };
  // Màn hình được vẽ lại mỗi khi có bảng tải xong → giữ kết quả đọc vài giây để không gọi lặp; bust() sau mỗi thao tác ghi.
  const qc = {};
  function cq(key, run, ttl) { const c = qc[key]; if (c && Date.now() - c.at < (ttl || 5000)) return c.p; const p = run(); qc[key] = { at: Date.now(), p }; return p; }
  function bust() { Object.keys(qc).forEach(k => delete qc[k]); }
  const lastDrawer = () => [...document.querySelectorAll('.drawer-wrap')].pop();
  const mealTypes = cid => { const cts = cid ? new Set(Store.by('Contract', 'CustomerId', cid).map(c => c.Id)) : null; return [...new Set(Store.rows('ContractMeal').filter(m => !cts || cts.has(m.ContractId)).map(m => m.MealType).filter(Boolean))].sort(); };
  const opts = (list, lbl, first) => `<option value="">${first}</option>${list.map(r => `<option value="${esc(r.Id)}">${esc(lbl(r))}</option>`).join('')}`;
  const failBox = msg => `<div class="notice warn">${I('alert')}<div>${esc(msg || 'Không tải được dữ liệu')}</div></div>`;
  // ---------- Khách hàng 360° ----------
  // UI-1.2 (D-64): 4 thẻ của danh sách khách hàng. s = { totalCount, activeCount, activeContractCount, avgMealsPerDay7d } (null = đang tải).
  const customerCards = s => {
    const n = v => (s ? U.num(v || 0, 0) : '–');
    const pct = s && s.totalCount ? Math.round(100 * s.activeCount / s.totalCount) : 0;
    return UI.kpi('Tổng khách hàng', n(s && s.totalCount), '', 'users', 'green')
      + UI.kpi('Đang hoạt động', n(s && s.activeCount), s ? `/ ${U.num(s.totalCount)} · ${pct}%` : '', 'check-circle', 'lime')
      + UI.kpi('Hợp đồng hiệu lực', n(s && s.activeContractCount), '', 'file', 'blue', 'crm-contract')
      + UI.kpi('Suất TB/ngày (7 ngày)', n(s && s.avgMealsPerDay7d), '', 'utensils', 'orange');
  };
  // Đơn Đã giao / Đã đối soát 7 ngày gần nhất (không gồm hôm nay) ÷ 7 – giống Customer/GetSummary.
  const avgMeals7d = cusIds => {
    const today = Store.ctx.date, since = U.addDays(today, -7);
    const ords = Store.rows('Order').filter(o => (!cusIds || cusIds.has(o.CustomerId)) && ['Delivered', 'Reconciled'].includes(o.Status) && U.day(o.MealDate) >= since && U.day(o.MealDate) < today);
    return U.sum(ords, 'TotalQuantity') / 7;
  };
  APP.register('crm-customer', (el, f) => {
    const listRows = () => Store.rows('Customer').filter(c => !Store.ctx.branch || c.BranchId === Store.ctx.branch);
    const localSummary = () => {
      const rows = listRows(), ids = new Set(rows.map(r => r.Id)), today = Store.ctx.date;
      return {
        totalCount: rows.length, activeCount: rows.filter(r => r.Status === 'Active').length,
        activeContractCount: Store.rows('Contract').filter(c => ids.has(c.CustomerId) && c.Status === 'Active' && U.day(c.EffectiveFrom) <= today && (!c.EffectiveTo || U.day(c.EffectiveTo) >= today)).length,
        avgMealsPerDay7d: avgMeals7d(ids),
      };
    };
    APP.genericList(el, f, {
      kpis: customerCards(Store.isLive ? null : localSummary()),
      afterKpis: box => {
        if (!Store.isLive || !box) return;
        cq('cus-sum|' + (Store.ctx.branch || ''), () => Store.query('Customer/GetSummary', { branchId: Store.ctx.branch || '' }))
          .then(x => { if (box.isConnected) box.innerHTML = customerCards(x.ok && x.data ? x.data : localSummary()); });
      },
      cols: [{ k: 'Code', label: 'Mã' }, { k: 'DisplayName', label: 'Khách hàng' }, { k: 'CustomerType', label: 'Loại', render: r => esc(UI.statusVi('Customer', 'CustomerType', r.CustomerType) || '') }, { k: 'BranchId', label: 'Chi nhánh phục vụ' },
        { k: 'x1', label: 'Hợp đồng hiệu lực', col: false, nosort: true, render: r => { const c = Store.by('Contract', 'CustomerId', r.Id); return c.map(x => `${esc(x.ContractNumber)} ${UI.badge(x.Status, 'Contract', 'Status')}`).join('<br>'); } },
        { k: 'x2', label: 'Điểm giao', col: false, num: true, nosort: true, render: r => Store.by('CustomerDeliveryLocation', 'CustomerId', r.Id).length },
        { k: 'x3', label: 'Suất TB/ngày (7 ngày)', col: false, num: true, nosort: true, render: r => U.num(avgMeals7d(new Set([r.Id])), 0) },
        { k: 'Status', label: 'Trạng thái' }],
      rows: listRows
    });
  });
  // drawer 360 khi mở Customer từ bất kỳ đâu
  const baseDetail = APP.openDetail;
  APP.openDetail = (t, id, f, opt) => {
    if (t === 'Customer') return customer360(id, f);
    if (t === 'Order') return orderDetail(id);
    if (t === 'Contract' && Store.isLive && !opt) return contractDetail(id, f);
    return baseDetail(t, id, f, opt);
  };
  // Chế độ API: ngăn chi tiết hợp đồng thêm tab loại suất & đơn giá, yêu cầu duyệt đang chờ, cảnh báo (Contract/GetDetail).
  function contractDetail(id, f) {
    const row = Store.get('Contract', id);
    const q = Store.query('Contract/GetDetail', { id });
    const html = d => `${(d.listWarning || []).map(w => `<div class="notice warn">${I('alert')}<div>${esc(w)}</div></div>`).join('')}
      <div class="kv mb"><div><label>Khách hàng</label><span>${esc(d.customerName || '')}</span></div><div><label>Trạng thái</label><span>${esc(d.statusName || '')}</span></div><div><label>Yêu cầu duyệt đang chờ</label><span>${esc(d.pendingApprovalNo || '–')}</span></div><div><label>Đơn hàng đang mở</label><span>${U.num(d.openOrderCount || 0)}</span></div></div>
      <table class="table dc-table dt"><thead><tr><th>Loại suất</th><th>Tên suất</th><th>Hình thức phục vụ</th><th class="num">Đơn giá</th><th>Trạng thái</th></tr></thead><tbody>${(d.listMeal || []).map(m => `<tr><td>${UI.tag(m.mealType || '')}</td><td>${esc(m.mealName || '')}</td><td>${esc(m.serviceType || '')}</td><td class="num">${U.money(m.unitPrice)}</td><td>${UI.badge(m.status, 'ContractMeal', 'Status')}</td></tr>`).join('') || '<tr><td colspan="5" class="empty">Hợp đồng chưa có loại suất</td></tr>'}</tbody></table>`;
    return baseDetail('Contract', id, f, {
      noEdit: !row || row.Status !== 'Draft',   // backend chỉ cho sửa hợp đồng Nháp
      tabs: [{ label: 'Loại suất & đơn giá', html: p => { q.then(x => { if (p && p.isConnected) p.innerHTML = x.ok && x.data ? html(x.data) : failBox(x.message); }); return '<div class="muted small">Đang tải…</div>'; } }]
    });
  }
  function customer360(id, f) {
    const L = Store.isLive, tech = s => (L ? '' : s);
    const ords = Store.by('Order', 'CustomerId', id).sort((a, b) => b.MealDate.localeCompare(a.MealDate));
    const none = '<div class="muted small">Chưa có dữ liệu</div>';
    // UI-1.3 (D-64): Hợp đồng hiệu lực · Suất TB/ngày (7 ngày) · Doanh thu đã đối soát (N đơn) · Khiếu nại đang mở (/ tổng · % đúng hạn).
    const kpiHtml = d => UI.kpi('Hợp đồng hiệu lực', d ? U.num(d.activeContractCount || 0) : '–', '', 'file', 'green')
      + UI.kpi('Suất TB/ngày (7 ngày)', d ? U.num(d.avgMealsPerDay7d || 0, 0) : '–', '', 'utensils', 'orange')
      + UI.kpi('Doanh thu đã đối soát', d ? U.money(d.reconciledRevenue || 0) : '–', d ? `${U.num(d.reconciledOrderCount || 0)} đơn` : '', 'calc', 'blue')
      + UI.kpi('Khiếu nại đang mở', d ? U.num(d.openComplaintCount || 0) : '–', d ? `/ ${U.num(d.complaintCount || 0)}${d.complaintOnTimePct === null || d.complaintOnTimePct === undefined ? '' : ' · ' + U.num(d.complaintOnTimePct, 0) + '% đúng hạn'}` : '', 'msg', 'red');
    const localKpi = () => {
      const today = Store.ctx.date, cpl = Store.by('CustomerComplaint', 'CustomerId', id);
      const recon = ords.filter(o => Store.by('OrderReconciliation', 'OrderId', o.Id).length);
      return {
        activeContractCount: Store.by('Contract', 'CustomerId', id).filter(c => c.Status === 'Active' && U.day(c.EffectiveFrom) <= today && (!c.EffectiveTo || U.day(c.EffectiveTo) >= today)).length,
        avgMealsPerDay7d: avgMeals7d(new Set([id])),
        reconciledRevenue: U.sum(ords.flatMap(o => Store.by('OrderReconciliation', 'OrderId', o.Id)), 'FinalAmount'), reconciledOrderCount: recon.length,
        openComplaintCount: cpl.filter(c => !['Resolved', 'Closed', 'Rejected'].includes(c.ComplaintStatus)).length, complaintCount: cpl.length, complaintOnTimePct: null,
      };
    };
    const contactHtml = (cts, locs) => `<h3>Người liên hệ${tech(' (CustomerContact)')}</h3>${cts.map(x => `<div class="spread" style="padding:7px 0;border-bottom:1px solid #F0F2ED"><span><b>${esc(x.FullName)}</b> · ${esc(x.Position || '')}${x.IsPrimary ? ' <span class="tag green">Chính</span>' : ''}</span><span class="small muted">${esc(x.Phone || '')} · ${esc(x.Email || '')}</span></div>`).join('') || (L ? none : '')}<h3 class="mt">Điểm giao hàng${tech(' (CustomerDeliveryLocation)')}</h3>${locs.map(x => `<div style="padding:7px 0;border-bottom:1px solid #F0F2ED"><b>${esc(x.Name)}</b> ${UI.badge(x.Status, 'CustomerDeliveryLocation', 'Status')}<div class="small muted">${esc(x.Address)} · Nhận ${U.time(x.DeliveryWindowFrom)}–${U.time(x.DeliveryWindowTo)} · ${esc(x.ContactName)} ${esc(x.ContactPhone)}</div></div>`).join('') || (L ? none : '')}`;
    const contractHtml = list => list.map(({ ct, meals }) => `<div class="card panel mb"><div class="spread"><b>${esc(ct.ContractNumber)} · ${esc(ct.Name)}</b>${UI.badge(ct.Status, 'Contract', 'Status')}</div><div class="small muted">${['Hiệu lực ' + U.date(ct.EffectiveFrom) + ' – ' + U.date(ct.EffectiveTo), esc(ct.PaymentTerm || ''), 'Giá trị ' + U.money(ct.ContractValue)].filter(Boolean).join(' · ')}</div><table class="table dc-table dt mt"><thead><tr><th>Loại suất${tech('<small>ContractMeal.MealType</small>')}</th><th>Tên</th><th class="num">Đơn giá</th><th>Trạng thái</th></tr></thead><tbody>${meals.map(m => `<tr><td>${UI.tag(m.MealType)}</td><td>${esc(m.MealName)}</td><td class="num">${U.money(m.UnitPrice)}</td><td>${UI.badge(m.Status, 'ContractMeal', 'Status')}</td></tr>`).join('')}</tbody></table></div>`).join('') || (L ? none : '');
    const orderHtml = list => `<table class="table dc-table dt"><thead><tr><th>Số đơn</th><th>Ngày ăn</th><th class="num">Suất</th><th class="num">Thành tiền</th><th>Trạng thái</th></tr></thead><tbody>${list.map(o => `<tr><td><a href="javascript:void 0" data-open="Order" data-id="${o.Id}">${esc(o.OrderNumber)}</a></td><td>${U.date(o.MealDate)}</td><td class="num">${U.num(o.TotalQuantity)}</td><td class="num">${U.money(o.TotalAmount)}</td><td>${UI.badge(o.Status, 'Order', 'Status')}</td></tr>`).join('')}</tbody></table>`;
    // Chế độ API: Customer/GetOverview trả đủ hồ sơ 360° trong 1 lần gọi – vẽ ngay bản cục bộ, có kết quả thì thay.
    const ov = L ? cq('cus|' + id, () => Store.query('Customer/GetOverview', { id })) : null;
    const late = (p, fn) => { if (ov) ov.then(x => { if (x.ok && x.data && p && p.isConnected) p.innerHTML = fn(x.data); }); };
    UI.detail('Customer', id, {
      screen: 'crm-customer',
      top: `<div class="kpis" data-c360="${esc(id)}">${kpiHtml(L ? null : localKpi())}</div>`,
      tabs: [
        { label: 'Liên hệ & điểm giao', html: p => { late(p, d => contactHtml((d.listContact || []).map(pas), (d.listLocation || []).map(pas))); return contactHtml(Store.by('CustomerContact', 'CustomerId', id), Store.by('CustomerDeliveryLocation', 'CustomerId', id)); } },
        { label: 'Hợp đồng & giá', html: p => { late(p, d => contractHtml((d.listContract || []).map(x => ({ ct: Object.assign(pas(x), { EffectiveFrom: x.startDate, EffectiveTo: x.endDate }), meals: (x.listMeal || []).map(pas) })))); return contractHtml(Store.by('Contract', 'CustomerId', id).map(ct => ({ ct, meals: Store.by('ContractMeal', 'ContractId', ct.Id) }))); } },
        { label: 'Đơn hàng', html: p => { late(p, d => orderHtml((d.listRecentOrder || []).map(pas))); return orderHtml(ords.slice(0, 15)); } }
      ]
    });
    if (ov) ov.then(x => { const k = [...document.querySelectorAll('[data-c360]')].find(n => n.dataset.c360 === String(id)); if (x.ok && x.data && k) k.innerHTML = kpiHtml(x.ok && x.data ? x.data : localKpi()); });
  }
  // ---------- Đơn hàng ----------
  const os = { view: 'board', date: null };   // date: null = chưa chọn, '' = Tất cả
  APP.register('crm-order', (el, f, params) => {
    const perm = Store.perm(f.id);
    const dates = [...new Set(Store.rows('Order').map(o => U.day(o.MealDate)))].sort();
    // Ngày mặc định (chỉ đặt 1 lần, khi đã có dữ liệu): ngày mai nếu có đơn, không thì ngày gần nhất từ hôm nay có đơn, không có thì Tất cả.
    // Bấm "Tất cả" (date = '') được giữ nguyên, không bị đặt lại.
    if (os.date === null && dates.length) { const tm = U.addDays(Store.ctx.date, 1); os.date = dates.includes(tm) ? tm : (dates.find(d => d >= Store.ctx.date) || ''); }
    const rows = Store.rows('Order').filter(o => (!os.date || U.day(o.MealDate) === os.date) && (!Store.ctx.branch || (Store.get('Customer', o.CustomerId) || {}).BranchId === Store.ctx.branch));
    el.innerHTML = APP.header(f, `<button class="btn" id="oView">${I(os.view === 'board' ? 'clipboard' : 'layers')} ${os.view === 'board' ? 'Dạng bảng' : 'Dạng Kanban'}</button><button class="btn primary" id="oNew" ${perm.add === false ? 'disabled' : ''}>${I('plus')} Tạo đơn hàng</button>`) + `
    <div class="card panel mb"><div class="row-flex"><span class="small muted">Ngày ăn (Order.MealDate):</span><button class="chip ${!os.date ? 'active' : ''}" data-d="">Tất cả</button>${dates.map(d => `<button class="chip ${d === os.date ? 'active' : ''}" data-d="${d}">${U.date(d).slice(0, 5)}${d === Store.ctx.date ? ' · hôm nay' : ''}</button>`).join('')}</div>
    <div class="mt">${UI.stepper(ORDER_FLOW.map(s => ({ l: UI.statusVi('Order', 'Status', s), s: rows.filter(o => o.Status === s).length + ' đơn' })), -1)}</div><div class="small muted mt">Luồng trạng thái Order.Status – PROPOSED – BA/PO CONFIRM. BRD: chỉ đơn Confirmed mới vào kế hoạch sản xuất.</div></div>
    <div id="oBody"></div>`;
    el.querySelectorAll('[data-d]').forEach(b => b.onclick = () => { os.date = b.dataset.d; APP.refresh(); });
    el.querySelector('#oView').onclick = () => { os.view = os.view === 'board' ? 'list' : 'board'; APP.refresh(); };
    el.querySelector('#oNew').onclick = newOrder;
    const body = el.querySelector('#oBody');
    if (os.view === 'board') {
      const paint = (box, cols) => {
        box.innerHTML = `<div class="kanban">${cols.map(k => `<div class="kcol"><div class="kcol-head"><span>${esc(k.name)}</span><span class="tag"${k.qty === undefined ? '' : ` title="${U.num(k.qty)} suất"`}>${k.count}</span></div>${k.items.map(o => `<div class="kcard" data-o="${esc(o.id)}"><b>${esc(o.no)}</b><small>${esc(o.cus)}</small>${o.loc ? `<small class="muted">${esc(o.loc)}</small>` : ''}<div class="row"><span class="small">${U.date(o.date)}</span><b>${U.num(o.qty)} suất</b></div></div>`).join('') || '<div class="small muted">—</div>'}</div>`).join('')}</div>`;
        box.querySelectorAll('[data-o]').forEach(c => c.onclick = () => orderDetail(c.dataset.o));
      };
      paint(body, ORDER_FLOW.concat(['Cancelled']).map(s => { const r = rows.filter(o => o.Status === s); return { name: UI.statusVi('Order', 'Status', s), count: r.length, items: r.map(o => ({ id: o.Id, no: o.OrderNumber, cus: Store.get('Customer', o.CustomerId).DisplayName, date: o.MealDate, qty: o.TotalQuantity })) }; }));
      // Chế độ API: bảng Kanban lấy từ Order/GetBoard (8 cột theo trạng thái, kèm điểm giao và tổng suất mỗi cột).
      if (Store.isLive) {
        const key = os.boardKey = 'board|' + (os.date || '') + '|' + (Store.ctx.branch || '');
        cq(key, () => Store.query('Order/GetBoard', { MealDate: os.date || null, BranchId: Store.ctx.branch })).then(x => {
          const box = document.getElementById('oBody');
          if (!x.ok || !x.data || !box || APP.current !== 'crm-order' || os.view !== 'board' || os.boardKey !== key) return;
          paint(box, (x.data.listColumn || []).map(k => ({ name: k.statusName || UI.statusVi('Order', 'Status', k.status), count: k.count, qty: k.totalQuantity, items: (k.listItem || []).map(o => ({ id: o.id, no: o.orderNumber, cus: o.customerName, loc: o.locationName, date: o.mealDate, qty: o.totalQuantity })) })));
        });
      }
    } else {
      body.innerHTML = '<div class="card panel" id="oTbl"></div>';
      UI.table(body.querySelector('#oTbl'), { table: 'Order', rows, onRow: orderDetail, cols: [{ k: 'OrderNumber', label: 'Số đơn' }, { k: 'CustomerId', label: 'Khách hàng' }, { k: 'ContractId', label: 'Hợp đồng' }, { k: 'MealDate', label: 'Ngày ăn' }, { k: 'TotalQuantity', label: 'Tổng suất', num: true }, { k: 'TotalAmount', label: 'Thành tiền', num: true }, { k: 'Status', label: 'Trạng thái' }], filters: [{ k: 'Status', label: 'Trạng thái', options: ORDER_FLOW.map(s => ({ v: s, l: UI.statusVi('Order', 'Status', s) })) }] });
    }
    if (params.id) setTimeout(() => orderDetail(params.id), 50);
  });
  function recalc(oid) { const ls = Store.by('OrderLine', 'OrderId', oid); Store.get('Order', oid).TotalQuantity = U.sum(ls, 'Quantity'); Store.get('Order', oid).TotalAmount = U.sum(ls, 'LineAmount'); }
  function orderDetail(id) {
    const o = Store.get('Order', id); if (!o) return UI.toast('Không tìm thấy đơn hàng – tải lại trang rồi thử lại', 'err');
    const c = Store.get('Customer', o.CustomerId) || {};
    const lines = Store.by('OrderLine', 'OrderId', id);
    const fc = Store.rows('Forecast').filter(x => x.CustomerId === o.CustomerId && U.day(x.ForecastDate) === U.day(o.MealDate));
    const i = ORDER_FLOW.indexOf(o.Status);
    const editable = ['Draft', 'Submitted'].includes(o.Status);
    const dvs = Store.by('DeliveryOrder', 'OrderId', id);
    const rec = Store.one('OrderReconciliation', 'OrderId', id);
    const actions = { Draft: ['submit', 'Gửi đơn (Submitted)'], Submitted: ['confirm', 'Xác nhận (Confirmed)'], Confirmed: ['lock', 'Chốt số lượng (Locked)'] }[o.Status];
    UI.detail('Order', id, {
      screen: 'crm-order', noEdit: !editable,
      top: `${UI.stepper(ORDER_FLOW.map(s => UI.statusVi('Order', 'Status', s)), i, o.Status === 'Cancelled')}<div data-odlive></div><div class="kpis mt">${UI.kpi('Khách hàng', esc(c.DisplayName).slice(0, 26), esc(UI.statusVi('Customer', 'CustomerType', c.CustomerType) || ''), 'building', 'green')}${UI.kpi('Tổng suất', U.num(o.TotalQuantity), '', 'utensils', 'blue')}${UI.kpi('Thành tiền', U.money(o.TotalAmount), '', 'calc', 'orange')}</div>
        <h3>Chi tiết theo ca / loại suất / điểm giao (OrderLine)</h3><table class="table dc-table dt"><thead><tr><th>Ca<small>ShiftId</small></th><th>Loại suất<small>MealType</small></th><th>Điểm giao<small>Note</small></th><th class="num">Số lượng<small>Quantity</small></th><th class="num">Dự báo<small>Forecast</small></th><th class="num">Đơn giá</th><th class="num">Thành tiền<small>LineAmount</small></th></tr></thead><tbody>${lines.map(l => { const fq = U.sum(fc.filter(x => x.MealType === l.MealType), 'ForecastQuantity'); return `<tr><td>${esc(Store.label('Shift', l.ShiftId))}</td><td>${UI.tag(l.MealType)}</td><td class="small">${esc(l.Note || '')}</td><td class="num">${editable ? `<input type="number" class="input" style="width:90px;text-align:right" data-ql="${l.Id}" value="${l.Quantity}">` : U.num(l.Quantity)}</td><td class="num muted" data-fq="${l.Id}">${fq ? U.num(fq) : '–'}</td><td class="num">${U.money(l.UnitPrice)}</td><td class="num">${U.money(l.LineAmount)}</td></tr>`; }).join('')}</tbody></table>
        ${editable ? '<div class="small muted mt">Có thể sửa số lượng khi đơn ở trạng thái Nháp / Đã gửi. Sau khi chốt, thay đổi phải qua Yêu cầu đổi món / phê duyệt.</div>' : ''}
        ${dvs.length ? `<h3 class="mt">Đơn giao hàng (DeliveryOrder)</h3>${dvs.map(d => `<div class="spread" style="padding:6px 0;border-bottom:1px solid #F0F2ED"><a href="javascript:void 0" data-open="DeliveryOrder" data-id="${d.Id}">${esc(d.DeliveryNumber)}</a><span class="small">${esc(Store.label('CustomerDeliveryLocation', d.CustomerDeliveryLocationId))}</span>${UI.badge(d.Status, 'DeliveryOrder', 'Status')}</div>`).join('')}` : ''}
        ${rec ? `<h3 class="mt">Đối soát (OrderReconciliation)</h3><div class="kv"><div><label>Đặt</label><span>${U.num(rec.OrderedQuantity)}</span></div><div><label>Sản xuất</label><span>${U.num(rec.ProducedQty)}</span></div><div><label>Giao</label><span>${U.num(rec.DeliveredQuantity)}</span></div><div><label>Nhận</label><span>${U.num(rec.AcceptedQuantity)} (${U.pct(rec.VariancePercent, 2)})</span></div></div>` : ''}`,
      foot: () => `${editable ? `<button class="btn" data-act="saveq">${I('ok')} Lưu số lượng</button>` : ''}${actions ? `<button class="btn orange" data-act="${actions[0]}">${I('approve')} ${actions[1]}</button>` : ''}${['Draft', 'Submitted', 'Confirmed'].includes(o.Status) ? `<button class="btn danger" data-act="cancel">Hủy đơn</button>` : ''}`,
      onAct: (a, row, close, btn) => {
        const d = btn.closest('.drawer');
        // Chế độ API: action nghiệp vụ Order/* (backend kiểm tra giờ chốt, hợp đồng, dự báo…); DEMO: đổi trạng thái tại chỗ.
        const T = ['Order', 'OrderLine', 'ApprovalRequest'];
        const step = (action, form, status, msg) => Store.act('Order/' + action, { form: Object.assign({ Id: id }, form), tables: T, demo: () => Store.update('Order', id, Object.assign({ Status: status }, form && form.Reason ? { Note: form.Reason } : {})) }).then(r => { if (r.ok) done(msg); });
        if (a === 'saveq') {
          const qty = [...d.querySelectorAll('[data-ql]')].map(inp => ({ id: inp.dataset.ql, quantity: Math.max(0, Number(inp.value) || 0) }));
          Store.act('Order/UpdateLines', { method: 'PUT', json: { id, listLine: qty }, tables: T, demo: () => { qty.forEach(x => { const l = Store.get('OrderLine', x.id); Store.update('OrderLine', l.Id, { Quantity: x.quantity, LineAmount: x.quantity * (l.UnitPrice || 0) }); }); recalc(id); } })
            .then(r => { if (!r.ok) return; bust(); UI.toast(Store.isLive ? 'Đã cập nhật số lượng' : 'Đã cập nhật số lượng – OrderLine.Quantity (DEMO)', 'ok'); close(); orderDetail(id); APP.refresh(); });
        }
        if (a === 'submit') { if (!lines.length || !lines.every(l => l.ShiftId && l.MealType)) return UI.toast('Đơn phải có ít nhất 1 dòng với Ca và Loại suất', 'err'); step('Submit', null, 'Submitted', 'Đã gửi đơn'); }
        if (a === 'confirm') { const fq = U.sum(fc, 'ForecastQuantity'); const diff = fq ? (o.TotalQuantity - fq) / fq * 100 : 0; UI.confirm(`Xác nhận đơn <b>${esc(o.OrderNumber)}</b> – ${U.num(o.TotalQuantity)} suất.<br><span class="small ${Math.abs(diff) > 10 ? 'bad' : 'muted'}">So với dự báo: ${fq ? U.pct(diff) : 'không có dự báo'}</span>`, () => step('Confirm', null, 'Confirmed', 'Đã xác nhận – đơn sẵn sàng vào kế hoạch sản xuất')); }
        if (a === 'lock') UI.confirm('Chốt số lượng? Sau khi chốt, mọi thay đổi phải qua Yêu cầu đổi món / phê duyệt.<div class="notice gap mt small">GAP-F-07: SQL chưa có cột giờ chốt (cut-off). Mockup cho phép chốt thủ công.</div>', () => step('Lock', null, 'Locked', 'Đã chốt số lượng'));
        if (a === 'cancel') UI.confirm('Hủy đơn hàng này?', r => step('Cancel', { Reason: r }, 'Cancelled', 'Đã hủy đơn'), { input: 'Lý do hủy', required: true, danger: true, ok: 'Hủy đơn' });
        function done(m) { bust(); close(); UI.toast(m + (Store.isLive ? '' : ' (Order.Status · DEMO)'), 'ok'); APP.refresh(); orderDetail(id); }
      }
    });
    // Chế độ API: Order/GetDetail – hạn chốt, yêu cầu duyệt đang chờ, dự báo theo dòng; chỉ giữ nút mà backend cho phép (allowedActions).
    if (Store.isLive) {
      const dw = lastDrawer();
      Store.query('Order/GetDetail', { id }).then(x => {
        if (!x.ok || !x.data || !dw || !dw.isConnected) return;
        const d = x.data, acts = d.allowedActions || [], info = [], has = v => v !== null && v !== undefined;
        if (d.cutoffAt && !['Cancelled', 'InProduction', 'Delivered', 'Reconciled'].includes(o.Status)) info.push('Hạn chốt đơn: <b>' + esc(U.dt(d.cutoffAt)) + '</b>' + (d.isAfterCutoff ? ' – đã quá giờ chốt' : ''));
        if (d.pendingApprovalNo) info.push('Đang chờ duyệt: <b>' + esc(d.pendingApprovalNo) + '</b>' + ({ ORDER_DEVIATION: ' (lệch suất so với dự báo)', ORDER_CHANGE_AFTER_CUTOFF: ' (thay đổi sau giờ chốt)' }[d.pendingApprovalType] || ''));
        if (has(d.forecastQuantity)) info.push('Dự báo: ' + U.num(d.forecastQuantity) + ' suất' + (has(d.forecastVariancePercent) ? ' · lệch <span class="' + (d.forecastVarianceExceeded ? 'bad' : 'muted') + '">' + U.pct(d.forecastVariancePercent) + '</span>' : ''));
        if (acts.includes('requestChange')) info.push('Sau giờ chốt, thay đổi số lượng phải gửi yêu cầu duyệt.');
        (d.listWarning || []).forEach(w => info.push(esc(w)));
        const box = dw.querySelector('[data-odlive]');
        if (box && info.length) box.innerHTML = `<div class="notice ${d.isAfterCutoff || d.pendingApprovalNo || (d.listWarning || []).length ? 'warn' : 'info'} mt">${I('alert')}<div>${info.join('<br>')}</div></div>`;
        (d.listLine || []).forEach(l => { const td = [...dw.querySelectorAll('[data-fq]')].find(n => n.dataset.fq === String(l.id)); if (td) td.textContent = has(l.forecastQuantity) ? U.num(l.forecastQuantity) : '–'; });
        const need = { saveq: 'updateLines', submit: 'submit', confirm: 'confirm', lock: 'lock', cancel: 'cancel' };
        dw.querySelectorAll('.drawer-foot [data-act]').forEach(b => { const k = need[b.dataset.act]; if (k && !acts.includes(k)) b.remove(); });
        if (!acts.includes('updateLines')) dw.querySelectorAll('[data-ql]').forEach(inp => { inp.disabled = true; });
      });
    }
  }
  APP.orderDetail = orderDetail;
  function newOrder() {
    const cus = Store.rows('Customer').filter(c => c.Status === 'Active');
    UI.modal({
      title: 'Tạo đơn đặt suất ăn', sub: Store.isLive ? 'Loại suất theo hợp đồng hiệu lực · mỗi điểm giao một đơn · số lượng gợi ý từ dự báo đã công bố' : 'Order + OrderLine · lấy loại suất từ ContractMeal, điểm giao từ CustomerDeliveryLocation, gợi ý số lượng từ Forecast', size: 'lg',
      body: `<div class="form-grid"><div class="field"><label>Khách hàng <span class="req">*</span><code>CustomerId</code></label><select class="select" id="nC"><option value="">— Chọn —</option>${cus.map(c => `<option value="${c.Id}">${esc(c.DisplayName)}</option>`).join('')}</select></div><div class="field"><label>Ngày ăn <span class="req">*</span><code>MealDate</code></label><input type="date" class="input" id="nD" value="${U.addDays(Store.ctx.date, 4)}"></div></div><div id="nLines" class="mt"></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="nSave">Tạo đơn (Nháp)</button>',
      onMount: (m, close) => {
        // Chế độ API: Order/GetNewTemplate dựng sẵn dòng theo hợp đồng hiệu lực tại ngày ăn × điểm giao (mỗi điểm giao = 1 đơn),
        // ca mặc định, đơn giá áp dụng và số lượng gợi ý từ dự báo đã công bố.
        let seq = 0, tpl = null;
        const drawLive = () => {
          const cid = m.querySelector('#nC').value, dte = m.querySelector('#nD').value, box = m.querySelector('#nLines');
          tpl = null; delete box.dataset.ct;
          if (!cid || !dte) { box.innerHTML = '<div class="muted small">Chọn khách hàng và ngày ăn để tải hợp đồng, loại suất và số lượng gợi ý.</div>'; return; }
          const my = ++seq; box.innerHTML = '<div class="muted small">Đang tải hợp đồng và số lượng gợi ý…</div>';
          Store.query('Order/GetNewTemplate', { customerId: cid, mealDate: dte }).then(x => {
            if (my !== seq || !box.isConnected) return;
            if (!x.ok || !x.data) { box.innerHTML = failBox(x.message || 'Không tải được mẫu tạo đơn'); return; }
            tpl = x.data; box.dataset.ct = tpl.contractId || '';
            const locs = tpl.listLocation || [];
            box.innerHTML = `<div class="notice ok">${I('ok')}<div>Hợp đồng ${esc(tpl.contractNumber || '')}${tpl.contractName ? ' · ' + esc(tpl.contractName) : ''} · hạn chốt đơn ${esc(U.dt(tpl.cutoffAt))}</div></div>
              ${tpl.isAfterCutoff ? `<div class="notice warn">${I('alert')}<div>Đã quá giờ chốt của ngày ăn này. Đơn tạo mới sẽ ở trạng thái Nháp và được gửi duyệt.</div></div><div class="field"><label>Lý do thêm đơn sau giờ chốt <span class="req">*</span></label><textarea class="input" rows="2" id="nReason"></textarea></div>` : ''}
              ${locs.map(l => `<h3 class="mt">${esc(l.locationName || l.locationCode || 'Điểm giao')}</h3>${l.existingOrderId ? `<div class="small muted">Điểm giao này đã có đơn <b>${esc(l.existingOrderNumber || '')}</b> cho ngày ăn đã chọn – mở đơn đó để sửa số lượng.</div>` : `<table class="table dc-table dt"><thead><tr><th>Loại suất</th><th>Ca</th><th class="num">Đơn giá</th><th class="num">Dự báo</th><th class="num">Số lượng</th></tr></thead><tbody>${(l.listLine || []).map(ln => `<tr><td>${UI.tag(ln.mealType || '')} ${esc(ln.mealName || '')}</td><td>${esc(ln.shiftName || ln.shiftCode || '')}</td><td class="num">${U.money(ln.unitPrice)}</td><td class="num muted">${ln.suggestedQuantity ? U.num(ln.suggestedQuantity) : '–'}</td><td class="num"><input type="number" min="0" class="input" style="width:90px;text-align:right" data-m="${esc(ln.contractMealId)}" data-l="${esc(l.deliveryLocationId)}" data-s="${esc(ln.shiftId || '')}" value="${Number(ln.suggestedQuantity) || 0}"></td></tr>`).join('')}</tbody></table>`}`).join('') || '<div class="muted small">Khách hàng chưa có điểm giao hàng.</div>'}`;
          });
        };
        const drawDemo = () => {
          const cid = m.querySelector('#nC').value, box = m.querySelector('#nLines'); if (!cid) { box.innerHTML = '<div class="muted small">Chọn khách hàng để tải hợp đồng & loại suất.</div>'; return; }
          const ct = Store.rows('Contract').find(x => x.CustomerId === cid && x.Status === 'Active');
          if (!ct) { box.innerHTML = '<div class="notice gap">Khách hàng chưa có hợp đồng hiệu lực (Contract.Status = Active) – không thể tạo đơn.</div>'; return; }
          const meals = Store.by('ContractMeal', 'ContractId', ct.Id), locs = Store.by('CustomerDeliveryLocation', 'CustomerId', cid), dte = m.querySelector('#nD').value;
          box.innerHTML = `<div class="notice ok">${I('ok')}<div>Hợp đồng ${esc(ct.ContractNumber)} · hiệu lực ${U.date(ct.EffectiveFrom)}–${U.date(ct.EffectiveTo)}</div></div><table class="table dc-table dt"><thead><tr><th>Loại suất</th><th>Điểm giao</th><th>Ca</th><th class="num">Dự báo</th><th class="num">Số lượng</th></tr></thead><tbody>${meals.flatMap(ml => locs.map(l => { const fq = U.sum(Store.rows('Forecast').filter(x => x.CustomerId === cid && x.MealType === ml.MealType && U.day(x.ForecastDate) === dte), 'ForecastQuantity'); const shf = Store.rows('Shift')[{ BREAKFAST: 0, LUNCH: 1, DIET: 1, DINNER: 2, NIGHT: 3 }[ml.MealType] || 1]; return `<tr><td>${UI.tag(ml.MealType)} ${esc(ml.MealName)}</td><td class="small">${esc(l.Name)}</td><td>${esc(shf.Code)}</td><td class="num muted">${fq ? U.num(Math.round(fq / locs.length)) : '–'}</td><td class="num"><input type="number" class="input" style="width:90px;text-align:right" data-m="${ml.Id}" data-l="${l.Id}" data-s="${shf.Id}" value="${fq ? Math.round(fq / locs.length) : 0}"></td></tr>`; })).join('')}</tbody></table>`;
          box.dataset.ct = ct.Id;
        };
        const draw = Store.isLive ? drawLive : drawDemo;
        m.querySelector('#nC').onchange = draw; m.querySelector('#nD').onchange = draw; draw();
        m.querySelector('#nSave').onclick = () => {
          const cid = m.querySelector('#nC').value, box = m.querySelector('#nLines'), dte = m.querySelector('#nD').value;
          if (!cid || !box.dataset.ct) return UI.toast('Chọn khách hàng có hợp đồng hiệu lực', 'err');
          const ins = [...box.querySelectorAll('[data-m]')].filter(x => +x.value > 0); if (!ins.length) return UI.toast('Nhập ít nhất 1 dòng số lượng > 0', 'err');
          if (Store.isLive) {
            // Order/Create: backend tự sinh số đơn, tra đơn giá, kiểm tra hợp đồng hiệu lực và giờ chốt. Mỗi điểm giao tạo 1 đơn (theo mẫu GetNewTemplate).
            const reasonEl = m.querySelector('#nReason'), reason = reasonEl ? reasonEl.value.trim() : '';
            if (tpl && tpl.isAfterCutoff && !reason) return UI.toast('Nhập lý do thêm đơn sau giờ chốt', 'err');
            const byLoc = {}; ins.forEach(x => { (byLoc[x.dataset.l] = byLoc[x.dataset.l] || []).push(x); });
            const btn = m.querySelector('#nSave'), made = []; btn.disabled = true;
            return Object.entries(byLoc).reduce((p, [locId, list]) => p.then(() => Store.act('Order/Create', { json: { customerId: cid, contractId: box.dataset.ct, deliveryLocationId: locId, mealDate: dte, listLine: list.map(x => ({ contractMealId: x.dataset.m, shiftId: x.dataset.s || null, quantity: +x.value, deliveryLocationId: locId })), afterCutoffReason: reason || null }, tables: ['Order', 'OrderLine', 'ApprovalRequest'] }).then(r => { if (r.ok && r.data && r.data.order) made.push(r.data.order); })), Promise.resolve())
              .then(() => {
                btn.disabled = false; if (!made.length) return;
                bust(); close(); os.date = dte;
                UI.toast('Đã tạo ' + (made.length > 1 ? made.length + ' đơn: ' : 'đơn ') + esc(made.map(x => x.orderNumber).join(', ')) + (reason ? ' – đã gửi duyệt' : ''), 'ok');
                APP.go('crm-order'); APP.refresh(); if (made.length === 1) setTimeout(() => orderDetail(made[0].id), 80);
              });
          }
          const n = Store.rows('Order').filter(o => U.day(o.MealDate) === dte).length + 1;
          const o = Store.add('Order', { CustomerId: cid, ContractId: box.dataset.ct, DeliveryLocationId: Store.by('CustomerDeliveryLocation', 'CustomerId', cid)[0].Id, OrderNumber: 'DH-' + dte.slice(2).replace(/-/g, '') + '-' + String(n).padStart(2, '0'), OrderDate: U.iso(new Date()), MealDate: dte, DeliveryDate: dte + 'T10:00', Status: 'Draft' });
          ins.forEach(x => { const ml = Store.get('ContractMeal', x.dataset.m); const q = +x.value; Store.add('OrderLine', { OrderId: o.Id, MenuId: (Store.rows('Menu').find(mn => U.day(mn.EffectiveFrom) <= dte && dte <= U.day(mn.EffectiveTo)) || {}).Id, MealType: ml.MealType, ShiftId: x.dataset.s, Quantity: q, UnitPrice: ml.UnitPrice, LineAmount: q * ml.UnitPrice, ContractMealId: ml.Id, Note: Store.get('CustomerDeliveryLocation', x.dataset.l).Name }); });
          recalc(o.Id); close(); os.date = dte; UI.toast('Đã tạo đơn ' + esc(o.OrderNumber) + ' (Draft · DEMO)', 'ok'); APP.go('crm-order'); APP.refresh(); setTimeout(() => orderDetail(o.Id), 80);
        };
      }
    });
  }
  // ---------- Đối soát ----------
  APP.register('crm-reconciliation', (el, f) => {
    const rows = Store.rows('OrderReconciliation');
    const byDay = U.group(rows, r => U.day((Store.get('Order', r.OrderId) || {}).MealDate));
    const days = Object.keys(byDay).sort();
    APP.genericList(el, f, {
      noAdd: true,
      top: `<div class="card panel mb"><div class="panel-head"><div><div class="panel-title">Chuỗi đối soát Đặt → Sản xuất → Đóng gói → Giao → Nhận (BRD)</div><div class="panel-desc">Σ theo ngày ăn · chênh lệch = AcceptedQuantity − OrderedQuantity</div></div><div class="chart-legend"><span><i class="legend-dot" style="background:#B9CE8B"></i>Đặt</span><span><i class="legend-dot" style="background:#79A8CC"></i>Giao</span><span><i class="legend-dot" style="background:#F5B17E"></i>Nhận</span></div></div>${UI.bars(days.map(d => U.date(d).slice(0, 5)), [{ name: 'Đặt', values: days.map(d => U.sum(byDay[d], 'OrderedQuantity')), color: '#B9CE8B' }, { name: 'Giao', values: days.map(d => U.sum(byDay[d], 'DeliveredQuantity')), color: '#79A8CC' }, { name: 'Nhận', values: days.map(d => U.sum(byDay[d], 'AcceptedQuantity')), color: '#F5B17E' }], { h: 180 })}<div class="notice warn small mt">${I('alert')}<div>GAP-F-11: chưa có nơi cấu hình ngưỡng chênh lệch. Mockup đánh dấu đỏ khi |VariancePercent| &gt; 1%.</div></div></div>`,
      cols: [{ k: 'OrderId', label: 'Đơn hàng' }, { k: 'OrderedQuantity', label: 'Đặt', num: true }, { k: 'ProducedQty', label: 'Sản xuất', num: true }, { k: 'PackedQty', label: 'Đóng gói', num: true }, { k: 'DeliveredQuantity', label: 'Giao', num: true }, { k: 'AcceptedQuantity', label: 'Nhận', num: true }, { k: 'VariancePercent', label: 'Chênh lệch %', num: true, render: r => `<span class="${Math.abs(r.VariancePercent) > 1 ? 'bad' : 'up'}">${U.pct(r.VariancePercent, 2)}</span>` }, { k: 'FinalAmount', label: 'Giá trị chốt', num: true }, { k: 'Status', label: 'Trạng thái' }]
    });
    // Chế độ API: in biên bản đối soát (PDF) và chốt đối soát qua action nghiệp vụ.
    const canEdit = Store.perm(f.id).edit !== false;
    // Chế độ API: ghi suất thực tế phục vụ cho đơn Đã giao (OrderReconciliation/Reconcile) – nút đầu trang mở danh sách đơn chờ đối soát.
    if (Store.isLive && canEdit) {
      const ha = el.querySelector('.heading-actions');
      if (ha && !ha.querySelector('#rcNew')) { ha.insertAdjacentHTML('afterbegin', `<button class="btn primary" id="rcNew">${I('plus')} Ghi suất phục vụ</button>`); ha.querySelector('#rcNew').onclick = pendingReconcile; }
    }
    if (Store.isLive) APP.rowExtra(el, tr => {
      const r = Store.get('OrderReconciliation', tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td) return;
      td.insertAdjacentHTML('afterbegin', `<button class="btn xs" data-prt title="In biên bản đối soát (PDF)">${I('export')} In</button> ${r.Status !== 'Closed' && canEdit ? '<button class="btn xs" data-rec title="Ghi lại suất thực tế phục vụ">Ghi lại</button> ' : ''}${r.Status !== 'Closed' ? '<button class="btn xs orange" data-cls>Chốt</button> ' : ''}`);
      const rb = td.querySelector('[data-rec]'); if (rb) rb.onclick = () => reconcileForm({ orderId: r.OrderId, label: Store.label('Order', r.OrderId), ordered: r.OrderedQuantity, delivered: r.DeliveredQuantity, served: r.AcceptedQuantity });
      td.querySelector('[data-prt]').onclick = () => Store.download('OrderReconciliation/Print', { id: r.Id });
      const c = td.querySelector('[data-cls]'); if (c) c.onclick = () => UI.confirm('Chốt đối soát đơn <b>' + esc(Store.label('Order', r.OrderId)) + '</b>? Sau khi chốt không sửa được số liệu.', note => Store.act('OrderReconciliation/Close', { form: { Id: r.Id, Remark: note }, tables: ['OrderReconciliation', 'Order'] }).then(x => { if (x.ok) { UI.toast('Đã chốt đối soát', 'ok'); APP.refresh(); } }), { input: 'Ghi chú', ok: 'Chốt đối soát' });
    });
  });
  // Đơn Đã giao chờ đối soát → chọn đơn để ghi suất phục vụ.
  function pendingReconcile() {
    UI.modal({
      title: 'Đơn đã giao chờ đối soát', sub: 'Chọn đơn để ghi số suất thực tế phục vụ', size: 'lg',
      body: '<div id="rcList" class="muted small">Đang tải…</div>',
      onMount: (m, close) => Store.query('OrderReconciliation/GetPendingOrders').then(x => {
        const box = m.querySelector('#rcList'); if (!box || !box.isConnected) return;
        if (!x.ok) { box.innerHTML = failBox(x.message); return; }
        const list = x.list.filter(o => o.reconciliationStatus !== 'Closed');
        if (!list.length) { box.className = 'empty'; box.textContent = 'Không có đơn Đã giao nào đang chờ đối soát'; return; }
        box.className = 'table-wrap';
        box.innerHTML = `<table class="table dc-table dt"><thead><tr><th>Số đơn</th><th>Ngày ăn</th><th>Khách hàng</th><th class="num">Đặt</th><th class="num">Giao</th><th class="num">Khách nhận</th><th>Đối soát</th><th></th></tr></thead><tbody>${list.map((o, i) => `<tr><td>${esc(o.orderNumber || '')}</td><td>${U.date(o.mealDate)}</td><td>${esc(o.customerName || '')}</td><td class="num">${U.num(o.orderedQuantity)}</td><td class="num">${U.num(o.deliveredQuantity)}</td><td class="num">${U.num(o.podAcceptedQuantity)}</td><td>${o.reconciliationStatus ? UI.badge(o.reconciliationStatus, 'OrderReconciliation', 'Status') : '<span class="muted">Chưa ghi</span>'}</td><td class="actions"><button class="btn xs primary" data-i="${i}">Ghi suất</button></td></tr>`).join('')}</tbody></table>`;
        box.querySelectorAll('[data-i]').forEach(b => b.onclick = () => { const o = list[+b.dataset.i]; close(); reconcileForm({ orderId: o.orderId, label: o.orderNumber, ordered: o.orderedQuantity, delivered: o.deliveredQuantity, pod: o.podAcceptedQuantity }); });
      })
    });
  }
  // Form ghi suất thực tế phục vụ: 0 ≤ phục vụ ≤ suất khách nhận; phục vụ + trả về + hủy ≤ suất khách nhận (backend kiểm tra lại).
  function reconcileForm(o) {
    const has = v => v !== null && v !== undefined;
    const def = has(o.served) ? o.served : (has(o.pod) ? o.pod : '');
    UI.modal({
      title: 'Ghi suất thực tế phục vụ', sub: 'Đơn ' + esc(o.label || ''),
      body: `<div class="kv mb"><div><label>Suất đặt</label><span>${U.num(o.ordered)}</span></div><div><label>Suất giao</label><span>${U.num(o.delivered)}</span></div>${has(o.pod) ? `<div><label>Khách nhận khi bàn giao</label><span>${U.num(o.pod)}</span></div>` : ''}</div>
        <div class="form-grid"><div class="field"><label>Suất phục vụ thực tế <span class="req">*</span></label><input type="number" min="0" step="any" class="input" id="rcServed" value="${def}"></div><div class="field"><label>Suất trả về</label><input type="number" min="0" step="any" class="input" id="rcRet"></div><div class="field"><label>Suất hủy</label><input type="number" min="0" step="any" class="input" id="rcWaste"></div><div class="field full"><label>Ghi chú</label><textarea class="input" rows="2" id="rcNote" maxlength="2000"></textarea></div></div>
        <div class="small muted mt">Số suất tính tiền và chênh lệch được tính lại theo số liệu này. Có thể ghi lại cho tới khi chốt đối soát.</div>`,
      foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="rcSave">${I('ok')} Lưu đối soát</button>`,
      onMount: (m, close) => m.querySelector('#rcSave').onclick = () => {
        const val = sel => { const v = m.querySelector(sel).value; return v === '' ? null : Number(v); };
        const served = val('#rcServed'), ret = val('#rcRet'), waste = val('#rcWaste');
        if (served === null || isNaN(served) || served < 0) return UI.toast('Nhập số suất phục vụ thực tế (≥ 0)', 'err');
        if ((ret !== null && ret < 0) || (waste !== null && waste < 0)) return UI.toast('Suất trả về / hủy không được âm', 'err');
        if (has(o.pod) && served + (ret || 0) + (waste || 0) > Number(o.pod)) return UI.toast('Phục vụ + trả về + hủy không được vượt số suất khách nhận (' + U.num(o.pod) + ')', 'err');
        Store.act('OrderReconciliation/Reconcile', { form: { OrderId: o.orderId, ServedQuantity: served, ReturnedQuantity: ret, WastedQuantity: waste, Remark: m.querySelector('#rcNote').value.trim() }, tables: ['OrderReconciliation', 'Order'] })
          .then(r => { if (!r.ok) return; bust(); close(); UI.toast('Đã ghi đối soát đơn ' + esc(o.label || ''), 'ok'); APP.refresh(); });
      }
    });
  }
  // Chế độ API: tạo hợp đồng kèm các loại suất trong 1 lần (Contract/Create) – hợp đồng luôn bắt đầu ở Nháp, số tự sinh nếu để trống.
  function newContract() {
    const cus = Store.rows('Customer').filter(c => c.Status !== 'Inactive');
    const mealRow = () => `<tr><td><input class="input" data-mt list="ctMealTypes" maxlength="50" placeholder="Mã loại suất"></td><td><input class="input" data-mn maxlength="250" placeholder="Tên suất"></td><td><input type="number" min="0" step="any" class="input" data-mp style="text-align:right" placeholder="Đơn giá"></td><td class="actions"><button class="btn xs danger" data-rm title="Bỏ dòng">${I('trash')}</button></td></tr>`;
    UI.modal({
      title: 'Tạo hợp đồng', sub: 'Hợp đồng mới ở trạng thái Nháp – gửi duyệt để có hiệu lực', size: 'lg',
      body: `<div class="form-grid">
          <div class="field"><label>Khách hàng <span class="req">*</span></label><select class="select" id="ctCus">${opts(cus, c => c.DisplayName || c.Code, '— Chọn —')}</select></div>
          <div class="field"><label>Tên hợp đồng <span class="req">*</span></label><input class="input" id="ctName" maxlength="250"></div>
          <div class="field"><label>Số hợp đồng</label><input class="input mono" id="ctNo" placeholder="(tự sinh khi lưu)" readonly title="MA-1 (D-63): HD-YYMMDD-NNN – số hợp đồng giấy của khách ghi vào Ghi chú"></div>
          <div class="field"><label>Ngày ký</label><input type="date" class="input" id="ctSigned"></div>
          <div class="field"><label>Hiệu lực từ <span class="req">*</span></label><input type="date" class="input" id="ctFrom" value="${Store.ctx.date}"></div>
          <div class="field"><label>Hiệu lực đến</label><input type="date" class="input" id="ctTo"></div>
          <div class="field"><label>Điều khoản thanh toán</label><input class="input" id="ctPay" maxlength="250"></div>
          <div class="field"><label>Giá trị hợp đồng</label><input type="number" min="0" step="any" class="input" id="ctVal"></div>
          <div class="field full"><label>Ghi chú</label><textarea class="input" rows="2" id="ctNote"></textarea></div></div>
        <div class="spread mt"><h3 style="margin:0">Loại suất & đơn giá <span class="req">*</span></h3><button class="btn xs" id="ctAdd">${I('plus')} Thêm loại suất</button></div>
        <datalist id="ctMealTypes">${mealTypes().map(t => `<option value="${esc(t)}">`).join('')}</datalist>
        <table class="table dc-table dt mt"><thead><tr><th>Loại suất</th><th>Tên suất</th><th class="num">Đơn giá</th><th></th></tr></thead><tbody id="ctMeals">${mealRow()}</tbody></table>
        <div class="small muted mt">Mỗi loại suất chỉ xuất hiện một lần trong hợp đồng; đơn giá (nếu nhập) phải lớn hơn 0.</div>`,
      foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="ctSave">${I('ok')} Tạo hợp đồng</button>`,
      onMount: (m, close) => {
        const tb = m.querySelector('#ctMeals'), v = sel => m.querySelector(sel).value.trim();
        m.querySelector('#ctAdd').onclick = () => tb.insertAdjacentHTML('beforeend', mealRow());
        tb.addEventListener('click', e => { const b = e.target.closest('[data-rm]'); if (b && tb.rows.length > 1) b.closest('tr').remove(); });
        m.querySelector('#ctSave').onclick = () => {
          const meals = [...tb.rows].map(tr => ({ mealType: tr.querySelector('[data-mt]').value.trim(), mealName: tr.querySelector('[data-mn]').value.trim() || null, unitPrice: tr.querySelector('[data-mp]').value === '' ? null : Number(tr.querySelector('[data-mp]').value) })).filter(x => x.mealType || x.mealName || x.unitPrice !== null);
          if (!v('#ctCus')) return UI.toast('Chọn khách hàng', 'err');
          if (!v('#ctName')) return UI.toast('Nhập tên hợp đồng', 'err');
          if (!v('#ctFrom')) return UI.toast('Nhập ngày bắt đầu hiệu lực', 'err');
          if (v('#ctTo') && v('#ctTo') < v('#ctFrom')) return UI.toast('Ngày hết hiệu lực phải sau ngày bắt đầu', 'err');
          if (!meals.length) return UI.toast('Nhập ít nhất 1 loại suất', 'err');
          if (meals.some(x => !x.mealType)) return UI.toast('Mỗi dòng phải có mã loại suất', 'err');
          if (new Set(meals.map(x => x.mealType.toLowerCase())).size !== meals.length) return UI.toast('Mỗi loại suất chỉ được xuất hiện một lần', 'err');
          if (meals.some(x => x.unitPrice !== null && !(x.unitPrice > 0))) return UI.toast('Đơn giá phải lớn hơn 0', 'err');
          // JSON vì có danh sách loại suất lồng trong hợp đồng.
          Store.act('Contract/Create', { json: { customerId: v('#ctCus'), name: v('#ctName'), signedDate: v('#ctSigned') || null, effectiveFrom: v('#ctFrom'), effectiveTo: v('#ctTo') || null, paymentTerm: v('#ctPay') || null, contractValue: v('#ctVal') === '' ? null : Number(v('#ctVal')), note: v('#ctNote') || null, listMeal: meals }, tables: ['Contract', 'ContractMeal'] })
            .then(r => { if (!r.ok) return; bust(); close(); const ct = r.data && r.data.contract; UI.toast('Đã tạo hợp đồng ' + esc(ct ? ct.contractNumber || '' : '') + ' (Nháp)', 'ok'); APP.refresh(); });
        };
      }
    });
  }
  // Chế độ API: chuyển hợp đồng Hiệu lực đã quá ngày hết hiệu lực sang Hết hạn (xem trước rồi mới xác nhận) – cùng hàm với job tự động ban đêm.
  function expireContracts() {
    Store.act('Contract/ExpireOverdue', { form: { Preview: true } }).then(r => {
      if (!r.ok) return;
      const d = r.data || {}, nos = d.listContractNumber || [];
      if (!d.expiredCount) return UI.toast('Không có hợp đồng Hiệu lực nào đã quá ngày hết hiệu lực', 'ok');
      UI.confirm(`Có <b>${U.num(d.expiredCount)}</b> hợp đồng đang Hiệu lực nhưng đã quá ngày hết hiệu lực:<br><span class="small">${nos.slice(0, 20).map(esc).join(', ')}${nos.length > 20 ? ' …' : ''}</span><br>Chuyển các hợp đồng này sang <b>Hết hạn</b>? Thao tác không hoàn tác được.`,
        () => Store.act('Contract/ExpireOverdue', { form: { Preview: false }, tables: ['Contract'] }).then(x => { if (!x.ok) return; bust(); UI.toast('Đã chuyển ' + U.num((x.data || {}).expiredCount || 0) + ' hợp đồng sang Hết hạn', 'ok'); APP.refresh(); }),
        { danger: true, ok: 'Chuyển sang Hết hạn' });
    });
  }
  // ---------- Hợp đồng: gửi phê duyệt ----------
  APP.register('crm-contract', (el, f) => {
    APP.genericList(el, f, { noTransit: true });
    if (Store.isLive) {
      const perm = Store.perm(f.id), add = el.querySelector('[data-act="add"]'), ha = el.querySelector('.heading-actions');
      if (add) add.onclick = newContract;
      if (ha && perm.edit !== false && !ha.querySelector('#ctExp')) { ha.insertAdjacentHTML('afterbegin', `<button class="btn" id="ctExp" title="Chuyển hợp đồng Hiệu lực đã quá ngày hết hiệu lực sang Hết hạn">${I('calendar')} Quét hợp đồng hết hạn</button>`); ha.querySelector('#ctExp').onclick = expireContracts; }
    }
    APP.rowExtra(el, tr => { const r = Store.get('Contract', tr.dataset.row); if (r.Status === 'Draft') { const td = tr.querySelector('.actions'); td.insertAdjacentHTML('afterbegin', `<button class="btn xs orange" data-sub="${r.Id}">Gửi duyệt</button> `); td.querySelector('[data-sub]').onclick = () => Store.act('Contract/Submit', { form: { Id: r.Id }, tables: ['Contract', 'ApprovalRequest', 'ApprovalStep', 'ApprovalItem'], demo: () => { APP.submitApproval('CONTRACT', 'Contract', r.Id, [['Ban giám đốc']], 'Duyệt hợp đồng ' + r.ContractNumber); Store.update('Contract', r.Id, { Status: 'PendingApproval' }); } }).then(x => { if (!x.ok) return; if (!x.demo) UI.toast('Đã gửi duyệt hợp đồng ' + esc(r.ContractNumber), 'ok'); APP.refresh(); }); }
      if (r.Status === 'Active' && Store.isLive) { const td = tr.querySelector('.actions'); td.insertAdjacentHTML('afterbegin', `<button class="btn xs danger" data-term="${r.Id}">Thanh lý</button> `); td.querySelector('[data-term]').onclick = () => UI.confirm('Thanh lý hợp đồng <b>' + esc(r.ContractNumber) + '</b>?', note => Store.act('Contract/Terminate', { form: { Id: r.Id, Reason: note }, tables: ['Contract'] }).then(x => { if (x.ok) { UI.toast('Đã thanh lý hợp đồng', 'ok'); APP.refresh(); } }), { input: 'Lý do thanh lý', required: true, danger: true, ok: 'Thanh lý' }); } });
  });
  // ---------- Dự báo: pivot ----------
  const fcs = { from: null, to: null };   // khoảng ngày của ma trận (chế độ API)
  const dayCount = (a, b) => Math.round((new Date(b + 'T00:00') - new Date(a + 'T00:00')) / 86400000) + 1;
  // Chế độ API: ma trận khách × ngày từ Forecast/GetMatrix – số đã công bố, kèm số bản nháp chưa công bố.
  function loadMatrix() {
    const key = fcs.key = 'fc|' + fcs.from + '|' + fcs.to + '|' + (Store.ctx.branch || '');
    cq(key, () => Store.query('Forecast/GetMatrix', { From: fcs.from, To: fcs.to, BranchId: Store.ctx.branch })).then(x => {
      const box = document.getElementById('fcMx'); if (!box || fcs.key !== key) return;
      if (!x.ok || !x.data) { box.innerHTML = failBox(x.message); return; }
      const d = x.data, days = (d.dates || []).map(U.day), rows = d.rows || [], tot = d.totals || {};
      if (!rows.length) { box.innerHTML = '<div class="empty">Chưa có dự báo trong khoảng ngày này</div>'; return; }
      const cell = (r, k) => { const pv = (r.values || {})[k], dv = (r.draftValues || {})[k]; return `<td class="num">${pv ? U.num(pv) : (dv ? '' : '<span class="muted">–</span>')}${dv ? `<div class="small muted">nháp ${U.num(dv)}</div>` : ''}</td>`; };
      box.innerHTML = `<div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Khách hàng</th>${days.map(k => `<th class="num">${U.date(k).slice(0, 5)}</th>`).join('')}<th class="num">Tổng</th></tr></thead><tbody>${rows.map(r => `<tr><td>${esc(r.customerName || r.customerCode || '')}</td>${days.map(k => cell(r, k)).join('')}<td class="num"><b>${U.num(r.total)}</b></td></tr>`).join('')}<tr><td><b>Tổng</b></td>${days.map(k => `<td class="num"><b>${U.num(tot[k] || 0)}</b></td>`).join('')}<td class="num"><b>${U.num(d.grandTotal)}</b></td></tr></tbody></table></div><div class="small muted mt">Số chính = dự báo đã công bố · "nháp" = bản đã nhập nhưng chưa công bố.</div>`;
    });
  }
  // Chế độ API: nhập dự báo theo khách × loại suất × khoảng ngày (Forecast/Import) → bản Nháp phiên bản mới.
  function importForecast() {
    const cus = Store.rows('Customer').filter(c => c.Status !== 'Inactive'), shifts = Store.rows('Shift');
    const d1 = U.addDays(Store.ctx.date, 1);
    UI.modal({
      title: 'Nhập dự báo suất ăn', sub: 'Mỗi lần nhập tạo phiên bản mới ở trạng thái Nháp – bấm Công bố để áp dụng', size: 'lg',
      body: `<div class="form-grid">
          <div class="field"><label>Khách hàng <span class="req">*</span></label><select class="select" id="fiCus">${opts(cus, c => c.DisplayName || c.Code, '— Chọn —')}</select></div>
          <div class="field"><label>Điểm giao</label><select class="select" id="fiLoc"><option value="">Chung cho khách hàng</option></select></div>
          <div class="field"><label>Loại suất <span class="req">*</span></label><input class="input" id="fiMeal" list="fiMealTypes" maxlength="50"><datalist id="fiMealTypes"></datalist></div>
          <div class="field"><label>Ca</label><select class="select" id="fiShift">${opts(shifts, s => Store.label('Shift', s), 'Mọi ca')}</select></div>
          <div class="field"><label>Từ ngày <span class="req">*</span></label><input type="date" class="input" id="fiFrom" value="${d1}"></div>
          <div class="field"><label>Đến ngày <span class="req">*</span></label><input type="date" class="input" id="fiTo" value="${U.addDays(d1, 6)}"></div>
          <div class="field"><label>Điền nhanh số suất mỗi ngày</label><input type="number" min="0" class="input" id="fiAll" placeholder="Nhập rồi áp cho mọi ngày"></div>
          <div class="field"><label>Ghi chú</label><input class="input" id="fiNote" maxlength="500"></div></div>
        <div id="fiDays" class="mt"></div><div class="small muted mt">Ngày để trống sẽ không được nhập.</div>`,
      foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="fiSave">${I('ok')} Nhập dự báo</button>`,
      onMount: (m, close) => {
        const g = sel => m.querySelector(sel);
        const drawDays = () => {
          const a = g('#fiFrom').value, b = g('#fiTo').value, box = g('#fiDays'), keep = {};
          box.querySelectorAll('[data-day]').forEach(i => { keep[i.dataset.day] = i.value; });
          if (!a || !b || b < a || dayCount(a, b) > 62) { box.innerHTML = '<div class="muted small">Chọn khoảng ngày hợp lệ (tối đa 62 ngày).</div>'; return; }
          const days = Array.from({ length: dayCount(a, b) }, (_, i) => U.addDays(a, i));
          box.innerHTML = `<div class="row-flex">${days.map(k => `<label class="small" style="display:inline-flex;flex-direction:column;gap:3px">${U.date(k).slice(0, 5)}<input type="number" min="0" class="input" style="width:84px;text-align:right" data-day="${k}" value="${esc(keep[k] || '')}"></label>`).join('')}</div>`;
        };
        const onCus = () => { const cid = g('#fiCus').value; g('#fiLoc').innerHTML = opts(cid ? Store.by('CustomerDeliveryLocation', 'CustomerId', cid) : [], l => l.Name, 'Chung cho khách hàng'); g('#fiMealTypes').innerHTML = mealTypes(cid).map(t => `<option value="${esc(t)}">`).join(''); };
        g('#fiCus').onchange = onCus; g('#fiFrom').onchange = drawDays; g('#fiTo').onchange = drawDays;
        g('#fiAll').oninput = e => g('#fiDays').querySelectorAll('[data-day]').forEach(i => { i.value = e.target.value; });
        onCus(); drawDays();
        g('#fiSave').onclick = () => {
          const cid = g('#fiCus').value, meal = g('#fiMeal').value.trim();
          if (!cid) return UI.toast('Chọn khách hàng', 'err');
          if (!meal) return UI.toast('Nhập loại suất', 'err');
          const ins = [...g('#fiDays').querySelectorAll('[data-day]')].filter(i => i.value !== '');
          if (!ins.length) return UI.toast('Nhập số suất cho ít nhất 1 ngày', 'err');
          if (ins.some(i => !(Number(i.value) >= 0))) return UI.toast('Số suất không được âm', 'err');
          // JSON vì là danh sách dòng dự báo.
          Store.act('Forecast/Import', { json: { note: g('#fiNote').value.trim() || null, listItem: ins.map(i => ({ customerId: cid, deliveryLocationId: g('#fiLoc').value || null, forecastDate: i.dataset.day, shiftId: g('#fiShift').value || null, mealType: meal, forecastQuantity: Number(i.value) })) }, tables: ['Forecast'] })
            .then(r => { if (!r.ok) return; bust(); close(); const d = r.data || {}; UI.toast('Đã nhập ' + U.num(d.created || ins.length) + ' dòng dự báo (bản Nháp' + (d.versionNo ? ', phiên bản ' + d.versionNo : '') + ') – bấm Công bố để áp dụng', 'ok'); APP.refresh(); });
        };
      }
    });
  }
  // Chế độ API: công bố các bản dự báo Nháp trong khoảng ngày (Forecast/Publish) – bản đã công bố cũ cùng khóa bị thay thế.
  function publishForecast() {
    const cus = Store.rows('Customer');
    UI.modal({
      title: 'Công bố dự báo', size: 'sm',
      body: `<p style="margin-top:0">Các bản dự báo <b>Nháp</b> trong khoảng ngày sẽ chuyển sang <b>Đã công bố</b>; bản đã công bố cũ cùng khách / điểm giao / ngày / ca / loại suất sẽ bị thay thế. Thao tác không hoàn tác được.</p>
        <div class="field"><label>Từ ngày <span class="req">*</span></label><input type="date" class="input" id="fpFrom" value="${fcs.from || Store.ctx.date}"></div>
        <div class="field"><label>Đến ngày <span class="req">*</span></label><input type="date" class="input" id="fpTo" value="${fcs.to || U.addDays(Store.ctx.date, 13)}"></div>
        <div class="field"><label>Khách hàng</label><select class="select" id="fpCus">${opts(cus, c => c.DisplayName || c.Code, 'Tất cả khách hàng')}</select></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="fpOk">Công bố</button>',
      onMount: (m, close) => m.querySelector('#fpOk').onclick = () => {
        const a = m.querySelector('#fpFrom').value, b = m.querySelector('#fpTo').value;
        if (!a || !b || b < a) return UI.toast('Chọn khoảng ngày hợp lệ', 'err');
        Store.act('Forecast/Publish', { form: { From: a, To: b, ListCustomerId: m.querySelector('#fpCus').value || null }, tables: ['Forecast'] })
          .then(r => { if (!r.ok) return; bust(); close(); const d = r.data || {}; UI.toast('Đã công bố ' + U.num(d.published || 0) + ' dòng dự báo' + (d.superseded ? ' · thay thế ' + U.num(d.superseded) + ' dòng cũ' : ''), 'ok'); APP.refresh(); });
      }
    });
  }
  APP.register('crm-forecast', (el, f) => {
    if (Store.isLive) {
      const perm = Store.perm(f.id);
      if (!fcs.from) { fcs.from = Store.ctx.date; fcs.to = U.addDays(Store.ctx.date, 13); }
      APP.genericList(el, f, { top: `<div class="card panel mb"><div class="spread mb"><div class="panel-title">Ma trận dự báo suất ăn theo khách hàng × ngày</div><div class="row-flex"><span class="small muted">Từ</span><input type="date" class="input" id="fcFrom" value="${fcs.from}" style="width:150px"><span class="small muted">đến</span><input type="date" class="input" id="fcTo" value="${fcs.to}" style="width:150px"></div></div><div id="fcMx"><div class="muted small">Đang tải…</div></div></div>` });
      const add = el.querySelector('[data-act="add"]'), ha = el.querySelector('.heading-actions');
      // Forecast/Insert bị backend chặn → nút tạo mới chuyển thành nhập hàng loạt (quyền Nhập).
      if (add) { add.innerHTML = `${I('plus')} Nhập dự báo`; add.disabled = perm.imp === false; add.title = perm.imp === false ? 'Tài khoản không có quyền Nhập' : ''; add.onclick = importForecast; }
      if (ha && perm.edit !== false && !ha.querySelector('#fcPub')) { ha.insertAdjacentHTML('beforeend', `<button class="btn orange" id="fcPub">${I('approve')} Công bố</button>`); ha.querySelector('#fcPub').onclick = publishForecast; }
      const onRange = () => { const a = el.querySelector('#fcFrom').value, b = el.querySelector('#fcTo').value; if (!a || !b) return; if (b < a || dayCount(a, b) > 62) return UI.toast('Khoảng ngày không hợp lệ (tối đa 62 ngày)', 'err'); fcs.from = a; fcs.to = b; document.getElementById('fcMx').innerHTML = '<div class="muted small">Đang tải…</div>'; loadMatrix(); };
      el.querySelector('#fcFrom').onchange = onRange; el.querySelector('#fcTo').onchange = onRange;
      return loadMatrix();
    }
    const rows = Store.rows('Forecast'); const days = [...new Set(rows.map(r => U.day(r.ForecastDate)))].sort();
    const cus = Store.rows('Customer');
    APP.genericList(el, f, {
      top: `<div class="card panel mb"><div class="panel-title mb">Ma trận dự báo suất ăn (Σ ForecastQuantity theo khách hàng × ngày)</div><div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Khách hàng</th>${days.map(d => `<th class="num">${U.date(d).slice(0, 5)}</th>`).join('')}</tr></thead><tbody>${cus.map(c => `<tr><td>${esc(c.DisplayName)}</td>${days.map(d => `<td class="num">${U.num(U.sum(rows.filter(r => r.CustomerId === c.Id && U.day(r.ForecastDate) === d), 'ForecastQuantity'))}</td>`).join('')}</tr>`).join('')}<tr><td><b>Tổng</b></td>${days.map(d => `<td class="num"><b>${U.num(U.sum(rows.filter(r => U.day(r.ForecastDate) === d), 'ForecastQuantity'))}</b></td>`).join('')}</tr></tbody></table></div></div>`
    });
  });
  // ---------- Chính sách giá: tra đơn giá áp dụng ----------
  // Chế độ API: PricePolicy/Resolve trả đơn giá áp dụng cho (khách, hợp đồng, loại suất, ngày) – ưu tiên chính sách theo hợp đồng → theo khách → đơn giá trong hợp đồng.
  function resolvePrice() {
    const cus = Store.rows('Customer');
    const SRC = { 'PricePolicy:Contract': 'Chính sách giá theo hợp đồng', 'PricePolicy:Customer': 'Chính sách giá theo khách hàng', ContractMeal: 'Đơn giá loại suất trong hợp đồng' };
    UI.modal({
      title: 'Tra đơn giá áp dụng', sub: 'Đơn giá hệ thống sẽ dùng khi tạo dòng đơn hàng',
      body: `<div class="form-grid">
          <div class="field"><label>Khách hàng <span class="req">*</span></label><select class="select" id="rpCus">${opts(cus, c => c.DisplayName || c.Code, '— Chọn —')}</select></div>
          <div class="field"><label>Hợp đồng</label><select class="select" id="rpCt"><option value="">Không chọn</option></select></div>
          <div class="field"><label>Loại suất</label><input class="input" id="rpMeal" list="rpMealTypes" maxlength="50" placeholder="Để trống = mọi loại suất"><datalist id="rpMealTypes"></datalist></div>
          <div class="field"><label>Ngày áp dụng</label><input type="date" class="input" id="rpDate" value="${Store.ctx.date}"></div></div>
        <div id="rpOut" class="mt"></div>`,
      foot: `<button class="btn" data-close>Đóng</button><button class="btn primary" id="rpGo">${I('calc')} Tra giá</button>`,
      onMount: m => {
        const g = sel => m.querySelector(sel);
        g('#rpCus').onchange = () => { const cid = g('#rpCus').value; g('#rpCt').innerHTML = opts(cid ? Store.by('Contract', 'CustomerId', cid) : [], c => (c.ContractNumber || '') + (c.Name ? ' · ' + c.Name : ''), 'Không chọn'); g('#rpMealTypes').innerHTML = mealTypes(cid).map(t => `<option value="${esc(t)}">`).join(''); g('#rpOut').innerHTML = ''; };
        g('#rpGo').onclick = () => {
          const cid = g('#rpCus').value, out = g('#rpOut'); if (!cid) return UI.toast('Chọn khách hàng', 'err');
          out.innerHTML = '<div class="muted small">Đang tra…</div>';
          Store.query('PricePolicy/Resolve', { CustomerId: cid, ContractId: g('#rpCt').value, MealType: g('#rpMeal').value.trim(), Date: g('#rpDate').value }).then(x => {
            if (!out.isConnected) return;
            if (!x.ok || !x.data) { out.innerHTML = failBox(x.message || 'Không tìm thấy đơn giá áp dụng'); return; }
            out.innerHTML = `<div class="notice ok">${I('ok')}<div><b>${U.money(x.data.unitPrice)}</b> / suất<div class="small">Nguồn: ${esc(SRC[x.data.source] || x.data.source || '')}</div></div></div>`;
          });
        };
      }
    });
  }
  APP.register('crm-price-policy', (el, f) => {
    APP.genericList(el, f);
    if (!Store.isLive) return;
    const ha = el.querySelector('.heading-actions');
    if (ha && !ha.querySelector('#rpOpen')) { ha.insertAdjacentHTML('afterbegin', `<button class="btn" id="rpOpen">${I('calc')} Tra đơn giá áp dụng</button>`); ha.querySelector('#rpOpen').onclick = resolvePrice; }
  });
  // ---------- Gửi phê duyệt dùng chung ----------
  APP.submitApproval = (btype, table, id, steps, title) => {
    const n = Store.rows('ApprovalRequest').length + 1;
    const ar = Store.add('ApprovalRequest', { RequestNo: 'PD-2611-' + String(n).padStart(3, '0'), BusinessType: btype, BusinessId: id, RequestedBy: Store.user.id, RequestedAt: U.iso(new Date()), RequestStatus: 'Pending', CurrentStepNo: 1, PriorityCode: 'NORMAL', DueAt: U.addDays(Store.ctx.date, 1) + 'T17:00', PayloadJson: JSON.stringify({ title }) });
    steps.forEach((s, i) => { const g = Store.one('UserGroup', 'Name', s[0]); const uc = g && Store.one('UserClaim', 'UserGroupId', g.Id); Store.add('ApprovalStep', { ApprovalRequestId: ar.Id, StepNo: i + 1, ApproverUserId: uc ? uc.UserId : null, ApproverRoleCode: s[0], DecisionStatus: 'Waiting', AssignedAt: U.iso(new Date()), DueAt: ar.DueAt }); });
    Store.add('ApprovalItem', { ApprovalRequestId: ar.Id, ItemType: btype, ItemId: id, ActionCode: 'APPROVE', ItemStatus: 'Pending' });
    UI.toast(`Đã gửi phê duyệt ${esc(ar.RequestNo)} – ${steps.length} cấp (ApprovalRequest/ApprovalStep · DEMO)`, 'ok');
    return ar;
  };
})();
