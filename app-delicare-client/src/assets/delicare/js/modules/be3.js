/* BE-3 – nối API mới vào giao diện (chỉ chạy ở chế độ API; backend chưa có API → giữ nguyên màn hình cũ):
 *   Common/GetRelated (tab Liên quan) · Dashboard/GetOrderJourney (tab Hành trình của đơn hàng) · DeliveryOrder/GetEvents (tab Sự kiện giao)
 *   OrderReconciliation/GetList (biểu đồ Đặt – Giao – Nhận) · MealWasteRecord/GetSummary · TemperatureLog/GetSeries · DeliveryEvent/GetVehiclePositions
 *   KpiSnapshot/GetDaily | GetMonthly | Refresh
 * Tài liệu: docs/frontend-handoff/BE3.1.md, BE3.3.md, BE3.4.md, BE3.5.md */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const TTL = 60000;
  const is404 = x => /404/.test((x && x.message) || '');
  const screenOf = route => String(route || '').replace(/^#\//, '');
  const dtx = v => U.dt(String(v || '').replace(/\.\d+$/, ''));

  // ---------- Đọc API có cache 60 giây (màn hình vẽ lại nhiều lần trong lúc tải dữ liệu) ----------
  const cache = {};
  function fetchCached(path, params) {
    const ck = path + '|' + JSON.stringify(params || {});
    const c = cache[ck];
    if (c && Date.now() - c.at < TTL) return c.p;
    const p = Store.query(path, params);
    cache[ck] = { at: Date.now(), p };
    return p;
  }
  const clearCache = prefix => Object.keys(cache).forEach(k => { if (k.startsWith(prefix)) delete cache[k]; });

  // Tên danh sách của API (tên controller) → bảng của giao diện (bảng legacy giữ tên cũ: Dish → Dishe…).
  const TABLE_OF = {};
  Object.keys(Store.schema.tables).forEach(t => { TABLE_OF[String(Store.controllerOf(t)).toLowerCase()] = t; });
  const tableOf = key => TABLE_OF[String(key || '').toLowerCase()] || null;
  function openRecord(table, id, screen) {
    if (!table || !Store.schema.tables[table]) { if (screen) APP.go(screen); return; }
    Store.ensure(table).then(() => UI.detail(table, id, { screen }));
  }
  const statusChip = (code, name) => name || code ? `<span class="badge ${/Done|Matched|Closed|DELIVERED|Passed|Packed|Approved|Confirmed|Reconciled|Succeeded/i.test(code || '') ? 'green' : /Issue|FAILED|Reject|Cancel|Variance/i.test(code || '') ? 'red' : /InProgress|Pending|Open|HOLD|Running/i.test(code || '') ? 'orange' : 'gray'}">${esc(name || code)}</span>` : '';

  // ---------- Tab "Liên quan" (BE3.3) ----------
  const RELATED = new Set(['customer', 'contract', 'order', 'supplier', 'ingredient', 'purchaseorder', 'materiallot', 'dish', 'employee']);
  UI.related = (t, id, pane) => {
    const res = Store.controllerOf(t);
    if (Store.isLive && pane && RELATED.has(String(res).toLowerCase())) {
      fetchCached('Common/GetRelated', { resource: res, id }).then(x => {
        if (!x.ok || !pane.isConnected) return;
        const groups = x.list.filter(g => g.count > 0);
        pane.innerHTML = groups.length ? groups.map(g => {
          const sid = screenOf(g.route), tb = tableOf(g.key);
          return `<div class="mb"><div class="spread"><h3 style="margin:0">${esc(g.title)} <span class="muted small">${g.count} bản ghi</span></h3>${sid ? `<a class="btn xs" href="#/${esc(sid)}" data-close>${g.count > (g.items || []).length ? 'Xem tất cả (' + g.count + ')' : 'Mở màn hình'} →</a>` : ''}</div>${(g.items || []).map(it => `<div class="spread" style="padding:6px 0;border-bottom:1px solid #F0F2ED"><a href="javascript:void 0" data-rel-id="${esc(it.id)}" data-rel-t="${esc(tb || '')}" data-rel-s="${esc(sid)}">${esc(it.label || it.id)}</a>${statusChip(it.status, it.statusName)}</div>`).join('')}</div>`;
        }).join('') : '<div class="empty">Không có bản ghi liên quan</div>';
        pane.querySelectorAll('[data-rel-id]').forEach(a => a.onclick = () => openRecord(a.dataset.relT, a.dataset.relId, a.dataset.relS));
      });
    }
    return UI.children(t, id);
  };

  // ---------- Tab riêng theo bảng ở chế độ API ----------
  const STAGE_COLOR = { Done: 'done', None: 'done', InProgress: 'now', Issue: 'bad', Cancelled: 'bad' };
  function journeyHtml(d) {
    const st = d.stages || [];
    return `<div class="small muted mb">${esc(d.orderNumber || '')} · ${esc(d.customerName || '')}${d.mealDate ? ' · ngày ăn ' + U.date(d.mealDate) : ''}${d.contractNumber ? ' · HĐ ' + esc(d.contractNumber) : ''}</div>
      <div class="timeline">${st.map((s, i) => `<div class="tl ${s.status === 'Done' || s.status === 'None' ? '' : s.status === 'Issue' || s.status === 'Cancelled' ? 'red' : s.status === 'InProgress' ? 'orange' : 'blue'}">
        <b>${i + 1}. ${esc(s.title)} ${statusChip(s.status, s.statusName)}${d.currentStage === s.stage ? ' <span class="badge orange">Đang ở chặng này</span>' : ''}</b>
        <small>${s.at ? esc(dtx(s.at)) + ' · ' : ''}${s.documentCount || 0} chứng từ${s.note ? ' · ' + esc(s.note) : ''}</small>
        ${s.canView === false ? `<div class="small muted">${s.documentCount || 0} chứng từ – không có quyền xem</div>` : (s.documents || []).map(doc => typeof doc === 'string' ? `<div class="small">${esc(doc)}</div>` : `<div class="spread small" style="padding:3px 0"><a href="#/${esc(screenOf(doc.route || s.route))}" data-close>${esc(doc.number || doc.id || '')}</a>${statusChip(doc.status, doc.statusName)}</div>`).join('')}
      </div>`).join('')}</div>`;
  }
  function asyncPane(path, params, render, empty) {
    return pane => {
      fetchCached(path, params).then(x => {
        if (!pane.isConnected) return;
        pane.innerHTML = x.ok ? (render(x) || `<div class="empty">${empty}</div>`) : `<div class="empty">${esc(is404(x) ? 'Máy chủ chưa có chức năng này' : (x.message || 'Không tải được dữ liệu'))}</div>`;
      });
      return '<div class="empty">Đang tải…</div>';
    };
  }
  UI.liveTabs = (t, id) => {
    if (!Store.isLive) return [];
    if (t === 'Order') return [{ label: 'Hành trình', html: asyncPane('Dashboard/GetOrderJourney', { orderId: id }, x => x.data && journeyHtml(x.data), 'Chưa có hành trình') }];
    if (t === 'DeliveryOrder') return [{
      label: 'Sự kiện giao', html: asyncPane('DeliveryOrder/GetEvents', { id }, x => x.list.length ? `<div class="timeline">${x.list.map(e => `<div class="tl ${e.eventType === 'FAILED' ? 'red' : e.eventType === 'DELIVERED' ? '' : e.eventType === 'POSITION' ? 'blue' : 'orange'}"><b>${esc(e.eventTypeName || e.eventType)}${e.stopSequenceNo ? ' · điểm ' + e.stopSequenceNo : ''}${e.temperatureC !== null && e.temperatureC !== undefined ? ' · ' + U.num(e.temperatureC, 1) + ' °C' : ''}</b><small>${esc(dtx(e.eventAt))}${e.routeNumber ? ' · ' + esc(e.routeNumber) : ''}${e.vehiclePlate ? ' · ' + esc(e.vehiclePlate) : ''}${e.recordedByName ? ' · ' + esc(e.recordedByName) : ''}${e.description ? ' · ' + esc(e.description) : ''}${e.latitude ? ` · (${U.num(e.latitude, 5)}, ${U.num(e.longitude, 5)})` : ''}</small></div>`).join('')}</div>` : '', 'Chưa có sự kiện giao')
    }];
    return [];
  };

  // ---------- Khối số liệu chèn dưới tiêu đề màn hình ----------
  function panel(el, title, path, params, render) {
    const anchor = el.querySelector('.page-heading'); if (!anchor) return null;
    const host = document.createElement('div'); host.className = 'card panel mb';
    host.innerHTML = `<div class="panel-title">${esc(title)}</div><div class="muted small">Đang tải…</div>`;
    anchor.insertAdjacentElement('afterend', host);
    fetchCached(path, params).then(x => {
      if (!host.isConnected) return;
      if (!x.ok) { if (is404(x)) host.remove(); else host.innerHTML = `<div class="panel-title">${esc(title)}</div><div class="muted small">Không tải được: ${esc(x.message || '')}</div>`; return; }
      host.innerHTML = render(x, host);
      if (host._bind) host._bind();
    });
    return host;
  }
  // Bọc màn hình đã có: vẽ như cũ rồi thêm khối số liệu từ server.
  function extend(id, after) {
    const base = APP.SCREENS[id];
    APP.register(id, (el, f, p) => {
      if (base) base(el, f, p); else if (f.table) APP.genericList(el, f); else APP.gapScreen(el, f);
      if (Store.isLive) { try { after(el, f, p); } catch (e) { console.warn('[DELICARE] BE-3', id, e); } }
    });
  }
  const legend = items => `<div class="chart-legend">${items.map(([c, l]) => `<span><i class="legend-dot" style="background:${c}"></i>${esc(l)}</span>`).join('')}</div>`;

  // Đối soát: biểu đồ Đặt – Giao – Nhận 30 ngày gần nhất (BE3.1 mục 1)
  extend('crm-reconciliation', el => panel(el, 'Đặt – Giao – Nhận theo ngày ăn', 'OrderReconciliation/GetList', { PageSize: 1 }, x => {
    const d = x.data || {}, ch = d.chart || [];
    const sc = d.statusCounts || {};
    return `<div class="panel-head"><div><div class="panel-title">Đặt – Giao – Nhận theo ngày ăn</div><div class="panel-desc">${U.date(d.from)} → ${U.date(d.to)} · ${d.totalRecords || 0} bản đối soát${Object.keys(sc).length ? ' · ' + Object.entries(sc).map(([k, v]) => v + ' ' + esc(UI.statusVi('OrderReconciliation', 'Status', k) || k).toLowerCase()).join(' · ') : ''}</div></div>${legend([['#B9CE8B', 'Đặt'], ['#8FB8DE', 'Giao'], ['#F5B17E', 'Khách nhận']])}</div>
      ${ch.length ? UI.lines(ch.map(p => U.date(p.date).slice(0, 5)), [{ name: 'Đặt', values: ch.map(p => p.ordered || 0), color: '#B9CE8B' }, { name: 'Giao', values: ch.map(p => p.delivered || 0), color: '#8FB8DE' }, { name: 'Khách nhận', values: ch.map(p => p.accepted || 0), color: '#F5B17E' }], { min: 0 }) : '<div class="empty">Chưa có dữ liệu đối soát trong khoảng này</div>'}`;
  }));

  // Hao hụt suất ăn: tổng hợp 30 ngày theo loại / món / ca / ngày (BE3.1 mục 2)
  const ws = { by: 'wasteType' };
  const WASTE_BY = [['wasteType', 'Theo loại'], ['dish', 'Theo món'], ['shift', 'Theo ca'], ['date', 'Theo ngày']];
  extend('prod-waste', el => {
    const host = panel(el, 'Tổng hợp hao hụt 30 ngày', 'MealWasteRecord/GetSummary', { GroupBy: ws.by }, x => {
      const d = x.data || {}, li = d.listItem || [];
      return `<div class="panel-head"><div><div class="panel-title">Tổng hợp hao hụt</div><div class="panel-desc">${U.date(d.from)} → ${U.date(d.to)} · ${U.num(d.totalMeals || 0)} suất · ${U.num(d.totalKg || 0, 1)} kg · ước tính ${U.money(d.totalCost || 0)}${d.recordsWithoutCost ? ` · ${d.recordsWithoutCost} dòng chưa tính được giá` : ''}</div></div><div class="row-flex">${WASTE_BY.map(([k, l]) => `<button class="chip ${k === ws.by ? 'active' : ''}" data-wby="${k}">${l}</button>`).join('')}</div></div>
        ${li.length ? UI.bars(li.slice(0, 14).map(r => { const l = ws.by === 'date' ? U.date(r.key || r.label).slice(0, 5) : String(r.label || r.key || ''); return l.length > 14 ? l.slice(0, 13) + '…' : l; }), [{ name: 'Suất hao hụt', values: li.slice(0, 14).map(r => r.quantityMeals || 0), color: '#F5B17E' }]) : '<div class="empty">Không có hao hụt trong khoảng này</div>'}`;
    });
    if (host) host._bind = () => host.querySelectorAll('[data-wby]').forEach(b => b.onclick = () => { ws.by = b.dataset.wby; APP.refresh(); });
  });

  // Nhiệt độ: chuỗi 48 giờ của một khu vực kho, kèm ngưỡng (BE3.1 mục 3)
  const ts = { zone: '' };
  extend('qc-temperature', el => {
    const zones = Store.rows('StorageZone');
    if (!zones.length) return;
    if (!ts.zone || !zones.some(z => z.Id === ts.zone)) ts.zone = zones[0].Id;
    const host = panel(el, 'Chuỗi nhiệt độ khu vực kho', 'TemperatureLog/GetSeries', { StorageZoneId: ts.zone }, x => {
      const d = x.data || {}, pts = d.points || [];
      const refs = []; if (d.temperatureMin !== null && d.temperatureMin !== undefined) refs.push({ v: d.temperatureMin, color: '#4C83B6', label: 'Tối thiểu ' + U.num(d.temperatureMin, 1) + '°C' });
      if (d.temperatureMax !== null && d.temperatureMax !== undefined) refs.push({ v: d.temperatureMax, color: '#D95C4F', label: 'Tối đa ' + U.num(d.temperatureMax, 1) + '°C' });
      const bad = new Set(pts.filter(p => p.isWithinRange === false).map(p => p.temperatureC));
      return `<div class="panel-head"><div><div class="panel-title">Chuỗi nhiệt độ khu vực kho</div><div class="panel-desc">${esc(dtx(d.from))} → ${esc(dtx(d.to))} · ${d.measurementCount || 0} lần đo · <span class="${d.outOfRangeCount ? 'warn' : ''}">${d.outOfRangeCount || 0} lần ngoài ngưỡng</span>${d.bucketMinutes ? ' · gộp ' + d.bucketMinutes + ' phút / điểm' : ''}</div></div><select class="select" data-zone>${zones.map(z => `<option value="${z.Id}" ${z.Id === ts.zone ? 'selected' : ''}>${esc(Store.label('StorageZone', z))}</option>`).join('')}</select></div>
        ${pts.length ? UI.lines(pts.map(p => dtx(p.measuredAt).slice(0, 11)), [{ name: 'Nhiệt độ', values: pts.map(p => p.temperatureC), color: '#91AF42' }], { refs, bands: refs.length === 2 ? [[d.temperatureMin, d.temperatureMax]] : [], badFn: v => bad.has(v) }) : '<div class="empty">Khu vực này chưa có lần đo trong 48 giờ qua</div>'}`;
    });
    if (host) host._bind = () => { const s = host.querySelector('[data-zone]'); if (s) s.onchange = e => { ts.zone = e.target.value; APP.refresh(); }; };
  });

  // Vị trí xe theo sự kiện trong ngày (BE3.1 mục 5)
  extend('log-gps', el => panel(el, 'Vị trí xe hôm nay', 'DeliveryEvent/GetVehiclePositions', { DeliveryDate: Store.ctx.date }, x => {
    const d = x.data || {}, latest = d.listLatest || [], all = d.listPosition || [];
    return `<div class="panel-head"><div><div class="panel-title">Vị trí xe theo sự kiện – ${U.date(d.deliveryDate || Store.ctx.date)}</div><div class="panel-desc">${latest.length} chuyến có tọa độ · ${all.length} điểm ghi nhận (đến điểm, giao không thành công, tài xế gửi vị trí)</div></div></div>
      ${latest.length ? `<div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Chuyến</th><th>Xe</th><th>Tài xế</th><th>Sự kiện gần nhất</th><th>Lúc</th><th>Tọa độ</th></tr></thead><tbody>${latest.map(p => `<tr><td><b>${esc(p.routeNumber || '')}</b></td><td>${esc(p.vehiclePlate || '')}</td><td>${esc(p.driverName || '')}</td><td>${esc(p.eventTypeName || p.eventType || '')}</td><td>${esc(dtx(p.eventAt))}</td><td class="mono">${U.num(p.latitude, 5)}, ${U.num(p.longitude, 5)}</td></tr>`).join('')}</tbody></table></div>` : '<div class="empty">Chưa có chuyến nào ghi tọa độ trong ngày</div>'}`;
  }));

  // ---------- KPI ngày / tháng (BE3.4) ----------
  const ks = { dom: 'ALL', off: false };
  const DOMS = [['ALL', 'Tất cả'], ['PROD', 'Sản xuất'], ['QA', 'Chất lượng'], ['DEL', 'Giao hàng'], ['INV', 'Kho'], ['FIN', 'Tài chính'], ['PKG', 'Bao bì'], ['CS', 'Khách hàng']];
  const fmt = (v, unit) => v === null || v === undefined ? '–' : unit === 'VND' ? U.money(v) : U.num(v, 2) + (unit === '%' ? '%' : unit ? ' ' + esc(unit) : '');
  function kpiLive(monthly, base) {
    return (el, f, p) => {
      if (!Store.isLive || ks.off) return base(el, f, p);
      const path = monthly ? 'KpiSnapshot/GetMonthly' : 'KpiSnapshot/GetDaily';
      const params = { BranchId: Store.ctx.branch, DomainCode: ks.dom === 'ALL' ? '' : ks.dom };
      const admin = !!(window.DC_HOST && window.DC_HOST.user && window.DC_HOST.user.isAdmin);
      el.innerHTML = APP.header(f, admin ? `<button class="btn primary" id="kRefresh">${I('reset')} Làm mới số liệu</button>` : '', { noNote: true, sub: `Chỉ tiêu ${monthly ? '12 tháng' : '30 ngày'} gần nhất do máy chủ tính. ${monthly ? 'Tháng đang chạy là số tới ngày làm mới.' : 'Ngày chưa làm mới thì để trống.'}` }) +
        `<div class="row-flex mb">${DOMS.map(([k, l]) => `<button class="chip ${k === ks.dom ? 'active' : ''}" data-dom="${k}">${l}</button>`).join('')}<span class="small muted">· Đơn vị: ${Store.ctx.branch ? esc(Store.label('OrganizationUnit', Store.ctx.branch)) : 'Toàn công ty'}</span></div><div id="kBody"><div class="card panel empty">${I('chart')}<h2 class="mt">Đang tải KPI…</h2></div></div>`;
      el.querySelectorAll('[data-dom]').forEach(b => b.onclick = () => { ks.dom = b.dataset.dom; APP.refresh(); });
      const rb = el.querySelector('#kRefresh');
      if (rb) rb.onclick = () => {
        rb.disabled = true;
        Store.act('KpiSnapshot/Refresh', { form: { jobCode: monthly ? 'KPI_MONTHLY' : 'KPI_DAILY', branchId: Store.ctx.branch } }).then(r => {
          rb.disabled = false; if (!r.ok) return;
          const d = r.data || {}; clearCache('KpiSnapshot/'); clearCache('Dashboard/');
          const miss = (d.metrics || []).filter(m => m.note).slice(0, 4).map(m => esc(m.metricName || m.metricCode) + ': ' + esc(m.note)).join('<br>');
          UI.toast(`Đã làm mới ${d.rowsProcessed || 0} chỉ tiêu${d.incompleteCount ? ` · ${d.incompleteCount} chỉ tiêu thiếu dữ liệu` : ''}${miss ? '<br><span class="small">' + miss + '</span>' : ''}`, d.incompleteCount ? 'warn' : 'ok');
          APP.refresh();
        });
      };
      const body = el.querySelector('#kBody');
      fetchCached(path, params).then(x => {
        if (is404(x)) { ks.off = true; if (APP.current === f.id) APP.refresh(); return; }
        if (!body.isConnected) return;
        if (!x.ok) { body.innerHTML = `<div class="notice gap">${I('alert')}<div><b>Không tải được KPI</b>${esc(x.message || '')}</div></div>`; return; }
        const ms = (x.data && x.data.metrics) || [];
        body.innerHTML = ms.length ? `<div class="grid g3 mb">${ms.map(m => {
          const df = m.definition || {}, la = m.latest, unit = df.unitCode;
          const ser = (m.series || []).filter(s => s.value !== null && s.value !== undefined);
          const met = la ? la.met : null, tg = la && la.target !== null && la.target !== undefined ? la.target : df.targetValue;
          const badge = !la ? '<span class="badge gray">Chưa có số</span>' : la.value === null || la.value === undefined ? '<span class="badge orange">Thiếu dữ liệu</span>' : met === true ? '<span class="badge green">Đạt</span>' : met === false ? '<span class="badge red">Chưa đạt</span>' : '<span class="badge gray">Không có mục tiêu</span>';
          return `<div class="card panel"><div class="spread"><div><div class="eyebrow">${esc((DOMS.find(x => x[0] === df.domainCode) || [0, df.domainCode || ''])[1])}</div><b title="${esc(String(df.formula || '').replace(/s*[A-Z_]{6,}[^.]*./g, '').replace(/s*PROPOSED[^.]*.?/g, '').trim())}">${esc(df.metricName || '')}</b></div>${badge}</div>
            <div class="kpi-value">${la ? fmt(la.value, unit) : '–'}</div>
            <div class="kpi-foot">${tg !== null && tg !== undefined ? 'Mục tiêu ' + (df.targetDirection === 'LOWER' ? '≤ ' : '≥ ') + fmt(tg, unit) + ' · ' : ''}${la ? (monthly ? 'tháng ' + U.date(la.date).slice(3) : 'ngày ' + U.date(la.date)) : 'chưa làm mới'}${la && la.dataQualityStatus === 'INCOMPLETE' && la.value !== null && la.value !== undefined ? ' · <span class="warn">chưa đủ dữ liệu</span>' : ''}${df.hasCalculator === false ? ' · <span class="warn">chưa có công thức tính</span>' : ''}</div>
            
            ${ser.length > 1 ? UI.lines(ser.map(s => monthly ? U.date(s.date).slice(3, 8) : U.date(s.date).slice(0, 5)), [{ name: df.metricName, values: ser.map(s => s.value), color: met === false ? '#F07825' : '#91AF42' }], { h: 110, refs: tg !== null && tg !== undefined ? [{ v: tg, color: '#D95C4F', label: 'Mục tiêu' }] : [] }) : ''}</div>`;
        }).join('')}</div>` : '<div class="card panel empty">Chưa có chỉ tiêu nào đang dùng trong nhóm này</div>';
      });
    };
  }
  ['kpi-daily', 'kpi-monthly'].forEach((id, i) => { const base = APP.SCREENS[id]; if (base) APP.register(id, kpiLive(i === 1, base)); });

  // ---------- Kho tài liệu (#/adm-document): tải lên / tải xuống tệp đính kèm – docs/frontend-handoff/BE2.7.md ----------
  // Backend: tối đa 20 MB; chỉ nhận pdf, jpg, png, xlsx, docx. Tệp gửi multipart nên FormData đi thẳng qua Store.act (không qua objForm).
  const DOC_MAX = 20 * 1024 * 1024, DOC_EXT = ['pdf', 'jpg', 'jpeg', 'png', 'xlsx', 'docx'];
  APP.register('adm-document', (el, f) => {
    const perm = Store.perm(f.id);
    APP.genericList(el, f, { noAdd: Store.isLive });   // chế độ thật: thêm tài liệu = tải tệp lên (không nhập tay dòng DocumentFile)
    if (!Store.isLive) return;
    const bar = el.querySelector('.heading-actions');
    if (bar && perm.add !== false) {
      bar.insertAdjacentHTML('beforeend', `<button class="btn primary" data-act="upload">${I('plus')} Tải tệp lên</button><input type="file" data-file hidden accept="${DOC_EXT.map(x => '.' + x).join(',')}">`);
      const inp = bar.querySelector('[data-file]');
      bar.querySelector('[data-act="upload"]').onclick = () => inp.click();
      inp.onchange = () => {
        const file = inp.files && inp.files[0]; inp.value = '';
        if (!file) return;
        const ext = (file.name.split('.').pop() || '').toLowerCase();
        if (!DOC_EXT.includes(ext)) return UI.toast('Chỉ nhận tệp ' + DOC_EXT.join(', '), 'err');
        if (file.size > DOC_MAX) return UI.toast('Tệp vượt quá 20 MB', 'err');
        const fd = new FormData(); fd.append('File', file); fd.append('Category', 'other');
        Store.act('DocumentFile/Upload', { json: fd, tables: ['DocumentFile'] }).then(r => {
          if (!r.ok) return;
          UI.toast(r.data && r.data.isDuplicate ? 'Tệp này đã có trong kho tài liệu' : 'Đã tải lên ' + esc(file.name), r.data && r.data.isDuplicate ? 'warn' : 'ok');
          APP.refresh();
        });
      };
    }
    APP.rowExtra(el, tr => {
      const td = tr.querySelector('.actions'); if (!td || td.querySelector('[data-dl]')) return;
      td.insertAdjacentHTML('afterbegin', `<button class="btn xs" data-dl title="Tải xuống">${I('download')}</button> `);
      td.querySelector('[data-dl]').onclick = e => { e.stopPropagation(); Store.download('DocumentFile/Download', { id: tr.dataset.row }); };
    });
  });
})();
