/* Dashboard điều hành + Luồng nghiệp vụ E2E */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const st = { date: null, shift: '' };
  const ORDER_OK = o => !['Draft', 'Cancelled'].includes(o.Status);
  function custOk(cid) { if (!Store.ctx.branch) return true; const c = Store.get('Customer', cid); return c && c.BranchId === Store.ctx.branch; }
  // ---------- Chế độ API (BE3.5): Dashboard/GetExecutive – server tính theo ngày / ca / chi nhánh, cache 60 giây ----------
  const ex = { key: null, data: null, at: 0, busy: false, failed: false, error: '' };
  const EX_TTL = 60000;
  const dateOptions = date => Array.from({ length: 10 }, (_, i) => U.addDays(Store.ctx.date, i - 6)).map(d => `<option value="${d}" ${d === date ? 'selected' : ''}>${d === Store.ctx.date ? 'Hôm nay, ' : ''}${U.date(d)}</option>`).join('');
  const headActions = (date, sh, shifts) => `<select class="select" id="dDate">${dateOptions(date)}</select><select class="select" id="dShift"><option value="">Tất cả ca</option>${shifts.map(s => `<option value="${s.Id}" ${s.Id === sh ? 'selected' : ''}>${esc(s.Name)}</option>`).join('')}</select><a class="btn" href="#/flow-e2e">${I('flow')} 9 luồng nghiệp vụ</a>`;
  function bindFilters(el) {
    el.querySelector('#dDate').onchange = e => { st.date = e.target.value; APP.refresh(); };
    el.querySelector('#dShift').onchange = e => { st.shift = e.target.value; APP.refresh(); };
  }
  const pad2 = n => String(n || 0).padStart(2, '0');
  // Trả true khi đã vẽ (hoặc đang chờ) bằng số liệu server; false → backend chưa có API, vẽ cách cũ.
  function renderLive(el, f, date, sh, shifts) {
    const key = [date, sh, Store.ctx.branch || ''].join('|');
    if ((ex.key !== key || Date.now() - ex.at > EX_TTL) && !ex.busy) {
      ex.busy = true;
      Store.query('Dashboard/GetExecutive', { date, shiftId: sh, branchId: Store.ctx.branch }).then(x => {
        ex.busy = false; ex.key = key; ex.at = Date.now();
        ex.failed = !x.ok && /404/.test(x.message || ''); ex.error = x.ok ? '' : (x.message || 'Lỗi API');
        ex.data = x.ok ? x.data : null;
        if (APP.current === 'dash-exec') APP.refresh();
      });
    }
    if (ex.failed) return false;
    const head = APP.header(f, headActions(date, sh, shifts), { sub: 'Theo dõi toàn bộ hoạt động bếp trung tâm, kho, QC, giao nhận và nhân sự theo ngày / ca / đơn vị. Số liệu do máy chủ tính, làm mới sau 60 giây.' });
    if (ex.key !== key || !ex.data) {
      el.innerHTML = head + (ex.key === key && ex.error ? `<div class="notice gap">${I('alert')}<div><b>Không tải được số liệu điều hành</b>${esc(ex.error)}</div></div>` : `<div class="card panel empty">${I('chart')}<h2 class="mt">Đang tải số liệu điều hành…</h2></div>`);
      bindFilters(el); return true;
    }
    const d = ex.data, k = d.kpi || {};
    const od = k.orders || {}, pp = k.productionProgress || {}, dl = k.delivery || {}, qd = k.qcDetail || {}, sf = k.staffing || {};
    const bs = od.byStatus || {}, okOrders = (od.total || 0) - (bs.Draft || 0) - (bs.Cancelled || 0);
    const series = (d.chart && d.chart.meals7d) || [];
    const alerts = (d.alerts || []).map(a => ({ c: a.severity === 'HIGH' ? 'red' : a.severity === 'LOW' ? 'blue' : 'orange', t: a.title, s: a.detail, go: String(a.route || '').replace(/^#\//, '') }));
    el.innerHTML = head + `
    <div class="kpis">
      ${UI.kpi('Suất ăn kế hoạch', U.num(k.plannedMeals || 0), `${okOrders} đơn`, 'utensils', 'green', 'crm-order')}
      ${UI.kpi('Đơn hàng trong ngày', U.num(od.total || 0), (od.listStatus || []).map(s => s.count + ' ' + esc(String(s.statusName || s.status).toLowerCase())).join(' · ') || 'Chưa có đơn', 'clipboard', 'orange', 'crm-order')}
      ${UI.kpi('Tiến độ sản xuất', pp.percent === null || pp.percent === undefined ? '–' : U.pct(pp.percent, 0), `${U.num(pp.donePortions || 0)} / ${U.num(pp.plannedPortions || 0)} phần`, 'factory', 'blue', 'prod-batch')}
      ${UI.kpi('Giao hàng hoàn tất', `${dl.delivered || 0}/${dl.total || 0}`, '', 'truck', 'purple', 'log-execution')}
      ${UI.kpi('Cảnh báo tồn kho', pad2(k.stockAlerts), k.stockAlerts ? '<span class="warn">Cần xử lý</span>' : '', 'alert', 'red', 'scm-inv-balance')}
      ${UI.kpi('QC tạm giữ / loại', pad2(k.qcHoldReject), `${qd.batchHold || 0} mẻ giữ · ${qd.batchReject || 0} mẻ loại · ${qd.lotOnHold || 0} lô tạm giữ`, 'shield', 'red', 'prod-batch')}
      ${UI.kpi('Nhân sự theo ca', `${sf.checkedIn || 0}/${sf.assigned || 0}`, 'đã vào ca', 'users', 'green', 'hr-shift-assign')}
      ${UI.kpi('Khiếu nại đang mở', pad2(k.openComplaints), '', 'msg', 'orange', 'log-complaint')}
    </div>
    <div class="grid g-main mb">
      <article class="card panel"><div class="panel-head"><div><div class="panel-title">Sản lượng suất ăn 7 ngày</div><div class="panel-desc">Suất kế hoạch theo ngày ăn so với suất khách đã nhận</div></div><div class="chart-legend"><span><i class="legend-dot" style="background:#B9CE8B"></i>Kế hoạch</span><span><i class="legend-dot" style="background:#F5B17E"></i>Thực nhận</span></div></div>
        ${UI.bars(series.map(p => U.date(p.date).slice(0, 5)), [{ name: 'Kế hoạch', values: series.map(p => p.planned || 0), color: '#B9CE8B' }, { name: 'Thực nhận', values: series.map(p => p.accepted || 0), color: '#F5B17E' }])}</article>
      <article class="card panel"><div class="panel-head"><div><div class="panel-title">Tiến độ theo ca sản xuất</div><div class="panel-desc">Phần món đạt / đã đóng gói so với kế hoạch của mẻ</div></div></div>
        ${(d.shiftProgress || []).map(s => { const p = s.percent; return `<div class="progress-row"><span class="name">${esc(s.name || s.code)} <span class="muted small">${s.batchCount || 0} mẻ</span></span><span class="value">${p === null || p === undefined ? '–' : U.pct(p, 0)}</span><div class="track"><div class="fill ${p >= 95 ? '' : p >= 60 ? 'orange' : 'blue'}" style="width:${Math.min(100, p || 0)}%"></div></div></div>`; }).join('') || '<div class="muted small">Chưa có ca sản xuất</div>'}
        <div class="panel-title mt">KPI ngày</div>${(d.kpiSnapshot || []).map(m => `<div class="spread small" style="padding:5px 0;border-bottom:1px solid #F0F2ED"><span>${esc(m.name || m.metricCode)}${m.target !== null && m.target !== undefined ? ` <span class="muted">· mục tiêu ${m.lowerIsBetter ? '≤' : '≥'} ${U.num(m.target, 1)}${m.unitCode === '%' ? '%' : ''}</span>` : ''}</span><b class="${m.met === false ? 'warn' : m.met === true ? 'up' : 'muted'}">${m.value === null || m.value === undefined ? '–' : U.num(m.value, 1) + (m.unitCode === '%' ? '%' : '')}</b></div>`).join('')}
      </article></div>
    <div class="grid g-main">
      <article class="card panel"><div class="panel-head"><div><div class="panel-title">Tình hình đơn hàng & giao nhận</div><div class="panel-desc">Đơn hàng ngày ${U.date(date)}${d.ordersTotal > (d.orders || []).length ? ` · hiện ${(d.orders || []).length} / ${d.ordersTotal} đơn` : ''}</div></div><a class="btn" href="#/crm-order">Xem tất cả →</a></div><div id="dOrders"></div></article>
      <article class="card panel"><div class="panel-head"><div><div class="panel-title">Cảnh báo & công việc</div><div class="panel-desc">Theo quyền của tài khoản đang đăng nhập</div></div><span class="badge orange">${alerts.length} việc</span></div><div class="alert-list">${alerts.slice(0, 7).map(a => `<div class="alert"><div class="alert-ico ${a.c}">${I('alert')}</div><div class="alert-copy"><b>${esc(a.t)}</b><small>${esc(a.s)}</small></div><a class="btn xs" href="#/${a.go}">Mở</a></div>`).join('') || '<div class="empty">Không có cảnh báo</div>'}</div></article>
    </div>`;
    bindFilters(el);
    UI.table(el.querySelector('#dOrders'), {
      pageSize: 6, toolbar: false, rows: (d.orders || []).map(o => Object.assign({ Id: o.id }, o)), onRow: id => APP.go('crm-order?id=' + id),
      cols: [{ k: 'orderNumber', label: 'Mã đơn' }, { k: 'customerName', label: 'Khách hàng' }, { k: 'totalQuantity', label: 'Suất ăn', num: true, render: r => U.num(r.totalQuantity || 0) }, { k: 'status', label: 'Đơn hàng', render: r => UI.badge(r.status, 'Order', 'Status') },
        { k: 'x', label: 'Giao hàng', nosort: true, render: r => (r.listDelivery || []).length ? r.listDelivery.map(x => UI.badge(x.status, 'DeliveryExecution', 'Status')).join(' ') : '<span class="muted small">Chưa giao</span>' }]
    });
    return true;
  }
  APP.register('dash-exec', (el, f) => {
    const date = st.date || Store.ctx.date; st.date = date;
    const shifts = Store.rows('Shift');
    const sh = st.shift;
    if (Store.isLive && renderLive(el, f, date, sh, shifts)) return;
    const orders = Store.rows('Order').filter(o => U.day(o.MealDate) === date && custOk(o.CustomerId));
    const lines = Store.rows('OrderLine').filter(l => orders.some(o => o.Id === l.OrderId) && (!sh || l.ShiftId === sh));
    const okOrders = orders.filter(ORDER_OK);
    const meals = U.sum(lines.filter(l => okOrders.some(o => o.Id === l.OrderId)), 'Quantity');
    const pos = Store.rows('ProductionOrder').filter(p => { const pl = Store.get('ProductionPlan', p.ProductionPlanId); return pl && U.day(pl.ProductionDate) === date && (!sh || p.ShiftId === sh); });
    const batches = Store.rows('ProductionBatche').filter(b => pos.some(p => p.Id === b.ProductionOrderId));
    const planQ = U.sum(batches, 'PlannedQuantity'), doneQ = U.sum(batches.filter(b => ['Passed', 'Packed'].includes(b.Status)), 'ActualQuantity');
    const prog = planQ ? doneQ / planQ * 100 : 0;
    const under = APP.alerts().filter(a => a.go === 'scm-inv-balance').length ? Store.rows('InventoryPolicie').filter(p => U.sum(Store.by('InventoryBalance', 'IngredientId', p.IngredientId), 'AvailableQuantity') < (p.MinimumStock || 0)).length : 0;
    const dvs = Store.rows('DeliveryOrder').filter(d => U.day(d.DeliveryDate) === date && custOk(d.CustomerId));
    const exe = Store.rows('DeliveryExecution').filter(x => dvs.some(d => d.Id === x.DeliveryOrderId));
    const delivered = exe.filter(x => x.Status === 'DELIVERED').length;
    const sa = Store.rows('ShiftAssignment').filter(a => U.day(a.WorkDate) === date && (!sh || a.ShiftId === sh));
    const qcHold = Store.rows('ProductionBatche').filter(b => ['HOLD', 'REJECT'].includes(b.QcStatus) && batches.includes(b)).length + Store.rows('QCInspectionLine').filter(l => l.Result === 'HOLD').length;
    const cmp = Store.rows('CustomerComplaint').filter(c => ['Open', 'InProgress'].includes(c.ComplaintStatus));
    // 7 ngày
    const days = Array.from({ length: 7 }, (_, i) => U.addDays(date, i - 6));
    const planSeries = days.map(d => U.sum(Store.rows('OrderLine').filter(l => { const o = Store.get('Order', l.OrderId); return o && U.day(o.MealDate) === d && ORDER_OK(o) && custOk(o.CustomerId) && (!sh || l.ShiftId === sh); }), 'Quantity'));
    const actSeries = days.map(d => U.sum(Store.rows('ProofOfDeliverie').filter(p => { const dv = Store.get('DeliveryOrder', p.DeliveryOrderId); return dv && U.day(dv.DeliveryDate) === d && custOk(dv.CustomerId); }), 'AcceptedQuantity'));
    const kpiRows = Store.rows('KpiDailySnapshot').filter(k => U.day(k.SnapshotDate) === date && (!Store.ctx.branch || k.BranchId === Store.ctx.branch));
    const kpiAvg = code => { const r = kpiRows.filter(k => k.MetricCode === code); return r.length ? U.sum(r, 'MetricValue') / r.length : null; };
    const kpiDef = Store.rows('KpiMetricDefinition');
    el.innerHTML = APP.header(f, `<select class="select" id="dDate">${Array.from({ length: 10 }, (_, i) => U.addDays(Store.ctx.date, i - 6)).map(d => `<option value="${d}" ${d === date ? 'selected' : ''}>${d === Store.ctx.date ? 'Hôm nay, ' : ''}${U.date(d)}</option>`).join('')}</select><select class="select" id="dShift"><option value="">Tất cả ca</option>${shifts.map(s => `<option value="${s.Id}" ${s.Id === sh ? 'selected' : ''}>${esc(s.Name)}</option>`).join('')}</select><a class="btn" href="#/flow-e2e">${I('flow')} 9 luồng nghiệp vụ</a>`, { sub: 'Theo dõi toàn bộ hoạt động bếp trung tâm, kho, QC, giao nhận và nhân sự theo ngày / ca / đơn vị. Bộ lọc thay đổi toàn bộ số liệu (DỮ LIỆU DEMO).' }) + `
    <div class="kpis">
      ${UI.kpi('Suất ăn kế hoạch', U.num(meals), `${okOrders.length} đơn`, 'utensils', 'green', 'crm-order')}
      ${UI.kpi('Đơn hàng trong ngày', U.num(orders.length), Object.entries(U.group(orders, 'Status')).map(([k, v]) => v.length + ' ' + UI.statusVi('Order', 'Status', k).toLowerCase()).join(' · '), 'clipboard', 'orange', 'crm-order')}
      ${UI.kpi('Tiến độ sản xuất', U.pct(prog, 0), `${U.num(doneQ)} / ${U.num(planQ)} phần`, 'factory', 'blue', 'prod-batch')}
      ${UI.kpi('Giao hàng hoàn tất', `${delivered}/${exe.length || dvs.length}`, '', 'truck', 'purple', 'log-execution')}
      ${UI.kpi('Cảnh báo tồn kho', String(under).padStart(2, '0'), '<span class="warn">Cần xử lý</span>', 'alert', 'red', 'scm-inv-balance')}
      ${UI.kpi('QC tạm giữ / loại', String(qcHold).padStart(2, '0'), '', 'shield', 'red', 'qc-inspection')}
      ${UI.kpi('Nhân sự theo ca', `${sa.filter(a => ['CheckedIn', 'Completed'].includes(a.AssignmentStatus)).length}/${sa.length}`, 'đã chấm công', 'users', 'green', 'hr-shift-assign')}
      ${UI.kpi('Khiếu nại đang mở', String(cmp.length).padStart(2, '0'), '', 'msg', 'orange', 'log-complaint')}
    </div>
    <div class="grid g-main mb">
      <article class="card panel"><div class="panel-head"><div><div class="panel-title">Sản lượng suất ăn 7 ngày</div><div class="panel-desc">Kế hoạch (Σ OrderLine.Quantity) so với Khách hàng nhận (Σ ProofOfDeliverie.AcceptedQuantity)</div></div><div class="chart-legend"><span><i class="legend-dot" style="background:#B9CE8B"></i>Kế hoạch</span><span><i class="legend-dot" style="background:#F5B17E"></i>Thực nhận</span></div></div>
        ${UI.bars(days.map(d => U.date(d).slice(0, 5)), [{ name: 'Kế hoạch', values: planSeries, color: '#B9CE8B' }, { name: 'Thực nhận', values: actSeries, color: '#F5B17E' }])}</article>
      <article class="card panel"><div class="panel-head"><div><div class="panel-title">Tiến độ theo ca sản xuất</div><div class="panel-desc">Σ ActualQuantity mẻ Passed/Packed ÷ Σ PlannedQuantity</div></div></div>
        ${shifts.map(s => { const bs = batches.filter(b => { const p = Store.get('ProductionOrder', b.ProductionOrderId); return p && p.ShiftId === s.Id; }); const pq = U.sum(bs, 'PlannedQuantity'), dq = U.sum(bs.filter(b => ['Passed', 'Packed'].includes(b.Status)), 'ActualQuantity'); const p = pq ? dq / pq * 100 : 0; return `<div class="progress-row"><span class="name">${esc(s.Name)} <span class="muted small">${bs.length} mẻ</span></span><span class="value">${pq ? U.pct(p, 0) : '–'}</span><div class="track"><div class="fill ${p >= 95 ? '' : p >= 60 ? 'orange' : 'blue'}" style="width:${Math.min(100, p)}%"></div></div></div>`; }).join('')}
        <div class="panel-title mt">KPI ngày (KpiDailySnapshot)</div>${['PROD_ACHIEVEMENT', 'DEL_ONTIME', 'INV_ACCURACY', 'FIN_FOOD_COST'].map(c => { const v = kpiAvg(c), d = kpiDef.find(k => k.MetricCode === c); const r = kpiRows.find(k => k.MetricCode === c); return `<div class="spread small" style="padding:5px 0;border-bottom:1px solid #F0F2ED"><span>${esc(d ? d.MetricName : c)}</span><b class="${r && v !== null && ((c === 'FIN_FOOD_COST' && v > r.TargetValue) || (c !== 'FIN_FOOD_COST' && v < r.TargetValue)) ? 'warn' : 'up'}">${v === null ? '–' : U.num(v, 1) + (d && d.UnitCode === '%' ? '%' : '')}</b></div>`; }).join('')}
      </article></div>
    <div class="grid g-main">
      <article class="card panel"><div class="panel-head"><div><div class="panel-title">Tình hình đơn hàng & giao nhận</div><div class="panel-desc">Đơn hàng ngày ${U.date(date)} · trạng thái giao từ DeliveryExecution</div></div><a class="btn" href="#/crm-order">Xem tất cả →</a></div><div id="dOrders"></div></article>
      <article class="card panel"><div class="panel-head"><div><div class="panel-title">Cảnh báo & công việc</div><div class="panel-desc">Tính trực tiếp từ dữ liệu demo</div></div><span class="badge orange">${APP.alerts().length} việc</span></div><div class="alert-list">${APP.alerts().slice(0, 7).map(a => `<div class="alert"><div class="alert-ico ${a.c}">${I(a.i)}</div><div class="alert-copy"><b>${esc(a.t)}</b><small>${esc(a.s)}</small></div><a class="btn xs" href="#/${a.go}">Mở</a></div>`).join('')}</div></article>
    </div>`;
    el.querySelector('#dDate').onchange = e => { st.date = e.target.value; APP.refresh(); };
    el.querySelector('#dShift').onchange = e => { st.shift = e.target.value; APP.refresh(); };
    UI.table(el.querySelector('#dOrders'), {
      table: 'Order', pageSize: 6, toolbar: false, rows: orders, onRow: id => APP.go('crm-order?id=' + id),
      cols: [{ k: 'OrderNumber', label: 'Mã đơn' }, { k: 'CustomerId', label: 'Khách hàng', render: r => esc(Store.get('Customer', r.CustomerId).DisplayName) }, { k: 'TotalQuantity', label: 'Suất ăn', num: true }, { k: 'Status', label: 'Đơn hàng' },
        { k: 'x', label: 'Giao hàng', col: false, nosort: true, render: r => { const ex = Store.rows('DeliveryExecution').filter(x => Store.rows('DeliveryOrder').some(d => d.Id === x.DeliveryOrderId && d.OrderId === r.Id)); return ex.length ? ex.map(x => UI.badge(x.Status, 'DeliveryExecution', 'Status')).join(' ') : '<span class="muted small">Chưa giao</span>'; } }]
    });
  });
  // ---------- 9 luồng nghiệp vụ ----------
  const fs = { sel: 1 };
  function progressOf(w) {
    // Ước lượng bước hiện tại dựa trên dữ liệu demo (minh họa)
    const has = t => Store.rows(t).length > 0;
    return w.steps.map(s => { const t = (s.tables.match(/^[A-Za-z]+/) || [])[0]; return t && Store.schema.tables[t] ? (has(t) ? 'done' : '') : 'done'; });
  }
  APP.register('flow-e2e', (el, f) => {
    const W = Store.cat.workflows;
    const w = W.find(x => x.id === fs.sel) || W[0];
    el.innerHTML = APP.header(f, `<a class="btn" href="docs/DELICARE_Business_Workflow.md" target="_blank">${I('export')} Tải đặc tả .md</a>`, { sub: '9 luồng nghiệp vụ có thể trình diễn. Mỗi bước mở đúng màn hình thao tác và hiển thị bảng Database liên quan. Đặc tả chi tiết: DELICARE_Business_Workflow.md.' }) + `
    <div class="row-flex mb">${W.map(x => `<button class="chip ${x.id === w.id ? 'active' : ''}" data-w="${x.id}">${x.id}. ${esc(x.name)}</button>`).join('')}</div>
    <article class="card flow-card"><div class="spread"><div><h2>${esc(w.name)}</h2></div>${Store.isLive ? '' : `<button class="btn primary" data-start>${I('play')} Bắt đầu demo từ bước 1</button>`}</div><div class="meta">Người thực hiện: ${esc(w.actors)}</div>
      <div class="stepper">${w.steps.map((s, i) => `<div class="step done"><a href="#/${s.screen}"><div class="dot">${i + 1}</div><div class="lbl">${esc(s.label)}</div><small>#/${s.screen}</small></a></div>`).join('')}</div></article>
    <div class="card panel"><div class="panel-title mb">Chi tiết từng bước – màn hình & bảng Database</div><div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>#</th><th>Bước</th><th>Màn hình</th><th>Phase</th><th>Bảng / cột Database</th><th>Trạng thái mapping</th><th></th></tr></thead><tbody>${w.steps.map((s, i) => { const fn = APP.FN[s.screen]; return `<tr><td>${i + 1}</td><td><b>${esc(s.label)}</b></td><td>${esc(fn.name)}<span class="sub mono">#/${s.screen}</span></td><td>${fn.ph}</td><td class="mono" style="white-space:normal">${esc(s.tables)}</td><td>${UI.badge(fn.st === 'MAPPED' ? 'Matched' : fn.st === 'PARTIAL' ? 'PARTIAL' : 'Failed')} <span class="small muted">${fn.st}</span></td><td><a class="btn xs" href="#/${s.screen}">Mở →</a></td></tr>`; }).join('')}</tbody></table></div></div>
    <div class="notice info mt">${I('alert')}<div><b>Trạng thái & quy tắc chuyển bước</b>Giá trị theo SQL DEFAULT / BRD được giữ nguyên; các giá trị còn lại là PROPOSED – BA/PO CONFIRM (xem Gap Register sheet 07).</div></div>`;
    el.querySelectorAll('[data-w]').forEach(b => b.onclick = () => { fs.sel = +b.dataset.w; APP.refresh(); });
    const st = el.querySelector('[data-start]'); if (st) st.onclick = () => APP.go(w.steps[0].screen);
  });
})();
