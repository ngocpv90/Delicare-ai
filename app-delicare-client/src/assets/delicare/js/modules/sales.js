/* P1/P3 – Khách hàng 360°, Đơn hàng, Đối soát, Hợp đồng, Dự báo */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  // Thứ tự buổi (BUSINESS_RULES §0): Sáng, Trưa, Tối, Đêm.
  const mealRank = t => { const i = ['SANG', 'TRUA', 'TOI', 'DEM'].indexOf(String(t || '').toUpperCase()); return i < 0 ? 9 : i; };
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
  // UI-1.3 (D-64): Hợp đồng hiệu lực (còn N ngày) · Suất TB/ngày (7 ngày) · Doanh thu đã đối soát (N đơn) · Khiếu nại đang mở (/ tổng · % đúng hạn).
  // compact = hàng chỉ số gọn ở phần đầu màn 360 (UI-2R fix – D-80 d): tiền ≥ 10 triệu rút gọn, số phụ cùng dòng.
  APP.customerKpis = (d, compact) => UI.kpi('Hợp đồng hiệu lực', d ? U.num(d.activeContractCount || 0) : '–', d && d.daysToContractEnd !== null && d.daysToContractEnd !== undefined ? `còn ${d.daysToContractEnd} ngày` : '', 'file', 'green', 'crm-contract')
    + UI.kpi('Suất TB/ngày (7 ngày)', d ? U.num(d.avgMealsPerDay7d || 0, 0) : '–', '', 'utensils', 'orange')
    + UI.kpi('Doanh thu đã đối soát', d ? (compact ? U.moneyShort : U.money)(d.reconciledRevenue || 0) : '–', d ? `${compact ? '· ' : ''}${U.num(d.reconciledOrderCount || 0)} đơn` : '', 'calc', 'blue', 'crm-reconciliation')
    + UI.kpi('Phản ánh đang mở', d ? U.num(d.openComplaintCount || 0) : '–', d ? `/ ${U.num(d.complaintCount || 0)}${d.complaintOnTimePct === null || d.complaintOnTimePct === undefined ? '' : ' · ' + U.num(d.complaintOnTimePct, 0) + '% đúng hạn'}` : '', 'msg', 'red', 'log-complaint');
  APP.customerAvgMeals7d = ids => avgMeals7d(ids);
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
      onRow: id => APP.go('crm-customer-360?id=' + encodeURIComponent(id)),
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
  // D-78 (2) – GAP-BIZ-01: cách tính tiền đối soát theo hợp đồng (Contract.ConfigJson).
  const BILLING_BASIS = [['', 'Theo cấu hình chung'], ['RECEIVED', 'Theo số khách nhận (trừ suất lỗi Delicare)'], ['SERVED', 'Theo suất ăn thực tế'], ['ORDERED', 'Theo số đặt / chốt']];
  const billingSelect = (id, v) => `<select class="select" id="${id}">${BILLING_BASIS.map(([c, l]) => `<option value="${c}" ${(v || '') === c ? 'selected' : ''}>${l}</option>`).join('')}</select>`;
  // Chế độ API: ngăn chi tiết hợp đồng thêm tab loại suất & đơn giá, yêu cầu duyệt đang chờ, cảnh báo (Contract/GetDetail).
  function contractDetail(id, f) {
    const canEdit = Store.perm('crm-contract').edit !== false;
    const billBox = d => `<div class="card panel mt"><div class="spread"><div><div class="small muted">Cách tính tiền đối soát</div><b>${esc(d.billingBasisName || 'Theo cấu hình chung')}</b></div>${canEdit && d.contract && d.contract.status !== 'Expired' ? `<div class="row-flex">${billingSelect('cdBill', d.billingBasis)}<button class="btn sm" id="cdBillOk">Đổi</button></div>` : ''}</div><div class="small muted mt">Áp cho các lần đối soát sau; trống = theo cấu hình chung của hệ thống.</div></div>`;
    const bindBill = (p, d) => { const b = p && p.querySelector('#cdBillOk'); if (!b || !d) return; b.onclick = () => contractBilling(d.contract.id, p.querySelector('#cdBill').value).then(r => { if (!r.ok) return; p.innerHTML = html(r.data) + billBox(r.data); bindBill(p, r.data); }); };
    const row = Store.get('Contract', id);
    const q = Store.query('Contract/GetDetail', { id });
    const html = d => `${(d.listWarning || []).map(w => `<div class="notice warn">${I('alert')}<div>${esc(w)}</div></div>`).join('')}
      <div class="kv mb"><div><label>Khách hàng</label><span>${esc(d.customerName || '')}</span></div><div><label>Trạng thái</label><span>${esc(d.statusName || '')}</span></div><div><label>Yêu cầu duyệt đang chờ</label><span>${esc(d.pendingApprovalNo || '–')}</span></div><div><label>Đơn hàng đang mở</label><span>${U.num(d.openOrderCount || 0)}</span></div></div>
      <table class="table dc-table dt"><thead><tr><th>Loại suất</th><th>Tên suất</th><th>Hình thức phục vụ</th><th class="num">Đơn giá</th><th>Trạng thái</th></tr></thead><tbody>${(d.listMeal || []).map(m => `<tr><td>${UI.tag(m.mealType || '')}</td><td>${esc(m.mealName || '')}</td><td>${esc(m.serviceType || '')}</td><td class="num">${U.money(m.unitPrice)}</td><td>${UI.badge(m.status, 'ContractMeal', 'Status')}</td></tr>`).join('') || '<tr><td colspan="5" class="empty">Hợp đồng chưa có loại suất</td></tr>'}</tbody></table>`;
    return baseDetail('Contract', id, f, {
      noEdit: !row || row.Status !== 'Draft',   // backend chỉ cho sửa hợp đồng Nháp
      tabs: [{ label: 'Loại suất & đơn giá', html: p => { q.then(x => { if (p && p.isConnected) { p.innerHTML = x.ok && x.data ? html(x.data) + billBox(x.data) : failBox(x.message); bindBill(p, x.data); } }); return '<div class="muted small">Đang tải…</div>'; } }]
    });
  }
  function customer360(id, f) {
    const L = Store.isLive, tech = s => (L ? '' : s);
    const ords = Store.by('Order', 'CustomerId', id).sort((a, b) => b.MealDate.localeCompare(a.MealDate));
    const none = '<div class="muted small">Chưa có dữ liệu</div>';
    const kpiHtml = APP.customerKpis;
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
      foot: () => `<button class="btn" data-act="open360">${I('eye')} Mở hồ sơ đầy đủ</button>`,
      onAct: (act, row, close) => { if (act === 'open360') { close(); APP.go('crm-customer-360?id=' + encodeURIComponent(id)); } },
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
    el.innerHTML = APP.header(f, `${Store.isLive && perm.edit !== false ? `<button class="btn" id="oLock" title="Chốt mọi đơn Đã xác nhận của 1 ngày ăn (khi đã tới giờ chốt)">${I('key')} Chốt đơn theo ngày</button>` : ''}<button class="btn" id="oView">${I(os.view === 'board' ? 'clipboard' : 'layers')} ${os.view === 'board' ? 'Dạng bảng' : 'Dạng Kanban'}</button><button class="btn primary" id="oNew" ${perm.add === false ? 'disabled' : ''}>${I('plus')} Tạo đơn hàng</button>`) + `
    <div class="card panel mb"><div class="row-flex"><span class="small muted">Ngày ăn (Order.MealDate):</span><button class="chip ${!os.date ? 'active' : ''}" data-d="">Tất cả</button>${dates.map(d => `<button class="chip ${d === os.date ? 'active' : ''}" data-d="${d}">${U.date(d).slice(0, 5)}${d === Store.ctx.date ? ' · hôm nay' : ''}</button>`).join('')}</div>
    <div class="mt">${UI.stepper(ORDER_FLOW.map(s => ({ l: UI.statusVi('Order', 'Status', s), s: rows.filter(o => o.Status === s).length + ' đơn' })), -1)}</div><div class="small muted mt">Luồng trạng thái Order.Status – PROPOSED – BA/PO CONFIRM. BRD: chỉ đơn Confirmed mới vào kế hoạch sản xuất.</div></div>
    <div id="oBody"></div>`;
    el.querySelectorAll('[data-d]').forEach(b => b.onclick = () => { os.date = b.dataset.d; APP.refresh(); });
    el.querySelector('#oView').onclick = () => { os.view = os.view === 'board' ? 'list' : 'board'; APP.refresh(); };
    el.querySelector('#oNew').onclick = newOrder;
    const lk = el.querySelector('#oLock'); if (lk) lk.onclick = lockByDay;
    const body = el.querySelector('#oBody');
    if (os.view === 'board') {
      const paint = (box, cols) => {
        box.innerHTML = `<div class="kanban">${cols.map(k => `<div class="kcol"><div class="kcol-head"><span>${esc(k.name)}</span><span class="tag"${k.qty === undefined ? '' : ` title="${U.num(k.qty)} suất"`}>${k.count}</span></div>${k.items.map(o => `<div class="kcard" data-o="${esc(o.id)}"><b>${esc(o.no)}</b><small>${esc(o.cus)}</small>${o.loc ? `<small class="muted">${esc(o.loc)}</small>` : ''}<div class="row"><span class="small">${U.date(o.date)}</span><b>${U.num(o.qty)} suất</b></div></div>`).join('') || '<div class="small muted">—</div>'}</div>`).join('')}</div>`;
        box.querySelectorAll('[data-o]').forEach(c => c.onclick = () => APP.openDetail('Order', c.dataset.o));   // UI-6: trang chi tiết (DEMO: ngăn trượt)
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
      UI.table(body.querySelector('#oTbl'), { table: 'Order', rows, onRow: id => APP.openDetail('Order', id), cols: [{ k: 'OrderNumber', label: 'Số đơn' }, { k: 'CustomerId', label: 'Khách hàng' }, { k: 'ContractId', label: 'Hợp đồng' }, { k: 'MealDate', label: 'Ngày ăn' }, { k: 'TotalQuantity', label: 'Tổng suất', num: true }, { k: 'TotalAmount', label: 'Thành tiền', num: true }, { k: 'Status', label: 'Trạng thái' }], filters: [{ k: 'Status', label: 'Trạng thái', options: ORDER_FLOW.map(s => ({ v: s, l: UI.statusVi('Order', 'Status', s) })) }] });
    }
    if (params.id) setTimeout(() => APP.openDetail('Order', params.id), 50);
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
    // BIZ-2.1: đơn nhân viên nhập Nháp → Xác nhận thẳng (bỏ Gửi); Đã gửi = đơn khách tự đăng ký.
    const actions = { Draft: ['confirm', 'Xác nhận'], Submitted: ['confirm', 'Xác nhận'], Confirmed: ['lock', 'Chốt số lượng'] }[o.Status];
    UI.detail('Order', id, {
      screen: 'crm-order', noEdit: !editable,
      top: `${UI.stepper(ORDER_FLOW.map(s => UI.statusVi('Order', 'Status', s)), i, o.Status === 'Cancelled')}<div data-odlive></div><div class="small muted mt">Khách hàng: <b>${esc(c.DisplayName || c.Name || '')}</b>${c.CustomerType ? ' · ' + esc(UI.statusVi('Customer', 'CustomerType', c.CustomerType) || '') : ''}</div><div class="kpis mt">${UI.kpi('Số dòng suất', U.num(Store.by('OrderLine', 'OrderId', id).length), '', 'clipboard', 'green')}${UI.kpi('Tổng suất', U.num(o.TotalQuantity), '', 'utensils', 'blue')}${UI.kpi('Thành tiền', U.money(o.TotalAmount), '', 'calc', 'orange')}</div>
        <h3>Chi tiết theo ca / loại suất / điểm giao (OrderLine)</h3><table class="table dc-table dt"><thead><tr><th>Ca<small>ShiftId</small></th><th>Loại suất<small>MealType</small></th><th>Lựa chọn<small>MenuOptionNo</small></th><th>Điểm giao<small>Note</small></th><th class="num">Số lượng<small>Quantity</small></th><th class="num">Dự báo<small>Forecast</small></th><th class="num">Đơn giá</th><th class="num">Thành tiền<small>LineAmount</small></th></tr></thead><tbody>${lines.slice().sort((a, b) => mealRank(a.MealType) - mealRank(b.MealType) || (a.MenuOptionNo || 0) - (b.MenuOptionNo || 0)).map(l => { const fq = U.sum(fc.filter(x => x.MealType === l.MealType), 'ForecastQuantity'); return `<tr><td>${esc(Store.label('Shift', l.ShiftId))}</td><td>${UI.tag(l.MealType)}</td><td data-op="${l.Id}">${l.MenuOptionNo ? `<b>Lựa chọn ${l.MenuOptionNo}</b>` : '<span class="muted">–</span>'}</td><td class="small">${esc(l.Note || '')}</td><td class="num">${editable ? `<input type="number" class="input" style="width:90px;text-align:right" data-ql="${l.Id}" value="${l.Quantity}">` : U.num(l.Quantity)}</td><td class="num muted" data-fq="${l.Id}">${fq ? U.num(fq) : '–'}</td><td class="num">${U.money(l.UnitPrice)}</td><td class="num">${U.money(l.LineAmount)}</td></tr>`; }).join('')}</tbody></table>
        ${editable ? '<div class="small muted mt">Có thể sửa số lượng khi đơn ở trạng thái Nháp / Đã gửi. Sau khi chốt, thay đổi phải qua Yêu cầu đổi món / phê duyệt.</div>' : ''}
        ${dvs.length ? `<h3 class="mt">Đơn giao hàng (DeliveryOrder)</h3>${dvs.map(d => `<div class="spread" style="padding:6px 0;border-bottom:1px solid #F0F2ED"><a href="javascript:void 0" data-open="DeliveryOrder" data-id="${d.Id}">${esc(d.DeliveryNumber)}</a><span class="small">${esc(Store.label('CustomerDeliveryLocation', d.CustomerDeliveryLocationId))}</span>${UI.badge(d.Status, 'DeliveryOrder', 'Status')}</div>`).join('')}` : ''}
        ${rec ? `<h3 class="mt">Đối soát (OrderReconciliation)</h3><div class="kv"><div><label>Đặt</label><span>${U.num(rec.OrderedQuantity)}</span></div><div><label>Sản xuất</label><span>${U.num(rec.ProducedQty)}</span></div><div><label>Giao</label><span>${U.num(rec.DeliveredQuantity)}</span></div><div><label>Nhận</label><span>${U.num(rec.AcceptedQuantity)} (${U.pct(rec.VariancePercent, 2)})</span></div></div>` : ''}`,
      foot: () => `${editable ? `<button class="btn" data-act="saveq">${I('ok')} Lưu số lượng</button>` : ''}${actions ? `<button class="btn orange" data-act="${actions[0]}">${I('approve')} ${actions[1]}</button>` : ''}${['Draft', 'Submitted', 'Confirmed'].includes(o.Status) ? `<button class="btn danger" data-act="cancel">Hủy đơn</button>` : ''}`,
      onAct: (a, row, close, btn) => {
        const d = btn.closest('.drawer');
        if (a === 'saveq') {
          const qty = [...d.querySelectorAll('[data-ql]')].map(inp => ({ id: inp.dataset.ql, quantity: Math.max(0, Number(inp.value) || 0) }));
          saveOrderLines(id, qty, () => { close(); orderDetail(id); APP.refresh(); });
        }
        if (a === 'submit' && (!lines.length || !lines.every(l => l.ShiftId && l.MealType))) return UI.toast('Đơn phải có ít nhất 1 dòng với Ca và Loại suất', 'err');
        if (['submit', 'confirm', 'lock', 'cancel'].includes(a)) orderStep(a, o, U.sum(fc, 'ForecastQuantity'), done);
        function done(m) { close(); UI.toast(m + (Store.isLive ? '' : ' (Order.Status · DEMO)'), 'ok'); APP.refresh(); orderDetail(id); }
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
        // Bảng OrderLine chưa tải về (mở ngay sau khi tạo đơn / từ màn khác) → dựng dòng từ Order/GetDetail.
        if (!lines.length && (d.listLine || []).length) {
          const tb = [...dw.querySelectorAll('table')].find(t => t.querySelector('th') && /Lựa chọn/.test(t.querySelector('thead').innerText));
          if (tb) tb.querySelector('tbody').innerHTML = d.listLine.map(l => `<tr><td>${esc(l.shiftName || '')}</td><td>${UI.tag(l.mealType)}</td><td data-op="${l.id}">${l.menuOptionNo ? `<b>Lựa chọn ${l.menuOptionNo}</b>` : '<span class="muted">–</span>'}</td><td class="small">${esc(l.note || '')}</td><td class="num">${acts.includes('updateLines') ? `<input type="number" class="input" style="width:90px;text-align:right" data-ql="${l.id}" value="${l.quantity}">` : U.num(l.quantity)}</td><td class="num muted" data-fq="${l.id}">–</td><td class="num">${U.money(l.unitPrice)}</td><td class="num">${U.money(l.lineAmount)}</td></tr>`).join('');
        }
        (d.listLine || []).forEach(l => { const td = [...dw.querySelectorAll('[data-fq]')].find(n => n.dataset.fq === String(l.id)); if (td) td.textContent = has(l.forecastQuantity) ? U.num(l.forecastQuantity) : '–';
          // ORD-OPT-1.8: dòng theo lựa chọn hiện món trong set.
          const op = [...dw.querySelectorAll('[data-op]')].find(n => n.dataset.op === String(l.id)); if (op && l.menuOptionNo) op.innerHTML = `<b>Lựa chọn ${l.menuOptionNo}</b>${l.optionDishText ? `<div class="small muted">${esc(l.optionDishText)}</div>` : ''}`; });
        const need = { saveq: 'updateLines', submit: 'submit', confirm: 'confirm', lock: 'lock', cancel: 'cancel' };
        // UG-1 C-01: 'Yêu cầu sửa sau chốt' (modules/ghep.js gx-order-change) chỉ khi máy chủ cho phép (đã quá giờ chốt).
        need['gx-order-change'] = 'requestChange';
        dw.querySelectorAll('.drawer-foot [data-act]').forEach(b => { const k = need[b.dataset.act]; if (k && !acts.includes(k)) b.remove(); });
        if (!acts.includes('updateLines')) dw.querySelectorAll('[data-ql]').forEach(inp => { inp.disabled = true; });
      });
    }
  }
  APP.orderDetail = orderDetail;
  // Thao tác nghiệp vụ của đơn – dùng chung cho ngăn chi tiết (DEMO) và trang chi tiết (UI-6, modules/detail-sales.js gọi qua APP.crmOrder).
  // Chế độ API: action Order/* (backend kiểm tra giờ chốt, hợp đồng, dự báo…); DEMO: đổi trạng thái tại chỗ. o = { Id, OrderNumber, TotalQuantity }, fq = Σ dự báo (0 = không có).
  const ORDER_T = ['Order', 'OrderLine', 'ApprovalRequest'];
  function orderStep(a, o, fq, after) {
    const id = o.Id;
    const step = (action, form, status, msg) => Store.act('Order/' + action, { form: Object.assign({ Id: id }, form), tables: ORDER_T, demo: () => Store.update('Order', id, Object.assign({ Status: status }, form && form.Reason ? { Note: form.Reason } : {})) }).then(r => { if (r.ok) { bust(); after(msg); } });
    if (a === 'submit') step('Submit', null, 'Submitted', 'Đã gửi đơn');
    if (a === 'confirm') { const diff = fq ? (o.TotalQuantity - fq) / fq * 100 : 0; UI.confirm(`Xác nhận đơn <b>${esc(o.OrderNumber)}</b> – ${U.num(o.TotalQuantity)} suất.<br><span class="small ${Math.abs(diff) > 10 ? 'bad' : 'muted'}">So với dự báo: ${fq ? U.pct(diff) : 'không có dự báo'}</span>`, () => step('Confirm', null, 'Confirmed', 'Đã xác nhận – đơn sẵn sàng vào kế hoạch sản xuất')); }
    if (a === 'lock') UI.confirm('Chốt số lượng? Sau khi chốt, mọi thay đổi phải qua Yêu cầu đổi món / phê duyệt.' + (Store.isLive ? '' : '<div class="notice gap mt small">GAP-F-07: SQL chưa có cột giờ chốt (cut-off). Mockup cho phép chốt thủ công.</div>'), () => step('Lock', null, 'Locked', 'Đã chốt số lượng'));
    if (a === 'cancel') UI.confirm('Hủy đơn hàng này?', r => step('Cancel', { Reason: r }, 'Cancelled', 'Đã hủy đơn'), { input: 'Lý do hủy', required: true, danger: true, ok: 'Hủy đơn' });
  }
  // qty = [{ id: OrderLine.Id, quantity }].
  function saveOrderLines(id, qty, after) {
    return Store.act('Order/UpdateLines', { method: 'PUT', json: { id, listLine: qty }, tables: ORDER_T, demo: () => { qty.forEach(x => { const l = Store.get('OrderLine', x.id); Store.update('OrderLine', l.Id, { Quantity: x.quantity, LineAmount: x.quantity * (l.UnitPrice || 0) }); }); recalc(id); } })
      .then(r => { if (!r.ok) return; bust(); UI.toast(Store.isLive ? (r.data && r.data.approvalRequested ? 'Số suất lệch dự báo – đã gửi duyệt, chưa ghi số mới' : 'Đã cập nhật số lượng') : 'Đã cập nhật số lượng – OrderLine.Quantity (DEMO)', r.data && r.data.approvalRequested ? 'warn' : 'ok'); after(r); });
  }
  APP.crmOrder = { act: orderStep, saveLines: saveOrderLines };
  // UG-1 C-05: chốt đơn theo ngày ăn (Order/LockByMealDate – thay job 20:00 khi cần chốt tay): chọn ngày → xác nhận lần 2 → báo số đơn đã chốt / chưa xác nhận / chờ duyệt.
  function lockByDay() {
    const def = U.addDays(Store.ctx.date, 1);
    UI.modal({ title: 'Chốt đơn theo ngày ăn', size: 'sm',
      body: `<div class="field"><label>Ngày ăn <span class="req">*</span></label><input type="date" class="input" id="lkDay" value="${def}"></div><div class="small muted mt">Khóa mọi đơn <b>Đã xác nhận</b> của ngày ăn – chỉ làm được khi đã tới giờ chốt (20:00 hôm trước). Đơn đã chốt chỉ sửa được qua Yêu cầu sửa sau chốt.</div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn orange" id="lkOk">Chốt đơn</button>',
      onMount: (m, close) => m.querySelector('#lkOk').onclick = () => {
        const day = m.querySelector('#lkDay').value; if (!day) return UI.toast('Chọn ngày ăn', 'err');
        close();
        UI.confirm(`Chốt mọi đơn Đã xác nhận của ngày ăn <b>${esc(U.date(day))}</b>? Thao tác không hoàn tác.`, () => Store.act('Order/LockByMealDate', { form: { MealDate: day }, tables: ['Order', 'OrderLine'] }).then(res => {
          if (!res.ok) return;
          const d = res.data || {}, nc = (d.listNotConfirmedNumber || []).length, pa = (d.listPendingApprovalNumber || []).length;
          const extra = [nc ? nc + ' đơn chưa xác nhận' : '', pa ? pa + ' đơn đang chờ duyệt' : ''].filter(Boolean).join(' · ');
          UI.toast('Đã chốt ' + U.num(d.lockedCount || 0) + ' đơn ngày ' + esc(U.date(day)) + (extra ? ' – còn ' + extra : ''), extra ? 'warn' : 'ok'); bust(); APP.refresh();
          // BIZ-2.4: kế hoạch SX tự lập / phát hành sau chốt.
          const ap = d.autoPlan; if (ap) { const T = { RELEASED: ['ok', 'Hệ thống đã lập và phát hành kế hoạch ' + (ap.planNumber || '') + ' (' + U.num(ap.productionOrderCount || 0) + ' lệnh SX)'], HELD: ['warn', 'Kế hoạch ' + (ap.planNumber || '') + ' chưa tự phát hành – chờ COO: ' + (ap.listReason || []).join('; ')], SKIPPED: ['info', (ap.listReason || []).join('; ')], NONE: ['info', 'Chưa lập kế hoạch: ' + (ap.listReason || []).join('; ')] }[ap.status]; if (T) setTimeout(() => UI.toast(esc(T[1]), T[0]), 600); }
        }), { danger: true, ok: 'Chốt đơn' });
      } });
  }
  function newOrder() {
    const cus = Store.rows('Customer').filter(c => c.Status === 'Active');
    UI.modal({
      title: 'Tạo đơn đặt suất', sub: Store.isLive ? 'Loại suất theo hợp đồng hiệu lực · mỗi điểm giao một đơn · số lượng gợi ý từ dự báo đã công bố' : 'Order + OrderLine · lấy loại suất từ ContractMeal, điểm giao từ CustomerDeliveryLocation, gợi ý số lượng từ Forecast', size: 'lg',
      body: `<div class="form-grid"><div class="field"><label>Khách hàng <span class="req">*</span><code>CustomerId</code></label><select class="select" id="nC"><option value="">— Chọn —</option>${cus.map(c => `<option value="${c.Id}">${esc(c.DisplayName)}</option>`).join('')}</select></div><div class="field"><label>Ngày ăn <span class="req">*</span><code>MealDate</code></label><input type="date" class="input" id="nD" value="${U.addDays(Store.ctx.date, 1)}"></div></div><div id="nLines" class="mt"></div>`,
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
              ${(() => { const sh = {}; locs.forEach(l => (l.listLine || []).forEach(ln => { (sh[ln.mealType] = sh[ln.mealType] || new Set()).add(ln.shiftId || ''); })); window.__ncShowShift = Object.values(sh).some(x => x.size > 1); return ''; })()}${locs.map(l => `<h3 class="mt">${esc(l.locationName || l.locationCode || 'Điểm giao')}</h3>${l.existingOrderId ? `<div class="small muted">Điểm giao này đã có đơn <b>${esc(l.existingOrderNumber || '')}</b> cho ngày ăn đã chọn – mở đơn đó để sửa số lượng.</div>` : `<table class="table dc-table dt"><thead><tr><th>Loại suất</th>${window.__ncShowShift ? '<th>Ca</th>' : ''}<th class="num">Đơn giá</th><th class="num">Dự báo</th><th class="num">Số lượng</th></tr></thead><tbody>${(l.listLine || []).map(ln => { const ops = ln.listOption || []; const inp = (o, v) => `<input type="number" min="0" class="input" style="width:90px;text-align:right" data-m="${esc(ln.contractMealId)}" data-l="${esc(l.deliveryLocationId)}" data-s="${esc(ln.shiftId || '')}" data-o="${o || ''}" value="${v}">`;
                // ORD-OPT-1.3 / 1.8: buổi nhiều lựa chọn → mỗi lựa chọn 1 ô số suất (món trong set); dự báo là của cả buổi.
                if (ops.length > 1) return ops.map((op, k) => `<tr><td>${k ? '' : `${UI.tag(ln.mealType || '')} ${esc(ln.mealName || '')}`}<div><b>Lựa chọn ${op.optionNo}</b> <span class="small muted">${esc(op.dishText || '')}</span></div></td>${window.__ncShowShift ? `<td>${esc(ln.shiftName || ln.shiftCode || '')}</td>` : ''}<td class="num">${U.money(ln.unitPrice)}</td><td class="num muted">${!k && ln.suggestedQuantity ? U.num(ln.suggestedQuantity) + ' (cả buổi)' : ''}</td><td class="num">${inp(op.optionNo, 0)}</td></tr>`).join('');
                return `<tr><td>${UI.tag(ln.mealType || '')} ${esc(ln.mealName || '')}${ops.length === 1 && ops[0].dishText ? `<div class="small muted">${esc(ops[0].dishText)}</div>` : ''}</td>${window.__ncShowShift ? `<td>${esc(ln.shiftName || ln.shiftCode || '')}</td>` : ''}<td class="num">${U.money(ln.unitPrice)}</td><td class="num muted">${ln.suggestedQuantity ? U.num(ln.suggestedQuantity) : '–'}</td><td class="num">${inp(ops.length === 1 ? ops[0].optionNo : '', Number(ln.suggestedQuantity) || 0)}</td></tr>`; }).join('')}</tbody></table>`}`).join('') || '<div class="muted small">Khách hàng chưa có điểm giao hàng.</div>'}`;
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
            return Object.entries(byLoc).reduce((p, [locId, list]) => p.then(() => Store.act('Order/Create', { json: { customerId: cid, contractId: box.dataset.ct, deliveryLocationId: locId, mealDate: dte, listLine: list.map(x => ({ contractMealId: x.dataset.m, shiftId: x.dataset.s || null, menuOptionNo: x.dataset.o ? +x.dataset.o : null, quantity: +x.value, deliveryLocationId: locId })), afterCutoffReason: reason || null }, tables: ['Order', 'OrderLine', 'ApprovalRequest'] }).then(r => { if (r.ok && r.data && r.data.order) made.push(r.data.order); })), Promise.resolve())
              .then(() => {
                btn.disabled = false; if (!made.length) return;
                bust(); close(); os.date = dte;
                UI.toast('Đã tạo ' + (made.length > 1 ? made.length + ' đơn: ' : 'đơn ') + esc(made.map(x => x.orderNumber).join(', ')) + (reason ? ' – đã gửi duyệt' : ''), 'ok');
                APP.go('crm-order'); APP.refresh(); if (made.length === 1) setTimeout(() => APP.openDetail('Order', made[0].id), 80);
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
    // Chuỗi Đặt → SX → Đóng → Giao → Nhận theo ngày ăn – 10 ngày ăn gần nhất có đối soát (SX / đóng gói: máy chủ tính từ kế hoạch, mẻ, lệnh đóng gói của đơn).
    const byDay = U.group(rows.filter(r => (Store.get('Order', r.OrderId) || {}).MealDate), r => U.day(Store.get('Order', r.OrderId).MealDate));
    const days = Object.keys(byDay).sort().slice(-10);
    const CHAIN = [['Đặt', 'OrderedQuantity', UI.CHART_COLORS.order], ['Sản xuất', 'ProducedQty', UI.CHART_SEQ[2]], ['Đóng gói', 'PackedQty', UI.CHART_SEQ[3]], ['Giao', 'DeliveredQuantity', UI.CHART_COLORS.deliver], ['Nhận', 'AcceptedQuantity', UI.CHART_COLORS.receive]];
    // CHART-1 (D-94, D-86 b): 2 biểu đồ cạnh nhau (≥ 1200px; nhỏ hơn xếp dọc).
    function reconCharts(days, byDay, CHAIN) {
      const val = (d, k) => U.sum(byDay[d], k), ord = days.map(d => val(d, 'OrderedQuantity')), acc = days.map(d => val(d, 'AcceptedQuantity'));
      const left = UI.combo(days.map(d => U.date(d).slice(0, 5)), [
        { name: 'Đặt', type: 'bar', role: 'order', values: ord },
        { name: 'Giao', type: 'line', role: 'deliver', values: days.map(d => val(d, 'DeliveredQuantity')) },
        { name: 'Nhận', type: 'line', role: 'receive', dashed: true, values: acc }],
        { title: 'Đặt – Giao – Nhận theo ngày ăn', unit: 'suất', h: 240, tipExtra: i => { const g = acc[i] - ord[i]; return 'Chênh Nhận − Đặt: ' + (g > 0 ? '+' : '') + U.num(g) + (ord[i] ? ' (' + U.num(g / ord[i] * 100, 1) + '%)' : ''); } });
      const tot = CHAIN.map(([, k]) => days.reduce((a, d) => a + val(d, k), 0));
      const right = UI.hbars(CHAIN.map(([n]) => n), [{ name: 'Suất cả kỳ', values: tot, color: UI.CHART_COLORS.deliver }],
        { title: 'Chuỗi Đặt → Sản xuất → Đóng gói → Giao → Nhận', unit: 'suất', h: 240, labelHead: 'Khâu', tipExtra: i => i > 0 ? 'Hao hụt so với ' + CHAIN[i - 1][0] + ': ' + U.num(tot[i] - tot[i - 1]) + (tot[i - 1] ? ' (' + U.num((tot[i] - tot[i - 1]) / tot[i - 1] * 100, 1) + '%)' : '') : '' });
      return `<div class="dc-chart-pair"><div>${left}</div><div>${right}</div></div>`;
    }
    APP.genericList(el, f, {
      noAdd: true,
      top: `<div class="card panel mb"><div class="panel-head"><div><div class="panel-title">Chuỗi đối soát Đặt → Sản xuất → Đóng gói → Giao → Nhận</div><div class="panel-desc">Σ suất theo ngày ăn · ${days.length ? days.length + ' ngày ăn gần nhất có đối soát (' + U.date(days[0]) + ' – ' + U.date(days[days.length - 1]) + ')' : 'chưa có đối soát'} · chênh lệch = Nhận − Đặt</div></div></div>${days.length ? reconCharts(days, byDay, CHAIN) : '<div class="empty">Chưa có bản đối soát</div>'}</div>`,
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
  // BIZ-1.4: lý do lỗi Delicare (khớp DeliveryCode.FaultReasons máy chủ).
  const RC_FAULT = [['SHORTAGE', 'Thiếu suất'], ['DAMAGED', 'Hỏng / đổ vỡ'], ['TEMPERATURE', 'Không đạt nhiệt độ'], ['WRONG_DISH', 'Sai món'], ['QUALITY', 'Chất lượng không đạt']];
  // Form ghi suất thực tế phục vụ: 0 ≤ phục vụ ≤ suất khách nhận; phục vụ + trả về + hủy ≤ suất khách nhận (backend kiểm tra lại).
  function reconcileForm(o) {
    const has = v => v !== null && v !== undefined;
    const def = has(o.served) ? o.served : (has(o.pod) ? o.pod : '');
    // ORD-OPT-1.7 (D-74 i): đơn đặt theo lựa chọn → ghi suất phục vụ từng dòng (loại suất × lựa chọn); tổng tự cộng, tiền = Σ suất × giá buổi.
    const optLines = Store.by('OrderLine', 'OrderId', o.orderId).filter(l => !l.IsDeleted).sort((a, b) => mealRank(a.MealType) - mealRank(b.MealType) || (a.MenuOptionNo || 0) - (b.MenuOptionNo || 0));
    const byLine = Store.isLive && optLines.length > 1 && optLines.some(l => l.MenuOptionNo);
    const lineHtml = byLine ? `<h3 class="mt">Suất phục vụ theo lựa chọn</h3><table class="table dc-table dt"><thead><tr><th>Loại suất</th><th>Lựa chọn</th><th class="num">Đặt</th><th class="num">Đơn giá</th><th class="num">Phục vụ</th></tr></thead><tbody>${optLines.map(l => `<tr><td>${UI.tag(l.MealType)}</td><td>${l.MenuOptionNo ? 'Lựa chọn ' + l.MenuOptionNo : '–'}</td><td class="num">${U.num(l.Quantity)}</td><td class="num">${U.money(l.UnitPrice)}</td><td class="num"><input type="number" min="0" step="any" class="input" style="width:100px;text-align:right" data-rcl="${l.Id}" value="${l.Quantity}"></td></tr>`).join('')}</tbody></table>` : '';
    UI.modal({
      title: 'Ghi suất thực tế phục vụ', sub: 'Đơn ' + esc(o.label || ''), size: byLine ? 'lg' : undefined,
      body: `<div class="kv mb"><div><label>Suất đặt</label><span>${U.num(o.ordered)}</span></div><div><label>Suất giao</label><span>${U.num(o.delivered)}</span></div>${has(o.pod) ? `<div><label>Khách nhận khi bàn giao</label><span>${U.num(o.pod)}</span></div>` : ''}</div>
        <div class="form-grid"><div class="field"><label>Suất phục vụ thực tế <span class="req">*</span></label><input type="number" min="0" step="any" class="input" id="rcServed" value="${def}"></div><div class="field"><label>Suất trả về</label><input type="number" min="0" step="any" class="input" id="rcRet"></div><div class="field"><label>Suất hủy</label><input type="number" min="0" step="any" class="input" id="rcWaste"></div><div class="field"><label>Suất trả lại / hủy do lỗi Delicare</label><input type="number" min="0" step="any" class="input" id="rcFault"></div><div class="field"><label>Lý do lỗi Delicare</label><select class="select" id="rcFaultReason"><option value="">— Chọn khi có suất lỗi —</option>${RC_FAULT.map(([c, l]) => `<option value="${c}">${l}</option>`).join('')}</select></div><div class="field full"><label>Ghi chú</label><textarea class="input" rows="2" id="rcNote" maxlength="2000"></textarea></div></div>${lineHtml}
        <div class="notice info mt small" id="rcBill"></div>
        <div class="small muted mt">Tính tiền theo <b>số khách ký nhận trừ suất lỗi Delicare</b> (thiếu, hỏng, không đạt nhiệt độ, sai món); khách không dùng hết không giảm tiền. Suất phục vụ thực tế chỉ để tham khảo và làm dự báo.${byLine ? ' Suất phục vụ = tổng các lựa chọn; tiền = Σ suất phục vụ × giá buổi.' : ''} Có thể ghi lại cho tới khi chốt đối soát.</div>`,
      foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="rcSave">${I('ok')} Lưu đối soát</button>`,
      onMount: (m, close) => {
        const lineInputs = [...m.querySelectorAll('[data-rcl]')];
        const sumLines = () => { m.querySelector('#rcServed').value = lineInputs.reduce((a, x) => a + (Number(x.value) || 0), 0); };
        if (lineInputs.length) { m.querySelector('#rcServed').readOnly = true; lineInputs.forEach(x => x.addEventListener('input', sumLines)); sumLines(); }
        // BIZ-1.4 (D-77 d): hộp tính tiền – nhận · trả lại do lỗi Delicare · tính tiền · phục vụ (tham khảo).
        const bill = () => { const pod = Number(o.pod) || 0, f = Number(m.querySelector('#rcFault').value) || 0, sv = m.querySelector('#rcServed').value; m.querySelector('#rcBill').innerHTML = `${I('calc')}<div>Khách nhận <b>${U.num(pod)}</b> − lỗi Delicare <b>${U.num(f)}</b> = tính tiền <b>${U.num(Math.max(0, pod - f))}</b> suất · phục vụ (tham khảo) ${sv === '' ? '–' : U.num(Number(sv))}</div>`; };
        ['#rcFault', '#rcServed'].forEach(s => m.querySelector(s).addEventListener('input', bill)); lineInputs.forEach(x => x.addEventListener('input', bill)); bill();
        m.querySelector('#rcSave').onclick = () => {
        const val = sel => { const v = m.querySelector(sel).value; return v === '' ? null : Number(v); };
        const served = val('#rcServed'), ret = val('#rcRet'), waste = val('#rcWaste'), fault = val('#rcFault'), faultReason = m.querySelector('#rcFaultReason').value;
        if (fault !== null && (fault < 0 || (has(o.pod) && fault > Number(o.pod)))) return UI.toast('Suất lỗi Delicare từ 0 đến số suất khách nhận', 'err');
        if (fault > 0 && !faultReason) return UI.toast('Chọn lý do lỗi Delicare', 'err');
        if (served === null || isNaN(served) || served < 0) return UI.toast('Nhập số suất phục vụ thực tế (≥ 0)', 'err');
        if ((ret !== null && ret < 0) || (waste !== null && waste < 0)) return UI.toast('Suất trả về / hủy không được âm', 'err');
        if (has(o.pod) && served + (ret || 0) + (waste || 0) > Number(o.pod)) return UI.toast('Phục vụ + trả về + hủy không được vượt số suất khách nhận (' + U.num(o.pod) + ')', 'err');
        const form = { OrderId: o.orderId, ServedQuantity: served, ReturnedQuantity: ret, WastedQuantity: waste, DelicareFaultQuantity: fault, FaultReason: fault > 0 ? faultReason : null, Remark: m.querySelector('#rcNote').value.trim() };
        lineInputs.forEach((x, k) => { form[`ListLine[${k}].OrderLineId`] = x.dataset.rcl; form[`ListLine[${k}].ServedQuantity`] = Math.max(0, Number(x.value) || 0); });
        Store.act('OrderReconciliation/Reconcile', { form, tables: ['OrderReconciliation', 'Order'] })
          .then(r => { if (!r.ok) return; bust(); close(); UI.toast('Đã ghi đối soát đơn ' + esc(o.label || ''), 'ok'); APP.refresh(); });
        };
      }
    });
  }
  // Chế độ API: tạo hợp đồng kèm các loại suất trong 1 lần (Contract/Create) – hợp đồng luôn bắt đầu ở Nháp, số tự sinh nếu để trống.
  // Lỗi UAT 2026-11-21: loại suất chọn từ danh mục ReferenceLists:MealType (không gõ chữ tự do); Tên suất tự điền theo loại suất, sửa được.
  function newContract() {
    if (Store.isLive && !Store.mealTypes().length) return Store.loadMealTypes().then(() => Store.mealTypes().length ? newContract() : UI.toast('Chưa tải được danh mục loại suất – thử lại sau', 'err'));
    const cus = Store.rows('Customer').filter(c => c.Status !== 'Inactive');
    const cat = Store.mealTypes();
    const mtInput = () => cat.length
      ? `<select class="select" data-mt><option value="">— Chọn loại suất —</option>${cat.map(x => `<option value="${esc(x.code)}" data-label="${esc(x.name)}">${esc(x.name)} (${esc(x.code)})</option>`).join('')}</select>`
      : `<input class="input" data-mt list="ctMealTypes" maxlength="50" placeholder="Mã loại suất">`;
    const mealRow = () => `<tr><td>${mtInput()}</td><td><input class="input" data-mn maxlength="250" placeholder="Tự điền theo loại suất"></td><td><input type="number" min="0" step="any" class="input" data-mp style="text-align:right" placeholder="Đơn giá"></td><td class="actions"><button class="btn xs danger" data-rm title="Bỏ dòng">${I('trash')}</button></td></tr>`;
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
          <div class="field"><label>Cách tính tiền đối soát</label>${billingSelect('ctBill', '')}</div>
          <div class="field"><label>Giá trị hợp đồng</label><input type="number" min="0" step="any" class="input" id="ctVal"></div>
          <div class="field full"><label>Ghi chú</label><textarea class="input" rows="2" id="ctNote"></textarea></div></div>
        <div class="spread mt"><h3 style="margin:0">Loại suất & đơn giá <span class="req">*</span></h3><button class="btn xs" id="ctAdd">${I('plus')} Thêm loại suất</button></div>
        ${cat.length ? '' : `<datalist id="ctMealTypes">${mealTypes().map(t => `<option value="${esc(t)}">`).join('')}</datalist>`}
        <table class="table dc-table dt mt"><thead><tr><th>Loại suất</th><th>Tên suất</th><th class="num">Đơn giá</th><th></th></tr></thead><tbody id="ctMeals">${mealRow()}</tbody></table>
        <div class="small muted mt">Loại suất chọn trong danh mục (Sáng / Trưa / Tối / Đêm – cấu hình ReferenceLists:MealType); mỗi loại suất chỉ một lần trong hợp đồng; đơn giá (nếu nhập) phải lớn hơn 0.</div>`,
      foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="ctSave">${I('ok')} Tạo hợp đồng</button>`,
      onMount: (m, close) => {
        const tb = m.querySelector('#ctMeals'), v = sel => m.querySelector(sel).value.trim();
        m.querySelector('#ctAdd').onclick = () => tb.insertAdjacentHTML('beforeend', mealRow());
        tb.addEventListener('click', e => { const b = e.target.closest('[data-rm]'); if (b && tb.rows.length > 1) b.closest('tr').remove(); });
        // Chọn loại suất → Tên suất = nhãn danh mục khi ô còn trống / vẫn là giá trị tự điền.
        tb.addEventListener('change', e => { const s = e.target.closest('select[data-mt]'); if (!s) return; const mn = s.closest('tr').querySelector('[data-mn]'), o = s.selectedOptions[0], lb = o && o.value ? 'Suất ' + (o.dataset.label || o.value).toLowerCase() : ''; if (!mn.value || mn.value === mn.dataset.auto) { mn.value = lb; mn.dataset.auto = lb; } });
        m.querySelector('#ctSave').onclick = () => {
          const meals = [...tb.rows].map(tr => ({ mealType: tr.querySelector('[data-mt]').value.trim(), mealName: tr.querySelector('[data-mn]').value.trim() || null, unitPrice: tr.querySelector('[data-mp]').value === '' ? null : Number(tr.querySelector('[data-mp]').value) })).filter(x => x.mealType || x.mealName || x.unitPrice !== null);
          if (!v('#ctCus')) return UI.toast('Chọn khách hàng', 'err');
          if (!v('#ctName')) return UI.toast('Nhập tên hợp đồng', 'err');
          if (!v('#ctFrom')) return UI.toast('Nhập ngày bắt đầu hiệu lực', 'err');
          if (v('#ctTo') && v('#ctTo') < v('#ctFrom')) return UI.toast('Ngày hết hiệu lực phải sau ngày bắt đầu', 'err');
          if (!meals.length) return UI.toast('Nhập ít nhất 1 loại suất', 'err');
          if (meals.some(x => !x.mealType)) return UI.toast('Mỗi dòng phải chọn loại suất', 'err');
          if (new Set(meals.map(x => x.mealType.toLowerCase())).size !== meals.length) return UI.toast('Mỗi loại suất chỉ được xuất hiện một lần', 'err');
          if (meals.some(x => x.unitPrice !== null && !(x.unitPrice > 0))) return UI.toast('Đơn giá phải lớn hơn 0', 'err');
          // JSON vì có danh sách loại suất lồng trong hợp đồng.
          Store.act('Contract/Create', { json: { customerId: v('#ctCus'), name: v('#ctName'), signedDate: v('#ctSigned') || null, effectiveFrom: v('#ctFrom'), effectiveTo: v('#ctTo') || null, paymentTerm: v('#ctPay') || null, billingBasis: v('#ctBill') || null, contractValue: v('#ctVal') === '' ? null : Number(v('#ctVal')), note: v('#ctNote') || null, listMeal: meals }, tables: ['Contract', 'ContractMeal'] })
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
    APP.rowExtra(el, tr => { const r = Store.get('Contract', tr.dataset.row); if (r.Status === 'Draft') { const td = tr.querySelector('.actions'); td.insertAdjacentHTML('afterbegin', `<button class="btn xs orange" data-sub="${r.Id}">Gửi duyệt</button> `); td.querySelector('[data-sub]').onclick = () => contractSubmit(r); }
      if (r.Status === 'Active' && Store.isLive) { const td = tr.querySelector('.actions'); td.insertAdjacentHTML('afterbegin', `<button class="btn xs danger" data-term="${r.Id}">Thanh lý</button> `); td.querySelector('[data-term]').onclick = () => contractTerminate(r); } });
  });
  // Gửi duyệt / thanh lý / đổi cách tính tiền đối soát – dùng chung cho danh sách và trang chi tiết (UI-6, modules/detail-sales.js gọi qua APP.crmContract).
  // r = { Id, ContractNumber }; after() sau khi ghi xong (mặc định vẽ lại màn).
  function contractSubmit(r, after) {
    return Store.act('Contract/Submit', { form: { Id: r.Id }, tables: ['Contract', 'ApprovalRequest', 'ApprovalStep', 'ApprovalItem'], demo: () => { APP.submitApproval('CONTRACT', 'Contract', r.Id, [['Ban giám đốc']], 'Duyệt hợp đồng ' + r.ContractNumber); Store.update('Contract', r.Id, { Status: 'PendingApproval' }); } })
      .then(x => { if (!x.ok) return; if (!x.demo) UI.toast('Đã gửi duyệt hợp đồng ' + esc(r.ContractNumber), 'ok'); bust(); (after || APP.refresh)(); });
  }
  function contractTerminate(r, after) {
    UI.confirm('Thanh lý hợp đồng <b>' + esc(r.ContractNumber) + '</b>?', note => Store.act('Contract/Terminate', { form: { Id: r.Id, Reason: note }, tables: ['Contract'] }).then(x => { if (x.ok) { UI.toast('Đã thanh lý hợp đồng', 'ok'); bust(); (after || APP.refresh)(); } }), { input: 'Lý do thanh lý', required: true, danger: true, ok: 'Thanh lý' });
  }
  function contractBilling(id, basis) {
    return Store.act('Contract/UpdateBillingBasis', { method: 'PUT', form: { Id: id, BillingBasis: basis || null }, tables: ['Contract'] }).then(r => { if (r.ok) UI.toast('Đã đổi cách tính tiền: ' + esc((r.data || {}).billingBasisName || ''), 'ok'); return r; });
  }
  APP.crmContract = { submit: contractSubmit, terminate: contractTerminate, setBilling: contractBilling, billingSelect };
  // ---------- BIZ-3.2 (D-77 – A7): Bảng kê thanh toán tháng ----------
  const bk = { cus: '', ct: '', from: '', to: '' };
  APP.register('crm-billing', (el, f) => {
    const t = Store.ctx.date, first = t.slice(0, 8) + '01';
    if (!bk.from) { bk.from = U.addDays(first, -1).slice(0, 8) + '01'; bk.to = U.addDays(first, -1); }
    const cus = Store.rows('Customer').filter(c => c.Status !== 'Inactive' && (!Store.ctx.branch || c.BranchId === Store.ctx.branch));
    const perm = Store.perm(f.id);
    el.innerHTML = `<div class="page-head"><div><h1>Bảng kê thanh toán</h1><div class="muted small">Suất tính tiền đã chốt đối soát × đơn giá cố định lúc chốt + phí thất thoát khay + VAT. Hóa đơn / công nợ ở phần mềm kế toán.</div></div>
      <div class="heading-actions">${perm.exp !== false ? `<button class="btn" id="bkXls">${I('download')} Xuất Excel</button>` : ''}${perm.print !== false ? `<button class="btn" id="bkPdf">${I('print')} In PDF</button>` : ''}</div></div>
      <div class="card panel mb"><div class="row-flex" style="gap:10px;flex-wrap:wrap">
        <div class="field" style="min-width:240px"><label>Khách hàng <span class="req">*</span></label><select class="select" id="bkCus">${opts(cus, c => c.DisplayName || c.Code, '— Chọn khách hàng —')}</select></div>
        <div class="field" style="min-width:200px"><label>Hợp đồng</label><select class="select" id="bkCt"><option value="">Tất cả</option></select></div>
        <div class="field"><label>Từ ngày</label><input type="date" class="input" id="bkFrom" value="${bk.from}"></div>
        <div class="field"><label>Đến ngày</label><input type="date" class="input" id="bkTo" value="${bk.to}"></div>
        <div class="field" style="align-self:flex-end"><button class="btn primary" id="bkGo">${I('calc')} Lập bảng kê</button></div></div></div>
      <div id="bkOut"><div class="empty">Chọn khách hàng và kỳ để lập bảng kê</div></div>`;
    const g = s => el.querySelector(s);
    const fillCt = () => { g('#bkCt').innerHTML = opts(bk.cus ? Store.by('Contract', 'CustomerId', bk.cus) : [], c => (c.ContractNumber || '') + (c.Name ? ' · ' + c.Name : ''), 'Tất cả'); g('#bkCt').value = bk.ct || ''; };
    g('#bkCus').value = bk.cus; fillCt();
    g('#bkCus').onchange = () => { bk.cus = g('#bkCus').value; bk.ct = ''; fillCt(); };
    const params = () => { bk.cus = g('#bkCus').value; bk.ct = g('#bkCt').value; bk.from = g('#bkFrom').value; bk.to = g('#bkTo').value; return { CustomerId: bk.cus, ContractId: bk.ct || null, From: bk.from, To: bk.to }; };
    const need = () => { if (!g('#bkCus').value) { UI.toast('Chọn khách hàng', 'err'); return false; } return true; };
    const draw = () => {
      if (!need()) return; const out = g('#bkOut'); out.innerHTML = '<div class="muted small">Đang lập…</div>';
      Store.query('BillingStatement/GetStatement', params()).then(x => {
        if (!out.isConnected) return; if (!x.ok || !x.data) { out.innerHTML = failBox(x.message); return; }
        const d = x.data, rows = d.listRow || [];
        out.innerHTML = `<div class="kpis mb">${UI.kpi('Suất tính tiền', U.num(d.totalBilledQuantity), U.num(rows.length) + ' dòng', 'utensils', 'green')}${UI.kpi('Tiền suất ăn', U.money(d.mealAmount), '', 'calc', 'blue')}${UI.kpi('Phí thất thoát khay', U.money(d.containerFeeAmount), '', 'box', d.containerFeeAmount ? 'orange' : 'gray')}${UI.kpi('Tổng thanh toán', U.money(d.grandTotal), 'gồm VAT ' + U.num(d.vatPct) + '%', 'money', 'purple')}</div>
          <div class="card panel mb"><div class="panel-title mb">${esc(d.customerName || '')}${d.contractNumber ? ' · HĐ ' + esc(d.contractNumber) : ''} · ${U.date(U.day(d.from))} – ${U.date(U.day(d.to))} <span class="small muted">(${esc(d.billingBasisName || '')})</span></div>
          ${rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Ngày</th><th>Buổi</th><th>Đơn</th><th>Điểm giao</th><th class="num">Suất tính tiền</th><th class="num">Suất lỗi (đã trừ)</th><th class="num">Đơn giá</th><th class="num">Thành tiền</th></tr></thead><tbody>${rows.map(r => `<tr><td>${U.date(U.day(r.mealDate))}</td><td>${esc(r.mealName || r.mealType || '')}</td><td>${esc(r.orderNumber || '')}</td><td>${esc(r.locationName || '')}</td><td class="num">${U.num(r.billedQuantity)}</td><td class="num">${r.faultQuantity ? U.num(r.faultQuantity) : '<span class="muted">–</span>'}</td><td class="num">${U.money(r.unitPrice)}</td><td class="num">${U.money(r.amount)}</td></tr>`).join('')}</tbody>
            <tfoot><tr><td colspan="4"><b>Cộng tiền suất ăn</b></td><td class="num"><b>${U.num(d.totalBilledQuantity)}</b></td><td class="num">${U.num(d.totalFaultQuantity)}</td><td></td><td class="num"><b>${U.money(d.mealAmount)}</b></td></tr>
            ${(d.listContainerFee || []).map(c => `<tr><td colspan="4">Phí thất thoát khay ${esc(c.packagingType)} (mất ${U.num(c.periodLossQuantity)} / giao ${U.num(c.periodOutQuantity)}, ngưỡng ${U.num(c.thresholdPct)}%)</td><td class="num">${U.num(c.overQuantity)}</td><td></td><td class="num">${U.money(c.feePerUnit)}</td><td class="num">${U.money(c.amount)}</td></tr>`).join('')}
            <tr><td colspan="7">Cộng chưa VAT</td><td class="num">${U.money(d.subTotal)}</td></tr><tr><td colspan="7">VAT ${U.num(d.vatPct)}%</td><td class="num">${U.money(d.vatAmount)}</td></tr><tr><td colspan="7"><b>TỔNG THANH TOÁN</b></td><td class="num"><b>${U.money(d.grandTotal)}</b></td></tr></tfoot></table></div>` : '<div class="empty">Chưa có đơn nào đã chốt đối soát trong kỳ</div>'}
          ${d.compensationOrderCount ? `<div class="small muted mt">${d.compensationOrderCount} đơn bù do lỗi Delicare (giá 0) không tính tiền.</div>` : ''}</div>
          ${(d.listPending || []).length ? `<div class="card panel"><div class="panel-title mb">Chờ đối soát – chưa tính tiền (${d.listPending.length} đơn)</div><div class="table-wrap"><table class="table dc-table"><thead><tr><th>Đơn</th><th>Ngày ăn</th><th>Tình trạng</th><th class="num">Suất đặt</th><th class="num">Giá trị đơn</th></tr></thead><tbody>${d.listPending.map(p => `<tr><td>${esc(p.orderNumber || '')}</td><td>${U.date(U.day(p.mealDate))}</td><td>${esc(p.statusName || '')}</td><td class="num">${U.num(p.orderedQuantity)}</td><td class="num">${U.money(p.orderAmount)}</td></tr>`).join('')}</tbody></table></div></div>` : ''}`;
      });
    };
    g('#bkGo').onclick = draw;
    const dl = path => { if (need()) Store.download(path, params()); };
    if (g('#bkXls')) g('#bkXls').onclick = () => dl('BillingStatement/ExportStatement');
    if (g('#bkPdf')) g('#bkPdf').onclick = () => dl('BillingStatement/PrintStatement');
    if (bk.cus) draw();
  });
  // ---------- Dự báo suất ăn theo tuần (UI-6.5, D-84) ----------
  // Khách có hợp đồng hiệu lực tự hiện; khách → điểm giao · buổi × 7 ngày, một số mỗi ô. Sửa ô = lưu Nháp (Forecast/SaveCell, Enter / rời ô);
  // ô khách đã đăng ký chỉ đọc (dùng số đăng ký); ngày ngoài hiệu lực hợp đồng khóa. Vòng tự động: T4 22:00 gợi ý → T5 08:00 báo → T6 12:00 nhắc → T6 16:00 tự công bố.
  const dayCount = (a, b) => Math.round((new Date(b + 'T00:00') - new Date(a + 'T00:00')) / 86400000) + 1;
  const MEAL_VI = { SANG: 'Sáng', TRUA: 'Trưa', TOI: 'Tối', DEM: 'Đêm', CHIEU: 'Chiều', PHU: 'Phụ' };
  const mealVi = t => MEAL_VI[String(t || '').toUpperCase()] || t || 'Chung';
  const DOW = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
  const mondayOf = iso => { const d = new Date(iso + 'T00:00'), wd = d.getDay(); return U.addDays(iso, wd === 0 ? -6 : 1 - wd); };
  const fw = { week: null, tab: 'week', closed: new Set(), data: null, key: null, q: '', init: null };
  const nextMonday = () => U.addDays(mondayOf(Store.ctx.date), 7);
  const FW_CLS = { PUBLISHED: 'pub', DRAFT: 'draft', REGISTERED: 'reg', MISSING: 'miss', LOCKED: 'lock', EMPTY: '' };
  const has = v => v !== null && v !== undefined;
  function fwTip(c) {
    const h = (c.listHistory || []).map((v, i) => (c.listHistoryDate && c.listHistoryDate[i] ? U.date(U.day(c.listHistoryDate[i])).slice(0, 5) : 'tuần -' + (i + 1)) + ': ' + (has(v) ? U.num(v) : '– (không có đơn)'));
    const st = { PUBLISHED: 'Đã công bố', DRAFT: 'Nháp – chưa công bố', REGISTERED: 'Khách đã đăng ký – dùng số này', MISSING: 'Thiếu dữ liệu – nhập tay', LOCKED: 'Ngoài hiệu lực hợp đồng', EMPTY: 'Chưa có số' }[c.status] || '';
    return st + (has(c.published) && c.status === 'DRAFT' ? ' · đang công bố ' + U.num(c.published) : '') + (has(c.suggested) ? ' · gợi ý ' + U.num(c.suggested) : '') + (h.length ? '\nSuất thực nhận cùng thứ: ' + h.join(' · ') : '');
  }
  function fwHint(c) {
    if (c.status === 'LOCKED') return 'ngoài hợp đồng';
    if (c.status === 'REGISTERED') return 'đã đăng ký';
    if (c.status === 'MISSING') return 'thiếu dữ liệu';
    return has(c.average) ? 'TB ' + U.num(c.average) + (c.draftEdited ? ' · đã sửa' : '') : (c.draftEdited ? 'đã sửa' : '&nbsp;');
  }
  function fwInput(c, key, canEdit) {
    if (c.status === 'LOCKED') return '<span class="fw-q lock">–</span>';
    const ro = c.status === 'REGISTERED' || !canEdit;
    return `<input class="fw-q ${FW_CLS[c.status] || ''}" type="number" min="0" inputmode="numeric" data-fw="${key}" value="${has(c.value) ? c.value : ''}" ${c.status === 'MISSING' ? 'placeholder="nhập"' : ''} ${ro ? 'readonly' : ''} aria-label="${esc(fwTip(c).split('\n')[0])}">`;
  }
  function fwDraw(el, f) {
    const box = el.querySelector('#fwBody'), d = fw.data; if (!box || !d) return;
    const canEdit = Store.perm(f.id).edit !== false, days = (d.listDate || []).map(U.day);
    const head = days.map(k => `<th>${DOW[new Date(k + 'T00:00').getDay()]} ${U.date(k).slice(0, 5)}</th>`).join('');
    const all = d.listCustomer || [], qq = U.norm ? U.norm(fw.q) : fw.q.toLowerCase(), nm = c => (c.customerName || '') + ' ' + (c.customerCode || '');
    const cus = qq ? all.filter(c => (U.norm ? U.norm(nm(c)) : nm(c).toLowerCase()).includes(qq)) : all;
    if (fw.init !== fw.week) { fw.init = fw.week; fw.closed = new Set(all.length > 10 ? all.map(c => c.customerId) : []); }
    const tools = `<div class="fw-tools"><input class="input" id="fwQ" placeholder="Lọc khách hàng…" value="${esc(fw.q)}"><button class="btn sm" id="fwOpenAll">Mở tất cả</button><button class="btn sm" id="fwCloseAll">Thu gọn tất cả</button><span class="small muted">${U.num(cus.length)} / ${U.num(all.length)} khách · bấm dòng khách để mở / thu gọn</span></div>`;
    if (!all.length) { box.innerHTML = '<div class="empty">Tuần này không có khách nào có hợp đồng hiệu lực.</div>'; return; }
    const sum = a => (a || []).reduce((s, v) => s + (Number(v) || 0), 0);
    const desk = `<div class="table-wrap"><table class="fw-table"><colgroup><col class="name">${days.map(() => '<col>').join('')}<col class="tot"></colgroup><thead><tr><th class="l">Khách hàng · điểm giao · buổi</th>${head}<th>Tổng</th></tr></thead><tbody>${cus.map((c, ci) => {
      const closed = fw.closed.has(c.customerId);
      return `<tr class="fw-cus${closed ? ' closed' : ''}" data-fwc="${ci}"><td class="l fw-name"><span class="fw-chev">▾</span> ${esc(c.customerName || c.customerCode || '')} <span class="small muted">(${c.listRow.length})</span></td>${(c.listTotal || []).map(v => `<td class="tot">${U.num(v)}</td>`).join('')}<td class="tot">${U.num(sum(c.listTotal))}${canEdit ? ` <button class="btn xs fw-more" data-fws="${ci}" title="Tạo lại gợi ý cho riêng khách này" aria-label="Tạo lại gợi ý cho riêng khách này">⋯</button>` : ''}</td></tr>`
        + (closed ? '' : c.listRow.map((r, ri) => `<tr class="fw-sub"><td class="l">${esc(r.deliveryLocationName || 'Chung cho khách')} · ${esc(mealVi(r.mealType))}</td>${r.listCell.map((x, di) => `<td title="${esc(fwTip(x))}"><div class="fw-cell">${fwInput(x, ci + '|' + ri + '|' + di, canEdit)}<span class="fw-hint">${fwHint(x)}</span></div></td>`).join('')}<td class="tot">${U.num(sum(r.listCell.map(x => x.value)))}</td></tr>`).join(''));
    }).join('')}</tbody><tfoot><tr class="fw-foot"><td class="l">Tổng cộng</td>${(d.listTotal || []).map(v => `<td>${U.num(v)}</td>`).join('')}<td>${U.num(d.totalWeek)}</td></tr></tfoot></table></div>`;
    const mob = `<div class="fw-mob">${cus.map((c, ci) => `<div class="fw-card${fw.closed.has(c.customerId) ? ' closed' : ''}"><div class="fw-card-h" data-fwm="${ci}"><b><span class="fw-chev">▾</span> ${esc(c.customerName || c.customerCode || '')}</b><span class="small muted">${U.num(sum(c.listTotal))} suất${canEdit ? ` <button class="btn xs fw-more" data-fws="${ci}" title="Tạo lại gợi ý cho riêng khách này">⋯ Gợi ý</button>` : ''}</span></div>${fw.closed.has(c.customerId) ? '' : c.listRow.map((r, ri) => `<div class="fw-card-r"><div class="small"><b>${esc(r.deliveryLocationName || 'Chung cho khách')} · ${esc(mealVi(r.mealType))}</b></div><div class="fw-days">${r.listCell.map((x, di) => `<label title="${esc(fwTip(x))}"><span class="small muted">${DOW[new Date(days[di] + 'T00:00').getDay()]} ${U.date(days[di]).slice(0, 5)}</span>${fwInput(x, ci + '|' + ri + '|' + di, canEdit)}<span class="fw-hint">${fwHint(x)}</span></label>`).join('')}</div></div>`).join('')}</div>`).join('')}</div>`;
    box.innerHTML = tools + `<div class="fw-legend"><span><i class="fw-sw pub"></i>Đã công bố</span><span><i class="fw-sw draft"></i>Nháp (chưa công bố)</span><span><i class="fw-sw reg"></i>Khách đã đăng ký (dùng số này)</span><span><i class="fw-sw miss"></i>Thiếu dữ liệu – nhập tay</span><span><i class="fw-sw lock"></i>Ngoài hiệu lực hợp đồng</span><span>Chữ nhỏ dưới ô: TB 4 tuần · rê chuột xem 4 số</span></div><div class="fw-desk">${desk}</div>${mob}`;
    const q = box.querySelector('#fwQ'); q.oninput = () => { fw.q = q.value; clearTimeout(fw.t); fw.t = setTimeout(() => { fwDraw(el, f); const n = el.querySelector('#fwQ'); n.focus(); n.setSelectionRange(n.value.length, n.value.length); }, 250); };
    box.querySelector('#fwOpenAll').onclick = () => { cus.forEach(c => fw.closed.delete(c.customerId)); fwDraw(el, f); };
    box.querySelector('#fwCloseAll').onclick = () => { cus.forEach(c => fw.closed.add(c.customerId)); fwDraw(el, f); };
    box.querySelectorAll('tr.fw-cus').forEach(tr => tr.onclick = e => { if (e.target.closest('button')) return; const id = cus[+tr.dataset.fwc].customerId; fw.closed.has(id) ? fw.closed.delete(id) : fw.closed.add(id); fwDraw(el, f); });
    box.querySelectorAll('[data-fwm]').forEach(h => h.onclick = e => { if (e.target.closest('button')) return; const id = cus[+h.dataset.fwm].customerId; fw.closed.has(id) ? fw.closed.delete(id) : fw.closed.add(id); fwDraw(el, f); });
    box.querySelectorAll('[data-fws]').forEach(b => b.onclick = () => fwSuggest(el, f, cus[+b.dataset.fws]));
    box.querySelectorAll('input[data-fw]:not([readonly])').forEach(inp => {
      const [ci, ri, di] = inp.dataset.fw.split('|').map(Number), c = cus[ci], r = c.listRow[ri], x = r.listCell[di];
      const save = () => {
        const raw = inp.value.trim(), q = raw === '' ? null : Number(raw);
        if (q === (has(x.value) ? Number(x.value) : null)) return;
        if (q !== null && !(q >= 0)) { UI.toast('Số suất không được âm', 'err'); inp.value = has(x.value) ? x.value : ''; return; }
        inp.disabled = true;
        Store.act('Forecast/SaveCell', { json: { customerId: c.customerId, deliveryLocationId: r.deliveryLocationId || null, mealType: r.mealType || null, date: U.day(x.date), quantity: q }, quiet: false }).then(z => {
          inp.disabled = false; if (!z.ok) { inp.value = has(x.value) ? x.value : ''; return; }
          bust(); UI.toast(q === null ? 'Đã xóa bản Nháp của ô' : 'Đã lưu ' + U.num(q) + ' suất (bản Nháp)', 'ok'); fwLoad(el, f, true);
        });
      };
      inp.onchange = save; inp.onkeydown = e => { if (e.key === 'Enter') { e.preventDefault(); inp.blur(); } };
    });
  }
  function fwHead(el, f) {
    const d = fw.data, h = el.querySelector('#fwHead'); if (!h) return;
    const nm = nextMonday(), rel = fw.week === nm ? ' (tuần sau)' : fw.week === U.addDays(nm, -7) ? ' (tuần này)' : '';
    h.querySelector('#fwLbl').textContent = 'Tuần ' + U.date(fw.week).slice(0, 5) + ' – ' + U.date(U.addDays(fw.week, 6)) + rel;
    const draft = d ? d.draftCells : 0, canEdit = Store.perm(f.id).edit !== false;
    h.querySelector('#fwDraftN').textContent = U.num(draft) + ' ô Nháp chờ công bố';
    const pb = h.querySelector('#fwPub'); pb.disabled = !canEdit || !draft; pb.title = !canEdit ? 'Tài khoản không có quyền Sửa' : !draft ? 'Không còn ô Nháp' : '';
    h.querySelector('#fwSug').disabled = !canEdit;
    const k = el.querySelector('#fwKpis'); if (!k) return;
    const pct = v => has(v) ? U.num(v) + '%' : '–';
    k.innerHTML = d ? UI.kpis([
      UI.kpi('Tổng suất tuần', U.num(d.totalWeek), U.num((d.listCustomer || []).length) + ' khách', 'calc', 'green'),
      UI.kpi('Ô Nháp', U.num(d.draftCells), 'chưa công bố · đã công bố ' + U.num(d.publishedCells) + ' · đã đăng ký ' + U.num(d.registeredCells), 'edit', d.draftCells ? 'orange' : 'green'),
      UI.kpi('Ô thiếu dữ liệu', U.num(d.missingCells), 'nhập tay – dưới 2 tuần có đơn', 'alert', d.missingCells ? 'red' : 'green'),
      UI.kpi('Sai số 4 tuần', pct(d.mape4Weeks), 'dự báo so với thực nhận', 'chart', 'blue')], 'compact') : '';
  }
  function fwLoad(el, f, force) {
    const key = fw.key = 'fw|' + fw.week + '|' + (Store.ctx.branch || '');
    if (force) bust();
    const body = el.querySelector('#fwBody'); if (body && !fw.data) body.innerHTML = '<div class="muted small">Đang tải…</div>';
    cq(key, () => Store.query('Forecast/GetWeek', { WeekStart: fw.week, BranchId: Store.ctx.branch })).then(x => {
      if (fw.key !== key || !el.isConnected) return;
      if (!x.ok || !x.data) { fw.data = null; fwHead(el, f); const b = el.querySelector('#fwBody'); if (b) b.innerHTML = failBox(x.message); return; }
      fw.data = x.data; fwHead(el, f); if (fw.tab === 'week') fwDraw(el, f);
    });
  }
  function fwSuggest(el, f, cus) {
    const who = cus ? '<b>' + esc(cus.customerName || cus.customerCode || '') + '</b>' : 'mọi khách có hợp đồng hiệu lực';
    UI.confirm(`Tạo gợi ý tuần ${U.date(fw.week).slice(0, 5)} – ${U.date(U.addDays(fw.week, 6)).slice(0, 5)} cho ${who}?<div class="small muted mt">Hệ thống lấy trung bình suất thực nhận cùng thứ 4 tuần gần nhất, ghi vào ô Nháp. Ô người đã sửa và ô khách đã đăng ký được giữ nguyên.</div>`, () =>
      Store.act('Forecast/SuggestWeek', { form: { WeekStart: fw.week, CustomerId: cus ? cus.customerId : null } }).then(r => {
        if (!r.ok) return; const d = r.data || {};
        UI.toast('Đã tạo ' + U.num((d.created || 0) + (d.refreshed || 0)) + ' ô gợi ý' + (d.skippedEdited ? ' · giữ ' + U.num(d.skippedEdited) + ' ô đã sửa' : '') + (d.insufficientData ? ' · ' + U.num(d.insufficientData) + ' ô thiếu dữ liệu' : ''), 'ok');
        fwLoad(el, f, true);
      }), { ok: 'Tạo gợi ý', title: 'Tạo gợi ý dự báo' });
  }
  function fwPublish(el, f) {
    const d = fw.data || {};
    UI.confirm(`Công bố <b>${U.num(d.draftCells || 0)}</b> ô Nháp tuần ${U.date(fw.week).slice(0, 5)} – ${U.date(U.addDays(fw.week, 6)).slice(0, 5)}?<div class="small muted mt">Số đã công bố cũ cùng ô được thay thế. Ô khách đã đăng ký giữ số đăng ký, không công bố. Ô thiếu dữ liệu để trống.</div>`, () =>
      Store.act('Forecast/PublishWeek', { form: { WeekStart: fw.week } }).then(r => {
        if (!r.ok) return; const x = r.data || {};
        UI.toast('Đã công bố ' + U.num(x.published || 0) + ' ô' + (x.skippedRegistered ? ' · bỏ qua ' + U.num(x.skippedRegistered) + ' ô đã đăng ký' : '') + (x.missing ? ' · ' + U.num(x.missing) + ' ô còn trống' : ''), 'ok');
        fwLoad(el, f, true);
      }), { ok: 'Công bố', title: 'Công bố dự báo tuần' });
  }
  function fwHistory(el, f) {
    const p = el.querySelector('#fwBody'); if (!p) return;
    p.innerHTML = '<div class="small muted mb">Các dòng dự báo đã công bố (mới nhất trước). Dòng ghi "Hệ thống tự công bố" do hệ thống công bố lúc 16:00 thứ Sáu.</div><div id="fwHist"></div>';
    const cusName = id => { const c = Store.get('Customer', id); return c ? (c.DisplayName || c.Name || c.Code) : ''; };
    const locName = id => { const l = id && Store.get('CustomerDeliveryLocation', id); return l ? l.Name : 'Chung cho khách'; };
    UI.table(p.querySelector('#fwHist'), {
      cols: [{ k: 'ForecastDate', label: 'Ngày', render: r => U.date(U.day(r.ForecastDate)) }, { k: 'CustomerId', label: 'Khách hàng', render: r => esc(cusName(r.CustomerId)) }, { k: 'DeliveryLocationId', label: 'Điểm giao', render: r => esc(locName(r.DeliveryLocationId)) }, { k: 'MealType', label: 'Buổi', render: r => esc(mealVi(r.MealType)) }, { k: 'ForecastQuantity', label: 'Số suất', num: true, render: r => U.num(r.ForecastQuantity) }, { k: 'VersionNo', label: 'Phiên bản', num: true }, { k: 'Note', label: 'Ghi chú', render: r => esc(r.Note || '') }],
      rows: () => Store.rows('Forecast').filter(r => r.Status === 'Published').sort((a, b) => String(b.ModifiedDate || b.CreatedDate || '').localeCompare(String(a.ModifiedDate || a.CreatedDate || ''))), pageSize: 15
    });
  }
  function fwTab(el, f) {
    el.querySelectorAll('[data-fwtab]').forEach(t => t.classList.toggle('active', t.dataset.fwtab === fw.tab));
    const p = el.querySelector('#fwBody'); if (!p) return;
    if (fw.tab === 'week') { if (fw.data) fwDraw(el, f); else fwLoad(el, f); }
    else if (fw.tab === 'acc') { p.innerHTML = '<div id="fcAcc"><div class="muted small">Đang tải…</div></div>'; loadAccuracy(); }
    else fwHistory(el, f);
  }
  // FC-1.5: độ chính xác dự báo – 4 tuần đã qua gần nhất, theo khách (sai số % = Σ|dự báo − thực nhận| / Σ thực nhận).
  function loadAccuracy() {
    const box = document.getElementById('fcAcc'); if (!box) return;
    cq('fcacc|' + (Store.ctx.branch || ''), () => Store.query('Forecast/GetAccuracy', { BranchId: Store.ctx.branch })).then(x => {
      const el = document.getElementById('fcAcc'); if (!el) return;
      if (!x.ok || !x.data) { el.innerHTML = failBox(x.message); return; }
      const a = x.data, rows = a.listCustomer || [], warn = a.warningPct;
      const pct = v => v === null || v === undefined ? '–' : U.num(v) + '%';
      el.innerHTML = `<div class="kpis mb">${UI.kpi('Sai số dự báo', pct(a.errorPct), U.date(U.day(a.from)).slice(0, 5) + ' – ' + U.date(U.day(a.to)).slice(0, 5), 'calc', a.errorPct > warn ? 'red' : 'green')}${UI.kpi('Lệch chung', pct(a.biasPct), a.biasPct > 0 ? 'dự báo cao' : a.biasPct < 0 ? 'dự báo thấp' : '', 'chart', 'blue')}${UI.kpi('Khách sai số > ' + U.num(warn) + '%', U.num(a.customerOverWarning || 0), '/ ' + U.num(a.customerCount || 0) + ' khách', 'alert', a.customerOverWarning ? 'orange' : 'green')}</div>`
        + (rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Khách hàng</th><th class="num">Số ngày</th><th class="num">Dự báo</th><th class="num">Thực nhận</th><th class="num">Sai số</th><th class="num">Lệch</th></tr></thead><tbody>${rows.map(r => { const wk = (a.listWeek || []).filter(w => w.customerId === r.customerId); return `<tr title="${esc(wk.map(w => 'Tuần ' + U.date(U.day(w.weekStart)).slice(0, 5) + ': ' + pct(w.errorPct)).join(' · '))}"><td>${esc(r.customerName || r.customerCode || '')}</td><td class="num">${r.days}</td><td class="num">${U.num(r.forecast)}</td><td class="num">${U.num(r.actual)}</td><td class="num">${r.errorPct > warn ? `<b style="color:var(--red,#D64545)">${pct(r.errorPct)}</b>` : pct(r.errorPct)}</td><td class="num">${pct(r.biasPct)}</td></tr>`; }).join('')}</tbody></table></div><div class="small muted mt">So dự báo đã công bố với suất thực nhận (đối soát → POD → số đặt đã chốt). Rê chuột vào dòng để xem từng tuần.</div>` : '<div class="muted small">Chưa có dự báo đã công bố cho các ngày đã qua.</div>');
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
          <div class="field"><label>Loại suất <span class="req">*</span></label>${UI.refSelect('fiMeal', 'MealType', { required: true, placeholder: '— Chọn loại suất —' })}</div>
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
        const onCus = () => { const cid = g('#fiCus').value; g('#fiLoc').innerHTML = opts(cid ? Store.by('CustomerDeliveryLocation', 'CustomerId', cid) : [], l => l.Name, 'Chung cho khách hàng'); };
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
  APP.register('crm-forecast', (el, f) => {
    if (Store.isLive) {
      const perm = Store.perm(f.id);
      if (!fw.week) fw.week = nextMonday();
      el.innerHTML = APP.header(f, perm.imp === false ? '' : `<button class="btn" id="fwImp">${I('plus')} Nhập dự báo nhiều ngày</button>`, { noNote: true })
        + `<div class="notice info mb">${I('help')}<div>Ước số suất từng khách cho tuần tới khi khách chưa gửi đăng ký – dùng để mua nguyên liệu sớm. Khách gửi đăng ký rồi thì hệ thống dùng số đăng ký. Hệ thống tự tạo gợi ý 22:00 thứ Tư, nhắc 12:00 thứ Sáu và tự công bố bản Nháp lúc 16:00 thứ Sáu.</div></div>
        <div class="card panel mb" id="fwHead"><div class="spread"><div class="fw-week"><button class="btn sm" id="fwPrev" title="Tuần trước" aria-label="Tuần trước">◀</button><span id="fwLbl"></span><button class="btn sm" id="fwNext" title="Tuần kế tiếp" aria-label="Tuần kế tiếp">▶</button><button class="btn sm" id="fwNow">Tuần sau</button></div></div>
          <div class="fw-steps"><div class="fw-step"><span class="n s1">1</span><div><b>Tạo gợi ý</b><span class="small muted">Hệ thống lấy TB 4 tuần cùng thứ</span></div><button class="btn sm" id="fwSug">${I('calc')} Tạo gợi ý tuần này</button></div>
          <div class="fw-step"><span class="n s2">2</span><div><b>Sửa ô cần</b><span class="small muted">Nghỉ lễ, tăng ca, khách mới – Enter / rời ô để lưu</span></div></div>
          <div class="fw-step"><span class="n s3">3</span><div><b>Công bố</b><span class="small muted" id="fwDraftN"></span></div><button class="btn orange sm" id="fwPub">${I('approve')} Công bố tuần này</button></div></div>
          <div id="fwKpis"></div></div>
        <div class="card panel"><div class="tabs"><button class="tab" data-fwtab="week">Dự báo tuần</button><button class="tab" data-fwtab="acc">Độ chính xác</button><button class="tab" data-fwtab="hist">Lịch sử công bố</button></div><div id="fwBody"></div></div>`;
      const g = s => el.querySelector(s);
      const go = wk => { fw.week = wk; fw.data = null; fwHead(el, f); fw.tab = 'week'; fwTab(el, f); };
      g('#fwPrev').onclick = () => go(U.addDays(fw.week, -7)); g('#fwNext').onclick = () => go(U.addDays(fw.week, 7)); g('#fwNow').onclick = () => go(nextMonday());
      g('#fwSug').onclick = () => fwSuggest(el, f, null); g('#fwPub').onclick = () => fwPublish(el, f);
      if (g('#fwImp')) g('#fwImp').onclick = importForecast;
      el.querySelectorAll('[data-fwtab]').forEach(t => t.onclick = () => { fw.tab = t.dataset.fwtab; fwTab(el, f); });
      fwHead(el, f); fwTab(el, f); fwLoad(el, f);
      return;
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
          <div class="field"><label>Loại suất</label>${UI.refSelect('rpMeal', 'MealType', { placeholder: '— Mọi loại suất —' })}</div>
          <div class="field"><label>Ngày áp dụng</label><input type="date" class="input" id="rpDate" value="${Store.ctx.date}"></div></div>
        <div id="rpOut" class="mt"></div>`,
      foot: `<button class="btn" data-close>Đóng</button><button class="btn primary" id="rpGo">${I('calc')} Tra giá</button>`,
      onMount: m => {
        const g = sel => m.querySelector(sel);
        g('#rpCus').onchange = () => { const cid = g('#rpCus').value; g('#rpCt').innerHTML = opts(cid ? Store.by('Contract', 'CustomerId', cid) : [], c => (c.ContractNumber || '') + (c.Name ? ' · ' + c.Name : ''), 'Không chọn'); g('#rpOut').innerHTML = ''; };
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
