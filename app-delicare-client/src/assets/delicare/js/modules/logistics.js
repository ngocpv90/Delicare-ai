/* P7 – Tuyến giao hàng, Thực hiện giao (READY→DELIVERED), POD, Khiếu nại */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const EX = ['READY', 'LOADED', 'DISPATCHED', 'ARRIVED', 'DELIVERED'];
  const ls = { date: null };
  // Bảng tải lại sau action giao hàng (backend ghi nhiều bảng trong một giao dịch).
  const LT = ['DeliveryRoute', 'DeliveryStop', 'DeliveryOrder', 'DeliveryExecution', 'DeliveryLoading', 'DeliveryEvent', 'ProofOfDeliverie', 'Order', 'CustomerComplaint', 'ComplaintHistory', 'TemperatureLog'];
  const stopOf = x => Store.one('DeliveryStop', 'DeliveryOrderId', x.DeliveryOrderId);
  // Hộp nhập nhiệt độ + hành động khắc phục; run(tc, ca) trả Promise { ok } – chỉ đóng hộp khi thành công.
  function tempPrompt(title, run) {
    UI.modal({ title, size: 'sm',
      body: `<div class="field"><label>Nhiệt độ thùng giữ nhiệt °C <code>TemperatureC</code></label><input type="number" step="0.1" class="input" id="tpC"></div><div class="field mt"><label>Hành động khắc phục (khi dưới ngưỡng) <code>CorrectiveAction</code></label><input class="input" id="tpA"></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="tpOk">Xác nhận</button>',
      onMount: (m, close) => m.querySelector('#tpOk').onclick = () => { const v = m.querySelector('#tpC').value; Promise.resolve(run(v === '' ? null : Number(v), m.querySelector('#tpA').value.trim())).then(r => { if (r && r.ok) close(); }); } });
  }
  // ================= CHẾ ĐỘ DỮ LIỆU THẬT – tiện ích dùng chung của module (docs/frontend-handoff/BE2.1.md, BE2.5.md, BE3.1.md, P8.md) =================
  const PT = ['PackingOrder', 'PackingResult', 'ProductionBatche', 'MealWasteRecord'];
  const CT = ['ContainerReturn', 'ReturnInspection', 'PackagingLedger', 'CleaningBatche'];
  // Đọc qua action riêng có nhớ tạm 30 giây: trả kết quả đã có (hoặc null khi đang tải) và tự vẽ lại màn hình khi có kết quả mới.
  const qc = {};
  function live(path, params) {
    const k = path + '|' + JSON.stringify(params || {}), c = qc[k];
    if (c && c.x && !c.busy && Date.now() - c.at < 30000) return c.x;
    if (!c || !c.busy) {
      const scr = APP.current, e = qc[k] = { busy: true, x: c ? c.x : null, at: 0 };
      Store.query(path, params).then(x => { e.busy = false; e.x = x; e.at = Date.now(); if (qc[k] === e && APP.current === scr && !document.querySelector('.overlay, .drawer-wrap')) APP.refresh(); });
    }
    return c ? c.x : null;
  }
  const bust = () => Object.keys(qc).forEach(k => delete qc[k]);
  // Tải trước các bảng mà hộp nhập liệu của màn hình cần (danh sách chọn), để bấm nút là có dữ liệu.
  const need = (...ts) => ts.forEach(t => Store.ensure(t));
  const warnHtml = w => (w || []).length ? '<br><span class="small">' + w.map(s => esc(s)).join('<br>') + '</span>' : '';
  // Sau thao tác ghi thành công: bỏ dữ liệu nhớ tạm, báo kết quả (kèm cảnh báo của máy chủ) và vẽ lại.
  const done = (x, msg) => { bust(); const w = (x.data && x.data.listWarning) || []; UI.toast(msg + warnHtml(w), w.length ? 'warn' : 'ok'); APP.refresh(); };
  const rowBtn = (td, cls, text, fn) => { const b = document.createElement('button'); b.className = 'btn xs ' + cls; b.textContent = text; b.onclick = e => { e.stopPropagation(); fn(); }; td.insertBefore(document.createTextNode(' '), td.firstChild); td.insertBefore(b, td.firstChild); };
  const headBtn = (el, html) => { const h = el.querySelector('.heading-actions'); if (h) h.insertAdjacentHTML('afterbegin', html); };
  const val = (m, id) => { const e = m.querySelector('#' + id); return e ? e.value.trim() : ''; };
  const numOr = v => v === '' ? null : Number(v);
  const opt = (v, l, sel) => `<option value="${esc(v)}" ${sel ? 'selected' : ''}>${esc(l)}</option>`;
  const fld = (label, html, full) => `<div class="field ${full ? 'full' : ''}"><label>${label}</label>${html}</div>`;
  const dtOf = (day, time) => time ? day + 'T' + time + ':00' : null;
  const distinct = (t, col, extra) => [...new Set((extra || []).concat(Store.rows(t).map(r => r[col]).filter(Boolean)))];
  const dataList = (id, vals) => `<datalist id="${id}">${vals.map(v => `<option value="${esc(v)}">`).join('')}</datalist>`;
  const orderLabel = o => (o.OrderNumber || o.OrderNo || '') + ' · ' + Store.label('Customer', o.CustomerId) + ' · ' + U.date(o.DeliveryDate || o.MealDate);
  const doLabel = d => (d.DeliveryNumber || d.DeliveryNo || '') + ' · ' + Store.label('Customer', d.CustomerId) + ' · ' + U.date(d.DeliveryDate);
  function dlg(title, body, ok, onOk, o = {}) {
    return UI.modal({ title, sub: o.sub, size: o.size === undefined ? 'sm' : o.size, body: `<div class="form-grid">${body}</div>`, foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="lgOk">${ok}</button>`,
      onMount: (m, close) => { m.querySelector('#lgOk').onclick = () => onOk(m, close); if (o.mount) o.mount(m); } });
  }

  // ---------- Đóng gói: PackingOrder/GetList · Create · Start · RecordResult · Cancel ----------
  const pk = { date: '' };
  function packCreate() {
    const bs = Store.rows('ProductionBatche').filter(b => b.Status === 'Passed'), os = Store.rows('Order').filter(o => ['Locked', 'InProduction'].includes(o.Status));
    if (!bs.length) return UI.toast('Chưa có mẻ nào đạt kiểm tra chất lượng để đóng gói', 'warn');
    if (!os.length) return UI.toast('Chưa có đơn hàng đã chốt / đang sản xuất để đóng gói', 'warn');
    dlg('Lập lệnh đóng gói',
      fld('Mẻ nấu đã đạt kiểm tra *', `<select class="select" id="pkB">${bs.map(b => opt(b.Id, (b.BatchNumber || b.BatchNo || '') + ' · ' + Store.label('Dishe', b.DishId) + ' · đạt ' + U.num(b.AcceptedQty) + ' phần')).join('')}</select>`, true) +
      fld('Đơn hàng *', `<select class="select" id="pkO">${os.map(o => opt(o.Id, orderLabel(o))).join('')}</select>`, true) +
      fld('Số phần đóng *', '<input type="number" min="1" class="input" id="pkQ">') + fld('Ngày đóng gói', `<input type="date" class="input" id="pkD" value="${esc(Store.ctx.date)}">`) + fld('Ghi chú', '<input class="input" id="pkN">', true),
      'Lập lệnh', (m, close) => {
        const q = Number(val(m, 'pkQ')); if (!(q > 0)) return UI.toast('Nhập số phần đóng lớn hơn 0', 'err');
        Store.act('PackingOrder/Create', { form: { ProductionBatchId: val(m, 'pkB'), OrderId: val(m, 'pkO'), PlannedQty: q, PackingDate: val(m, 'pkD'), Note: val(m, 'pkN') }, tables: PT }).then(x => { if (!x.ok) return; close(); done(x, 'Đã lập lệnh đóng gói'); });
      });
  }
  function packResult(r) {
    const plan = Number(r.PlannedQty) || 0;
    dlg('Ghi kết quả đóng gói', fld('Số phần đóng đạt *', `<input type="number" min="0" class="input" id="prP" value="${plan}">`) + fld('Số phần hỏng', '<input type="number" min="0" class="input" id="prD" value="0">') +
      fld('Số phần bị loại', '<input type="number" min="0" class="input" id="prR" value="0">') + fld('Loại khay / hộp', `<input class="input" id="prT" list="prTL">${dataList('prTL', distinct('PackingResult', 'PackagingType', ['KHAY']))}`) + fld('Ghi chú', '<input class="input" id="prN">', true),
      'Ghi kết quả', (m, close) => {
        const p = Number(val(m, 'prP')) || 0, d = Number(val(m, 'prD')) || 0, j = Number(val(m, 'prR')) || 0;
        if (p < 0 || d < 0 || j < 0) return UI.toast('Số lượng không được âm', 'err');
        if (Math.abs(p + d + j - plan) > 0.0001) return UI.toast(`Đạt + hỏng + loại phải bằng số kế hoạch (${U.num(plan)} phần)`, 'err');
        Store.act('PackingOrder/RecordResult', { form: { Id: r.Id, PackedQuantity: p, DamagedQty: d, RejectedQuantity: j, PackagingType: val(m, 'prT'), Note: val(m, 'prN') }, tables: PT }).then(x => { if (!x.ok) return; close(); done(x, 'Đã ghi kết quả đóng gói'); });
      }, { sub: esc(r.PackingOrderNumber || r.PackingNo || '') + ' · kế hoạch ' + U.num(plan) + ' phần' });
  }
  APP.register('log-packing-order', (el, f) => {
    if (!Store.isLive) return APP.genericList(el, f);
    const pm = Store.perm(f.id);
    need('ProductionBatche', 'Order', 'Customer', 'Dishe', 'PackingResult');
    const x = live('PackingOrder/GetList', { PackingDate: pk.date });
    const info = {}; if (x && x.ok) x.list.forEach(i => { if (i.packingOrder) info[i.packingOrder.id] = i; });
    const of = (r, k) => (info[r.Id] && info[r.Id][k]) || '';
    APP.genericList(el, f, { noAdd: true, noTransit: true,
      rows: () => Store.rows('PackingOrder').filter(r => !pk.date || (x && x.ok ? !!info[r.Id] : U.day(r.PackingDate) === pk.date)),
      cols: [{ k: 'PackingOrderNumber', label: 'Số lệnh' }, { k: 'ProductionBatchId', label: 'Mẻ nấu', render: r => esc(of(r, 'batchNumber') || Store.label('ProductionBatche', r.ProductionBatchId)), text: r => of(r, 'batchNumber') },
        { k: 'DishName', label: 'Món', nosort: true, render: r => esc(of(r, 'dishName')), text: r => of(r, 'dishName') },
        { k: 'OrderId', label: 'Đơn hàng', render: r => esc(of(r, 'orderNumber') || Store.label('Order', r.OrderId)) + (of(r, 'customerName') ? `<div class="small muted">${esc(of(r, 'customerName'))}</div>` : ''), text: r => of(r, 'orderNumber') + ' ' + of(r, 'customerName') },
        { k: 'PlannedQty', label: 'Số phần', num: true }, { k: 'PackingDate', label: 'Ngày đóng gói' }, { k: 'Status', label: 'Trạng thái' }],
      top: `<div class="card panel mb"><div class="row-flex"><span class="small muted">Ngày đóng gói:</span><input type="date" class="input" id="pkDate" value="${esc(pk.date)}" style="width:auto"><button class="chip ${pk.date ? '' : 'active'}" id="pkAll">Tất cả các ngày</button></div></div>` });
    el.querySelector('#pkDate').onchange = e => { pk.date = e.target.value; APP.refresh(); };
    el.querySelector('#pkAll').onclick = () => { pk.date = ''; APP.refresh(); };
    if (pm.add !== false) { headBtn(el, `<button class="btn primary" id="pkNew">${I('plus')} Lập lệnh đóng gói</button>`); el.querySelector('#pkNew').onclick = packCreate; }
    if (pm.edit !== false) APP.rowExtra(el, tr => {
      const r = Store.get('PackingOrder', tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td || td.dataset.live) return; td.dataset.live = '1';
      const no = esc(r.PackingOrderNumber || r.PackingNo || '');
      if (r.Status === 'Planned') rowBtn(td, 'danger', 'Hủy', () => UI.confirm(`Hủy lệnh đóng gói <b>${no}</b>? Số phần của lệnh sẽ trả về mẻ nấu.`, note => Store.act('PackingOrder/Cancel', { form: { Id: r.Id, Reason: note }, tables: PT }).then(x => { if (x.ok) done(x, 'Đã hủy lệnh đóng gói'); }), { input: 'Lý do', required: true, danger: true, ok: 'Hủy lệnh' }));
      if (['Planned', 'Packing'].includes(r.Status)) rowBtn(td, 'primary', 'Ghi kết quả', () => packResult(r));
      if (r.Status === 'Planned') rowBtn(td, 'orange', 'Bắt đầu', () => UI.confirm(`Bắt đầu đóng gói lệnh <b>${no}</b>?`, () => Store.act('PackingOrder/Start', { form: { Id: r.Id }, tables: PT }).then(x => { if (x.ok) done(x, 'Đã bắt đầu đóng gói'); }), { ok: 'Bắt đầu' }));
    });
  });
  APP.register('log-packing-result', (el, f) => {
    if (!Store.isLive) return APP.genericList(el, f);
    // Kết quả đóng gói chỉ ghi qua lệnh đóng gói (máy chủ chặn thêm trực tiếp).
    need('PackingOrder', 'Order');
    APP.genericList(el, f, { noAdd: true });
    if (Store.perm('log-packing-order').edit === false) return;
    headBtn(el, `<button class="btn primary" id="prNew">${I('plus')} Ghi kết quả đóng gói</button>`);
    el.querySelector('#prNew').onclick = () => {
      const open = Store.rows('PackingOrder').filter(r => ['Planned', 'Packing'].includes(r.Status));
      if (!open.length) return UI.toast('Không có lệnh đóng gói nào đang chờ ghi kết quả', 'warn');
      dlg('Chọn lệnh đóng gói', fld('Lệnh đóng gói *', `<select class="select" id="prO">${open.map(r => opt(r.Id, (r.PackingOrderNumber || r.PackingNo || '') + ' · ' + Store.label('Order', r.OrderId) + ' · ' + U.num(r.PlannedQty) + ' phần')).join('')}</select>`, true), 'Tiếp tục', (m, close) => { const r = Store.get('PackingOrder', val(m, 'prO')); close(); if (r) packResult(r); });
    };
  });

  // ---------- Đơn giao: DeliveryOrder/GetList · Create · Split · AssignRoute · Cancel ----------
  // d: { id, no, day, meal, routeId }
  const mealOf = r => U.sum(Store.by('DeliveryStop', 'DeliveryOrderId', r.Id), 'PlannedMealQty') || Number((/\[SUAT:([\d.]+)\]/.exec(r.Note || '') || [])[1]) || 0;
  const doOf = r => ({ id: r.Id, no: r.DeliveryNumber || r.DeliveryNo || '', day: U.day(r.DeliveryDate), meal: mealOf(r), routeId: r.DeliveryRouteId || null });
  function doCreate(date) {
    const all = Store.rows('Order').filter(o => ['Locked', 'InProduction'].includes(o.Status)), same = all.filter(o => U.day(o.DeliveryDate || o.MealDate) === date), os = same.length ? same : all;
    if (!os.length) return UI.toast('Chưa có đơn hàng đã chốt / đang sản xuất để lập đơn giao', 'warn');
    const locs = oid => { const o = Store.get('Order', oid) || {}; return opt('', 'Điểm giao của đơn hàng', true) + Store.by('CustomerDeliveryLocation', 'CustomerId', o.CustomerId).map(l => opt(l.Id, l.Name || l.Code || '')).join(''); };
    dlg('Lập đơn giao', fld('Đơn hàng *', `<select class="select" id="dcO">${os.map(o => opt(o.Id, orderLabel(o))).join('')}</select>`, true) + fld('Điểm giao', `<select class="select" id="dcL">${locs(os[0].Id)}</select>`, true) +
      fld('Số suất (trống = toàn bộ suất còn lại)', '<input type="number" min="1" class="input" id="dcQ">') + fld('Giờ giao dự kiến', '<input type="time" class="input" id="dcT">') + fld('Ghi chú', '<input class="input" id="dcN">', true),
      'Lập đơn giao', (m, close) => {
        const o = Store.get('Order', val(m, 'dcO')) || {}, q = val(m, 'dcQ');
        if (q !== '' && !(Number(q) > 0)) return UI.toast('Số suất phải lớn hơn 0', 'err');
        Store.act('DeliveryOrder/Create', { json: { orderId: o.Id, customerDeliveryLocationId: val(m, 'dcL') || null, plannedDeliveryTime: dtOf(U.day(o.DeliveryDate || o.MealDate) || date, val(m, 'dcT')), mealQuantity: numOr(q), listLine: [], note: val(m, 'dcN') || null }, tables: LT }).then(x => { if (!x.ok) return; close(); done(x, 'Đã lập đơn giao'); });
      }, { mount: m => { m.querySelector('#dcO').onchange = e => { m.querySelector('#dcL').innerHTML = locs(e.target.value); }; } });
  }
  function doAssign(d) {
    const rs = Store.rows('DeliveryRoute').filter(r => U.day(r.DeliveryDate) === d.day && r.Status === 'Planned' && r.Id !== d.routeId);
    if (!rs.length) return UI.toast('Không có chuyến nào còn ở trạng thái Kế hoạch trong ngày ' + U.date(d.day), 'warn');
    dlg((d.routeId ? 'Chuyển chuyến – ' : 'Gán chuyến – ') + esc(d.no), fld('Chuyến xe *', `<select class="select" id="daR">${rs.map(r => opt(r.Id, (r.RouteNo || '') + (r.Name ? ' · ' + r.Name : ''))).join('')}</select>`, true) + fld('Thứ tự điểm dừng (trống = cuối chuyến)', '<input type="number" min="1" class="input" id="daS">', true),
      d.routeId ? 'Chuyển chuyến' : 'Gán chuyến', (m, close) => Store.act('DeliveryOrder/AssignRoute', { form: { Id: d.id, DeliveryRouteId: val(m, 'daR'), SequenceNo: val(m, 'daS') }, tables: LT }).then(x => { if (!x.ok) return; close(); done(x, 'Đã gán đơn giao vào chuyến'); }));
  }
  function doSplit(d) {
    const row = i => `<div class="row-flex mb" data-part><span class="small muted" style="width:52px">Phần ${i}</span><input type="number" min="1" class="input" data-pq placeholder="Số suất" style="width:120px"><input type="time" class="input" data-pt style="width:auto" title="Giờ giao dự kiến"></div>`;
    UI.modal({ title: 'Tách đơn giao ' + esc(d.no), sub: d.meal ? 'Tổng ' + U.num(d.meal) + ' suất – tổng các phần phải bằng số này' : '', size: 'sm',
      body: `<div id="spList">${row(1)}${row(2)}</div><button class="btn sm" id="spAdd">${I('plus')} Thêm phần</button><div class="small muted mt">Phần 1 giữ số phiếu cũ; các phần sau thành đơn giao mới, chưa gán chuyến. Giờ giao có thể để trống.</div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="lgOk">Tách đơn giao</button>',
      onMount: (m, close) => {
        m.querySelector('#spAdd').onclick = () => m.querySelector('#spList').insertAdjacentHTML('beforeend', row(m.querySelectorAll('[data-part]').length + 1));
        m.querySelector('#lgOk').onclick = () => {
          const parts = [...m.querySelectorAll('[data-part]')].map(p => ({ q: Number(p.querySelector('[data-pq]').value) || 0, t: p.querySelector('[data-pt]').value })).filter(p => p.q || p.t);
          if (parts.length < 2 || parts.some(p => !(p.q > 0))) return UI.toast('Cần ít nhất 2 phần, mỗi phần có số suất lớn hơn 0', 'err');
          if (d.meal && Math.abs(U.sum(parts, 'q') - d.meal) > 0.0001) return UI.toast(`Tổng các phần phải bằng ${U.num(d.meal)} suất`, 'err');
          UI.confirm(`Tách đơn giao <b>${esc(d.no)}</b> thành ${parts.length} phần? Sau khi tách không gộp lại được.`, () => Store.act('DeliveryOrder/Split', { json: { id: d.id, listPart: parts.map(p => ({ mealQuantity: p.q, plannedDeliveryTime: dtOf(d.day, p.t), customerDeliveryLocationId: null })) }, tables: LT }).then(x => { if (!x.ok) return; close(); done(x, 'Đã tách đơn giao'); }), { ok: 'Tách', danger: true });
        };
      } });
  }
  const doCancel = d => UI.confirm(`Hủy đơn giao <b>${esc(d.no)}</b>? Đơn giao sẽ bị gỡ khỏi chuyến, số suất trả lại đơn hàng.`, note => Store.act('DeliveryOrder/Cancel', { form: { Id: d.id, Reason: note }, tables: LT }).then(x => { if (x.ok) done(x, 'Đã hủy đơn giao'); }), { input: 'Lý do', required: true, danger: true, ok: 'Hủy đơn giao' });
  APP.register('log-delivery-order', (el, f) => {
    if (!Store.isLive) return APP.genericList(el, f);
    const pm = Store.perm(f.id);
    // Đơn giao lập qua thao tác nghiệp vụ (máy chủ chặn thêm trực tiếp): tự sinh số phiếu, kiểm số suất còn lại của đơn hàng.
    need('Order', 'Customer', 'CustomerDeliveryLocation', 'DeliveryRoute', 'DeliveryStop', 'DeliveryLoading');
    APP.genericList(el, f, { noAdd: true, noTransit: true });
    if (pm.add !== false) { headBtn(el, `<button class="btn primary" id="doNew">${I('plus')} Lập đơn giao</button>`); el.querySelector('#doNew').onclick = () => doCreate(Store.ctx.date); }
    if (pm.edit !== false) APP.rowExtra(el, tr => {
      const r = Store.get('DeliveryOrder', tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td || td.dataset.live) return; td.dataset.live = '1';
      // Chỉ đơn giao Kế hoạch chưa xếp lên xe mới tách / chuyển chuyến / hủy được.
      if (r.Status !== 'Planned' || Store.one('DeliveryLoading', 'DeliveryOrderId', r.Id)) return;
      const d = doOf(r);
      rowBtn(td, 'danger', 'Hủy', () => doCancel(d)); rowBtn(td, '', 'Tách', () => doSplit(d)); rowBtn(td, 'orange', d.routeId ? 'Chuyển chuyến' : 'Gán chuyến', () => doAssign(d));
    });
  });

  // ---------- Chuyến xe: DeliveryRoute/Plan · Create · UpdateStops ----------
  const vehicles = () => Store.rows('Vehicle').filter(v => !/^(maintenance|inactive)$/i.test(v.Status || ''));
  const vehLabel = v => (v.LicensePlate || v.Code || '') + (v.VehicleType ? ' · ' + v.VehicleType : '') + (v.Capacity ? ' · ' + U.num(v.Capacity) + ' suất' : '');
  const driverOpts = () => opt('', '— Chưa chọn tài xế —', true) + Store.rows('Employee').slice(0, 400).map(e => opt(e.Id, Store.label('Employee', e))).join('');
  function routePlan(date) {
    const vs = vehicles(); if (!vs.length) return UI.toast('Chưa có xe nào sẵn sàng', 'warn');
    UI.modal({ title: 'Lập chuyến theo khu vực', sub: 'Ngày giao ' + U.date(date),
      body: `<p class="small muted" style="margin-top:0">Hệ thống gom các đơn đang sản xuất và đơn giao chưa gán chuyến của ngày theo khu vực, rồi chia cho các xe được chọn theo tải còn trống. Mỗi xe có hàng thành một chuyến.</p>
        <div class="table-wrap"><table class="table dc-table dt"><thead><tr><th></th><th>Xe</th><th>Tài xế</th></tr></thead><tbody>${vs.map(v => `<tr data-veh="${esc(v.Id)}"><td><input type="checkbox" data-vc></td><td>${esc(vehLabel(v))}</td><td><select class="select" data-vd>${driverOpts()}</select></td></tr>`).join('')}</tbody></table></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="lgOk">Lập chuyến</button>',
      onMount: (m, close) => m.querySelector('#lgOk').onclick = () => {
        const listVehicle = [...m.querySelectorAll('[data-veh]')].filter(tr => tr.querySelector('[data-vc]').checked).map(tr => ({ vehicleId: tr.dataset.veh, driverId: tr.querySelector('[data-vd]').value || null }));
        if (!listVehicle.length) return UI.toast('Chọn ít nhất một xe', 'err');
        Store.act('DeliveryRoute/Plan', { json: { deliveryDate: date + 'T00:00:00', shiftId: null, strategy: 'byArea', listVehicle }, tables: LT }).then(x => { if (!x.ok) return; close(); const n = ((x.data && x.data.listRoute) || []).length; done(x, n ? `Đã lập ${n} chuyến xe` : 'Không có đơn nào cần xếp chuyến'); });
      } });
  }
  function routeCreate(date) {
    const vs = vehicles(); if (!vs.length) return UI.toast('Chưa có xe nào sẵn sàng', 'warn');
    const box = (id, title) => `<div class="field full"><label>${title}</label><div id="${id}" class="small muted">Đang tải…</div></div>`;
    const checks = (list, attr, label) => list.length ? list.map(i => `<label style="display:block;padding:3px 0"><input type="checkbox" ${attr}="${esc(i.id)}"> ${label(i)}</label>`).join('') : 'Không có';
    dlg('Lập chuyến xe', fld('Xe *', `<select class="select" id="rcV">${vs.map(v => opt(v.Id, vehLabel(v))).join('')}</select>`) + fld('Tài xế', `<select class="select" id="rcD">${driverOpts()}</select>`) +
      fld('Giờ xuất phát dự kiến', '<input type="time" class="input" id="rcS">') + fld('Giờ kết thúc dự kiến', '<input type="time" class="input" id="rcE">') + fld('Tên chuyến', '<input class="input" id="rcN" maxlength="500">', true) +
      box('rcOrders', 'Đơn hàng chờ xếp chuyến') + box('rcDos', 'Đơn giao chưa gán chuyến') + '<div class="field full small muted">Không chọn dòng nào = lấy mọi đơn hàng chờ xếp chuyến của ngày.</div>',
      'Lập chuyến', (m, close) => {
        const ids = attr => [...m.querySelectorAll('[' + attr + ']')].filter(c => c.checked).map(c => c.getAttribute(attr));
        Store.act('DeliveryRoute/Create', { json: { deliveryDate: date + 'T00:00:00', shiftId: null, vehicleId: val(m, 'rcV'), driverId: val(m, 'rcD') || null, plannedStart: dtOf(date, val(m, 'rcS')), plannedEnd: dtOf(date, val(m, 'rcE')), name: val(m, 'rcN') || null, description: null, listOrderId: ids('data-ro'), listDeliveryOrderId: ids('data-rd') }, tables: LT })
          .then(x => { if (!x.ok) return; close(); done(x, 'Đã lập chuyến xe ' + esc((x.data && x.data.route && x.data.route.routeNo) || '')); });
      }, { size: '', sub: 'Ngày giao ' + U.date(date), mount: m => {
        Store.query('DeliveryRoute/GetReadyOrders', { DeliveryDate: date }).then(x => { const b = m.querySelector('#rcOrders'); if (!b || !b.isConnected) return; b.classList.remove('muted'); b.innerHTML = x.ok ? checks(x.list.map(o => Object.assign({ id: o.orderId }, o)), 'data-ro', o => `${esc(o.orderNumber || '')} · ${esc(o.customerName || '')}${o.locationName ? ' – ' + esc(o.locationName) : ''} · ${U.num(o.mealQuantity)} suất`) : esc(x.message || 'Không tải được'); });
        Store.query('DeliveryOrder/GetList', { DeliveryDate: date, Unassigned: true }).then(x => { const b = m.querySelector('#rcDos'); if (!b || !b.isConnected) return; b.classList.remove('muted'); b.innerHTML = x.ok ? checks(x.list.filter(i => i.deliveryOrder && i.deliveryOrder.status === 'Planned').map(i => Object.assign({ id: i.deliveryOrder.id }, i)), 'data-rd', i => `${esc(i.deliveryOrder.deliveryNumber || '')} · ${esc(i.customerName || '')}${i.locationName ? ' – ' + esc(i.locationName) : ''} · ${U.num(i.mealQuantity)} suất`) : esc(x.message || 'Không tải được'); });
      } });
  }
  function routeStops(r) {
    const stops = Store.by('DeliveryStop', 'DeliveryRouteId', r.Id).filter(s => s.DeliveryOrderId).sort((a, b) => a.SequenceNo - b.SequenceNo);
    if (!stops.length) return UI.toast('Chuyến chưa có điểm dừng', 'warn');
    UI.modal({ title: 'Sắp xếp điểm dừng – ' + esc(r.RouteNo || ''), sub: 'Đổi thứ tự và giờ đến dự kiến. Điểm có số thứ tự lớn nhất là điểm cuối.',
      body: `<div class="table-wrap"><table class="table dc-table dt"><thead><tr><th style="width:90px">Thứ tự</th><th>Điểm giao</th><th class="num">Suất</th><th style="width:130px">Giờ đến dự kiến</th></tr></thead><tbody>${stops.map(s => `<tr data-stop="${esc(s.Id)}"><td><input type="number" min="1" class="input" data-sq value="${esc(s.SequenceNo)}"></td><td>${esc(Store.label('CustomerDeliveryLocation', s.DeliveryLocationId))}</td><td class="num">${U.num(s.PlannedMealQty)}</td><td><input type="time" class="input" data-sa value="${esc(U.time(s.PlannedArrival))}"></td></tr>`).join('')}</tbody></table></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="lgOk">Lưu thứ tự</button>',
      onMount: (m, close) => m.querySelector('#lgOk').onclick = () => {
        const listStop = stops.map(s => { const tr = m.querySelector(`[data-stop="${s.Id}"]`); return { deliveryOrderId: s.DeliveryOrderId, sequenceNo: Number(tr.querySelector('[data-sq]').value), plannedArrival: dtOf(U.day(s.PlannedArrival) || U.day(r.DeliveryDate), tr.querySelector('[data-sa]').value) }; });
        if (listStop.some(s => !Number.isInteger(s.sequenceNo) || s.sequenceNo < 1)) return UI.toast('Thứ tự phải là số nguyên từ 1', 'err');
        if (new Set(listStop.map(s => s.sequenceNo)).size !== listStop.length) return UI.toast('Thứ tự điểm dừng không được trùng nhau', 'err');
        Store.act('DeliveryRoute/UpdateStops', { method: 'PUT', json: { id: r.Id, listStop }, tables: LT }).then(x => { if (!x.ok) return; close(); done(x, 'Đã lưu thứ tự điểm dừng'); });
      } });
  }
  // Tài xế ghi vị trí hiện tại của xe (sổ sự kiện giao – chỉ ghi thêm). Chỉ khi chuyến Đang xếp / Đang giao.
  function recordPosition(routeId) {
    if (!navigator.geolocation) return UI.toast('Thiết bị không hỗ trợ định vị', 'err');
    UI.toast('Đang lấy vị trí của thiết bị…');
    navigator.geolocation.getCurrentPosition(
      p => Store.act('DeliveryEvent/RecordPosition', { form: { RouteId: routeId, Latitude: p.coords.latitude.toFixed(6), Longitude: p.coords.longitude.toFixed(6) }, tables: ['DeliveryEvent'] }).then(x => { if (x.ok) UI.toast('Đã ghi vị trí xe', 'ok'); }),
      e => UI.toast(e && e.code === 1 ? 'Chưa cho phép trình duyệt dùng vị trí của thiết bị' : 'Không lấy được vị trí của thiết bị', 'err'),
      { enableHighAccuracy: true, timeout: 15000, maximumAge: 30000 });
  }

  // ---------- Tuyến giao hàng ----------
  APP.register('log-route', (el, f) => {
    const date = ls.date || Store.ctx.date; ls.date = date;
    const routes = Store.rows('DeliveryRoute').filter(r => U.day(r.DeliveryDate) === date);
    const dates = [...new Set(Store.rows('DeliveryRoute').map(r => U.day(r.DeliveryDate)))].sort().slice(-4);
    const pm = Store.perm(f.id);
    // Chế độ API: DeliveryRoute/GetBoard – tải, sức chở, tài xế, điều kiện xuất bến do máy chủ tính; DeliveryOrder/GetList – đơn giao của ngày.
    if (Store.isLive) need('Vehicle', 'Employee', 'Order', 'Customer', 'CustomerDeliveryLocation', 'DeliveryOrder', 'DeliveryStop');
    const bd = Store.isLive ? live('DeliveryRoute/GetBoard', { DeliveryDate: date }) : null, bm = {};
    if (bd && bd.ok && bd.data) (bd.data.listRoute || []).forEach(x => { if (x.route) bm[x.route.id] = x; });
    const dl = Store.isLive ? live('DeliveryOrder/GetList', { DeliveryDate: date }) : null;
    const dos = dl && dl.ok ? dl.list.filter(i => i.deliveryOrder) : [];
    const can = (i, a) => (i.allowedActions || []).some(z => String(z).toLowerCase() === a.toLowerCase());
    el.innerHTML = APP.header(f, `${Store.isLive && pm.add !== false ? `<button class="btn" id="rtPlan">${I('route')} Lập chuyến theo khu vực</button><button class="btn primary" id="rtNew">${I('plus')} Lập chuyến</button>` : ''}<a class="btn" href="#/log-execution">${I('truck')} Theo dõi giao hàng</a><a class="btn" href="#/log-gps">${I('pin')} Bản đồ${Store.isLive ? '' : ' (GAP)'}</a>`) + `
    <div class="card panel mb"><div class="row-flex"><span class="small muted">Ngày giao:</span>${dates.map(d => `<button class="chip ${d === date ? 'active' : ''}" data-d="${d}">${U.date(d)}${d === Store.ctx.date ? ' · hôm nay' : ''}</button>`).join('')}${Store.isLive ? `<input type="date" class="input" id="rtDate" value="${esc(date)}" style="width:auto">` : ''}</div></div>
    <div class="notice warn">${I('alert')}<div><b>GAP-C-02:</b> BRD yêu cầu điểm đầu/cuối tuyến – SQL chưa có cột. Mockup coi bếp trung tâm là điểm đầu và điểm dừng cuối (DeliveryStop.SequenceNo lớn nhất) là điểm cuối.</div></div>
    <div class="grid g2">${routes.map(r => { const stops = Store.by('DeliveryStop', 'DeliveryRouteId', r.Id).sort((a, b) => a.SequenceNo - b.SequenceNo); const v = Store.get('Vehicle', r.VehicleId); const b = bm[r.Id]; const meals = b && b.loadMeals != null ? Number(b.loadMeals) : U.sum(stops, 'PlannedMealQty'); const cap = b && b.capacity != null ? Number(b.capacity) : (v ? v.Capacity : 0); const blk = b && ['Planned', 'Loading'].includes(r.Status) ? (b.listBlocker || []) : []; return `<div class="card panel"><div class="spread"><div><div class="eyebrow">${esc(r.RouteNo)}</div><h2>${esc(r.Name)}</h2><div class="small muted">${esc(v ? v.LicensePlate + ' · ' + v.VehicleType : '–')} · Tài xế: ${esc(b && b.driverName ? b.driverName : Store.label('Employee', r.DriverId))}</div></div>${UI.badge(r.Status, 'DeliveryRoute', 'Status')}</div>
      <div class="progress-row mt"><span class="name">Tải trọng: ${U.num(meals)} / ${U.num(cap)} suất</span><span class="value">${cap ? U.pct(meals / cap * 100, 0) : '–'}</span><div class="track"><div class="fill ${meals > cap ? 'red' : meals > cap * .85 ? 'orange' : ''}" style="width:${Math.min(100, cap ? meals / cap * 100 : 0)}%"></div></div></div>
      <div class="timeline mt"><div class="tl blue"><b>Bếp trung tâm (điểm đầu)</b><small>KH xuất phát ${U.time(r.PlannedStart)}${r.ActualStart ? ' · TT ' + U.time(r.ActualStart) : ''}</small></div>${stops.map(s => `<div class="tl ${s.StopStatus === 'Delivered' ? '' : 'orange'}"><b>${s.SequenceNo}. ${esc(Store.label('CustomerDeliveryLocation', s.DeliveryLocationId))}</b><small>${U.num(s.PlannedMealQty)} suất · đến KH ${U.time(s.PlannedArrival)} · ${UI.statusVi('DeliveryStop', 'StopStatus', s.StopStatus)}</small></div>`).join('')}</div>
      ${blk.length ? `<div class="small bad mt">Chưa đủ điều kiện xuất bến: ${blk.map(s => esc(s)).join(' · ')}</div>` : ''}
      <div class="row-flex mt">${r.Status === 'Planned' ? `<button class="btn sm orange" data-load="${r.Id}">Xếp hàng lên xe</button>` : ''}${r.Status === 'Loading' ? `<button class="btn sm primary" data-dep="${r.Id}">${I('play')} Xuất bến</button>` : ''}${Store.isLive && r.Status === 'Planned' && pm.edit !== false && stops.length ? `<button class="btn sm" data-stops="${r.Id}">${I('edit')} Sắp điểm dừng</button>` : ''}<button class="btn sm" data-open="DeliveryRoute" data-id="${r.Id}">${I('eye')} Chi tiết</button>${Store.isLive ? `<button class="btn sm" data-prt="${r.Id}">${I('export')} In phiếu giao (PDF)</button><button class="btn sm" data-note="${r.Id}">${I('export')} Biên bản giao hàng (Excel)</button>` : ''}</div></div>`; }).join('') || '<div class="card panel empty">Không có tuyến trong ngày</div>'}</div>
    ${Store.isLive ? `<div class="card panel mt"><div class="panel-head"><div><div class="panel-title">Đơn giao trong ngày ${U.date(date)}</div><div class="panel-desc">Gán đơn giao vào chuyến còn ở trạng thái kế hoạch, tách đơn giao thành nhiều phần hoặc hủy khi chưa xếp lên xe.</div></div>${Store.perm('log-delivery-order').add !== false ? `<button class="btn sm primary" id="doNew">${I('plus')} Lập đơn giao</button>` : ''}</div>
      ${!dl ? '<div class="empty">Đang tải…</div>' : !dl.ok ? `<div class="empty">Không tải được danh sách đơn giao: ${esc(dl.message || '')}</div>` : !dos.length ? '<div class="empty">Chưa có đơn giao trong ngày</div>' : `<div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Số phiếu</th><th>Khách hàng / điểm giao</th><th class="num">Số suất</th><th>Chuyến xe</th><th>Trạng thái</th><th></th></tr></thead><tbody>${dos.map((i, n) => { const d = i.deliveryOrder; return `<tr><td><b>${esc(d.deliveryNumber || '')}</b><div class="small muted">${esc(i.orderNumber || '')}</div></td><td>${esc(i.customerName || '')}<div class="small muted">${esc(i.locationName || '')}${i.area ? ' · ' + esc(i.area) : ''}</div></td><td class="num">${U.num(i.mealQuantity)}</td><td>${i.routeNo ? esc(i.routeNo) + (i.sequenceNo ? ' · điểm ' + esc(i.sequenceNo) : '') : '<span class="badge orange">Chưa gán chuyến</span>'}</td><td>${i.statusName ? `<span class="badge ${d.status === 'Planned' ? 'gray' : d.status === 'Delivered' ? 'green' : d.status === 'Failed' || d.status === 'Cancelled' ? 'red' : 'blue'}">${esc(i.statusName)}</span>` : UI.badge(d.status)}${i.isLoaded ? ' <span class="small muted">đã xếp xe</span>' : ''}</td><td class="actions">${Store.perm('log-delivery-order').edit === false ? '' : `${can(i, 'assignRoute') ? `<button class="btn xs orange" data-doa="assign" data-i="${n}">${i.deliveryRouteId ? 'Chuyển chuyến' : 'Gán chuyến'}</button> ` : ''}${can(i, 'split') ? `<button class="btn xs" data-doa="split" data-i="${n}">Tách</button> ` : ''}${can(i, 'cancel') ? `<button class="btn xs danger" data-doa="cancel" data-i="${n}">Hủy</button>` : ''}`}</td></tr>`; }).join('')}</tbody></table></div>`}</div>` : ''}`;
    el.querySelectorAll('[data-d]').forEach(b => b.onclick = () => { ls.date = b.dataset.d; APP.refresh(); });
    if (Store.isLive) {
      const di = el.querySelector('#rtDate'); if (di) di.onchange = e => { if (e.target.value) { ls.date = e.target.value; APP.refresh(); } };
      const bp = el.querySelector('#rtPlan'); if (bp) bp.onclick = () => routePlan(date);
      const bn = el.querySelector('#rtNew'); if (bn) bn.onclick = () => routeCreate(date);
      const bo = el.querySelector('#doNew'); if (bo) bo.onclick = () => doCreate(date);
      el.querySelectorAll('[data-stops]').forEach(b => b.onclick = () => routeStops(Store.get('DeliveryRoute', b.dataset.stops)));
      // DeliveryRoute/ExportDeliveryNote: biên bản giao hàng của cả chuyến (tệp Excel).
      el.querySelectorAll('[data-note]').forEach(b => b.onclick = () => Store.download('DeliveryRoute/ExportDeliveryNote', { id: b.dataset.note }).then(x => { if (x.ok) UI.toast('Đã xuất ' + esc(x.fileName || 'biên bản giao hàng'), 'ok'); }));
      el.querySelectorAll('[data-doa]').forEach(b => b.onclick = () => { const i = dos[+b.dataset.i], o = i.deliveryOrder; const d = { id: o.id, no: o.deliveryNumber || '', day: U.day(o.deliveryDate) || date, meal: Number(i.mealQuantity) || 0, routeId: i.deliveryRouteId || null }; ({ assign: doAssign, split: doSplit, cancel: doCancel })[b.dataset.doa](d); });
    }
    // Common/PrintBatch: một tệp PDF gồm mọi phiếu giao của tuyến (mỗi phiếu một trang).
    el.querySelectorAll('[data-prt]').forEach(b => b.onclick = () => Store.download('Common/PrintBatch', null, { deliveryRouteId: b.dataset.prt }));
    el.querySelectorAll('[data-load]').forEach(b => b.onclick = () => { const r = Store.get('DeliveryRoute', b.dataset.load); const v = Store.get('Vehicle', r.VehicleId);
      // Chế độ API: DeliveryRoute/Load – backend ghi DeliveryLoading + DeliveryExecution và kiểm tra lệnh đóng gói.
      if (Store.isLive) return Store.act('DeliveryRoute/Load', { json: { id: r.Id, listItem: Store.by('DeliveryOrder', 'DeliveryRouteId', r.Id).map(d => ({ deliveryOrderId: d.Id, loadedQty: U.sum(Store.by('DeliveryStop', 'DeliveryOrderId', d.Id), 'PlannedMealQty') || null })) }, tables: LT }).then(x => { if (x.ok) { bust(); UI.toast('Đã xếp hàng lên xe', 'ok'); APP.refresh(); } });
Store.by('DeliveryOrder', 'DeliveryRouteId', r.Id).forEach(d => { if (!Store.one('DeliveryLoading', 'DeliveryOrderId', d.Id)) Store.add('DeliveryLoading', { DeliveryRouteId: r.Id, DeliveryOrderId: d.Id, LoadedQty: U.sum(Store.by('DeliveryStop', 'DeliveryOrderId', d.Id), 'PlannedMealQty'), LoadingTime: U.iso(new Date()), LoadingDate: U.iso(new Date()), VehicleCode: v && v.Code, Status: 'Loaded' }); if (!Store.one('DeliveryExecution', 'DeliveryOrderId', d.Id)) Store.add('DeliveryExecution', { DeliveryOrderId: d.Id, DeliveryRouteId: r.Id, Status: 'LOADED', VehicleCode: v && v.Code, DriverName: Store.label('Employee', r.DriverId) }); }); Store.update('DeliveryRoute', r.Id, { Status: 'Loading' }); UI.toast('Đã xếp hàng – DeliveryLoading + DeliveryExecution (LOADED). GAP-C-03: chưa liên kết PackingOrder.', 'ok'); APP.refresh(); });
    // (data-dep: core gắn sẵn [data-go] cho điều hướng nên không dùng tên đó cho nút Xuất bến.)
    el.querySelectorAll('[data-dep]').forEach(b => b.onclick = () => { const r = Store.get('DeliveryRoute', b.dataset.dep);
      // Chế độ API: DeliveryRoute/Dispatch – ghi nhiệt độ lúc xuất bến (backend chặn khi dưới ngưỡng mà không có hành động khắc phục).
      if (Store.isLive) return tempPrompt('Xuất bến tuyến ' + esc(r.RouteNo), (tc, ca) => Store.act('DeliveryRoute/Dispatch', { form: { Id: r.Id, TemperatureC: tc, CorrectiveAction: ca }, tables: LT }).then(x => { if (x.ok) { bust(); UI.toast('Tuyến đã xuất bến', 'ok'); APP.refresh(); } return x; }));
      Store.update('DeliveryRoute', r.Id, { Status: 'InTransit', ActualStart: U.iso(new Date()) }); Store.by('DeliveryExecution', 'DeliveryRouteId', r.Id).forEach(x => { if (['READY', 'LOADED'].includes(x.Status)) { Store.update('DeliveryExecution', x.Id, { Status: 'DISPATCHED', DispatchedAt: U.iso(new Date()), DepartedAt: U.iso(new Date()) }); Store.add('DeliveryEvent', { DeliveryRouteId: r.Id, DeliveryOrderId: x.DeliveryOrderId, EventType: 'DISPATCHED', EventAt: U.iso(new Date()), Latitude: 21.1388, Longitude: 105.8464, Description: 'Xe xuất bến', SourceChannel: 'WEB' }); } }); UI.toast('Tuyến đã xuất bến – DeliveryEvent DISPATCHED', 'ok'); APP.refresh(); });
  });
  // ---------- Thực hiện giao hàng ----------
  APP.register('log-execution', (el, f) => {
    const date = ls.date || Store.ctx.date;
    // Chế độ API: DeliveryExecution/GetBoard – đơn giao của ngày (bỏ đơn đã hủy), đếm theo trạng thái, xe / tài xế / số suất do máy chủ ghép.
    const bd = Store.isLive ? live('DeliveryExecution/GetBoard', { DeliveryDate: date }) : null, bi = {};
    const board = bd && bd.ok && bd.data ? bd.data : null, sc = board ? {} : null;
    if (board) { (board.listItem || []).forEach(i => { bi[i.deliveryOrderId] = i; }); Object.entries(board.statusCounts || {}).forEach(([k, n]) => { sc[String(k).toUpperCase()] = n; }); }
    const dvs = Store.rows('DeliveryOrder').filter(d => U.day(d.DeliveryDate) === date && (!board || bi[d.Id]));
    const ex = dvs.map(d => ({ d, x: Store.one('DeliveryExecution', 'DeliveryOrderId', d.Id) }));
    const cnt = U.group(ex.filter(e => e.x), e => e.x.Status);
    if (sc) EX.concat('FAILED').forEach(s => { cnt[s] = { length: sc[s] || 0 }; });
    el.innerHTML = APP.header(f, `${Store.isLive ? `<input type="date" class="input" id="exDate" value="${esc(date)}" style="width:auto">` : ''}<a class="btn" href="#/log-route">${I('route')} Tuyến giao</a>`, { sub: `Luồng 7 – trạng thái theo BRD: READY → LOADED → DISPATCHED → ARRIVED → DELIVERED / FAILED. Ngày ${U.date(date)}.` }) + `
    <div class="kpis">${EX.concat('FAILED').map((s, i) => UI.kpi(UI.statusVi('DeliveryExecution', 'Status', s), (cnt[s] || []).length, '', ['clipboard', 'package', 'truck', 'pin', 'ok', 'alert'][i], ['gray', 'blue', 'blue', 'orange', 'green', 'red'][i])).join('')}</div>
    <div class="grid g2">${ex.map(({ d, x }) => { const st = x ? x.Status : null; const i = st ? EX.indexOf(st) : -1; const loc = Store.get('CustomerDeliveryLocation', d.CustomerDeliveryLocationId) || {}; const pod = Store.one('ProofOfDeliverie', 'DeliveryOrderId', d.Id); const b = bi[d.Id]; const rt = d.DeliveryRouteId ? Store.get('DeliveryRoute', d.DeliveryRouteId) : null; return `<div class="card panel"><div class="spread"><div><b>${esc(d.DeliveryNumber)}</b> · <span class="small">${esc(loc.Name || '')}</span><div class="small muted">Khung nhận ${U.time(loc.DeliveryWindowFrom)}–${U.time(loc.DeliveryWindowTo)} · ${esc(Store.label('DeliveryRoute', d.DeliveryRouteId))}</div>${b ? `<div class="small muted">${esc(b.customerName || '')} · ${U.num(b.mealQuantity)} suất${b.vehicleCode ? ' · xe ' + esc(b.vehicleCode) : ''}${b.driverName ? ' · tài xế ' + esc(b.driverName) : ''}</div>` : ''}</div>${st ? UI.badge(st, 'DeliveryExecution', 'Status') : `<span class="badge gray">${esc(b && b.statusName ? b.statusName : 'Chưa xếp xe')}</span>`}</div>
      <div class="mt">${UI.stepper(EX.map(s => ({ l: UI.statusVi('DeliveryExecution', 'Status', s) })), st === 'FAILED' ? 3 : i, st === 'FAILED')}</div>
      <div class="row-flex mt">${x && st === 'DISPATCHED' ? `<button class="btn sm orange" data-arr="${x.Id}">${I('pin')} Đã đến điểm giao</button>` : ''}${x && st === 'ARRIVED' ? `<button class="btn sm primary" data-pod="${x.Id}">${I('ok')} Bàn giao & ghi POD</button><button class="btn sm danger" data-fail="${x.Id}">Giao thất bại</button>` : ''}${pod ? `<span class="small">POD: nhận ${U.num(pod.AcceptedQuantity)}/${U.num(pod.DeliveredQuantity)} · ${esc(pod.ReceiverName)}</span>` : ''}${!x ? '<a class="btn sm" href="#/log-route">Xếp xe tại Tuyến giao →</a>' : ''}<button class="btn sm" data-tl="${d.Id}">${I('history')} Sự kiện</button>${Store.isLive ? `<button class="btn sm" data-pdo="${d.Id}">${I('export')} In phiếu</button>${pod ? `<button class="btn sm" data-ppod="${pod.Id}">${I('export')} In POD</button>` : ''}${rt && ['Loading', 'InTransit'].includes(rt.Status) && !['DELIVERED', 'FAILED'].includes(st) ? `<button class="btn sm" data-gps="${rt.Id}">${I('pin')} Ghi vị trí hiện tại</button>` : ''}` : ''}</div></div>`; }).join('') || '<div class="card panel empty">Không có đơn giao</div>'}</div>`;
    if (Store.isLive) {
      const di = el.querySelector('#exDate'); if (di) di.onchange = e => { if (e.target.value) { ls.date = e.target.value; APP.refresh(); } };
      // DeliveryEvent/RecordPosition: tài xế gửi vị trí của thiết bị cho chuyến đang xếp / đang giao.
      el.querySelectorAll('[data-gps]').forEach(b => b.onclick = () => recordPosition(b.dataset.gps));
    }
    el.querySelectorAll('[data-arr]').forEach(b => b.onclick = () => { const x = Store.get('DeliveryExecution', b.dataset.arr); const loc = Store.get('CustomerDeliveryLocation', Store.get('DeliveryOrder', x.DeliveryOrderId).CustomerDeliveryLocationId) || {};
      if (Store.isLive) { const st = stopOf(x); if (!st) return UI.toast('Đơn giao chưa có điểm dừng (DeliveryStop) trên tuyến', 'err'); return Store.act('DeliveryStop/Arrive', { form: { Id: st.Id, Latitude: loc.Latitude, Longitude: loc.Longitude }, tables: LT }).then(r => { if (r.ok) { bust(); UI.toast('Đã đến điểm giao', 'ok'); APP.refresh(); } }); }
      Store.update('DeliveryExecution', x.Id, { Status: 'ARRIVED', ArrivedAt: U.iso(new Date()) }); Store.add('DeliveryEvent', { DeliveryRouteId: x.DeliveryRouteId, DeliveryOrderId: x.DeliveryOrderId, EventType: 'ARRIVED', EventAt: U.iso(new Date()), Latitude: loc.Latitude, Longitude: loc.Longitude, TemperatureC: 66.5, Description: 'Đến điểm giao', SourceChannel: 'MOBILE' }); const s = Store.one('DeliveryStop', 'DeliveryOrderId', x.DeliveryOrderId); if (s) Store.update('DeliveryStop', s.Id, { StopStatus: 'Arrived', ActualArrival: U.iso(new Date()) }); UI.toast('Đã đến – DeliveryEvent ARRIVED (có tọa độ)', 'ok'); APP.refresh(); });
    el.querySelectorAll('[data-pod]').forEach(b => b.onclick = () => podForm(b.dataset.pod));
    el.querySelectorAll('[data-pdo]').forEach(b => b.onclick = () => Store.download('DeliveryOrder/Print', { id: b.dataset.pdo }));
    el.querySelectorAll('[data-ppod]').forEach(b => b.onclick = () => Store.download('ProofOfDelivery/Print', { id: b.dataset.ppod }));
    el.querySelectorAll('[data-fail]').forEach(b => b.onclick = () => UI.confirm('Ghi nhận giao thất bại?', note => { const x = Store.get('DeliveryExecution', b.dataset.fail);
      if (Store.isLive) { const st = stopOf(x); if (!st) return UI.toast('Đơn giao chưa có điểm dừng (DeliveryStop) trên tuyến', 'err'); return Store.act('DeliveryStop/Fail', { form: { Id: st.Id, Reason: note }, tables: LT }).then(r => { if (r.ok) { bust(); UI.toast('Đã ghi giao thất bại – cần điều phối lại', 'warn'); APP.refresh(); } }); }
      Store.update('DeliveryExecution', x.Id, { Status: 'FAILED', Remark: note }); Store.add('DeliveryEvent', { DeliveryRouteId: x.DeliveryRouteId, DeliveryOrderId: x.DeliveryOrderId, EventType: 'FAILED', EventAt: U.iso(new Date()), Description: note, SourceChannel: 'WEB' }); UI.toast('Đã ghi FAILED – cần điều phối lại', 'warn'); APP.refresh(); }, { input: 'Lý do', required: true, danger: true }));
    el.querySelectorAll('[data-tl]').forEach(b => b.onclick = () => { const ev = Store.by('DeliveryEvent', 'DeliveryOrderId', b.dataset.tl).sort((a, c) => a.EventAt.localeCompare(c.EventAt)); UI.modal({ title: 'Sự kiện giao hàng', sub: 'dbo.DeliveryEvent', body: `<div class="timeline">${ev.map(e => `<div class="tl ${e.EventType === 'FAILED' ? 'red' : ''}"><b>${UI.badge(e.EventType, 'DeliveryExecution', 'Status')} ${esc(e.Description || '')}</b><small>${U.dt(e.EventAt)} · ${e.Latitude ? U.num(e.Latitude, 4) + ', ' + U.num(e.Longitude, 4) : 'không có tọa độ'}${e.TemperatureC ? ' · ' + U.num(e.TemperatureC) + '°C' : ''} · ${esc(e.SourceChannel || '')}</small></div>`).join('') || '<div class="empty">Chưa có sự kiện</div>'}</div>` }); });
  });
  function podForm(xid) {
    const x = Store.get('DeliveryExecution', xid), d = Store.get('DeliveryOrder', x.DeliveryOrderId), loc = Store.get('CustomerDeliveryLocation', d.CustomerDeliveryLocationId) || {};
    const q = U.sum(Store.by('DeliveryStop', 'DeliveryOrderId', d.Id), 'PlannedMealQty');
    UI.modal({ title: 'Xác nhận giao hàng (POD)', sub: esc(d.DeliveryNumber) + ' · dbo.ProofOfDeliverie', size: 'sm',
      body: `<div class="form-grid"><div class="field"><label>SL giao <code>DeliveredQuantity</code></label><input type="number" class="input" id="pq" value="${q}"></div><div class="field"><label>SL khách nhận <code>AcceptedQuantity</code></label><input type="number" class="input" id="pa" value="${q}"></div><div class="field full"><label>Người nhận <code>ReceiverName</code> *</label><input class="input" id="pn" value="${esc(loc.ContactName || '')}"></div><div class="field full"><label>Chữ ký / ảnh <code>SignatureUrl · PhotoUrl</code></label><div class="row-flex"><span class="tag green">✓ Chữ ký (demo)</span><span class="tag green">✓ Ảnh bàn giao (demo)</span></div></div><div class="field full"><label>Ghi chú <code>Remark</code></label><input class="input" id="pr"></div>${Store.isLive ? `<div class="field"><label>Nhiệt độ khi giao °C <code>TemperatureC</code></label><input type="number" step="0.1" class="input" id="pt"></div><div class="field"><label>Khắc phục (nếu dưới ngưỡng) <code>CorrectiveAction</code></label><input class="input" id="pc"></div>` : ''}</div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="pOk">Xác nhận POD</button>',
      onMount: (m, close) => m.querySelector('#pOk').onclick = () => {
        const dq = +m.querySelector('#pq').value, aq = +m.querySelector('#pa').value, rn = m.querySelector('#pn').value.trim();
        if (!rn) return UI.toast('Bắt buộc nhập người nhận', 'err'); if (aq > dq) return UI.toast('SL nhận không được lớn hơn SL giao', 'err');
        if (Store.isLive) {
          // DeliveryStop/Deliver: backend ghi POD, sự kiện, cập nhật đơn giao / đơn hàng và tự lập phản ánh khi khách từ chối suất.
          const st = stopOf(x); if (!st) return UI.toast('Đơn giao chưa có điểm dừng (DeliveryStop) trên tuyến', 'err');
          const tv = m.querySelector('#pt').value;
          return Store.act('DeliveryStop/Deliver', { form: { Id: st.Id, DeliveredQuantity: dq, AcceptedQuantity: aq, ReceiverName: rn, ReceiverPhone: loc.ContactPhone, Remark: m.querySelector('#pr').value, TemperatureC: tv === '' ? null : Number(tv), CorrectiveAction: m.querySelector('#pc').value.trim() }, tables: LT })
            .then(r => { if (!r.ok) return; bust(); close(); UI.toast('Đã ghi POD – DELIVERED', 'ok'); APP.refresh(); });
        }
        const now = U.iso(new Date());
        Store.add('ProofOfDeliverie', { DeliveryOrderId: d.Id, DeliveredQuantity: dq, AcceptedQuantity: aq, RejectedQuantity: dq - aq, ReceiverName: rn, ReceiverPhone: loc.ContactPhone, ReceivedAt: now, SignatureUrl: 'demo://signature/' + d.DeliveryNumber, PhotoUrl: 'demo://photo/' + d.DeliveryNumber, Remark: m.querySelector('#pr').value, ConfirmedAt: now });
        Store.update('DeliveryExecution', x.Id, { Status: 'DELIVERED', DeliveredAt: now }); Store.update('DeliveryOrder', d.Id, { Status: 'DELIVERED', ActualDeliveryTime: now });
        const s = Store.one('DeliveryStop', 'DeliveryOrderId', d.Id); if (s) Store.update('DeliveryStop', s.Id, { StopStatus: 'Delivered', DeliveredMealQty: aq, ShortageQty: Math.max(0, s.PlannedMealQty - aq) });
        Store.add('DeliveryEvent', { DeliveryRouteId: x.DeliveryRouteId, DeliveryOrderId: d.Id, EventType: 'DELIVERED', EventAt: now, Latitude: loc.Latitude, Longitude: loc.Longitude, Description: 'Bàn giao xong – ' + rn, SourceChannel: 'MOBILE' });
        const ord = Store.get('Order', d.OrderId); if (ord && Store.by('DeliveryOrder', 'OrderId', ord.Id).every(z => z.Status === 'DELIVERED')) Store.update('Order', ord.Id, { Status: 'Delivered' });
        if (dq - aq > 0) Store.add('CustomerComplaint', { ComplaintNo: 'KN-2611-' + String(Store.rows('CustomerComplaint').length + 1).padStart(3, '0'), CustomerId: d.CustomerId, OrderId: d.OrderId, DeliveryOrderId: d.Id, DeliveryRouteId: d.DeliveryRouteId, SourceChannel: 'POD', CategoryCode: 'SHORTAGE', Severity: 'Medium', Description: `Khách từ chối ${dq - aq} suất khi nhận`, ReceivedAt: now, ComplaintStatus: 'Open' });
        close(); UI.toast('Đã ghi POD – DELIVERED' + (dq - aq ? ' · tự lập khiếu nại do khách từ chối suất' : ''), 'ok'); APP.refresh();
      } });
  }
  // ---------- Khiếu nại ----------
  // Tạo phản ánh qua CustomerComplaint/Create: máy chủ tự sinh số, tính hạn xử lý theo mức độ và ghi lịch sử.
  function complaintCreate() {
    const cs = Store.rows('Customer'); if (!cs.length) return UI.toast('Chưa có khách hàng', 'warn');
    const orders = cid => opt('', '— Không gắn đơn hàng —', true) + Store.by('Order', 'CustomerId', cid).sort((a, b) => String(b.DeliveryDate || b.MealDate || '').localeCompare(String(a.DeliveryDate || a.MealDate || ''))).slice(0, 100).map(o => opt(o.Id, (o.OrderNumber || o.OrderNo || '') + ' · ' + U.date(o.DeliveryDate || o.MealDate))).join('');
    dlg('Tạo phản ánh của khách hàng', fld('Khách hàng *', `<select class="select" id="ccC">${cs.map(c => opt(c.Id, Store.label('Customer', c))).join('')}</select>`, true) + fld('Đơn hàng liên quan', `<select class="select" id="ccO">${orders(cs[0].Id)}</select>`, true) +
      fld('Kênh tiếp nhận', `<input class="input" id="ccS" list="ccSL" maxlength="50">${dataList('ccSL', distinct('CustomerComplaint', 'SourceChannel'))}`) + fld('Phân loại', `<input class="input" id="ccK" list="ccKL" maxlength="50">${dataList('ccKL', distinct('CustomerComplaint', 'CategoryCode'))}`) +
      fld('Mức độ', `<select class="select" id="ccV">${opt('High', 'Cao')}${opt('Medium', 'Trung bình', true)}${opt('Low', 'Thấp')}</select>`, true) + fld('Nội dung phản ánh *', '<textarea class="input" rows="3" id="ccD" maxlength="1800"></textarea>', true),
      'Tạo phản ánh', (m, close) => {
        const desc = val(m, 'ccD'); if (!desc) return UI.toast('Nhập nội dung phản ánh', 'err');
        Store.act('CustomerComplaint/Create', { form: { CustomerId: val(m, 'ccC'), OrderId: val(m, 'ccO'), SourceChannel: val(m, 'ccS'), CategoryCode: val(m, 'ccK'), Severity: val(m, 'ccV'), Description: desc }, tables: ['CustomerComplaint', 'ComplaintHistory'] })
          .then(x => { if (!x.ok) return; close(); done(x, 'Đã tạo phản ánh ' + esc((x.data && x.data.complaint && x.data.complaint.complaintNo) || '')); });
      }, { size: '', mount: m => { m.querySelector('#ccC').onchange = e => { m.querySelector('#ccO').innerHTML = orders(e.target.value); }; } });
  }
  APP.register('log-complaint', (el, f) => {
    // Chế độ API: CustomerComplaint/GetList – quá hạn / số giờ còn lại do máy chủ tính, đếm số phản ánh quá hạn.
    if (Store.isLive) need('Customer', 'Order');
    const cl = Store.isLive ? live('CustomerComplaint/GetList', { Page: 1, PageSize: 500 }) : null, ci = {};
    const cd = cl && cl.ok && cl.data ? cl.data : null;
    if (cd) (cd.listItem || []).forEach(i => { if (i.complaint) ci[i.complaint.id] = i; });
    const overdue = r => ci[r.Id] ? !!ci[r.Id].isOverdue : !!(r.SlaDueAt && r.SlaDueAt < U.iso(new Date()) && !['Resolved', 'Closed'].includes(r.ComplaintStatus));
    const due = r => { const i = ci[r.Id]; if (!i || i.hoursToDue == null || ['Resolved', 'Closed', 'Rejected'].includes(r.ComplaintStatus)) return ''; const h = Math.round(Math.abs(Number(i.hoursToDue))); return `<div class="small ${i.isOverdue ? 'bad' : 'muted'}">${i.isOverdue ? 'Quá hạn ' : 'Còn '}${h} giờ</div>`; };
    APP.genericList(el, f, { noTransit: true, noAdd: Store.isLive, cols: [{ k: 'ComplaintNo', label: 'Số KN' }, { k: 'CustomerId', label: 'Khách hàng' }, { k: 'CategoryCode', label: 'Phân loại' }, { k: 'Severity', label: 'Mức độ', render: r => UI.badge(r.Severity) }, { k: 'ReceivedAt', label: 'Tiếp nhận' }, { k: 'SlaDueAt', label: 'Hạn SLA', render: r => `<span class="${overdue(r) ? 'bad' : ''}">${U.dt(r.SlaDueAt)}</span>${due(r)}` }, { k: 'ComplaintStatus', label: 'Trạng thái' }],
      top: Store.isLive ? (cd && cd.overdueCount ? `<div class="notice warn">${I('alert')}<div><b>${U.num(cd.overdueCount)} phản ánh đã quá hạn xử lý</b>Ưu tiên xử lý các dòng có hạn tô đỏ.</div></div>` : '') : `<div class="notice warn">${I('alert')}<div><b>GAP-F-02 (SLA):</b> chỉ có cột CustomerComplaint.SlaDueAt – chưa có đặc tả SLA theo hợp đồng.</div></div>` });
    if (Store.isLive && Store.perm(f.id).add !== false) { headBtn(el, `<button class="btn primary" id="ccNew">${I('plus')} Tạo phản ánh</button>`); el.querySelector('#ccNew').onclick = complaintCreate; }
    APP.rowExtra(el, tr => { const r = Store.get('CustomerComplaint', tr.dataset.row); const nx = APP.nextStatus('CustomerComplaint', 'ComplaintStatus', r.ComplaintStatus); if (!nx) return; const td = tr.querySelector('.actions'); td.insertAdjacentHTML('afterbegin', `<button class="btn xs orange" data-nx>→ ${esc(UI.statusVi('CustomerComplaint', 'ComplaintStatus', nx))}</button> `); td.querySelector('[data-nx]').onclick = () => UI.confirm(`Chuyển khiếu nại ${esc(r.ComplaintNo)} sang ${UI.badge(nx, 'CustomerComplaint', 'ComplaintStatus')}? Ghi lịch sử vào ComplaintHistory.`, note => { const old = r.ComplaintStatus;
      // Chế độ API: CustomerComplaint/Assign · Resolve · Close · Reject (backend ghi ComplaintHistory, tính SLA).
      const act = { InProgress: 'Assign', Resolved: 'Resolve', Closed: 'Close', Rejected: 'Reject' }[nx];
      if (Store.isLive && act) return Store.act('CustomerComplaint/' + act, { form: { Id: r.Id, Comment: note }, tables: ['CustomerComplaint', 'ComplaintHistory'] }).then(x => { if (x.ok) { bust(); UI.toast('Đã cập nhật khiếu nại', 'ok'); APP.refresh(); } });
      Store.update('CustomerComplaint', r.Id, Object.assign({ ComplaintStatus: nx }, nx === 'Resolved' ? { ResolvedAt: U.iso(new Date()), Resolution: note } : {})); Store.add('ComplaintHistory', { ComplaintId: r.Id, EventType: 'STATUS_CHANGED', OldStatus: old, NewStatus: nx, Comment: note, EventAt: U.iso(new Date()), ActorId: Store.user.id }); UI.toast('Đã cập nhật khiếu nại + ComplaintHistory', 'ok'); APP.refresh(); }, { input: 'Nội dung xử lý', required: true }); });
  });

  // ---------- Thu hồi khay hộp: ContainerReturn/GetList · Record · Inspect ----------
  // Ghi thu hồi → máy chủ ghi sổ bao bì (nhập lại phần tốt / phần hỏng). Sổ chỉ ghi thêm nên hỏi lại trước khi ghi.
  function returnRecord(cr) {
    const ds = Store.rows('DeliveryOrder').filter(d => d.Status === 'Delivered' || (cr && d.Id === cr.DeliveryOrderId));
    if (!ds.length) return UI.toast('Chưa có đơn giao nào đã giao để thu hồi khay hộp', 'warn');
    dlg('Ghi thu hồi khay hộp', fld('Đơn giao đã giao *', `<select class="select" id="crD" ${cr ? 'disabled' : ''}>${ds.map(d => opt(d.Id, doLabel(d), cr && d.Id === cr.DeliveryOrderId)).join('')}</select>`, true) +
      fld('Số phải thu (trống = số còn ở khách)', `<input type="number" min="0" step="1" class="input" id="crE" value="${cr && cr.ExpectedQuantity != null ? esc(cr.ExpectedQuantity) : ''}">`) + fld('Số thu về *', '<input type="number" min="0" step="1" class="input" id="crR">') +
      fld('Trong đó bị hỏng', '<input type="number" min="0" step="1" class="input" id="crG" value="0">') + fld('Ngày thu hồi', `<input type="date" class="input" id="crT" value="${esc(Store.ctx.date)}">`) +
      fld('Loại khay / hộp (trống = loại đã giao)', `<input class="input" id="crP" list="crPL" maxlength="100">${dataList('crPL', distinct('PackagingLedger', 'PackagingType'))}`, true) + fld('Ghi chú', '<input class="input" id="crN" maxlength="1000">', true),
      'Ghi thu hồi', (m, close) => {
        const rq = val(m, 'crR'), n = Number(rq), g = Number(val(m, 'crG')) || 0, ex = val(m, 'crE');
        if (rq === '' || !Number.isInteger(n) || n < 0) return UI.toast('Nhập số thu về (số nguyên, không âm)', 'err');
        if (!Number.isInteger(g) || g < 0 || g > n) return UI.toast('Số hỏng phải là số nguyên từ 0 đến số thu về', 'err');
        if (ex !== '' && (!Number.isInteger(Number(ex)) || Number(ex) < 0)) return UI.toast('Số phải thu phải là số nguyên không âm', 'err');
        const d = Store.get('DeliveryOrder', val(m, 'crD')) || {};
        UI.confirm(`Ghi thu hồi <b>${U.num(n)}</b> khay hộp${g ? ` (hỏng ${U.num(g)})` : ''} của đơn giao <b>${esc(d.DeliveryNumber || d.DeliveryNo || '')}</b>? Số này được ghi vào sổ bao bì và không sửa được – ghi sai phải ghi dòng điều chỉnh.`,
          () => Store.act('ContainerReturn/Record', { form: { DeliveryOrderId: d.Id, ReturnDate: val(m, 'crT'), ExpectedQuantity: ex, ReturnedQuantity: n, DamagedQuantity: g, PackagingType: val(m, 'crP'), Note: val(m, 'crN') }, tables: CT }).then(x => { if (!x.ok) return; close(); done(x, 'Đã ghi thu hồi khay hộp'); }), { ok: 'Ghi thu hồi' });
      }, { size: '' });
  }
  function returnInspect(cr) {
    const sd = Store.statusDef('ReturnInspection', 'InspectionResult') || { codes: ['OK', 'SHORTAGE', 'DAMAGED', 'DIRTY', 'REJECTED'], vi: ['Đủ', 'Thiếu', 'Hỏng', 'Bẩn', 'Không đạt'] };
    dlg('Kiểm tra khay hộp thu hồi', fld('Kết quả kiểm tra *', `<select class="select" id="riR">${sd.codes.map((c, i) => opt(c, sd.vi[i] || c)).join('')}</select>`, true) +
      fld('Số hỏng (trống = số hỏng lúc thu)', '<input type="number" min="0" class="input" id="riD">') + fld('Số thiếu (trống = phải thu − thu về)', '<input type="number" min="0" class="input" id="riM">') +
      fld('Ghi chú (bắt buộc khi kết quả khác Đủ)', '<textarea class="input" rows="2" id="riN" maxlength="1000"></textarea>', true) + '<div class="field full small muted">Khay hộp còn dùng được sẽ tự chuyển sang mẻ vệ sinh.</div>',
      'Ghi kết quả kiểm tra', (m, close) => {
        const res = val(m, 'riR'), note = val(m, 'riN'), dq = val(m, 'riD'), mq = val(m, 'riM');
        if (res !== 'OK' && !note) return UI.toast('Nhập ghi chú khi kết quả khác Đủ', 'err');
        if ((dq !== '' && !(Number(dq) >= 0)) || (mq !== '' && !(Number(mq) >= 0))) return UI.toast('Số lượng không được âm', 'err');
        Store.act('ContainerReturn/Inspect', { form: { Id: cr.Id, InspectionResult: res, DamagedQty: dq, MissingQty: mq, Note: note }, tables: CT }).then(x => {
          if (!x.ok) return; close(); const cb = ((x.data && x.data.listCleaningBatch) || []).map(b => b.cleaningBatchNumber).filter(Boolean);
          done(x, 'Đã ghi kết quả kiểm tra' + (cb.length ? ' · mẻ vệ sinh ' + esc(cb.join(', ')) : ''));
        });
      }, { size: '', sub: esc(Store.label('DeliveryOrder', cr.DeliveryOrderId)) + ' · thu về ' + U.num(cr.ReturnedQuantity) + (cr.ExpectedQuantity != null ? ' / phải thu ' + U.num(cr.ExpectedQuantity) : '') });
  }
  APP.register('log-container-return', (el, f) => {
    if (!Store.isLive) return APP.genericList(el, f);
    need('DeliveryOrder', 'Customer', 'PackagingLedger');
    const x = live('ContainerReturn/GetList', { Page: 1, PageSize: 500 }), info = {};
    if (x && x.ok && x.data) (x.data.listItem || []).forEach(i => { if (i.return) info[i.return.id] = i; });
    const of = (r, k) => (info[r.Id] && info[r.Id][k]) || '';
    APP.genericList(el, f, { noAdd: true, noTransit: true, cols: [{ k: 'DeliveryOrderId', label: 'Đơn giao' }, { k: 'CustomerId', label: 'Khách hàng' }, { k: 'PackagingType', label: 'Loại khay / hộp', nosort: true, render: r => esc(of(r, 'packagingType')), text: r => of(r, 'packagingType') },
      { k: 'ReturnDate', label: 'Ngày thu hồi' }, { k: 'ExpectedQuantity', label: 'Phải thu', num: true }, { k: 'ReturnedQuantity', label: 'Thu về', num: true }, { k: 'DamagedQuantity', label: 'Hỏng', num: true },
      { k: 'InspectionResult', label: 'Kết quả kiểm tra', nosort: true, render: r => of(r, 'inspectionResult') ? UI.badge(of(r, 'inspectionResult'), 'ReturnInspection', 'InspectionResult') : '', text: r => of(r, 'inspectionResultName') }, { k: 'Status', label: 'Trạng thái' }] });
    if (Store.perm(f.id).add !== false) { headBtn(el, `<button class="btn primary" id="crNew">${I('plus')} Ghi thu hồi</button>`); el.querySelector('#crNew').onclick = () => returnRecord(null); }
    APP.rowExtra(el, tr => {
      const r = Store.get('ContainerReturn', tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td || td.dataset.live) return; td.dataset.live = '1';
      if (r.Status === 'Expected' && Store.perm(f.id).add !== false) rowBtn(td, 'orange', 'Ghi thu hồi', () => returnRecord(r));
      if (r.Status === 'Returned' && Store.perm('log-return-inspection').add !== false) rowBtn(td, 'primary', 'Kiểm tra', () => returnInspect(r));
    });
  });
  APP.register('log-return-inspection', (el, f) => {
    if (!Store.isLive) return APP.genericList(el, f);
    // Kết quả kiểm tra ghi qua phiếu thu hồi (ContainerReturn/Inspect) để phiếu chuyển trạng thái và tự lập mẻ vệ sinh.
    need('ContainerReturn', 'DeliveryOrder', 'Customer');
    APP.genericList(el, f, { noAdd: true });
    if (Store.perm(f.id).add === false) return;
    headBtn(el, `<button class="btn primary" id="riNew">${I('plus')} Kiểm tra phiếu thu hồi</button>`);
    el.querySelector('#riNew').onclick = () => {
      const open = Store.rows('ContainerReturn').filter(r => r.Status === 'Returned');
      if (!open.length) return UI.toast('Không có phiếu thu hồi nào đang chờ kiểm tra', 'warn');
      dlg('Chọn phiếu thu hồi', fld('Phiếu thu hồi chờ kiểm tra *', `<select class="select" id="riC">${open.map(r => opt(r.Id, Store.label('DeliveryOrder', r.DeliveryOrderId) + ' · ' + Store.label('Customer', r.CustomerId) + ' · thu về ' + U.num(r.ReturnedQuantity))).join('')}</select>`, true), 'Tiếp tục', (m, close) => { const r = Store.get('ContainerReturn', val(m, 'riC')); close(); if (r) returnInspect(r); });
    };
  });

  // ---------- Sổ bao bì theo khách: PackagingLedger/GetBalance · Record (sổ chỉ ghi thêm – không sửa / xóa) ----------
  function ledgerRecord() {
    const cs = Store.rows('Customer'); if (!cs.length) return UI.toast('Chưa có khách hàng', 'warn');
    dlg('Ghi sổ bao bì', fld('Khách hàng *', `<select class="select" id="plC">${cs.map(c => opt(c.Id, Store.label('Customer', c))).join('')}</select>`, true) +
      fld('Loại ghi sổ *', `<select class="select" id="plT">${opt('OUT', 'Giao cho khách (tồn đầu kỳ ở khách)')}${opt('IN', 'Khách trả lại')}${opt('LOSS', 'Thất thoát')}</select>`, true) +
      fld('Loại khay / hộp *', `<input class="input" id="plP" list="plPL" maxlength="100">${dataList('plPL', distinct('PackagingLedger', 'PackagingType', ['KHAY']))}`) + fld('Số lượng *', '<input type="number" min="1" class="input" id="plQ">') +
      fld('Tình trạng (khi khách trả)', `<select class="select" id="plK">${opt('', '— Không ghi —', true)}${opt('GOOD', 'Tốt')}${opt('DAMAGED', 'Hỏng')}</select>`, true) + fld('Lý do *', '<textarea class="input" rows="2" id="plR" maxlength="1000"></textarea>', true),
      'Ghi sổ', (m, close) => {
        const q = Number(val(m, 'plQ')), type = val(m, 'plT'), pt = val(m, 'plP'), reason = val(m, 'plR');
        if (!pt) return UI.toast('Nhập loại khay / hộp', 'err'); if (!(q > 0)) return UI.toast('Số lượng phải lớn hơn 0', 'err'); if (!reason) return UI.toast('Nhập lý do ghi sổ', 'err');
        const tn = { OUT: 'giao cho khách', IN: 'khách trả lại', LOSS: 'thất thoát' }[type];
        UI.confirm(`Ghi sổ bao bì: <b>${U.num(q)} ${esc(pt)}</b> – ${tn} – khách <b>${esc(Store.label('Customer', val(m, 'plC')))}</b>? Dòng sổ đã ghi không sửa, không xóa được; ghi sai phải ghi dòng ngược lại.`,
          () => Store.act('PackagingLedger/Record', { form: { CustomerId: val(m, 'plC'), TransactionType: type, PackagingType: pt, Quantity: q, ConditionCode: type === 'IN' ? val(m, 'plK') : '', Reason: reason }, tables: CT }).then(x => { if (!x.ok) return; close(); done(x, 'Đã ghi sổ bao bì'); }), { ok: 'Ghi sổ' });
      }, { size: '' });
  }
  APP.register('log-packaging-ledger', (el, f) => {
    if (!Store.isLive) return APP.genericList(el, f);
    need('Customer');
    const x = live('PackagingLedger/GetBalance', {}), bal = x && x.ok && x.data ? x.data : null, items = bal ? (bal.listItem || []) : [];
    const top = `<div class="card panel mb"><div class="panel-head"><div><div class="panel-title">Khay hộp còn ở khách${bal ? ' · tổng ' + U.num(bal.totalOutstanding) : ''}</div><div class="panel-desc">Còn ở khách = đã giao − đã thu − thất thoát.${bal && bal.overThresholdCount ? ' Có ' + U.num(bal.overThresholdCount) + ' dòng vượt ngưỡng thất thoát.' : ''}</div></div></div>
      ${!x ? '<div class="empty">Đang tải…</div>' : !x.ok ? `<div class="empty">Không tải được số dư: ${esc(x.message || '')}</div>` : !items.length ? '<div class="empty">Chưa có khay hộp nào ở khách</div>' : `<div class="table-wrap" style="max-height:340px"><table class="table dc-table dt"><thead><tr><th>Khách hàng</th><th>Loại</th><th class="num">Đã giao</th><th class="num">Đã thu</th><th class="num">Trong đó hỏng</th><th class="num">Thất thoát</th><th class="num">Còn ở khách</th><th class="num">Tỷ lệ thất thoát</th></tr></thead><tbody>${items.map(i => `<tr><td>${esc(i.customerName || '')}${i.customerCode ? `<div class="small muted">${esc(i.customerCode)}</div>` : ''}</td><td>${esc(i.packagingType || '')}</td><td class="num">${U.num(i.outQuantity)}</td><td class="num">${U.num(i.inQuantity)}</td><td class="num">${U.num(i.damagedInQuantity)}</td><td class="num">${U.num(i.lossQuantity)}</td><td class="num"><b>${U.num(i.outstanding)}</b></td><td class="num"><span class="${i.isOverThreshold ? 'bad' : ''}">${i.lossRatePct == null ? '–' : U.pct(i.lossRatePct, 2)}</span><div class="small muted">ngưỡng ${U.pct(i.lossThresholdPct, 2)}</div></td></tr>`).join('')}</tbody></table></div>`}</div>`;
    APP.genericList(el, f, { noAdd: true, noTransit: true, top });
    if (Store.perm(f.id).add !== false) { headBtn(el, `<button class="btn primary" id="plNew">${I('plus')} Ghi sổ</button>`); el.querySelector('#plNew').onclick = ledgerRecord; }
    // Sổ chỉ ghi thêm: bỏ nút sửa / xóa trên dòng.
    APP.rowExtra(el, tr => tr.querySelectorAll('[data-edit], [data-del]').forEach(b => b.remove()));
  });
})();
