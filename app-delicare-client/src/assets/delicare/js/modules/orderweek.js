/* ORD-OPT-1.8 (D-74 g) – Đăng ký suất tuần theo lựa chọn: khách + điểm giao + tuần → lưới (buổi × lựa chọn) × T2…CN, mỗi ngày 1 đơn.
 * API: Order/GetWeek · Order/SaveWeek (lưu nháp / xác nhận) · Order/CopyPreviousWeek · Order/ExportWeekSlip · Order/ImportWeekSlip.
 * Bố cục theo tab 3 của docs/frontend-handoff/assets/order-by-option-mockup.html. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const MEAL_LABEL = { SANG: 'Sáng', TRUA: 'Trưa', TOI: 'Tối', DEM: 'Đêm' };
  const DOW = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
  const mondayOf = ymd => { const d = new Date(ymd + 'T00:00'); return U.addDays(ymd, -((d.getDay() + 6) % 7)); };
  const mealName = r => r.mealName || MEAL_LABEL[String(r.mealType || '').toUpperCase()] || r.mealType || '';
  // Trạng thái màn giữ khi vẽ lại (Store tải bảng xong sẽ gọi APP.refresh).
  const st = { customer: '', location: '', week: '', data: null, qty: {}, key: '', loading: false, result: null, slipErrors: [], dirty: false };
  const cellKey = (ri, di) => ri + '|' + di;

  APP.register('crm-order-week', (el, f) => {
    const canEdit = Store.perm(f.id).edit !== false;
    if (!st.week) st.week = mondayOf(U.addDays(Store.ctx.date, 7));
    const customers = Store.rows('Customer').filter(c => c.Status === 'Active').sort((a, b) => String(a.Code).localeCompare(String(b.Code)));
    const locs = st.customer ? Store.by('CustomerDeliveryLocation', 'CustomerId', st.customer).filter(l => !l.IsDeleted) : [];
    if (st.customer && !locs.some(l => l.Id === st.location)) st.location = (locs[0] || {}).Id || '';
    const weeks = []; const base = mondayOf(Store.ctx.date);
    for (let k = -2; k <= 8; k++) weeks.push(U.addDays(base, 7 * k));
    if (!weeks.includes(st.week)) weeks.push(st.week);
    const d = st.data;
    const menuInfo = d ? [...new Set(d.listRow.flatMap(r => r.listCell.filter(c => c.menuCode).map(c => c.menuCode + (c.menuLevel === 'COMMON' ? ' (thực đơn chung)' : c.menuLevel === 'LOCATION' ? ' (gán điểm giao)' : ' (gán khách)'))))].join(', ') : '';
    el.innerHTML = APP.header(f, canEdit && Store.isLive ? `<button class="btn" id="owCopy">${I('history')} Chép số tuần trước</button><button class="btn" id="owSlip">${I('down')} Tải phiếu đăng ký</button><button class="btn" id="owUp">${I('file')} Tải phiếu đăng ký lên</button><button class="btn" id="owDraft">${I('check')} Lưu nháp</button><button class="btn primary" id="owConfirm">${I('check')} Xác nhận cả tuần</button><input type="file" id="owFile" accept=".xlsx,.xlsm" hidden>` : '') + `
      <div class="card panel mb"><div class="form-grid ow-filter">
        <div class="field"><label>Khách hàng <span class="req">*</span></label><select class="select" id="owC"><option value="">— Chọn khách hàng —</option>${customers.map(c => `<option value="${c.Id}" ${c.Id === st.customer ? 'selected' : ''}>${esc((c.Code ? c.Code + ' · ' : '') + (c.DisplayName || c.Name || ''))}</option>`).join('')}</select></div>
        <div class="field"><label>Điểm giao <span class="req">*</span></label><select class="select" id="owL">${locs.length ? locs.map(l => `<option value="${l.Id}" ${l.Id === st.location ? 'selected' : ''}>${esc((l.Code ? l.Code + ' · ' : '') + (l.Name || ''))}</option>`).join('') : '<option value="">— Chọn khách hàng trước —</option>'}</select></div>
        <div class="field"><label>Tuần <span class="req">*</span></label><select class="select" id="owW">${weeks.sort().map(w => `<option value="${w}" ${w === st.week ? 'selected' : ''}>${U.date(w).slice(0, 5)} – ${U.date(U.addDays(w, 6))}</option>`).join('')}</select></div>
        <div class="field"><label>Thực đơn áp dụng</label><input class="input" value="${esc(menuInfo || (d ? 'Chưa có thực đơn' : ''))}" disabled></div>
      </div></div>
      ${Store.isLive ? '' : `<div class="notice gap">${I('alert')}<div>Màn đăng ký suất tuần dùng dữ liệu API (Order/GetWeek) – chuyển sang chế độ API để nhập.</div></div>`}
      <div class="ow-two"><div class="card panel"><div class="table-wrap" id="owGrid">${grid()}</div>
        <div class="small muted mt">${I('alert')} Ngày đã qua 20:00 hôm trước bị khóa – muốn sửa phải "Yêu cầu thay đổi sau giờ chốt" (cần duyệt). Ô trống / 0 = không đặt.</div></div>
        <div id="owSide">${side()}</div></div>`;
    const reload = () => { st.data = null; st.key = ''; st.result = null; st.slipErrors = []; APP.refresh(); };
    el.querySelector('#owC').onchange = e => { st.customer = e.target.value; st.location = ''; reload(); };
    el.querySelector('#owL').onchange = e => { st.location = e.target.value; reload(); };
    el.querySelector('#owW').onchange = e => { st.week = e.target.value; reload(); };
    bindGrid(el);
    const key = [st.customer, st.location, st.week].join('|');
    if (Store.isLive && st.customer && st.location && st.key !== key && !st.loading) load(key);
    if (!canEdit || !Store.isLive) return;
    const need = () => { if (!st.data) { UI.toast('Chọn khách hàng, điểm giao và tuần trước', 'err'); return false; } return true; };
    const q = () => ({ customerId: st.customer, deliveryLocationId: st.location, weekStart: st.week });
    el.querySelector('#owCopy').onclick = () => { if (!need()) return; Store.query('Order/CopyPreviousWeek', q()).then(x => {
      if (!x.ok) return UI.toast(x.message || 'Không chép được số tuần trước', 'err');
      let n = 0; (x.data.listRow || []).forEach(r => { const ri = rowIndex(r); if (ri < 0) return; r.listCell.forEach((c, di) => { const own = st.data.listRow[ri].listCell[di]; if (own.locked || !own.available) return; st.qty[cellKey(ri, di)] = Number(c.quantity) || 0; n++; }); });
      st.dirty = true; UI.toast(`Đã chép số tuần trước vào ${n} ô – chưa lưu`, 'ok'); APP.refresh(); }); };
    el.querySelector('#owSlip').onclick = () => { if (need()) Store.download('Order/ExportWeekSlip', q()); };
    const file = el.querySelector('#owFile');
    el.querySelector('#owUp').onclick = () => { if (need()) file.click(); };
    file.onchange = () => {
      const fl = file.files[0]; file.value = ''; if (!fl) return;
      if (!/\.(xlsx|xlsm)$/i.test(fl.name)) return UI.toast('Chỉ nhận tệp .xlsx / .xlsm', 'err');
      const fd = new FormData(); fd.append('CustomerId', st.customer); fd.append('DeliveryLocationId', st.location); fd.append('WeekStart', st.week); fd.append('File', fl);
      Store.act('Order/ImportWeekSlip', { json: fd }).then(r => {
        if (!r.ok || !r.data) return;
        let n = 0; (r.data.listCell || []).forEach(c => { const ri = st.data.listRow.findIndex(x => x.contractMealId === c.contractMealId && (x.shiftId || null) === (c.shiftId || null) && (x.menuOptionNo || null) === (c.menuOptionNo || null)); const di = st.data.listDay.findIndex(x => String(x.mealDate).slice(0, 10) === String(c.mealDate).slice(0, 10)); if (ri < 0 || di < 0) return; st.qty[cellKey(ri, di)] = Number(c.quantity) || 0; n++; });
        st.slipErrors = r.data.listError || []; st.dirty = true;
        UI.toast(`Đã đọc ${n} ô từ phiếu${st.slipErrors.length ? `, ${st.slipErrors.length} dòng lỗi` : ''} – kiểm tra rồi Lưu nháp / Xác nhận`, st.slipErrors.length ? 'warn' : 'ok'); APP.refresh();
      });
    };
    const save = confirm => {
      if (!need()) return;
      const cells = [];
      st.data.listRow.forEach((r, ri) => r.listCell.forEach((c, di) => { if (c.locked || !c.available) return; cells.push({ mealDate: String(c.mealDate).slice(0, 10), contractMealId: r.contractMealId, shiftId: r.shiftId || null, menuOptionNo: r.menuOptionNo || null, quantity: Number(st.qty[cellKey(ri, di)]) || 0 }); }));
      const go = () => Store.act('Order/SaveWeek', { json: Object.assign(q(), { confirm, listCell: cells }), tables: ['Order', 'OrderLine', 'ApprovalRequest'] }).then(r => {
        if (!r.ok || !r.data) return;
        st.result = r.data; st.dirty = false;
        if (r.data.week) { st.data = r.data.week; fill(); }
        UI.toast(esc((r.res && r.res.responseMessage) || 'Đã lưu'), r.data.listDay.some(x => x.action === 'ERROR') ? 'warn' : 'ok'); APP.refresh();
      });
      if (!confirm) return go();
      const total = cells.reduce((a, c) => a + c.quantity, 0);
      UI.confirm(`Xác nhận cả tuần cho <b>${esc(st.data.locationName || '')}</b>: ${U.num(total)} suất. Mỗi ngày có số sẽ thành 1 đơn <b>Đã xác nhận</b>; ngày đã chốt bỏ qua.`, go, { ok: 'Xác nhận cả tuần' });
    };
    el.querySelector('#owDraft').onclick = () => save(false);
    el.querySelector('#owConfirm').onclick = () => save(true);
  });

  function rowIndex(r) { return st.data ? st.data.listRow.findIndex(x => x.contractMealId === r.contractMealId && (x.shiftId || null) === (r.shiftId || null) && (x.menuOptionNo || null) === (r.menuOptionNo || null)) : -1; }
  function fill() { st.qty = {}; (st.data.listRow || []).forEach((r, ri) => r.listCell.forEach((c, di) => { st.qty[cellKey(ri, di)] = Number(c.quantity) || 0; })); }
  function load(key) {
    st.loading = true; st.key = key;
    Store.query('Order/GetWeek', { customerId: st.customer, deliveryLocationId: st.location, weekStart: st.week }).then(x => {
      st.loading = false;
      if (key !== [st.customer, st.location, st.week].join('|')) return;
      if (!x.ok || !x.data) { st.data = null; UI.toast(x.message || 'Không tải được tuần', 'err'); APP.refresh(); return; }
      st.data = x.data; st.week = String(x.data.weekStart).slice(0, 10); st.key = [st.customer, st.location, st.week].join('|'); fill(); st.dirty = false; APP.refresh();
    });
  }
  // Nhóm hàng theo buổi (giữ thứ tự máy chủ): [{ meal, rows: [{ r, ri }] }].
  function groups() {
    const out = []; (st.data.listRow || []).forEach((r, ri) => { const k = r.contractMealId + '|' + (r.shiftId || ''); let g = out.find(x => x.k === k); if (!g) { g = { k, r, rows: [] }; out.push(g); } g.rows.push({ r, ri }); });
    return out;
  }
  const price = (r, di) => { const c = r.listCell[di]; return Number(c.unitPrice) || 0; };
  function totals() {
    const day = Array(7).fill(0), money = Array(7).fill(0);
    (st.data.listRow || []).forEach((r, ri) => r.listCell.forEach((c, di) => { const v = Number(st.qty[cellKey(ri, di)]) || 0; day[di] += v; money[di] += v * price(r, di); }));
    return { day, money, qty: day.reduce((a, b) => a + b, 0), amount: money.reduce((a, b) => a + b, 0) };
  }
  function grid() {
    const d = st.data;
    if (!st.customer || !st.location) return '<div class="empty">Chọn khách hàng, điểm giao và tuần để nhập số suất theo từng lựa chọn.</div>';
    if (!d) return `<div class="empty">${st.loading ? 'Đang tải lưới tuần…' : 'Chưa có dữ liệu tuần.'}</div>`;
    if (!d.listRow.length) return `<div class="notice warn">${I('alert')}<div>${esc((d.listWarning || [])[0] || 'Khách chưa có hợp đồng hiệu lực trong tuần này.')}</div></div>`;
    const t = totals();
    let h = `<table class="table dc-table ow-grid"><colgroup><col class="ow-c0">${d.listDay.map(() => "<col>").join("")}</colgroup><thead><tr><th class="ow-first">Buổi / lựa chọn</th>${d.listDay.map((x, di) => `<th class="${x.locked ? 'ow-lock' : ''}" title="${esc(x.lockReason || '')}">${DOW[di]} ${U.date(x.mealDate).slice(0, 5)}${x.locked ? ` <span class="badge gray">Khóa</span>` : ''}${x.orderNumber ? `<span class="ow-ord"><a href="javascript:void 0" data-ow-order="${x.orderId}">${esc(x.orderNumber)}</a></span><span class="small muted">${esc(x.statusName || '')}</span>` : ''}</th>`).join('')}</tr></thead><tbody>`;
    groups().forEach(g => {
      const p = g.r.listCell.map(c => c.unitPrice).find(v => v != null);
      h += `<tr class="ow-meal"><td colspan="8">${esc(mealName(g.r))}${g.r.shiftName ? ` <span class="small muted">· ca ${esc(g.r.shiftName)}</span>` : ''}${p != null ? ` · ${U.money(p)}/suất` : ''}</td></tr>`;
      g.rows.forEach(({ r, ri }) => {
        h += `<tr><td class="ow-first"><b>${r.menuOptionNo ? (g.rows.length > 1 ? 'Lựa chọn ' + r.menuOptionNo : esc(mealName(r))) : 'Không theo lựa chọn'}</b></td>${r.listCell.map((c, di) => {
          if (!c.available) return `<td class="ow-cell ow-na"><span class="muted">–</span></td>`;
          const v = st.qty[cellKey(ri, di)] || '';
          return `<td class="ow-cell ${c.locked ? 'ow-lock' : ''}"><div class="ow-dish">${esc(c.dishText || (r.menuOptionNo ? '' : 'Chưa có thực đơn'))}</div><input class="input ow-qty" inputmode="numeric" data-ow="${cellKey(ri, di)}" value="${v}" ${c.locked ? 'disabled' : ''}></td>`;
        }).join('')}</tr>`;
      });
      if (g.rows.length > 1) h += `<tr class="ow-sum"><td class="ow-first">Tổng ${esc(mealName(g.r)).toLowerCase()}</td>${d.listDay.map((_, di) => `<td class="num" data-ows="${g.k}|${di}">${U.num(g.rows.reduce((a, x) => a + (Number(st.qty[cellKey(x.ri, di)]) || 0), 0))}</td>`).join('')}</tr>`;
    });
    h += `<tr class="ow-tot"><td class="ow-first">Tổng suất</td>${t.day.map((v, di) => `<td class="num" data-owt="${di}">${U.num(v)}</td>`).join('')}</tr>`;
    h += `<tr class="ow-tot"><td class="ow-first">Thành tiền</td>${t.money.map((v, di) => `<td class="num" data-owm="${di}">${U.num(v, 0)}</td>`).join('')}</tr>`;
    return h + '</tbody></table>';
  }
  function side() {
    const d = st.data;
    if (!d || !d.listRow.length) return '';
    const t = totals();
    const prices = (d.listPrice || []).slice().sort((a, b) => ['SANG', 'TRUA', 'TOI', 'DEM'].indexOf(String(a.mealType).toUpperCase()) - ['SANG', 'TRUA', 'TOI', 'DEM'].indexOf(String(b.mealType).toUpperCase())).map(p => `${esc(MEAL_LABEL[String(p.mealType || '').toUpperCase()] || p.mealName || p.mealType)} ${p.unitPrice != null ? U.num(p.unitPrice, 0) : '–'}`).join(' · ');
    const res = st.result;
    const ACT = { CREATED: ['green', 'Tạo đơn'], UPDATED: ['blue', 'Sửa đơn'], UNCHANGED: ['gray', 'Không đổi'], SKIPPED: ['orange', 'Bỏ qua'], ERROR: ['red', 'Lỗi'], APPROVAL: ['orange', 'Chờ duyệt'] };
    const warns = [...(d.listWarning || []), ...((res && res.listWarning) || [])];
    return `<div class="card panel mb"><h3>Tổng tuần</h3><div class="kv ow-kv"><div><label>Tổng suất</label><span data-ow-sum><b>${U.num(t.qty)}</b></span></div><div><label>Thành tiền (chưa VAT)</label><span data-ow-amount><b>${U.money(t.amount)}</b></span></div><div style="grid-column:1/-1"><label>Giá theo hợp đồng ${esc(d.contractNumber || '')}</label><span>${prices}</span></div><div style="grid-column:1/-1"><label>Hạn gửi phiếu</label><span>${U.dt(d.deadline)}</span></div></div>${st.dirty ? `<div class="notice warn small mt">${I('alert')}<div>Có số chưa lưu – bấm Lưu nháp hoặc Xác nhận cả tuần.</div></div>` : ''}</div>
      <div class="card panel mb"><h3>Khi bấm "Xác nhận cả tuần"</h3><div class="small" style="line-height:1.6">• Mỗi ngày có số → <b>1 đơn</b> cho ${esc(d.locationName || 'điểm giao')}, trạng thái <b>Đã xác nhận</b>.<br>• Mỗi đơn có dòng theo <b>buổi × lựa chọn</b>; đơn giá theo buổi.<br>• Vẫn mở từng đơn để sửa tới 20:00 ngày hôm trước.<br>• 20:00 D-1 đơn được chốt → vào kế hoạch sản xuất.</div></div>
      ${st.slipErrors.length ? `<div class="card panel mb"><h3>Lỗi đọc phiếu</h3><div class="small bad" style="line-height:1.6">${st.slipErrors.map(esc).join('<br>')}</div></div>` : ''}
      ${warns.length ? `<div class="notice warn mb">${I('alert')}<div>${warns.map(esc).join('<br>')}</div></div>` : ''}
      ${res ? `<div class="card panel"><h3>Kết quả lưu</h3>${(res.listDay || []).map(x => `<div class="spread small" style="padding:4px 0;border-bottom:1px solid #F0F2ED"><span>${DOW[(new Date(String(x.mealDate).slice(0, 10) + 'T00:00').getDay() + 6) % 7]} ${U.date(x.mealDate).slice(0, 5)} ${x.orderNumber ? '· ' + esc(x.orderNumber) : ''}</span><span>${UI.tag ? `<span class="badge ${(ACT[x.action] || ['gray'])[0]}">${(ACT[x.action] || [0, x.action])[1]}</span>` : esc(x.action)}</span></div>${x.message ? `<div class="small muted">${esc(x.message)}</div>` : ''}`).join('') || '<div class="small muted">Không có ngày nào thay đổi.</div>'}</div>` : ''}`;
  }
  function bindGrid(el) {
    el.querySelectorAll('[data-ow]').forEach(inp => inp.addEventListener('input', () => {
      inp.value = inp.value.replace(/\D/g, ''); st.qty[inp.dataset.ow] = Number(inp.value) || 0; st.dirty = true;
      // Tự cộng tổng buổi / ngày / tuần tại chỗ (không vẽ lại cả bảng để giữ con trỏ).
      const t = totals();
      t.day.forEach((v, di) => { const a = el.querySelector(`[data-owt="${di}"]`); if (a) a.textContent = U.num(v); const b = el.querySelector(`[data-owm="${di}"]`); if (b) b.textContent = U.num(t.money[di], 0); });
      groups().forEach(g => { if (g.rows.length < 2) return; for (let di = 0; di < 7; di++) { const c = el.querySelector(`[data-ows="${g.k}|${di}"]`); if (c) c.textContent = U.num(g.rows.reduce((a, x) => a + (Number(st.qty[cellKey(x.ri, di)]) || 0), 0)); } });
      const s = el.querySelector('[data-ow-sum]'); if (s) s.innerHTML = `<b>${U.num(t.qty)}</b>`; const m = el.querySelector('[data-ow-amount]'); if (m) m.innerHTML = `<b>${U.money(t.amount)}</b>`;
    }));
    el.querySelectorAll('[data-ow-order]').forEach(a => a.onclick = () => APP.openDetail('Order', a.dataset.owOrder));   // UI-6: trang chi tiết đơn (DEMO: ngăn trượt)
  }
})();
