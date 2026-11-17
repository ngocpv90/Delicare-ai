/* P1/P3 – Khách hàng 360°, Đơn hàng, Đối soát, Hợp đồng, Dự báo */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const ORDER_FLOW = ['Draft', 'Submitted', 'Confirmed', 'Locked', 'InProduction', 'Delivered', 'Reconciled'];
  // ---------- Khách hàng 360° ----------
  APP.register('crm-customer', (el, f) => {
    APP.genericList(el, f, {
      cols: [{ k: 'Code', label: 'Mã' }, { k: 'DisplayName', label: 'Khách hàng' }, { k: 'CustomerType', label: 'Loại' }, { k: 'BranchId', label: 'Chi nhánh phục vụ' },
        { k: 'x1', label: 'Hợp đồng hiệu lực', col: false, nosort: true, render: r => { const c = Store.by('Contract', 'CustomerId', r.Id); return c.map(x => `${esc(x.ContractNumber)} ${UI.badge(x.Status, 'Contract', 'Status')}`).join('<br>'); } },
        { k: 'x2', label: 'Điểm giao', col: false, num: true, nosort: true, render: r => Store.by('CustomerDeliveryLocation', 'CustomerId', r.Id).length },
        { k: 'x3', label: 'Suất TB/ngày (7 ngày)', col: false, num: true, nosort: true, render: r => U.num(U.sum(Store.by('Order', 'CustomerId', r.Id).filter(o => o.Status === 'Reconciled'), 'TotalQuantity') / 5, 0) },
        { k: 'Status', label: 'Trạng thái' }],
      rows: () => Store.rows('Customer').filter(c => !Store.ctx.branch || c.BranchId === Store.ctx.branch)
    });
  });
  // drawer 360 khi mở Customer từ bất kỳ đâu
  const baseDetail = APP.openDetail;
  APP.openDetail = (t, id, f, opt) => {
    if (t === 'Customer') return customer360(id, f);
    if (t === 'Order') return orderDetail(id);
    return baseDetail(t, id, f, opt);
  };
  function customer360(id, f) {
    const c = Store.get('Customer', id);
    const ords = Store.by('Order', 'CustomerId', id).sort((a, b) => b.MealDate.localeCompare(a.MealDate));
    UI.detail('Customer', id, {
      screen: 'crm-customer',
      top: `<div class="kpis">${UI.kpi('Đơn hàng', ords.length, 'dbo.Order', 'clipboard', 'green')}${UI.kpi('Doanh thu đã đối soát', U.money(U.sum(Store.by('OrderReconciliation', 'OrderId', null).concat(ords.flatMap(o => Store.by('OrderReconciliation', 'OrderId', o.Id))), 'FinalAmount')), 'OrderReconciliation.FinalAmount', 'calc', 'blue')}${UI.kpi('Khiếu nại', Store.by('CustomerComplaint', 'CustomerId', id).length, 'CustomerComplaint', 'msg', 'orange')}</div>`,
      tabs: [
        { label: 'Liên hệ & điểm giao', html: () => `<h3>Người liên hệ (CustomerContact)</h3>${Store.by('CustomerContact', 'CustomerId', id).map(x => `<div class="spread" style="padding:7px 0;border-bottom:1px solid #F0F2ED"><span><b>${esc(x.FullName)}</b> · ${esc(x.Position || '')}${x.IsPrimary ? ' <span class="tag green">Chính</span>' : ''}</span><span class="small muted">${esc(x.Phone || '')} · ${esc(x.Email || '')}</span></div>`).join('')}<h3 class="mt">Điểm giao hàng (CustomerDeliveryLocation)</h3>${Store.by('CustomerDeliveryLocation', 'CustomerId', id).map(x => `<div style="padding:7px 0;border-bottom:1px solid #F0F2ED"><b>${esc(x.Name)}</b> ${UI.badge(x.Status, 'CustomerDeliveryLocation', 'Status')}<div class="small muted">${esc(x.Address)} · Nhận ${U.time(x.DeliveryWindowFrom)}–${U.time(x.DeliveryWindowTo)} · ${esc(x.ContactName)} ${esc(x.ContactPhone)}</div></div>`).join('')}` },
        { label: 'Hợp đồng & giá', html: () => Store.by('Contract', 'CustomerId', id).map(ct => `<div class="card panel mb"><div class="spread"><b>${esc(ct.ContractNumber)} · ${esc(ct.Name)}</b>${UI.badge(ct.Status, 'Contract', 'Status')}</div><div class="small muted">Hiệu lực ${U.date(ct.EffectiveFrom)} – ${U.date(ct.EffectiveTo)} · ${esc(ct.PaymentTerm || '')} · Giá trị ${U.money(ct.ContractValue)}</div><table class="dt mt"><thead><tr><th>Loại suất<small>ContractMeal.MealType</small></th><th>Tên</th><th class="num">Đơn giá</th><th>Trạng thái</th></tr></thead><tbody>${Store.by('ContractMeal', 'ContractId', ct.Id).map(m => `<tr><td>${UI.tag(m.MealType)}</td><td>${esc(m.MealName)}</td><td class="num">${U.money(m.UnitPrice)}</td><td>${UI.badge(m.Status, 'ContractMeal', 'Status')}</td></tr>`).join('')}</tbody></table></div>`).join('') },
        { label: 'Đơn hàng', html: () => `<table class="dt"><thead><tr><th>Số đơn</th><th>Ngày ăn</th><th class="num">Suất</th><th class="num">Thành tiền</th><th>Trạng thái</th></tr></thead><tbody>${ords.slice(0, 15).map(o => `<tr><td><a href="javascript:void 0" data-open="Order" data-id="${o.Id}">${esc(o.OrderNumber)}</a></td><td>${U.date(o.MealDate)}</td><td class="num">${U.num(o.TotalQuantity)}</td><td class="num">${U.money(o.TotalAmount)}</td><td>${UI.badge(o.Status, 'Order', 'Status')}</td></tr>`).join('')}</tbody></table>` }
      ]
    });
  }
  // ---------- Đơn hàng ----------
  const os = { view: 'board', date: '' };
  APP.register('crm-order', (el, f, params) => {
    const perm = Store.perm(f.id);
    const dates = [...new Set(Store.rows('Order').map(o => U.day(o.MealDate)))].sort();
    if (!os.date) os.date = U.addDays(Store.ctx.date, 1);
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
      body.innerHTML = `<div class="kanban">${ORDER_FLOW.concat(['Cancelled']).map(s => { const r = rows.filter(o => o.Status === s); return `<div class="kcol"><div class="kcol-head"><span>${UI.statusVi('Order', 'Status', s)}</span><span class="tag">${r.length}</span></div>${r.map(o => { const c = Store.get('Customer', o.CustomerId); return `<div class="kcard" data-o="${o.Id}"><b>${esc(o.OrderNumber)}</b><small>${esc(c.DisplayName)}</small><div class="row"><span class="small">${U.date(o.MealDate)}</span><b>${U.num(o.TotalQuantity)} suất</b></div></div>`; }).join('') || '<div class="small muted">—</div>'}</div>`; }).join('')}</div>`;
      body.querySelectorAll('[data-o]').forEach(c => c.onclick = () => orderDetail(c.dataset.o));
    } else {
      body.innerHTML = '<div class="card panel" id="oTbl"></div>';
      UI.table(body.querySelector('#oTbl'), { table: 'Order', rows, onRow: orderDetail, cols: [{ k: 'OrderNumber', label: 'Số đơn' }, { k: 'CustomerId', label: 'Khách hàng' }, { k: 'ContractId', label: 'Hợp đồng' }, { k: 'MealDate', label: 'Ngày ăn' }, { k: 'TotalQuantity', label: 'Tổng suất', num: true }, { k: 'TotalAmount', label: 'Thành tiền', num: true }, { k: 'Status', label: 'Trạng thái' }], filters: [{ k: 'Status', label: 'Trạng thái', options: ORDER_FLOW.map(s => ({ v: s, l: UI.statusVi('Order', 'Status', s) })) }] });
    }
    if (params.id) setTimeout(() => orderDetail(params.id), 50);
  });
  function recalc(oid) { const ls = Store.by('OrderLine', 'OrderId', oid); Store.get('Order', oid).TotalQuantity = U.sum(ls, 'Quantity'); Store.get('Order', oid).TotalAmount = U.sum(ls, 'LineAmount'); }
  function orderDetail(id) {
    const o = Store.get('Order', id), c = Store.get('Customer', o.CustomerId);
    const lines = Store.by('OrderLine', 'OrderId', id);
    const fc = Store.rows('Forecast').filter(x => x.CustomerId === o.CustomerId && U.day(x.ForecastDate) === U.day(o.MealDate));
    const i = ORDER_FLOW.indexOf(o.Status);
    const editable = ['Draft', 'Submitted'].includes(o.Status);
    const dvs = Store.by('DeliveryOrder', 'OrderId', id);
    const rec = Store.one('OrderReconciliation', 'OrderId', id);
    const actions = { Draft: ['submit', 'Gửi đơn (Submitted)'], Submitted: ['confirm', 'Xác nhận (Confirmed)'], Confirmed: ['lock', 'Chốt số lượng (Locked)'] }[o.Status];
    UI.detail('Order', id, {
      screen: 'crm-order', noEdit: !editable,
      top: `${UI.stepper(ORDER_FLOW.map(s => UI.statusVi('Order', 'Status', s)), i, o.Status === 'Cancelled')}<div class="kpis mt">${UI.kpi('Khách hàng', esc(c.DisplayName).slice(0, 26), esc(c.CustomerType), 'building', 'green')}${UI.kpi('Tổng suất', U.num(o.TotalQuantity), 'Σ OrderLine.Quantity', 'utensils', 'blue')}${UI.kpi('Thành tiền', U.money(o.TotalAmount), 'Σ OrderLine.LineAmount', 'calc', 'orange')}</div>
        <h3>Chi tiết theo ca / loại suất / điểm giao (OrderLine)</h3><table class="dt"><thead><tr><th>Ca<small>ShiftId</small></th><th>Loại suất<small>MealType</small></th><th>Điểm giao<small>Note</small></th><th class="num">Số lượng<small>Quantity</small></th><th class="num">Dự báo<small>Forecast</small></th><th class="num">Đơn giá</th><th class="num">Thành tiền<small>LineAmount</small></th></tr></thead><tbody>${lines.map(l => { const fq = U.sum(fc.filter(x => x.MealType === l.MealType), 'ForecastQuantity'); return `<tr><td>${esc(Store.label('Shift', l.ShiftId))}</td><td>${UI.tag(l.MealType)}</td><td class="small">${esc(l.Note || '')}</td><td class="num">${editable ? `<input type="number" class="input" style="width:90px;text-align:right" data-ql="${l.Id}" value="${l.Quantity}">` : U.num(l.Quantity)}</td><td class="num muted">${fq ? U.num(fq) : '–'}</td><td class="num">${U.money(l.UnitPrice)}</td><td class="num">${U.money(l.LineAmount)}</td></tr>`; }).join('')}</tbody></table>
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
            .then(r => { if (!r.ok) return; UI.toast('Đã cập nhật số lượng – OrderLine.Quantity (DEMO)', 'ok'); close(); orderDetail(id); APP.refresh(); });
        }
        if (a === 'submit') { if (!lines.length || !lines.every(l => l.ShiftId && l.MealType)) return UI.toast('Đơn phải có ít nhất 1 dòng với Ca và Loại suất', 'err'); step('Submit', null, 'Submitted', 'Đã gửi đơn'); }
        if (a === 'confirm') { const fq = U.sum(fc, 'ForecastQuantity'); const diff = fq ? (o.TotalQuantity - fq) / fq * 100 : 0; UI.confirm(`Xác nhận đơn <b>${esc(o.OrderNumber)}</b> – ${U.num(o.TotalQuantity)} suất.<br><span class="small ${Math.abs(diff) > 10 ? 'bad' : 'muted'}">So với dự báo: ${fq ? U.pct(diff) : 'không có dự báo'}</span>`, () => step('Confirm', null, 'Confirmed', 'Đã xác nhận – đơn sẵn sàng vào kế hoạch sản xuất')); }
        if (a === 'lock') UI.confirm('Chốt số lượng? Sau khi chốt, mọi thay đổi phải qua Yêu cầu đổi món / phê duyệt.<div class="notice gap mt small">GAP-F-07: SQL chưa có cột giờ chốt (cut-off). Mockup cho phép chốt thủ công.</div>', () => step('Lock', null, 'Locked', 'Đã chốt số lượng'));
        if (a === 'cancel') UI.confirm('Hủy đơn hàng này?', r => step('Cancel', { Reason: r }, 'Cancelled', 'Đã hủy đơn'), { input: 'Lý do hủy', required: true, danger: true, ok: 'Hủy đơn' });
        function done(m) { close(); UI.toast(m + ' (Order.Status · DEMO)', 'ok'); APP.refresh(); orderDetail(id); }
      }
    });
  }
  APP.orderDetail = orderDetail;
  function newOrder() {
    const cus = Store.rows('Customer').filter(c => c.Status === 'Active');
    UI.modal({
      title: 'Tạo đơn đặt suất ăn', sub: 'Order + OrderLine · lấy loại suất từ ContractMeal, điểm giao từ CustomerDeliveryLocation, gợi ý số lượng từ Forecast', size: 'lg',
      body: `<div class="form-grid"><div class="field"><label>Khách hàng <span class="req">*</span><code>CustomerId</code></label><select class="select" id="nC"><option value="">— Chọn —</option>${cus.map(c => `<option value="${c.Id}">${esc(c.DisplayName)}</option>`).join('')}</select></div><div class="field"><label>Ngày ăn <span class="req">*</span><code>MealDate</code></label><input type="date" class="input" id="nD" value="${U.addDays(Store.ctx.date, 4)}"></div></div><div id="nLines" class="mt"></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="nSave">Tạo đơn (Nháp)</button>',
      onMount: (m, close) => {
        const draw = () => {
          const cid = m.querySelector('#nC').value, box = m.querySelector('#nLines'); if (!cid) { box.innerHTML = '<div class="muted small">Chọn khách hàng để tải hợp đồng & loại suất.</div>'; return; }
          const ct = Store.rows('Contract').find(x => x.CustomerId === cid && x.Status === 'Active');
          if (!ct) { box.innerHTML = '<div class="notice gap">Khách hàng chưa có hợp đồng hiệu lực (Contract.Status = Active) – không thể tạo đơn.</div>'; return; }
          const meals = Store.by('ContractMeal', 'ContractId', ct.Id), locs = Store.by('CustomerDeliveryLocation', 'CustomerId', cid), dte = m.querySelector('#nD').value;
          box.innerHTML = `<div class="notice ok">${I('ok')}<div>Hợp đồng ${esc(ct.ContractNumber)} · hiệu lực ${U.date(ct.EffectiveFrom)}–${U.date(ct.EffectiveTo)}</div></div><table class="dt"><thead><tr><th>Loại suất</th><th>Điểm giao</th><th>Ca</th><th class="num">Dự báo</th><th class="num">Số lượng</th></tr></thead><tbody>${meals.flatMap(ml => locs.map(l => { const fq = U.sum(Store.rows('Forecast').filter(x => x.CustomerId === cid && x.MealType === ml.MealType && U.day(x.ForecastDate) === dte), 'ForecastQuantity'); const shf = Store.rows('Shift')[{ BREAKFAST: 0, LUNCH: 1, DIET: 1, DINNER: 2, NIGHT: 3 }[ml.MealType] || 1]; return `<tr><td>${UI.tag(ml.MealType)} ${esc(ml.MealName)}</td><td class="small">${esc(l.Name)}</td><td>${esc(shf.Code)}</td><td class="num muted">${fq ? U.num(Math.round(fq / locs.length)) : '–'}</td><td class="num"><input type="number" class="input" style="width:90px;text-align:right" data-m="${ml.Id}" data-l="${l.Id}" data-s="${shf.Id}" value="${fq ? Math.round(fq / locs.length) : 0}"></td></tr>`; })).join('')}</tbody></table>`;
          box.dataset.ct = ct.Id;
        };
        m.querySelector('#nC').onchange = draw; m.querySelector('#nD').onchange = draw; draw();
        m.querySelector('#nSave').onclick = () => {
          const cid = m.querySelector('#nC').value, box = m.querySelector('#nLines'), dte = m.querySelector('#nD').value;
          if (!cid || !box.dataset.ct) return UI.toast('Chọn khách hàng có hợp đồng hiệu lực', 'err');
          const ins = [...box.querySelectorAll('[data-m]')].filter(x => +x.value > 0); if (!ins.length) return UI.toast('Nhập ít nhất 1 dòng số lượng > 0', 'err');
          if (Store.isLive) {
            // Order/Create: backend tự sinh số đơn, lấy đơn giá từ ContractMeal, kiểm tra hợp đồng hiệu lực và giờ chốt.
            const locId = Store.by('CustomerDeliveryLocation', 'CustomerId', cid)[0].Id;
            return Store.act('Order/Create', { json: { customerId: cid, contractId: box.dataset.ct, deliveryLocationId: locId, mealDate: dte, deliveryDate: dte + 'T10:00:00', listLine: ins.map(x => ({ contractMealId: x.dataset.m, shiftId: x.dataset.s, quantity: +x.value, deliveryLocationId: x.dataset.l })) }, tables: ['Order', 'OrderLine'] })
              .then(r => { if (!r.ok) return; const no = r.data && r.data.order; close(); os.date = dte; UI.toast('Đã tạo đơn ' + esc(no ? no.orderNumber : '') + ' (Draft)', 'ok'); APP.go('crm-order'); APP.refresh(); if (no) setTimeout(() => orderDetail(no.id), 80); });
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
    if (Store.isLive) APP.rowExtra(el, tr => {
      const r = Store.get('OrderReconciliation', tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td) return;
      td.insertAdjacentHTML('afterbegin', `<button class="btn xs" data-prt title="In biên bản đối soát (PDF)">${I('export')} In</button> ${r.Status !== 'Closed' ? '<button class="btn xs orange" data-cls>Chốt</button> ' : ''}`);
      td.querySelector('[data-prt]').onclick = () => Store.download('OrderReconciliation/Print', { id: r.Id });
      const c = td.querySelector('[data-cls]'); if (c) c.onclick = () => UI.confirm('Chốt đối soát đơn <b>' + esc(Store.label('Order', r.OrderId)) + '</b>? Sau khi chốt không sửa được số liệu.', note => Store.act('OrderReconciliation/Close', { form: { Id: r.Id, Remark: note }, tables: ['OrderReconciliation', 'Order'] }).then(x => { if (x.ok) { UI.toast('Đã chốt đối soát', 'ok'); APP.refresh(); } }), { input: 'Ghi chú', ok: 'Chốt đối soát' });
    });
  });
  // ---------- Hợp đồng: gửi phê duyệt ----------
  APP.register('crm-contract', (el, f) => {
    APP.genericList(el, f, { noTransit: true });
    APP.rowExtra(el, tr => { const r = Store.get('Contract', tr.dataset.row); if (r.Status === 'Draft') { const td = tr.querySelector('.actions'); td.insertAdjacentHTML('afterbegin', `<button class="btn xs orange" data-sub="${r.Id}">Gửi duyệt</button> `); td.querySelector('[data-sub]').onclick = () => Store.act('Contract/Submit', { form: { Id: r.Id }, tables: ['Contract', 'ApprovalRequest', 'ApprovalStep', 'ApprovalItem'], demo: () => { APP.submitApproval('CONTRACT', 'Contract', r.Id, [['Ban giám đốc']], 'Duyệt hợp đồng ' + r.ContractNumber); Store.update('Contract', r.Id, { Status: 'PendingApproval' }); } }).then(x => { if (!x.ok) return; if (!x.demo) UI.toast('Đã gửi duyệt hợp đồng ' + esc(r.ContractNumber), 'ok'); APP.refresh(); }); }
      if (r.Status === 'Active' && Store.isLive) { const td = tr.querySelector('.actions'); td.insertAdjacentHTML('afterbegin', `<button class="btn xs danger" data-term="${r.Id}">Thanh lý</button> `); td.querySelector('[data-term]').onclick = () => UI.confirm('Thanh lý hợp đồng <b>' + esc(r.ContractNumber) + '</b>?', note => Store.act('Contract/Terminate', { form: { Id: r.Id, Reason: note }, tables: ['Contract'] }).then(x => { if (x.ok) { UI.toast('Đã thanh lý hợp đồng', 'ok'); APP.refresh(); } }), { input: 'Lý do thanh lý', required: true, danger: true, ok: 'Thanh lý' }); } });
  });
  // ---------- Dự báo: pivot ----------
  APP.register('crm-forecast', (el, f) => {
    const rows = Store.rows('Forecast'); const days = [...new Set(rows.map(r => U.day(r.ForecastDate)))].sort();
    const cus = Store.rows('Customer');
    APP.genericList(el, f, {
      top: `<div class="card panel mb"><div class="panel-title mb">Ma trận dự báo suất ăn (Σ ForecastQuantity theo khách hàng × ngày)</div><div class="table-wrap"><table class="dt"><thead><tr><th>Khách hàng</th>${days.map(d => `<th class="num">${U.date(d).slice(0, 5)}</th>`).join('')}</tr></thead><tbody>${cus.map(c => `<tr><td>${esc(c.DisplayName)}</td>${days.map(d => `<td class="num">${U.num(U.sum(rows.filter(r => r.CustomerId === c.Id && U.day(r.ForecastDate) === d), 'ForecastQuantity'))}</td>`).join('')}</tr>`).join('')}<tr><td><b>Tổng</b></td>${days.map(d => `<td class="num"><b>${U.num(U.sum(rows.filter(r => U.day(r.ForecastDate) === d), 'ForecastQuantity'))}</b></td>`).join('')}</tr></tbody></table></div></div>`
    });
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
