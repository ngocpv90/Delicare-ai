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
          .then(x => { if (!box.isConnected) return; box.innerHTML = customerCards(x.ok && x.data ? x.data : localSummary()); box.querySelectorAll('[data-go]').forEach(b => b.onclick = () => APP.go(b.dataset.go)); });
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
  // ---------- Đơn hàng theo ngày – UI-6.15 (D-96), mô phỏng docs/frontend-handoff/assets/order-day-mockup.html ----------
  // Theo dõi 1 ngày ăn + xử lý việc phát sinh (số hằng tuần nhập ở Đăng ký suất tuần). Một bảng: khách → điểm giao (cả điểm chưa có đơn, số dự báo mờ)
  // → Sáng / Trưa / Tối / Tổng / Tiến độ 5 chấm / Cần chú ý + nút xử lý tại dòng. Bấm dòng → trang chi tiết đơn. API Order/GetDay, Order/CreateExtra.
  const od = { day: '', tab: 'all', q: '', data: null, key: '', closed: new Set() };
  const OD_MEAL = { SANG: 'Sáng', TRUA: 'Trưa', TOI: 'Tối', DEM: 'Đêm' };
  const OD_STEPS = ['Đã xác nhận', 'Đã chốt', 'Đang nấu', 'Đã giao', 'Đã đối soát'];
  const OD_REASON = [['EVENT', 'Sự kiện / tiệc'], ['ADDON', 'Khách đặt thêm'], ['COMPENSATION', 'Đơn bù do phản ánh'], ['OTHER', 'Khác']];
  const WDAY = ['Chủ nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'];
  const odLabel = d => { const x = new Date(d + 'T00:00'); return WDAY[x.getDay()] + ' ' + U.date(d); };
  function orderDay(el, f, params) {
    const perm = Store.perm(f.id);
    if (params && params.date && params.date !== od.lastParam) { od.lastParam = params.date; od.day = params.date; }
    if (!od.day) od.day = U.addDays(Store.ctx.date, 1);
    const canLock = perm.edit !== false && Store.canApprove && Store.canApprove('crm-order');
    el.innerHTML = APP.header(f, `<button class="btn primary" id="odNew" ${perm.add === false ? 'disabled' : ''}>${I('plus')} Thêm đơn phát sinh</button>${canLock ? `<button class="btn more" id="odMore" aria-label="Thêm thao tác">⋯</button><div class="c360-more hide" id="odMoreBox"><button id="odLock">Chốt đơn theo ngày</button></div>` : ''}`, { noNote: true, sub: 'Theo dõi một ngày ăn và xử lý việc phát sinh' }) + `
      <div class="notice info mb">${I('help')}<div>Mọi đơn của một ngày ăn: số suất từng khách, từng điểm giao, từng buổi và đơn đang ở khâu nào. Đơn tự sinh từ <a href="javascript:void 0" data-go="crm-order-week">Đăng ký suất tuần</a> – màn này chỉ để theo dõi và xử lý việc phát sinh.</div></div>
      <div class="card panel mb"><div class="fw-week od-days"><button class="chip" data-odq="0">Hôm nay</button><button class="chip" data-odq="1">Ngày mai</button><button class="btn sm" id="odPrev" aria-label="Ngày trước">◀</button><b id="odLbl"></b><button class="btn sm" id="odNext" aria-label="Ngày sau">▶</button><span class="small muted" id="odState"></span></div>
        <div class="fw-steps ow-steps" id="odSteps"></div><div id="odKpis"></div></div>
      <div class="card panel"><div class="spread mb od-bar"><div class="tabs" id="odTabs"></div><div class="row-flex"><input class="input" id="odQ" placeholder="Tìm khách / điểm giao / số đơn" value="${esc(od.q)}" style="max-width:260px"><button class="btn" id="odXls">${I('export')} Xuất Excel</button></div></div>
        <div class="small muted mb">Số suất đã xác nhận (rê chuột xem từng lựa chọn) · chữ mờ <span class="muted">~150</span> là dự báo của điểm chưa có số · Tiến độ: ${OD_STEPS.join(' → ')}</div>
        <div id="odBody">${'<div class="muted small">Đang tải…</div>'}</div>
        <div class="small muted mt">Bấm một dòng để mở trang chi tiết đơn (số theo lựa chọn, món, giao hàng, đối soát, lịch sử). Sửa số trước giờ chốt làm ở Đăng ký suất tuần; sau giờ chốt bấm "Sửa sau chốt" trong trang chi tiết – hệ thống gửi duyệt.</div></div>`;
    const g = s => el.querySelector(s);
    el.querySelectorAll('[data-go]').forEach(a => a.onclick = () => APP.go(a.dataset.go + '?week=' + mondayOf(od.day)));
    const go = d => { od.day = d; od.data = null; od.key = ''; orderDay(el, f, null); };
    g('#odPrev').onclick = () => go(U.addDays(od.day, -1)); g('#odNext').onclick = () => go(U.addDays(od.day, 1));
    el.querySelectorAll('[data-odq]').forEach(b => { const d = U.addDays(Store.ctx.date, +b.dataset.odq); b.classList.toggle('active', d === od.day); b.onclick = () => go(d); });
    g('#odLbl').textContent = odLabel(od.day);
    g('#odNew').onclick = () => odExtra(el, f);
    const mb = g('#odMore'); if (mb) { const box = g('#odMoreBox'); mb.onclick = e => { e.stopPropagation(); box.classList.toggle('hide'); }; g('#odLock').onclick = () => { box.classList.add('hide'); lockByDay(od.day); }; }
    let tq; g('#odQ').oninput = e => { clearTimeout(tq); tq = setTimeout(() => { od.q = e.target.value.trim(); odDraw(el, f); }, 200); };
    g('#odXls').onclick = () => odExport();
    const key = od.day + '|' + (Store.ctx.branch || '');
    if (od.data && od.key === key) return odDraw(el, f);
    Store.query('Order/GetDay', { MealDate: od.day, BranchId: Store.ctx.branch }).then(x => {
      if (APP.current !== 'crm-order' || !g('#odBody') || !g('#odBody').isConnected) return;
      if (!x.ok || !x.data) { g('#odBody').innerHTML = failBox(x.message); return; }
      od.data = x.data; od.key = key; odDraw(el, f);
    });
  }
  const odRows = () => {
    const d = od.data, q = U.fold ? U.fold(od.q) : od.q.toLowerCase(), fold = s => (U.fold ? U.fold(s || '') : String(s || '').toLowerCase());
    return (d.listCustomer || []).map(c => ({ c, rows: c.listRow.filter(r => {
      if (od.tab === 'need' && !r.listAttention.some(a => a.tone === 'warn')) return false;
      if (od.tab === 'extra' && r.source !== 'EXTRA') return false;
      if (od.tab === 'cancel' && r.status !== 'Cancelled') return false;
      if (od.tab !== 'cancel' && r.status === 'Cancelled') return false;
      return !q || fold(c.customerName).includes(q) || fold(r.locationName).includes(q) || fold(r.orderNumber).includes(q);
    }) })).filter(x => x.rows.length);
  };
  function odDraw(el, f) {
    const d = od.data, g = s => el.querySelector(s);
    if (!d) return;
    const cut = new Date(d.cutoffAt), now = new Date(), left = cut - now;
    const stTxt = d.dayState === 'DELIVERED' ? 'đã giao xong' : d.dayState === 'LOCKED' ? 'đã chốt' : `chưa chốt · tự chốt lúc ${String(cut.getHours()).padStart(2, '0')}:${String(cut.getMinutes()).padStart(2, '0')} ${U.date(U.day(d.cutoffAt))}`;
    g('#odState').textContent = '(' + stTxt + ')';
    const pts = d.pointCount, withNo = d.pointsWithOrder;
    const hh = Math.max(0, Math.floor(left / 3600e3)), mm = Math.max(0, Math.floor(left % 3600e3 / 60e3));
    g('#odSteps').innerHTML = `
      <div class="fw-step"><span class="n s1">1</span><div><b>Nhận số từ đăng ký tuần</b><span class="small muted">${U.num(withNo)} / ${U.num(pts)} điểm giao đã có số</span></div><button class="btn sm" id="odWeek">${I('calendar')} Mở Đăng ký suất tuần</button></div>
      <div class="fw-step"><span class="n s2">2</span><div><b>Xử lý việc cần chú ý</b><span class="small muted">Chưa có số · chưa xác nhận · lệch dự báo · chờ duyệt</span></div><button class="btn sm ${d.attentionCount ? 'orange' : ''}" id="odNeed">${U.num(d.attentionCount)} việc cần xử lý</button></div>
      <div class="fw-step"><span class="n s3">3</span><div><b>Hệ thống tự chốt ${String(cut.getHours()).padStart(2, '0')}:${String(cut.getMinutes()).padStart(2, '0')}</b><span class="small muted">${d.dayState === 'OPEN' ? `Còn ${hh} giờ ${mm} phút. Chốt xong tự lập kế hoạch sản xuất.` : 'Đã chốt – kế hoạch sản xuất tự lập sau chốt.'}</span></div></div>
      <div class="fw-step"><span class="n s4">4</span><div><b>Nấu → giao → đối soát</b><span class="small muted">Hệ thống tự cập nhật theo từng khâu, không cần thao tác</span></div></div>`;
    g('#odWeek').onclick = () => APP.go('crm-order-week?week=' + mondayOf(od.day));
    g('#odNeed').onclick = () => { od.tab = 'need'; od.q = ''; const q = g('#odQ'); if (q) q.value = ''; odDraw(el, f); const t = g('#odTabs'); if (t) t.closest('.card').scrollIntoView({ behavior: 'smooth', block: 'start' }); };
    const dev = d.forecastDeviationPct;
    g('#odKpis').innerHTML = UI.kpis([
      UI.kpi('Tổng suất ngày', U.num(d.totalQuantity), dev === null || dev === undefined ? 'chưa có dự báo' : (dev > 0 ? '+' : '') + U.num(dev, 1) + '% so dự báo', 'utensils', 'green'),
      UI.kpi('Điểm giao có đơn', U.num(withNo) + ' / ' + U.num(pts), U.num(d.customerCount) + ' khách hàng', 'pin', 'blue'),
      UI.kpi('Cần xử lý', U.num(d.attentionCount), d.dayState === 'OPEN' ? 'trước giờ chốt' : '', 'alert', d.attentionCount ? 'orange' : 'green'),
      UI.kpi('Thành tiền dự kiến', U.moneyShort ? U.moneyShort(d.totalAmount) : U.money(d.totalAmount), 'chưa VAT', 'calc', 'purple'),
    ], 'compact');
    const all = (d.listCustomer || []).flatMap(c => c.listRow);
    const cnt = { all: all.filter(r => r.status !== 'Cancelled').length, need: all.filter(r => r.listAttention.some(a => a.tone === 'warn')).length, extra: all.filter(r => r.source === 'EXTRA' && r.status !== 'Cancelled').length, cancel: d.cancelledCount };
    g('#odTabs').innerHTML = [['all', 'Tất cả'], ['need', 'Cần xử lý'], ['extra', 'Đơn phát sinh'], ['cancel', 'Đã hủy']].map(([k, l]) => `<button class="tab ${od.tab === k ? 'active' : ''}" data-odt="${k}">${l} <span class="${k === 'need' && cnt.need ? 'od-cnt warn' : 'od-cnt'}">${U.num(cnt[k] || 0)}</span></button>`).join('');
    el.querySelectorAll('[data-odt]').forEach(t => t.onclick = () => { od.tab = t.dataset.odt; odDraw(el, f); });
    const meals = d.listMealType || [];
    const qty = (r, m) => { const x = (r.listMeal || []).find(y => y.mealType === m); return x ? x : null; };
    const cellOf = (r, m) => {
      const x = qty(r, m);
      if (!r.orderId) return `<td class="num muted">${r.forecast && m === (meals.includes('TRUA') ? 'TRUA' : meals[0]) ? '~' + U.num(r.forecast) : '–'}</td>`;
      if (!x) return '<td class="num muted">–</td>';
      const tip = (x.listOption || []).map(o => 'Lựa chọn ' + o.optionNo + ': ' + U.num(o.quantity)).join(' · ');
      return `<td class="num" title="${esc(tip)}">${U.num(x.quantity)}</td>`;
    };
    const prog = r => {
      if (r.status === 'Cancelled') return '<span class="od-cancel">Đã hủy</span>';
      if (!r.orderId) return '<span class="small muted">Chưa có đơn</span>';
      if (r.progress === 0) return `<span class="od-warn">Chưa xác nhận</span>`;
      return `<span class="od-prog" title="${esc(OD_STEPS[r.progress - 1] || '')}">${OD_STEPS.map((s, i) => `<i class="${i < r.progress ? 'on' : ''}"></i>`).join('')}</span> <span class="small">${esc(OD_STEPS[r.progress - 1] || '')}</span>`;
    };
    const att = r => r.listAttention.map(a => `<span class="tag ${a.tone === 'info' ? 'blue' : 'orange'}">${esc(a.label)}</span>${a.action === 'INPUT' ? ` <button class="btn sm" data-odin="${esc(r.deliveryLocationId)}">Nhập số</button>` : a.action === 'CONFIRM' && Store.perm('crm-order').edit !== false ? ` <button class="btn sm" data-odok="${esc(r.orderId)}">Xác nhận</button>` : a.action === 'APPROVE' && Store.canApprove && Store.canApprove('crm-order') ? ` <button class="btn sm orange" data-odap="${esc(r.orderId)}">Duyệt</button>` : ''}`).join(' ');
    const list = odRows();
    const colN = meals.length + 4;
    let h = `<div class="table-wrap"><table class="table dc-table od-table"><thead><tr><th>Khách hàng / điểm giao</th>${meals.map(m => `<th class="num">${OD_MEAL[m] || m}</th>`).join('')}<th class="num">Tổng</th><th>Tiến độ</th><th>Cần chú ý</th></tr></thead><tbody>`;
    list.forEach(({ c, rows }) => {
      const closed = od.closed.has(c.customerId);
      h += `<tr class="od-grp ${closed ? 'closed' : ''}" data-odg="${esc(c.customerId)}"><td><span class="od-caret">${closed ? '▸' : '▾'}</span> <b>${esc(c.customerName || '')}</b> <span class="small muted">· ${U.num(c.pointCount)} điểm giao</span></td>${meals.map(m => `<td class="num"><b>${c.mealTotal[m] ? U.num(c.mealTotal[m]) : '–'}</b></td>`).join('')}<td class="num"><b>${U.num(c.total)}</b></td><td></td><td>${c.attentionCount ? `<span class="tag orange">${U.num(c.attentionCount)} cần xử lý</span>` : ''}</td></tr>`;
      if (closed) return;
      rows.forEach(r => {
        h += `<tr class="od-row ${r.orderId ? 'click' : ''} ${r.status === 'Cancelled' ? 'od-x' : ''}" ${r.orderId ? `data-odo="${esc(r.orderId)}"` : ''}><td class="od-pt"><div>${esc(r.locationName || r.locationCode || '')}</div><div class="small muted">${r.source === 'EXTRA' ? 'Đơn phát sinh · ' + esc(r.orderNumber || '') + (r.extraReason ? ' · ' + esc(r.extraReason) : '') : r.source === 'WEEK' ? 'Từ đăng ký suất tuần · ' + esc(r.orderNumber || '') : ''}</div></td>${meals.map(m => cellOf(r, m)).join('')}<td class="num">${r.orderId ? `<b>${U.num(r.total)}</b>` : '–'}</td><td>${prog(r)}</td><td>${att(r)}</td></tr>`;
      });
    });
    if (!list.length) h += `<tr><td colspan="${colN}" class="muted">${od.tab === 'all' && !od.q ? 'Không có khách có hợp đồng hiệu lực cho ngày này' : 'Không có dòng phù hợp'}</td></tr>`;
    else h += `<tr class="od-total"><td><b>Tổng ngày</b></td>${meals.map(m => `<td class="num"><b>${U.num(d.mealTotal[m] || 0)}</b></td>`).join('')}<td class="num"><b>${U.num(d.totalQuantity)}</b></td><td></td><td></td></tr>`;
    h += '</tbody></table></div>';
    const body = g('#odBody'); body.innerHTML = h;
    body.querySelectorAll('[data-odg]').forEach(r => r.onclick = () => { const k = r.dataset.odg; if (od.closed.has(k)) od.closed.delete(k); else od.closed.add(k); odDraw(el, f); });
    body.querySelectorAll('[data-odo]').forEach(r => r.onclick = e => { if (e.target.closest('button')) return; APP.openDetail('Order', r.dataset.odo, f); });
    body.querySelectorAll('[data-odin]').forEach(b => b.onclick = e => { e.stopPropagation(); APP.go('crm-order-week?week=' + mondayOf(od.day) + '&loc=' + b.dataset.odin); });
    body.querySelectorAll('[data-odap]').forEach(b => b.onclick = e => { e.stopPropagation(); APP.openDetail('Order', b.dataset.odap, f); });
    body.querySelectorAll('[data-odok]').forEach(b => b.onclick = e => {
      e.stopPropagation();
      const r = all.find(x => x.orderId === b.dataset.odok);
      orderStep('confirm', { Id: r.orderId, OrderNumber: r.orderNumber, TotalQuantity: r.total }, r.forecast || 0, m => { UI.toast(m, 'ok'); od.data = null; od.key = ''; orderDay(el, f, null); });
    });
  }
  // Xuất Excel đúng bảng đang xem (lọc + tìm hiện tại).
  function odExport() {
    const d = od.data; if (!d) return;
    const meals = d.listMealType || [], rows = [];
    odRows().forEach(({ c, rows: rs }) => rs.forEach(r => { const o = { kh: c.customerName, dg: r.locationName, nguon: r.source === 'EXTRA' ? 'Đơn phát sinh' : r.source === 'WEEK' ? 'Đăng ký suất tuần' : 'Chưa có đơn', so: r.orderNumber || '', tt: r.statusName || '', tong: r.orderId ? r.total : '', du: r.forecast || '', cy: r.listAttention.map(a => a.label).join('; ') }; meals.forEach(m => { const x = (r.listMeal || []).find(y => y.mealType === m); o[m] = x ? x.quantity : ''; }); rows.push(o); }));
    const cols = [{ k: 'kh', label: 'Khách hàng' }, { k: 'dg', label: 'Điểm giao' }, { k: 'nguon', label: 'Nguồn' }, { k: 'so', label: 'Số đơn' }].concat(meals.map(m => ({ k: m, label: OD_MEAL[m] || m }))).concat([{ k: 'tong', label: 'Tổng' }, { k: 'du', label: 'Dự báo' }, { k: 'tt', label: 'Trạng thái' }, { k: 'cy', label: 'Cần chú ý' }]);
    U.csv(rows, cols.map(c => ({ k: c.k, label: c.label, col: c.k, text: r => r[c.k] })), 'DonHang_' + od.day.replace(/-/g, '') + '.csv');
    UI.toast('Đã xuất ' + rows.length + ' dòng', 'ok');
  }
  // "Thêm đơn phát sinh": khách → điểm giao → ngày ăn → lý do → số suất theo buổi × lựa chọn (món + đơn giá theo hợp đồng) → "Tạo đơn · N suất · tiền".
  function odExtra(el, f) {
    const cus = (od.data ? od.data.listCustomer : []).map(c => ({ id: c.customerId, name: c.customerName }));
    UI.modal({ title: 'Thêm đơn phát sinh', sub: 'Đơn ngoài đăng ký tuần: sự kiện, khách đặt thêm, đơn bù. Giá lấy theo hợp đồng, món theo thực đơn tuần.', size: 'lg',
      body: `<div class="form-grid"><div class="field"><label>Khách hàng <span class="req">*</span></label><select class="select" id="exC"><option value="">— Chọn —</option>${cus.map(c => `<option value="${esc(c.id)}">${esc(c.name || '')}</option>`).join('')}</select></div>
        <div class="field"><label>Ngày ăn <span class="req">*</span></label><input type="date" class="input" id="exD" value="${od.day}"></div>
        <div class="field"><label>Điểm giao <span class="req">*</span></label><select class="select" id="exL"><option value="">— Chọn khách trước —</option></select></div>
        <div class="field"><label>Lý do <span class="req">*</span></label><div class="row-flex" id="exR">${OD_REASON.map(([k, l], i) => `<label class="chip-radio"><input type="radio" name="exR" value="${k}" ${i === 0 ? 'checked' : ''}> ${l}</label>`).join('')}</div></div>
        <div class="field" style="grid-column:1/-1"><label>Ghi chú</label><input class="input" id="exN" placeholder="vd. Tiệc kỷ niệm 10 năm"></div></div>
        <div id="exWarn"></div><div id="exLines" class="mt"><div class="muted small">Chọn khách hàng và ngày ăn để tải loại suất theo hợp đồng.</div></div><div id="exCut" class="small muted mt"></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="exOk" disabled>Tạo đơn</button>',
      onMount: (m, close) => {
        let tpl = null, seq = 0;
        const sel = s => m.querySelector(s);
        const sum = () => {
          let n = 0, money = 0;
          m.querySelectorAll('[data-exq]').forEach(i => { const q = Math.max(0, +i.value || 0); n += q; money += q * (+i.dataset.p || 0); });
          const ok = sel('#exOk'); ok.disabled = !n || !sel('#exL').value; ok.textContent = n ? `Tạo đơn · ${U.num(n)} suất · ${U.money(money)}` : 'Tạo đơn';
        };
        const warn = () => {
          const loc = sel('#exL').value, box = sel('#exWarn'); box.innerHTML = '';
          const l = tpl && (tpl.listLocation || []).find(x => x.deliveryLocationId === loc);
          if (l && l.existingOrderId) box.innerHTML = `<div class="notice warn">${I('alert')}<div>Điểm này đã có đơn <b>${esc(l.existingOrderNumber || '')}</b> từ đăng ký tuần – đơn này là đơn thêm.</div></div>`;
          sum();
        };
        const load = () => {
          const cid = sel('#exC').value, day = sel('#exD').value, box = sel('#exLines');
          tpl = null; sel('#exL').innerHTML = '<option value="">— Chọn —</option>';
          if (!cid || !day) { box.innerHTML = '<div class="muted small">Chọn khách hàng và ngày ăn để tải loại suất theo hợp đồng.</div>'; return sum(); }
          const my = ++seq; box.innerHTML = '<div class="muted small">Đang tải…</div>';
          Store.query('Order/GetNewTemplate', { customerId: cid, mealDate: day }).then(x => {
            if (my !== seq || !box.isConnected) return;
            if (!x.ok || !x.data) { box.innerHTML = failBox(x.message); return; }
            tpl = x.data;
            sel('#exL').innerHTML = '<option value="">— Chọn —</option>' + (tpl.listLocation || []).map(l => `<option value="${esc(l.deliveryLocationId)}">${esc(l.locationName || l.locationCode || '')}</option>`).join('');
            if ((tpl.listLocation || []).length === 1) sel('#exL').value = tpl.listLocation[0].deliveryLocationId;
            const lines = ((tpl.listLocation || [])[0] || {}).listLine || [];
            box.innerHTML = lines.map(ln => { const ops = (ln.listOption || []).length ? ln.listOption : [{ optionNo: 1, dishText: '' }];
              return `<h3 class="mt">${esc(ln.mealName || OD_MEAL[ln.mealType] || ln.mealType || '')} <span class="small muted">(hợp đồng ${esc(tpl.contractNumber || '')} · ${U.money(ln.unitPrice)}/suất)</span></h3>${ops.map(o => `<div class="spread od-opt"><div><b>Lựa chọn ${o.optionNo}</b> <span class="small muted">${esc(o.dishText || '')}</span></div><input type="number" min="0" class="input" style="width:110px;text-align:right" data-exq data-m="${esc(ln.contractMealId)}" data-s="${esc(ln.shiftId || '')}" data-o="${o.optionNo}" data-p="${ln.unitPrice || 0}" value="0"></div>`).join('')}`; }).join('') || '<div class="muted small">Hợp đồng chưa có loại suất.</div>';
            sel('#exCut').innerHTML = tpl.isAfterCutoff ? `<span class="bad">Đã quá giờ chốt (${esc(U.dt(tpl.cutoffAt))}): hệ thống tự gửi Trưởng KD duyệt và báo bếp.</span>` : `Trước ${esc(U.dt(tpl.cutoffAt))}: đơn vào thẳng kế hoạch, không cần duyệt. Sau giờ chốt: hệ thống tự gửi Trưởng KD duyệt và báo bếp.`;
            box.querySelectorAll('[data-exq]').forEach(i => i.oninput = sum);
            warn();
          });
        };
        sel('#exC').onchange = load; sel('#exD').onchange = load; sel('#exL').onchange = warn;
        sel('#exOk').onclick = () => {
          const lines = [...m.querySelectorAll('[data-exq]')].filter(i => +i.value > 0).map(i => ({ contractMealId: i.dataset.m, shiftId: i.dataset.s || null, menuOptionNo: +i.dataset.o, quantity: +i.value }));
          const reason = (m.querySelector('[name=exR]:checked') || {}).value;
          if (!lines.length || !sel('#exL').value) return UI.toast('Chọn điểm giao và nhập số suất', 'err');
          Store.act('Order/CreateExtra', { json: { customerId: sel('#exC').value, deliveryLocationId: sel('#exL').value, mealDate: sel('#exD').value, reason, note: sel('#exN').value.trim() || null, listLine: lines }, tables: ORDER_T }).then(r => {
            if (!r.ok || !r.data || !r.data.order) return;
            close(); bust();
            UI.toast('Đã tạo đơn ' + esc(r.data.order.orderNumber || '') + (r.data.approvalRequested ? ' – đã gửi Trưởng KD duyệt' : r.data.order.status === 'Confirmed' ? ' – đã xác nhận' : ''), r.data.approvalRequested ? 'warn' : 'ok');
            od.day = U.day(r.data.order.mealDate) || od.day; od.data = null; od.key = ''; orderDay(el, f, null);
          });
        };
      } });
  }
  // ---------- Đơn hàng (chế độ DEMO – bảng Kanban cũ; chế độ API dùng orderDay ở trên) ----------
  const os = { view: 'board', date: null };   // date: null = chưa chọn, '' = Tất cả
  APP.register('crm-order', (el, f, params) => {
    if (Store.isLive) { if (params && params.id) setTimeout(() => APP.openDetail('Order', params.id), 50); return orderDay(el, f, params); }
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
  function lockByDay(day0) {
    const def = day0 || U.addDays(Store.ctx.date, 1);
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
  // ---------- Đối soát suất ăn – UI-6.8 (D-86 b, c), mô phỏng docs/frontend-handoff/assets/reconciliation-mockup.html ----------
  // Kỳ 7 ngày / 30 ngày / Tháng + lọc khách · 3 bước · 4 chỉ số · 2 biểu đồ cạnh nhau · bảng xác nhận ngay trên trang (phục vụ mặc định = khách nhận,
  // chỉ sửa dòng có sự cố, Enter xuống dòng cùng cột, lý do lỗi hiện khi > 0) · Xác nhận tất cả dòng khớp / dòng đã chọn · Chốt theo lô.
  const ymd = d => d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
  const rcb = { mode: '7', end: null, cus: '', tab: 'open', day: '', loc: '', data: null, key: '', edits: {}, sel: new Set(), open: new Set() };
  const RC_TAB = [['open', 'Chờ đối soát'], ['VARIANCE', 'Có chênh lệch'], ['CLOSED', 'Đã chốt'], ['all', 'Tất cả']];
  const RC_SRC = { POD: 'Bàn giao', BOT: 'Bot', WEB: 'Web', AUTO: 'Tự động' };
  function rcRange() {
    const end = rcb.end || Store.ctx.date;
    if (rcb.mode === 'M') { const d = new Date(end + 'T00:00'), a = ymd(new Date(d.getFullYear(), d.getMonth(), 1)), b = ymd(new Date(d.getFullYear(), d.getMonth() + 1, 0)); return [a, b]; }
    const n = rcb.mode === '30' ? 30 : 7; return [U.addDays(end, -(n - 1)), end];
  }
  function rcShift(dir) {
    const end = rcb.end || Store.ctx.date;
    if (rcb.mode === 'M') { const d = new Date(end + 'T00:00'); rcb.end = ymd(new Date(d.getFullYear(), d.getMonth() + dir + 1, 0)); }
    else rcb.end = U.addDays(end, dir * (rcb.mode === '30' ? 30 : 7));
  }
  const rcVal = (r, k) => { const e = rcb.edits[r.orderId]; return e && e[k] !== undefined ? e[k] : r[k]; };
  const rcEdited = r => !!rcb.edits[r.orderId] && Object.keys(rcb.edits[r.orderId]).length > 0;
  const rcBilled = r => Math.max(0, (Number(r.accepted) || 0) - (Number(rcVal(r, 'fault')) || 0));
  const rcAmount = r => rcEdited(r) ? Math.round(rcBilled(r) * (Number(r.unitAmount) || 0)) : (Number(r.amount) || 0);
  const rcMoney = v => { v = Number(v) || 0; return v >= 1e9 ? U.num(Math.round(v / 1e7) / 100) + ' tỷ' : v >= 1e6 ? U.num(Math.round(v / 1e5) / 10) + ' tr' : U.num(v); };
  function reconBoard(el, f) {
    const canEdit = Store.perm(f.id).edit !== false;
    const [a, b] = rcRange();
    const cus = Store.rows('Customer').filter(c => c.Status !== 'Inactive').sort((x, y) => String(x.DisplayName || x.Name || '').localeCompare(String(y.DisplayName || y.Name || '')));
    el.innerHTML = APP.header(f, '', { noNote: true }) + `
      <div class="notice info mb">${I('help')}<div>So số suất qua từng khâu Đặt → Sản xuất → Đóng gói → Giao → Khách nhận, chốt số tính tiền cho từng đơn. Đơn giao xong tự có đối soát; khớp số thì tự chốt sau 24 giờ.</div></div>
      <div class="card panel mb"><div class="spread" style="flex-wrap:wrap;gap:10px"><div class="row-flex">${[['7', '7 ngày'], ['30', '30 ngày'], ['M', 'Tháng']].map(([k, l]) => `<button class="chip ${rcb.mode === k ? 'active' : ''}" data-rcm="${k}">${l}</button>`).join('')}<button class="btn sm" id="rcPrev" aria-label="Kỳ trước">◀</button><b>${U.date(a).slice(0, 5)} – ${U.date(b)}</b><button class="btn sm" id="rcNext" aria-label="Kỳ sau">▶</button></div>
        <select class="select" id="rcCus" style="max-width:300px"><option value="">Tất cả khách hàng</option>${cus.map(c => `<option value="${c.Id}" ${c.Id === rcb.cus ? 'selected' : ''}>${esc(c.DisplayName || c.Name || c.Code)}</option>`).join('')}</select></div>
        <div class="fw-steps ow-steps" id="rcSteps"></div><div id="rcKpis"></div></div>
      <div class="card panel mb" id="rcCharts"><div class="muted small">Đang tải…</div></div>
      <div class="card panel"><div class="tabs" id="rcTabs"></div><div id="rcBody"><div class="muted small">Đang tải…</div></div></div>`;
    el.querySelectorAll('[data-rcm]').forEach(c => c.onclick = () => { rcb.mode = c.dataset.rcm; rcb.end = null; rcb.day = ''; reconBoard(el, f); });
    el.querySelector('#rcPrev').onclick = () => { rcShift(-1); rcb.day = ''; reconBoard(el, f); };
    el.querySelector('#rcNext').onclick = () => { rcShift(1); rcb.day = ''; reconBoard(el, f); };
    el.querySelector('#rcCus').onchange = e => { rcb.cus = e.target.value; reconBoard(el, f); };
    const key = [a, b, rcb.cus, Store.ctx.branch || ''].join('|');
    if (rcb.key === key && rcb.data) return rcDraw(el, f, canEdit);
    rcb.key = key; rcb.data = null;
    Store.query('OrderReconciliation/GetBoard', { From: a, To: b, CustomerId: rcb.cus || null, BranchId: Store.ctx.branch }).then(x => {
      if (rcb.key !== key || !el.isConnected) return;
      if (!x.ok || !x.data) { el.querySelector('#rcBody').innerHTML = failBox(x.message); return; }
      rcb.data = x.data; rcb.edits = {}; rcb.sel = new Set(); rcDraw(el, f, canEdit);
    });
  }
  function rcReload(el, f) { rcb.key = ''; bust(); reconBoard(el, f); }
  function rcDraw(el, f, canEdit) {
    const d = rcb.data, rows = d.listRow || [];
    const pend = rows.filter(r => r.tab === 'PENDING' && r.hasPod && r.orderStatus === 'Delivered'), conf = rows.filter(r => r.tab !== 'CLOSED' && r.confirmed && r.reconciliationId);
    el.querySelector('#rcSteps').innerHTML = `
      <div class="fw-step"><span class="n s1">1</span><div><b>Xác nhận số</b><span class="small muted">Khớp số: 1 nút cho tất cả · lệch: sửa trực tiếp trên dòng</span></div><button class="btn sm primary" id="rcStep1" ${!canEdit || !pend.length ? 'disabled' : ''}>${I('check')} Xác nhận ${pend.length} đơn khớp</button></div>
      <div class="fw-step"><span class="n s2">2</span><div><b>Xử lý chênh lệch</b><span class="small muted">Đơn Nhận ≠ Đặt hoặc có lỗi Delicare</span></div><button class="btn sm" id="rcStep2">${U.num(d.varianceCount)} đơn có chênh lệch</button></div>
      <div class="fw-step"><span class="n s3">3</span><div><b>Chốt đối soát</b><span class="small muted">Khớp số → hệ thống tự chốt sau 24 giờ; còn lại kế toán chốt</span></div><div class="row-flex" style="grid-column:1/-1"><button class="btn sm orange" id="rcStep3" ${!canEdit || !conf.length ? 'disabled' : ''}>${I('approve')} Chốt ${conf.length} đơn đã xác nhận</button><button class="btn sm" id="rcBill">${I('file')} Bảng kê tháng</button></div></div>`;
    el.querySelector('#rcKpis').innerHTML = UI.kpis([
      UI.kpi('Suất đặt', U.num(d.ordered), U.num((d.listRow || []).length) + ' đơn', 'utensils', 'green'),
      UI.kpi('Suất khách nhận', U.num(d.accepted), d.acceptedPct != null ? U.num(d.acceptedPct) + '% so đặt' : '', 'check', 'blue'),
      UI.kpi('Giá trị đã chốt', rcMoney(d.closedAmount), U.num(d.closedCount) + ' đơn', 'calc', 'green'),
      UI.kpi('Chờ / chênh lệch', U.num(d.pendingCount) + ' / ' + U.num(d.varianceCount), 'đơn', 'alert', d.varianceCount ? 'orange' : 'green')], 'compact');
    el.querySelector('#rcStep1').onclick = () => rcConfirm(el, f, pend.map(r => ({ orderId: r.orderId })), `Xác nhận <b>${pend.length}</b> đơn khớp số (nhận = đặt)? Phục vụ ghi bằng số khách nhận.`);
    el.querySelector('#rcStep2').onclick = () => { rcb.tab = 'VARIANCE'; rcTable(el, f, canEdit); el.querySelector('#rcBody').scrollIntoView({ behavior: 'smooth', block: 'start' }); };
    el.querySelector('#rcStep3').onclick = () => UI.confirm(`Chốt <b>${conf.length}</b> đối soát đã xác nhận? Sau khi chốt không sửa được số liệu; đơn → Đã đối soát.`, note => Store.act('OrderReconciliation/CloseMany', { json: { listReconciliationId: conf.map(r => r.reconciliationId), remark: note || null }, tables: ['OrderReconciliation', 'Order'] }).then(x => { if (!x.ok) return; rcResult(x); rcReload(el, f); }), { input: 'Ghi chú (bắt buộc khi chênh lệch vượt ngưỡng)', ok: 'Chốt đối soát', title: 'Chốt đối soát' });
    el.querySelector('#rcBill').onclick = () => APP.go('crm-billing');
    // Biểu đồ: trái Đặt – Giao – Nhận theo ngày; phải chuỗi 5 khâu + 4 ô hao hụt giữa khâu.
    const days = (d.listDay || []), lbl = days.map(x => U.date(U.day(x.date)).slice(0, 5)), ord = days.map(x => x.ordered), acc = days.map(x => x.accepted);
    const CH = ['Đặt', 'Sản xuất', 'Đóng gói', 'Giao', 'Nhận'], tot = d.listChain || [0, 0, 0, 0, 0];
    const left = UI.combo(lbl, [{ name: 'Đặt', type: 'bar', role: 'order', values: ord }, { name: 'Giao', type: 'line', role: 'deliver', values: days.map(x => x.delivered) }, { name: 'Nhận', type: 'line', role: 'receive', dashed: true, values: acc }],
      { title: 'Đặt – Giao – Nhận theo ngày ăn', unit: 'suất', h: 240, tipExtra: i => { const g = acc[i] - ord[i]; return 'Chênh Nhận − Đặt: ' + (g > 0 ? '+' : '') + U.num(g) + (ord[i] ? ' (' + U.num(g / ord[i] * 100, 1) + '%)' : ''); } });
    const right = UI.hbars(CH, [{ name: 'Suất cả kỳ', values: tot, color: UI.CHART_COLORS.deliver }], { title: 'Chuỗi Đặt → Sản xuất → Đóng gói → Giao → Nhận', unit: 'suất', h: 200, labelHead: 'Khâu', tipExtra: i => i > 0 && tot[i - 1] ? 'So với ' + CH[i - 1] + ': ' + U.num(tot[i] / tot[i - 1] * 100, 1) + '%' : '' });
    const loss = [1, 2, 3, 4].map(i => { const v = tot[i] - tot[i - 1]; return `<div class="rc-loss"><span class="small muted">${CH[i - 1]} → ${CH[i]}</span><b class="${v < 0 ? 'bad' : ''}">${v > 0 ? '+' : ''}${U.num(v)}</b><span class="small muted">${tot[i - 1] ? U.num(v / tot[i - 1] * 100, 1) + '%' : '–'}</span></div>`; }).join('');
    el.querySelector('#rcCharts').innerHTML = rows.length ? `<div class="dc-chart-pair"><div>${left}</div><div>${right}<div class="rc-losses">${loss}</div></div></div>` : '<div class="empty">Chưa có đơn đã giao trong kỳ.</div>';
    rcTable(el, f, canEdit);
  }
  function rcRows() {
    const rows = (rcb.data.listRow || []).filter(r => rcb.tab === 'all' ? true : rcb.tab === 'open' ? r.tab !== 'CLOSED' : r.tab === rcb.tab);
    return rows.filter(r => (!rcb.day || U.day(r.mealDate) === rcb.day) && (!rcb.loc || r.locationName === rcb.loc));
  }
  function rcTable(el, f, canEdit) {
    const all = rcb.data.listRow || [], cnt = k => all.filter(r => k === 'all' ? true : k === 'open' ? r.tab !== 'CLOSED' : r.tab === k).length;
    el.querySelector('#rcTabs').innerHTML = RC_TAB.map(([k, l]) => `<button class="tab ${rcb.tab === k ? 'active' : ''}" data-rct="${k}">${l} (${cnt(k)})</button>`).join('');
    el.querySelectorAll('[data-rct]').forEach(t => t.onclick = () => { rcb.tab = t.dataset.rct; rcTable(el, f, canEdit); });
    const rows = rcRows(), days = [...new Set(all.map(r => U.day(r.mealDate)))].sort(), locs = [...new Set(all.map(r => r.locationName).filter(Boolean))].sort();
    const matched = rows.filter(r => r.tab === 'PENDING' && r.hasPod && r.orderStatus === 'Delivered' && !rcEdited(r));
    const reasons = rcb.data.listFaultReason || [];
    const edit = r => canEdit && r.tab !== 'CLOSED' && r.orderStatus === 'Delivered';
    const inp = (r, k, extra) => edit(r) ? `<input class="rc-in ${rcVal(r, k) == null || (k === 'served' && !rcEdited(r) && r.served == null) ? 'pre' : ''}" data-rcin="${r.orderId}|${k}" inputmode="decimal" value="${rcVal(r, k) != null ? rcVal(r, k) : k === 'served' ? r.accepted : ''}" ${extra || ''}>` : `<span>${rcVal(r, k) != null ? U.num(rcVal(r, k)) : '–'}</span>`;
    const body = rows.map(r => {
      const fault = Number(rcVal(r, 'fault')) || 0, lines = r.listLine || [], opened = rcb.open.has(r.orderId);
      const tr = `<tr class="${rcEdited(r) ? 'chg' : r.tab === 'CONFIRMED' ? 'ok' : ''}" data-rcr="${r.orderId}">
        <td>${edit(r) ? `<input type="checkbox" data-rcs="${r.orderId}" ${rcb.sel.has(r.orderId) ? 'checked' : ''} aria-label="Chọn dòng">` : ''}</td>
        <td>${lines.length ? `<span class="rc-chev" data-rco="${r.orderId}">${opened ? '▾' : '▸'}</span> ` : ''}<a href="javascript:void 0" data-rcd="${r.orderId}">${esc(r.orderNumber || '')}</a><div class="small muted">${U.date(U.day(r.mealDate)).slice(0, 5)}</div></td>
        <td>${esc(r.customerName || '')}<div class="small muted">${esc(r.locationName || '')}</div></td>
        <td class="num">${U.num(r.ordered)}</td>
        <td class="num ${Number(r.accepted) !== Number(r.ordered) ? 'bad' : ''}"><b>${U.num(r.accepted)}</b></td>
        <td class="num">${lines.length ? `<span>${U.num(rcVal(r, 'served') != null ? rcVal(r, 'served') : r.accepted)}</span>` : inp(r, 'served')}</td>
        <td class="num">${inp(r, 'returned')}</td>
        <td class="num">${inp(r, 'fault')}</td>
        <td>${fault > 0 ? (edit(r) ? `<select class="rc-sel" data-rcin="${r.orderId}|faultReason"><option value="">— Lý do —</option>${reasons.map(x => `<option value="${x.code}" ${rcVal(r, 'faultReason') === x.code ? 'selected' : ''}>${esc(x.name)}</option>`).join('')}</select>` : esc((reasons.find(x => x.code === r.faultReason) || {}).name || r.faultReason || '')) : '<span class="muted">–</span>'}</td>
        <td class="num"><b>${U.num(rcBilled(r))}</b></td>
        <td class="num">${U.num(rcAmount(r), 0)}</td>
        <td><span class="small">${esc(RC_SRC[r.source] || r.source)}</span>${r.tab === 'CLOSED' ? ' <span class="badge gray">Đã chốt</span>' : r.tab === 'CONFIRMED' ? ' <span class="badge green">Đã xác nhận</span>' : ''}</td>
        <td class="actions">${r.reconciliationId && r.tab === 'CLOSED' ? `<button class="btn xs" data-rcp="${r.reconciliationId}" title="In biên bản đối soát">${I('print')}</button>` : ''}</td></tr>`;
      const sub = lines.length && opened ? lines.map(l => `<tr class="opt"><td></td><td colspan="2">${esc(MEAL_VI2(l.mealType))}${l.menuOptionNo ? ' · Lựa chọn ' + l.menuOptionNo : ''}</td><td class="num">${U.num(l.quantity)}</td><td></td><td class="num">${edit(r) ? `<input class="rc-in" data-rcl="${r.orderId}|${l.orderLineId}" inputmode="decimal" value="${rcLine(r, l)}">` : U.num(rcLine(r, l))}</td><td colspan="7" class="small muted">${U.money(l.unitPrice)}/suất</td></tr>`).join('') : '';
      return tr + sub;
    }).join('');
    el.querySelector('#rcBody').innerHTML = `<div class="notice info small mb">${I('help')}<div>Mặc định <b style="display:inline">Phục vụ = Khách nhận</b>, không cần nhập. Chỉ sửa dòng có trả về / lỗi Delicare / phục vụ khác. Enter = xuống dòng dưới cùng cột.</div></div>
      <div class="row-flex mb" style="flex-wrap:wrap"><select class="select" id="rcDay" style="max-width:170px"><option value="">Mọi ngày ăn</option>${days.map(x => `<option value="${x}" ${x === rcb.day ? 'selected' : ''}>Ngày ăn ${U.date(x).slice(0, 5)}</option>`).join('')}</select><select class="select" id="rcLoc" style="max-width:240px"><option value="">Mọi điểm giao</option>${locs.map(x => `<option ${x === rcb.loc ? 'selected' : ''}>${esc(x)}</option>`).join('')}</select>
        ${canEdit ? `<button class="btn sm" id="rcSel">${I('check')} Xác nhận dòng đã chọn (<span id="rcSelN">${rcb.sel.size}</span>)</button><button class="btn sm primary" id="rcAll" ${matched.length ? '' : 'disabled'}>${I('check')} Xác nhận tất cả dòng khớp (${matched.length})</button>` : ''}</div>
      ${rows.length ? `<div class="table-wrap"><table class="table dc-table rc-grid"><thead><tr><th style="width:28px">${canEdit ? '<input type="checkbox" id="rcSelAll" aria-label="Chọn tất cả">' : ''}</th><th>Đơn</th><th>Khách / điểm giao</th><th class="num">Đặt</th><th class="num">Khách nhận</th><th class="num">Phục vụ</th><th class="num">Trả về</th><th class="num">Lỗi Delicare</th><th>Lý do</th><th class="num">Tính tiền</th><th class="num">Thành tiền</th><th>Nguồn</th><th></th></tr></thead><tbody>${body}</tbody></table></div>` : '<div class="empty">Không có đơn trong nhóm này.</div>'}
      <div class="small muted mt"><span class="rc-sw"></span> dòng đã sửa · Tiền = (Khách nhận − Lỗi Delicare) × đơn giá cố định lúc chốt đơn · Phục vụ chỉ để dự báo · Điểm phát bấm "Đã phục vụ đủ" trên bot sau bữa → dòng tự xác nhận (nguồn Bot).</div>`;
    const box = el.querySelector('#rcBody');
    box.querySelector('#rcDay').onchange = e => { rcb.day = e.target.value; rcTable(el, f, canEdit); };
    box.querySelector('#rcLoc').onchange = e => { rcb.loc = e.target.value; rcTable(el, f, canEdit); };
    box.querySelectorAll('[data-rcd]').forEach(a => a.onclick = () => APP.openDetail('Order', a.dataset.rcd));
    box.querySelectorAll('[data-rcp]').forEach(b => b.onclick = () => Store.download('OrderReconciliation/Print', { id: b.dataset.rcp }));
    box.querySelectorAll('[data-rco]').forEach(s => s.onclick = () => { const id = s.dataset.rco; rcb.open.has(id) ? rcb.open.delete(id) : rcb.open.add(id); rcTable(el, f, canEdit); });
    if (!canEdit) return;
    const byId = id => all.find(r => r.orderId === id);
    const mark = id => { rcb.sel.add(id); };
    box.querySelectorAll('[data-rcs]').forEach(c => c.onchange = () => { c.checked ? rcb.sel.add(c.dataset.rcs) : rcb.sel.delete(c.dataset.rcs); box.querySelector('#rcSelN').textContent = rcb.sel.size; });
    const sa = box.querySelector('#rcSelAll'); if (sa) sa.onchange = () => { box.querySelectorAll('[data-rcs]').forEach(c => { c.checked = sa.checked; sa.checked ? rcb.sel.add(c.dataset.rcs) : rcb.sel.delete(c.dataset.rcs); }); box.querySelector('#rcSelN').textContent = rcb.sel.size; };
    box.querySelectorAll('[data-rcin]').forEach(x => {
      const [id, k] = x.dataset.rcin.split('|');
      const apply = () => {
        const r = byId(id); if (!r) return;
        const v = k === 'faultReason' ? x.value : (x.value.trim() === '' ? null : Number(x.value.replace(',', '.')));
        const base = k === 'served' ? (r.served != null ? r.served : r.accepted) : r[k];
        const e = rcb.edits[id] || (rcb.edits[id] = {});
        if ((v ?? null) === (base ?? null)) delete e[k]; else e[k] = v;
        if (!Object.keys(e).length) delete rcb.edits[id];
        if (rcEdited(r)) mark(id);
      };
      x.onchange = () => { apply(); rcTable(el, f, canEdit); };
      x.onkeydown = ev => {
        if (ev.key !== 'Enter') return; ev.preventDefault(); apply();
        const list = [...box.querySelectorAll(`[data-rcin$="|${k}"]`)], i = list.indexOf(x), next = list[i + 1];
        const nid = next && next.dataset.rcin; rcTable(el, f, canEdit);
        if (nid) { const n = el.querySelector(`[data-rcin="${nid}"]`); if (n) { n.focus(); n.select && n.select(); } }
      };
    });
    box.querySelectorAll('[data-rcl]').forEach(x => x.onchange = () => {
      const [id, lid] = x.dataset.rcl.split('|'), r = byId(id), e = rcb.edits[id] || (rcb.edits[id] = {});
      e.lines = Object.assign({}, e.lines, { [lid]: Number(x.value) || 0 });
      e.served = (r.listLine || []).reduce((s, l) => s + rcLine(r, l), 0); mark(id); rcTable(el, f, canEdit);
    });
    const item = r => {
      const e = rcb.edits[r.orderId] || {}, o = { orderId: r.orderId };
      if (e.served !== undefined) o.servedQuantity = e.served;
      const ret = rcVal(r, 'returned'), fault = rcVal(r, 'fault');
      if (ret != null) o.returnedQuantity = ret;
      if (fault != null) { o.delicareFaultQuantity = fault; o.faultReason = rcVal(r, 'faultReason') || null; }
      if (r.listLine && r.listLine.length && (e.lines || r.listLine.some(l => l.served != null))) o.listLine = r.listLine.map(l => ({ orderLineId: l.orderLineId, servedQuantity: rcLine(r, l) }));
      return o;
    };
    box.querySelector('#rcSel').onclick = () => {
      const list = [...rcb.sel].map(byId).filter(r => r && edit(r));
      if (!list.length) return UI.toast('Chưa chọn dòng nào', 'err');
      const bad = list.find(r => (Number(rcVal(r, 'fault')) || 0) > 0 && !rcVal(r, 'faultReason'));
      if (bad) return UI.toast('Đơn ' + esc(bad.orderNumber) + ': chọn lý do lỗi Delicare', 'err');
      rcConfirm(el, f, list.map(item), `Xác nhận <b>${list.length}</b> dòng đã chọn?`);
    };
    box.querySelector('#rcAll').onclick = () => rcConfirm(el, f, matched.map(r => ({ orderId: r.orderId })), `Xác nhận <b>${matched.length}</b> dòng khớp (nhận = đặt, không sửa)? Phục vụ ghi bằng số khách nhận.`);
  }
  const MEAL_VI2 = t => ({ SANG: 'Sáng', TRUA: 'Trưa', TOI: 'Tối', DEM: 'Đêm' })[String(t || '').toUpperCase()] || t || '';
  function rcLine(r, l) { const e = rcb.edits[r.orderId]; return e && e.lines && e.lines[l.orderLineId] !== undefined ? e.lines[l.orderLineId] : l.served != null ? Number(l.served) : Number(l.quantity); }
  function rcResult(x) {
    const d = x.data || {};
    UI.toast(esc((x.res && x.res.responseMessage) || 'Đã lưu'), d.failed ? 'warn' : 'ok');
    if ((d.listError || []).length) {
      const no = id => ((rcb.data.listRow || []).find(r => r.orderId === id || r.reconciliationId === id) || {}).orderNumber || id;
      UI.modal({ title: 'Dòng chưa lưu được', body: `<div class="small">${d.listError.map(e => `<div class="mb"><b>${esc(no(e.id))}</b>: ${esc(e.message || '')}</div>`).join('')}</div>`, foot: '<button class="btn primary" data-close>Đóng</button>' });
    }
  }
  function rcConfirm(el, f, items, msg) {
    if (!items.length) return UI.toast('Chưa chọn dòng nào', 'err');
    UI.confirm(msg, () => Store.act('OrderReconciliation/ReconcileMany', { json: { listItem: items }, tables: ['OrderReconciliation', 'Order'] }).then(x => {
      if (!x.ok) return; rcResult(x); rcb.sel = new Set(); rcReload(el, f);
    }), { ok: 'Xác nhận', title: 'Xác nhận đối soát' });
  }
  APP.register('crm-reconciliation', (el, f) => {
    if (Store.isLive) return reconBoard(el, f);

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
      td.insertAdjacentHTML('afterbegin', `<button class="btn xs" data-prt title="In biên bản đối soát (PDF)">${I('export')} In</button> ${r.Status !== 'Closed' && canEdit ? '<button class="btn xs" data-rec title="Ghi lại suất thực tế phục vụ">Ghi lại</button> ' : ''}${r.Status !== 'Closed' ? '<button class="btn xs orange" data-cls>Chốt</button> ' : ''}${r.Status === 'Closed' && canEdit ? '<button class="btn xs" data-reo title="Gửi yêu cầu mở lại đối soát – COO duyệt">Mở lại</button> ' : ''}`);
      // ACC-3 (D-131 g): mở lại đối soát đã chốt – lý do bắt buộc, COO duyệt (Phê duyệt › Mở lại đối soát).
      const reo = td.querySelector('[data-reo]');
      if (reo) reo.onclick = () => UI.modal({ title: 'Mở lại đối soát ' + esc(Store.label('Order', r.OrderId)), body: '<div class="small muted mb">Đối soát về Khớp / Chênh lệch, đơn về Đã giao sau khi COO duyệt. Kỳ kế toán đã khóa thì không mở được – lập chứng từ điều chỉnh.</div><textarea class="input" id="rcRe" rows="3" placeholder="Lý do mở lại (bắt buộc)…"></textarea>',
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="rcReOk">Gửi duyệt</button>',
        onMount: (m, close) => m.querySelector('#rcReOk').onclick = () => { const why = m.querySelector('#rcRe').value.trim(); if (!why) { UI.toast('Nhập lý do mở lại', 'warn'); return; }
          Store.act('OrderReconciliation/RequestReopen', { form: { Id: r.Id, Reason: why } }).then(x => { if (x.ok) { close(); UI.toast(esc((x.res && x.res.responseMessage) || 'Đã gửi duyệt'), 'ok'); } }); } });
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
  // ---------- Bảng kê thanh toán – UI-6.9 (D-87), mô phỏng docs/frontend-handoff/assets/billing-mockup.html ----------
  // Tháng ◀ ▶ (mặc định tháng trước) · tự tính mọi khách khi mở (bỏ "Lập bảng kê") · 3 bước · 4 chỉ số · tab Theo khách hàng / Chờ đối soát / Lịch sử xuất ·
  // Excel / PDF trên từng dòng · chọn nhiều → Excel 1 tệp nhiều sheet / PDF tệp nén · "Gửi khách xác nhận" chưa nâng cấp (D-87 c).
  const bkm = { month: '', tab: 'cus', data: null, key: '', sel: new Set(), open: new Set(), detail: {}, hist: null };
  const BK_ST = { READY: ['green', 'Sẵn sàng'], PENDING: ['orange', 'Chờ đối soát'], NONE: ['gray', 'Không phát sinh'] };
  const bkMonth = () => { if (!bkm.month) { const d = new Date(Store.ctx.date + 'T00:00'); d.setDate(1); d.setMonth(d.getMonth() - 1); bkm.month = d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-01'; } return bkm.month; };
  const bkShift = n => { const d = new Date(bkMonth() + 'T00:00'); d.setMonth(d.getMonth() + n); bkm.month = d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-01'; };
  const bkShort = v => { v = Number(v) || 0; return v >= 1e9 ? U.num(Math.round(v / 1e7) / 100) + ' tỷ' : v >= 1e6 ? U.num(Math.round(v / 1e5) / 10) + ' tr' : U.num(v); };
  const bkNotYet = () => UI.toast('Tính năng chưa được nâng cấp. Vui lòng liên hệ quản trị viên.', 'warn');
  function bkExport(ids, fmt) {
    if (!ids.length) return UI.toast('Chưa chọn khách hàng nào', 'err');
    const one = ids.length === 1, m = bkMonth(), to = U.addDays(U.addDays(m, 32).slice(0, 8) + '01', -1);
    const p = one ? Store.download(fmt === 'pdf' ? 'BillingStatement/PrintStatement' : 'BillingStatement/ExportStatement', { CustomerId: ids[0], From: m, To: to })
      : Store.download(fmt === 'pdf' ? 'BillingStatement/PrintStatementBatch' : 'BillingStatement/ExportStatementBatch', null, { month: m, listCustomerId: ids });
    return p.then(r => { if (r && r.ok) { UI.toast('Đã tải ' + esc(r.fileName || ''), 'ok'); bkm.hist = null; } });
  }
  function bkBilling(el, f) {
    const perm = Store.perm(f.id), m = bkMonth(), d0 = new Date(m + 'T00:00');
    el.innerHTML = APP.header(f, '', { noNote: true }) + `
      <div class="notice info mb">${I('help')}<div>Tổng hợp tiền phải thu từng khách trong tháng (suất tính tiền đã chốt đối soát × đơn giá + phí thất thoát khay + VAT) để gửi khách xác nhận, rồi xuất hóa đơn ở phần mềm kế toán. Hệ thống tự lập cho mọi khách – không cần bấm "Lập".</div></div>
      <div class="card panel mb"><div class="fw-week"><button class="btn sm" id="bkPrev" aria-label="Tháng trước">◀</button><span>Tháng ${String(d0.getMonth() + 1).padStart(2, '0')}/${d0.getFullYear()}</span><button class="btn sm" id="bkNext" aria-label="Tháng sau">▶</button></div>
        <div class="fw-steps ow-steps" id="bkSteps"></div><div id="bkKpis"></div></div>
      <div class="card panel"><div class="tabs" id="bkTabs"></div><div id="bkBody"><div class="muted small">Đang tính bảng kê mọi khách…</div></div></div>
      <div class="bk-bar" id="bkBar" hidden></div>`;
    el.querySelector('#bkPrev').onclick = () => { bkShift(-1); bkm.sel = new Set(); bkBilling(el, f); };
    el.querySelector('#bkNext').onclick = () => { bkShift(1); bkm.sel = new Set(); bkBilling(el, f); };
    const key = m + '|' + (Store.ctx.branch || '');
    if (bkm.key === key && bkm.data) return bkDraw(el, f, perm);
    bkm.key = key; bkm.data = null; bkm.detail = {};
    Store.query('BillingStatement/GetMonthOverview', { Month: m, BranchId: Store.ctx.branch }).then(x => {
      if (bkm.key !== key || !el.isConnected) return;
      if (!x.ok || !x.data) { el.querySelector('#bkBody').innerHTML = failBox(x.message); return; }
      bkm.data = x.data; bkDraw(el, f, perm);
    });
  }
  function bkDraw(el, f, perm) {
    const d = bkm.data, ready = d.listCustomer.filter(c => c.status === 'READY');
    el.querySelector('#bkSteps').innerHTML = `
      <div class="fw-step"><span class="n s1">1</span><div><b>Chốt hết đối soát của tháng</b><span class="small muted">Khách còn đơn chờ đối soát thì chưa sẵn sàng</span></div><button class="btn sm" id="bkS1" ${d.pendingOrders ? '' : 'disabled'}>${U.num(d.pendingOrders)} đơn chờ đối soát (${U.num(d.pendingCustomers)} khách)</button></div>
      <div class="fw-step"><span class="n s2">2</span><div><b>Kiểm tra</b><span class="small muted">Bấm dòng để xem chi tiết; ⚠ = lệch &gt; ${U.num(d.deviationPct)}% so tháng trước</span></div></div>
      <div class="fw-step"><span class="n s3">3</span><div><b>Xuất / in hàng loạt</b><span class="small muted">Chọn nhiều khách hoặc tất cả khách sẵn sàng</span></div><button class="btn sm primary" id="bkS3" ${ready.length && perm.exp !== false ? '' : 'disabled'}>${I('export')} Xuất tất cả khách sẵn sàng (${ready.length})</button></div>
      <div class="fw-step"><span class="n s3">4</span><div><b>Khách xác nhận</b><span class="small muted">Gửi khách (email – chưa nâng cấp) hoặc xuất gửi tay, rồi chọn khách → Ghi nhận đã xác nhận</span><span class="small"><b>${U.num(d.listCustomer.filter(c => ['CONFIRMED', 'INVOICED'].includes(c.confirmState)).length)}</b> / ${U.num(ready.length)} khách đã xác nhận</span></div></div>`;
    el.querySelector('#bkKpis').innerHTML = UI.kpis([
      UI.kpi('Tổng phải thu (có VAT)', bkShort(d.grandTotal), '', 'calc', 'green'),
      UI.kpi('Khách sẵn sàng', U.num(d.readyCount), '/ ' + U.num(d.activeCount) + ' khách', 'check', d.readyCount === d.activeCount ? 'green' : 'orange'),
      UI.kpi('Đơn chờ đối soát', U.num(d.pendingOrders), U.num(d.pendingCustomers) + ' khách', 'alert', d.pendingOrders ? 'orange' : 'green'),
      UI.kpi('Phí thất thoát khay', bkShort(d.containerFee), 'đã gồm trong tổng', 'package', 'blue')], 'compact');
    el.querySelector('#bkS1').onclick = () => { bkm.tab = 'pend'; bkTab(el, f, perm); };
    el.querySelector('#bkS3').onclick = () => bkExport(ready.map(c => c.customerId), 'xlsx');
    bkTab(el, f, perm);
  }
  function bkTab(el, f, perm) {
    const d = bkm.data;
    el.querySelector('#bkTabs').innerHTML = [['cus', 'Theo khách hàng'], ['pend', `Chờ đối soát (${U.num(d.pendingOrders)})`], ['hist', 'Lịch sử xuất']].map(([k, l]) => `<button class="tab ${bkm.tab === k ? 'active' : ''}" data-bkt="${k}">${l}</button>`).join('');
    el.querySelectorAll('[data-bkt]').forEach(t => t.onclick = () => { bkm.tab = t.dataset.bkt; bkTab(el, f, perm); });
    const box = el.querySelector('#bkBody');
    el.querySelector('#bkBar').hidden = bkm.tab !== 'cus' || !bkm.sel.size;
    if (bkm.tab === 'pend') {
      box.innerHTML = d.listPendingGroup.length ? d.listPendingGroup.map(g => `<div class="mb"><div class="spread"><b>${esc(g.customerName || '')}</b><span class="small muted">${g.listOrder.length} đơn</span></div><div class="table-wrap"><table class="table dc-table"><thead><tr><th>Đơn</th><th>Ngày ăn</th><th>Trạng thái</th><th class="num">Suất đặt</th></tr></thead><tbody>${g.listOrder.map(o => `<tr><td><a href="javascript:void 0" data-bko="${o.orderId}">${esc(o.orderNumber || '')}</a></td><td>${U.date(U.day(o.mealDate))}</td><td>${esc(o.statusName || '')}</td><td class="num">${U.num(o.orderedQuantity)}</td></tr>`).join('')}</tbody></table></div></div>`).join('') + `<button class="btn primary" id="bkGoRc">${I('check')} Mở màn Đối soát để xác nhận / chốt</button>` : '<div class="empty">Tháng này không còn đơn chờ đối soát.</div>';
      box.querySelectorAll('[data-bko]').forEach(a => a.onclick = () => APP.openDetail('Order', a.dataset.bko));
      const g = box.querySelector('#bkGoRc'); if (g) g.onclick = () => APP.go('crm-reconciliation');
      return;
    }
    if (bkm.tab === 'hist') {
      box.innerHTML = '<div class="muted small">Đang tải…</div>';
      const load = bkm.hist ? Promise.resolve(bkm.hist) : Store.query('BillingStatement/GetExportHistory', { Month: bkMonth() }).then(x => (bkm.hist = x.ok ? x.list || [] : []));
      load.then(list => { if (!box.isConnected || bkm.tab !== 'hist') return; box.innerHTML = list.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Lúc</th><th>Người</th><th>Khách hàng</th><th>Thao tác</th><th>Chi tiết</th></tr></thead><tbody>${list.map(h => `<tr><td>${U.dt(h.at)}</td><td>${esc(h.byName || '')}</td><td>${esc(h.customerName || '')}</td><td>${esc(h.action || '')}</td><td class="small muted">${esc(h.detail || '')}</td></tr>`).join('')}</tbody></table></div>` : '<div class="empty">Chưa có lần xuất / in nào trong tháng.</div>'; });
      return;
    }
    const rows = d.listCustomer, sum = k => rows.reduce((s, r) => s + (Number(r[k]) || 0), 0);
    const can = { exp: perm.exp !== false, prt: perm.print !== false };
    box.innerHTML = `<div class="fw-legend">${Object.entries(BK_ST).map(([k, [c, l]]) => `<span><span class="badge ${c}">${l}</span> ${k === 'READY' ? 'đã chốt hết đối soát trong tháng' : k === 'PENDING' ? 'còn đơn chưa chốt – số tiền tạm tính' : ''}</span>`).join('')}<span>Biểu tượng Excel · PDF trên từng dòng – không cần lập trước</span></div>
      <div class="table-wrap"><table class="table dc-table bk-grid"><thead><tr><th style="width:28px"><input type="checkbox" id="bkAll" aria-label="Chọn tất cả"></th><th>Khách hàng</th><th class="num">Suất tính tiền</th><th class="num">Tiền suất ăn</th><th class="num">Phí khay</th><th class="num">VAT</th><th class="num">Tổng thanh toán</th><th>Trạng thái</th><th></th></tr></thead><tbody>
      ${rows.map(r => { const [c, l] = BK_ST[r.status] || ['gray', r.status], open = bkm.open.has(r.customerId);
        return `<tr class="bk-row ${open ? 'open' : ''}" data-bkr="${r.customerId}"><td><input type="checkbox" data-bks="${r.customerId}" ${bkm.sel.has(r.customerId) ? 'checked' : ''} ${r.status === 'NONE' ? 'disabled' : ''} aria-label="Chọn khách"></td>
          <td><b>${esc(r.customerName || '')}</b><div class="small muted">${esc(r.customerCode || '')}${r.contractNumber ? ' · ' + esc(r.contractNumber) : ''}</div></td>
          <td class="num">${U.num(r.billedQuantity)}</td><td class="num">${U.num(r.mealAmount, 0)}${r.warn ? ` <span class="ow-warn" title="Lệch ${U.num(r.changePct)}% so tháng trước (${U.num(r.previousMealAmount, 0)})">⚠</span>` : ''}</td>
          <td class="num">${r.containerFee ? U.num(r.containerFee, 0) : '–'}</td><td class="num">${U.num(r.vatAmount, 0)}</td><td class="num"><b>${U.num(r.grandTotal, 0)}</b></td>
          <td><span class="badge ${c}">${l}${r.status === 'PENDING' ? ' · ' + r.pendingOrders + ' đơn' : ''}</span>${r.status !== 'NONE' ? `<div class="small ${r.confirmState === 'DISPUTED' ? 'bad' : ['CONFIRMED', 'INVOICED'].includes(r.confirmState) ? 'good' : 'muted'}" title="${esc(r.confirmNote || '')}">${esc(r.confirmStateName || '')}${r.confirmAt ? ' · ' + U.date(r.confirmAt) : ''}</div>` : ''}</td>
          <td class="actions">${r.status !== 'NONE' ? `${can.exp ? `<button class="btn xs" data-bkx="${r.customerId}" title="Xuất Excel" aria-label="Xuất Excel">XLS</button>` : ''} ${can.prt ? `<button class="btn xs" data-bkp="${r.customerId}" title="In PDF" aria-label="In PDF">PDF</button>` : ''} <button class="btn xs" data-bkg title="Gửi khách xác nhận" aria-label="Gửi khách xác nhận">${I('mail')}</button>` : ''}</td></tr>`
          + (open ? `<tr class="bk-det"><td></td><td colspan="8" data-bkdet="${r.customerId}"><div class="muted small">Đang tải chi tiết…</div></td></tr>` : ''); }).join('')}
      </tbody><tfoot><tr class="fw-foot"><td></td><td><b>Tổng</b></td><td class="num">${U.num(sum('billedQuantity'))}</td><td class="num">${U.num(sum('mealAmount'), 0)}</td><td class="num">${U.num(sum('containerFee'), 0)}</td><td class="num">${U.num(sum('vatAmount'), 0)}</td><td class="num"><b>${U.num(sum('grandTotal'), 0)}</b></td><td colspan="2"></td></tr></tfoot></table></div>`;
    const bar = el.querySelector('#bkBar');
    const drawBar = () => {
      const sel = rows.filter(r => bkm.sel.has(r.customerId)), pend = sel.filter(r => r.status === 'PENDING').length;
      bar.hidden = !sel.length;
      bar.innerHTML = `<span>Đã chọn <b>${sel.length}</b> khách · <b>${U.money(sel.reduce((s, r) => s + (Number(r.grandTotal) || 0), 0))}</b>${pend ? ` <span class="bad small">· ${pend} khách còn chờ đối soát (số tạm tính)</span>` : ''}</span><span class="row-flex">${can.exp ? `<button class="btn sm" id="bkBx">${I('export')} Excel (1 tệp, mỗi khách 1 sheet)</button>` : ''}${can.prt ? `<button class="btn sm" id="bkBp">${I('print')} PDF (tệp nén, mỗi khách 1 tệp)</button>` : ''}<button class="btn sm" id="bkBg">${I('mail')} Gửi khách xác nhận</button>${perm.edit !== false ? `<button class="btn sm primary" id="bkBk">${I('check')} Ghi nhận khách đã xác nhận</button><button class="btn sm" id="bkBd">Khách phản hồi lệch</button>` : ''}<button class="btn sm" id="bkBc">Bỏ chọn</button></span>`;
      const ids = sel.map(r => r.customerId);
      if (bar.querySelector('#bkBx')) bar.querySelector('#bkBx').onclick = () => bkExport(ids, 'xlsx');
      if (bar.querySelector('#bkBp')) bar.querySelector('#bkBp').onclick = () => bkExport(ids, 'pdf');
      bar.querySelector('#bkBg').onclick = () => Store.act('BillingStatement/SendToCustomers', { json: { month: bkMonth(), listCustomerId: ids } });
      // ACC-3 (D-131 f): ghi nhận hàng loạt khách đã xác nhận → phiếu bán tháng Sẵn sàng ở Kế toán › Chứng từ kế toán.
      const bk = bar.querySelector('#bkBk');
      if (bk) bk.onclick = () => UI.confirm(`Ghi nhận <b>${ids.length}</b> khách đã xác nhận bảng kê tháng? Chỉ khách Sẵn sàng (đã chốt hết đối soát) được ghi nhận.`, note =>
        Store.act('BillingStatement/ConfirmCustomers', { json: { month: bkMonth(), listCustomerId: ids, note: note || null } }).then(x => { if (x.ok) { UI.toast(esc((x.res && x.res.responseMessage) || 'Đã ghi nhận'), 'ok'); bkm.sel = new Set(); bkm.key = null; bkBilling(el, f); } }), { input: 'Ghi chú (vd. khách xác nhận qua email ngày …)' });
      const bd = bar.querySelector('#bkBd');
      if (bd) bd.onclick = () => UI.modal({ title: 'Khách phản hồi lệch bảng kê', body: `<div class="small muted mb">${ids.length} khách đã chọn. Ghi nội dung khách phản hồi (bắt buộc) – kiểm tra lại đối soát, mở lại đối soát nếu cần (COO duyệt).</div><textarea class="input" id="bkDn" rows="3" placeholder="Nội dung khách phản hồi…"></textarea>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="bkDok">Ghi nhận</button>',
        onMount: (m, close) => m.querySelector('#bkDok').onclick = () => { const n = m.querySelector('#bkDn').value.trim(); if (!n) { UI.toast('Nhập nội dung khách phản hồi', 'warn'); return; }
          Store.act('BillingStatement/DisputeCustomer', { json: { month: bkMonth(), listCustomerId: ids, note: n } }).then(x => { if (x.ok) { close(); UI.toast(esc((x.res && x.res.responseMessage) || 'Đã ghi'), 'ok'); bkm.sel = new Set(); bkm.key = null; bkBilling(el, f); } }); } });
      bar.querySelector('#bkBc').onclick = () => { bkm.sel = new Set(); bkTab(el, f, perm); };
    };
    drawBar();
    box.querySelector('#bkAll').onchange = e => { rows.filter(r => r.status !== 'NONE').forEach(r => e.target.checked ? bkm.sel.add(r.customerId) : bkm.sel.delete(r.customerId)); bkTab(el, f, perm); };
    box.querySelectorAll('[data-bks]').forEach(c => { c.onclick = e => e.stopPropagation(); c.onchange = () => { c.checked ? bkm.sel.add(c.dataset.bks) : bkm.sel.delete(c.dataset.bks); drawBar(); }; });
    box.querySelectorAll('[data-bkx]').forEach(b => b.onclick = e => { e.stopPropagation(); bkExport([b.dataset.bkx], 'xlsx'); });
    box.querySelectorAll('[data-bkp]').forEach(b => b.onclick = e => { e.stopPropagation(); bkExport([b.dataset.bkp], 'pdf'); });
    box.querySelectorAll('[data-bkg]').forEach(b => b.onclick = e => { e.stopPropagation(); bkNotYet(); });
    box.querySelectorAll('tr.bk-row').forEach(tr => tr.onclick = () => { const id = tr.dataset.bkr; bkm.open.has(id) ? bkm.open.delete(id) : bkm.open.add(id); bkTab(el, f, perm); });
    // Chi tiết ngay dưới dòng: theo buổi (suất tính tiền, lỗi đã trừ, đơn giá, thành tiền) + ghi chú đơn bù / số tạm tính.
    box.querySelectorAll('[data-bkdet]').forEach(cell => {
      const id = cell.dataset.bkdet, m = bkMonth(), to = U.addDays(U.addDays(m, 32).slice(0, 8) + '01', -1);
      const p = bkm.detail[id] || (bkm.detail[id] = Store.query('BillingStatement/GetStatement', { CustomerId: id, From: m, To: to }));
      p.then(x => {
        if (!cell.isConnected) return;
        if (!x.ok || !x.data) { cell.innerHTML = failBox(x.message); return; }
        const s = x.data, by = {};
        (s.listRow || []).forEach(r => { const k = (r.mealName || r.mealType || '') + '|' + r.unitPrice; const g = by[k] || (by[k] = { meal: r.mealName || r.mealType || '', price: r.unitPrice, billed: 0, fault: 0, amount: 0 }); g.billed += r.billedQuantity; g.fault += r.faultQuantity; g.amount += r.amount; });
        cell.innerHTML = `<table class="table dc-table"><thead><tr><th>Buổi</th><th class="num">Suất tính tiền</th><th class="num">Suất lỗi (đã trừ)</th><th class="num">Đơn giá</th><th class="num">Thành tiền</th></tr></thead><tbody>${Object.values(by).map(g => `<tr><td>${esc(g.meal)}</td><td class="num">${U.num(g.billed)}</td><td class="num">${g.fault ? U.num(g.fault) : '–'}</td><td class="num">${U.money(g.price)}</td><td class="num">${U.num(g.amount, 0)}</td></tr>`).join('') || '<tr><td colspan="5" class="muted">Chưa có suất tính tiền</td></tr>'}${(s.listContainerFee || []).map(c => `<tr><td>Phí thất thoát khay ${esc(c.packagingType)}</td><td class="num">${U.num(c.overQuantity)}</td><td></td><td class="num">${U.money(c.feePerUnit)}</td><td class="num">${U.num(c.amount, 0)}</td></tr>`).join('')}</tbody></table>`
          + `${s.compensationOrderCount ? `<div class="small muted">${s.compensationOrderCount} đơn bù do lỗi Delicare (giá 0) không tính tiền.</div>` : ''}${(s.listPending || []).length ? `<div class="notice warn small mt">${I('alert')}<div>Số tạm tính – còn ${s.listPending.length} đơn chờ đối soát: ${s.listPending.slice(0, 10).map(o => esc(o.orderNumber)).join(', ')}</div></div>` : ''}`;
      });
    });
  }
  APP.register('crm-billing', (el, f) => {
    if (!Store.isLive) { el.innerHTML = APP.header(f, '', { noNote: true }) + `<div class="notice gap">${I('alert')}<div>Bảng kê thanh toán dùng dữ liệu API – chuyển sang chế độ API.</div></div>`; return; }
    bkBilling(el, f);
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
