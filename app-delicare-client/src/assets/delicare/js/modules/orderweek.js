/* UI-6.6 (D-85) – Đăng ký suất tuần theo mẫu chuẩn D-84 (g), mô phỏng docs/frontend-handoff/assets/order-week-v2-mockup.html:
 * tuần ◀ ▶ (mặc định tuần sau) · 4 bước (Gửi phiếu → Nhận số → Kiểm tra & xác nhận → Hệ thống tự chốt) · 4 chỉ số ·
 * tab Tổng quan tuần (mọi khách / điểm giao × ngày) · Nhập số theo điểm giao (buổi → lựa chọn × ngày, lưu Nháp khi rời ô) · Lịch sử.
 * API: Order/GetWeekOverview · ConfirmWeekPoints · ImportWeekSlips · GetWeek · SaveWeek · CopyPreviousWeek · FillWeekFromForecast · ExportWeekSlip · Common/GetHistory. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const MEAL_LABEL = { SANG: 'Sáng', TRUA: 'Trưa', TOI: 'Tối', DEM: 'Đêm' };
  const DOW = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
  const mondayOf = ymd => { const d = new Date(ymd + 'T00:00'); return U.addDays(ymd, -((d.getDay() + 6) % 7)); };
  const mealName = r => MEAL_LABEL[String(r.mealType || '').toUpperCase()] || r.mealName || r.mealType || '';
  const has = v => v !== null && v !== undefined;
  const short = v => { v = Number(v) || 0; return v >= 1e9 ? U.num(Math.round(v / 1e7) / 100) + " tỷ" : v >= 1e6 ? U.num(Math.round(v / 1e5) / 10) + " tr" : U.num(v); };
  const ST_VI = { NONE: 'Chưa đăng ký', DRAFT: 'Nháp', SUBMITTED: 'Khách gửi – chờ xác nhận', CONFIRMED: 'Đã xác nhận', LOCKED: 'Đã chốt', NOCONTRACT: 'Ngoài hợp đồng', MIXED: 'Nhiều trạng thái' };
  const ST_CLS = { NONE: 'none', DRAFT: 'draft', SUBMITTED: 'bot', CONFIRMED: 'ok', LOCKED: 'lock', NOCONTRACT: 'nc', MIXED: 'draft' };
  // Trạng thái màn giữ khi vẽ lại (Store tải bảng xong gọi APP.refresh).
  const st = { week: '', tab: 'ov', ov: null, ovKey: '', closed: new Set(), init: '', q: '', cur: -1, data: null, dataKey: '', qty: {}, dirty: false, collapsed: new Set(), hist: null, histKey: '' };
  const cellKey = (ri, di) => ri + '|' + di;
  const nextMonday = () => U.addDays(mondayOf(Store.ctx.date), 7);
  const points = () => st.ov ? st.ov.listCustomer.flatMap(c => c.listPoint.map(p => Object.assign({ customerId: c.customerId, customerName: c.customerName }, p))) : [];

  APP.register('crm-order-week', (el, f, params) => {
    const perm = Store.perm(f.id), canEdit = perm.edit !== false;
    // UI-6.15: mở từ Đơn hàng theo ngày – đúng tuần (?week=) và đúng điểm giao (?loc=), áp 1 lần cho mỗi link.
    const pk = params && (params.week || params.loc) ? (params.week || '') + '|' + (params.loc || '') : '';
    if (pk && pk !== st.lastParam) { st.lastParam = pk; if (params.week) { st.week = params.week; st.ov = null; st.data = null; st.dataKey = ''; } if (params.loc) { st.tab = 'pt'; st.wantLoc = params.loc; } }
    if (!st.week) st.week = nextMonday();
    if (!Store.isLive) { el.innerHTML = APP.header(f, '', { noNote: true }) + `<div class="notice gap">${I('alert')}<div>Màn đăng ký suất tuần dùng dữ liệu API – chuyển sang chế độ API để nhập.</div></div>`; return; }
    el.innerHTML = APP.header(f, '', { noNote: true }) + `
      <div class="notice info mb">${I('help')}<div>Ghi số suất khách đăng ký cho từng điểm giao, từng buổi / lựa chọn trong tuần. Số đã xác nhận đi thẳng vào đơn hàng; 20:00 hôm trước hệ thống tự chốt từng ngày. Điểm chưa có số lúc chốt: hệ thống <span style="font-weight:700">không tự tạo đơn</span> mà báo KD + Trưởng KD + COO.</div></div>
      <div class="card panel mb" id="owHead"><div class="fw-week"><button class="btn sm" id="owPrev" title="Tuần trước" aria-label="Tuần trước">◀</button><span id="owLbl"></span><button class="btn sm" id="owNext" title="Tuần kế tiếp" aria-label="Tuần kế tiếp">▶</button><button class="btn sm" id="owNow">Tuần sau</button></div>
        <div class="fw-steps ow-steps">
          <div class="fw-step"><span class="n s1">1</span><div><b>Gửi phiếu cho khách</b><span class="small muted">Phiếu có thực đơn + ô số suất</span></div><button class="btn sm" id="owSend">${I('down')} <span id="owSendT">Tải phiếu</span></button></div>
          <div class="fw-step"><span class="n s2">2</span><div><b>Nhận số</b><span class="small muted">Tải phiếu khách gửi lại / nhập tay / khách gửi qua bot</span></div><button class="btn sm" id="owUp">${I('file')} Tải phiếu lên (nhiều file)</button><input type="file" id="owFiles" accept=".xlsx,.xlsm" multiple hidden></div>
          <div class="fw-step"><span class="n s3">3</span><div><b>Kiểm tra &amp; xác nhận</b><span class="small muted">Ô ⚠ lệch &gt; 30% so TB 4 tuần</span></div><button class="btn primary sm" id="owConfAll">${I('check')} <span id="owConfT">Xác nhận</span></button></div>
          <div class="fw-step"><span class="n s4">4</span><div><b>Hệ thống tự chốt</b><span class="small muted" id="owCut">20:00 hôm trước, từng ngày. 18:00 nhắc điểm chưa có số.</span></div></div>
        </div><div id="owKpis"></div></div>
      <div class="card panel"><div class="tabs"><button class="tab" data-owtab="ov">Tổng quan tuần</button><button class="tab" data-owtab="pt">Nhập số theo điểm giao</button><button class="tab" data-owtab="hist">Lịch sử</button></div><div id="owBody"></div></div>`;
    const g = s => el.querySelector(s);
    const go = wk => { st.week = wk; st.ov = null; st.data = null; st.dataKey = ''; st.hist = null; st.histKey = ''; draw(el, f); loadOv(el, f, true); };
    g('#owPrev').onclick = () => go(U.addDays(st.week, -7)); g('#owNext').onclick = () => go(U.addDays(st.week, 7)); g('#owNow').onclick = () => go(nextMonday());
    el.querySelectorAll('[data-owtab]').forEach(t => t.onclick = () => { st.tab = t.dataset.owtab; draw(el, f); });
    // ① Gửi phiếu: tải phiếu Excel cho mọi điểm chưa đăng ký (bot gửi khách khi có kênh).
    g('#owSend').onclick = () => {
      const list = points().filter(p => p.status === 'NONE' && p.listCell.some(c => c.status === 'NONE'));
      if (!list.length) return UI.toast('Mọi điểm giao đã có số trong tuần', 'ok');
      UI.confirm(`Tải <b>${list.length}</b> phiếu đăng ký (mỗi điểm giao chưa đăng ký một tệp Excel) để gửi khách?<div class="small muted mt">${list.slice(0, 8).map(p => esc(p.customerName + ' · ' + p.locationName)).join('<br>')}${list.length > 8 ? '<br>…' : ''}</div>`, async () => {
        for (const p of list) await Store.download('Order/ExportWeekSlip', { customerId: p.customerId, deliveryLocationId: p.deliveryLocationId, weekStart: st.week });
        UI.toast('Đã tải ' + list.length + ' phiếu', 'ok');
      }, { ok: 'Tải phiếu', title: 'Gửi phiếu cho khách' });
    };
    // ② Nhận số: tải nhiều phiếu một lần – máy chủ tự nhận điểm qua mã ẩn, lưu Nháp.
    const files = g('#owFiles');
    g('#owUp').onclick = () => files.click();
    files.onchange = () => {
      const fl = [...files.files]; files.value = ''; if (!fl.length) return;
      if (fl.some(x => !/\.(xlsx|xlsm)$/i.test(x.name))) return UI.toast('Chỉ nhận tệp .xlsx / .xlsm', 'err');
      const fd = new FormData(); fd.append('weekStart', st.week); fl.forEach(x => fd.append('files', x));
      Store.act('Order/ImportWeekSlips', { json: fd, tables: ['Order'] }).then(r => {
        if (!r.ok || !r.data) return; const d = r.data;
        UI.modal({ title: 'Kết quả tải phiếu', size: 'lg', body: `<div class="mb"><b>${U.num(d.saved)}</b> / ${U.num(d.files)} phiếu đã lưu Nháp · lỗi ${U.num(d.failed)}</div><div class="table-wrap"><table class="table dc-table"><thead><tr><th>Tệp</th><th>Khách · điểm giao</th><th class="num">Ô</th><th>Kết quả</th></tr></thead><tbody>${d.listFile.map(x => `<tr><td>${esc(x.fileName || '')}</td><td>${esc([x.customerName, x.locationName].filter(Boolean).join(' · ') || '–')}</td><td class="num">${U.num(x.cells)}</td><td>${x.ok && x.cells ? `<span class="badge green">Đã lưu Nháp</span>` : x.ok ? `<span class="badge gray">Phiếu trống</span>` : `<span class="badge red">Lỗi</span>`} <span class="small">${esc(x.message || '')}</span>${(x.listError || []).length ? `<div class="small bad">${x.listError.map(esc).join('<br>')}</div>` : ''}</td></tr>`).join('')}</tbody></table></div>`, foot: '<button class="btn primary" data-close>Đóng</button>' });
        loadOv(el, f, true); st.dataKey = '';
      });
    };
    // ③ Xác nhận mọi điểm đang Nháp / khách gửi (trước giờ chốt).
    g('#owConfAll').onclick = () => {
      const n = st.ov ? st.ov.pendingPoints : 0; if (!n) return UI.toast('Không có điểm nào chờ xác nhận', 'ok');
      UI.confirm(`Xác nhận mọi đơn Nháp / khách gửi của <b>${n}</b> điểm giao tuần ${U.date(st.week).slice(0, 5)} – ${U.date(U.addDays(st.week, 6)).slice(0, 5)}?<div class="small muted mt">Ngày đã quá giờ chốt bỏ qua. Lệch dự báo &gt; 10% chỉ cảnh báo.</div>`, () =>
        Store.act('Order/ConfirmWeekPoints', { json: { weekStart: st.week, listDeliveryLocationId: [] }, tables: ['Order'] }).then(r => {
          if (!r.ok || !r.data) return; const d = r.data;
          UI.toast(esc((r.res && r.res.responseMessage) || 'Đã xác nhận'), d.failed ? 'warn' : 'ok');
          if (d.listError.length || d.listWarning.length) UI.modal({ title: 'Kết quả xác nhận', body: `${d.listError.length ? `<div class="bad small mb">${d.listError.map(esc).join('<br>')}</div>` : ''}${d.listWarning.length ? `<div class="small muted">${d.listWarning.map(esc).join('<br>')}</div>` : ''}`, foot: '<button class="btn primary" data-close>Đóng</button>' });
          loadOv(el, f, true); st.dataKey = '';
        }), { ok: 'Xác nhận', title: 'Xác nhận nhiều điểm' });
    };
    if (!canEdit) ['#owUp', '#owConfAll'].forEach(s => { g(s).disabled = true; g(s).title = 'Tài khoản không có quyền Sửa'; });
    if (canEdit && perm.imp === false) { g('#owUp').disabled = true; g('#owUp').title = 'Tài khoản không có quyền Nhập'; }
    if (perm.exp === false) { g('#owSend').disabled = true; g('#owSend').title = 'Tài khoản không có quyền Xuất'; }
    draw(el, f);
    loadOv(el, f, false);
  });

  function head(el) {
    const d = st.ov, nm = nextMonday(), rel = st.week === nm ? 'tuần sau' : st.week === U.addDays(nm, -7) ? 'tuần này' : '';
    const menu = d && d.listMenuCode.length ? 'thực đơn ' + d.listMenuCode.join(', ') + ' đã công bố' : d ? 'chưa có thực đơn chung đã công bố' : '';
    const lbl = el.querySelector('#owLbl'); if (!lbl) return;
    lbl.innerHTML = `Tuần ${U.date(st.week).slice(0, 5)} – ${U.date(U.addDays(st.week, 6))} <span class="small muted" style="font-weight:400">${[rel, menu].filter(Boolean).join(' · ')}</span>`;
    const unreg = d ? d.unregisteredPoints : 0;
    el.querySelector('#owSendT').textContent = d ? (unreg ? `Tải phiếu cho ${unreg} điểm chưa đăng ký` : 'Mọi điểm đã có số') : 'Tải phiếu';
    el.querySelector('#owConfT').textContent = d ? (d.pendingPoints ? `Xác nhận ${d.pendingPoints} điểm đã nhập` : 'Không có điểm chờ xác nhận') : 'Xác nhận';
    if (d) el.querySelector('#owCut').textContent = `20:00 hôm trước, từng ngày (lần chốt tới: ${U.dt(d.nextCutoff)}). 18:00 nhắc điểm chưa có số.`;
    const k = el.querySelector('#owKpis');
    k.innerHTML = d ? UI.kpis([
      UI.kpi('Điểm đã đăng ký', U.num(d.registeredPoints), '/ ' + U.num(d.pointCount) + ' điểm', 'pin', d.unregisteredPoints ? 'orange' : 'green'),
      UI.kpi('Tổng suất tuần', U.num(d.totalWeek), U.num(d.listCustomer.length) + ' khách', 'utensils', 'green'),
      UI.kpi('Thành tiền', short(d.totalAmount), 'chưa VAT · ' + U.money(d.totalAmount), 'calc', 'blue'),
      UI.kpi('Lệch so dự báo', has(d.forecastDeviationPct) ? (d.forecastDeviationPct > 0 ? '+' : '') + U.num(d.forecastDeviationPct) + '%' : '–', 'ô có cả đăng ký và dự báo', 'chart', has(d.forecastDeviationPct) && Math.abs(d.forecastDeviationPct) > 10 ? 'orange' : 'green')], 'compact') : '';
  }
  function draw(el, f) {
    el.querySelectorAll('[data-owtab]').forEach(t => t.classList.toggle('active', t.dataset.owtab === st.tab));
    head(el);
    const box = el.querySelector('#owBody'); if (!box) return;
    if (!st.ov) { box.innerHTML = '<div class="muted small">Đang tải…</div>'; return; }
    if (st.tab === 'ov') drawOv(el, f, box);
    else if (st.tab === 'pt') drawPoint(el, f, box);
    else drawHist(el, f, box);
  }
  function loadOv(el, f, force) {
    const key = st.week + '|' + (Store.ctx.branch || '');
    if (!force && st.ovKey === key && st.ov) return;
    st.ovKey = key;
    Store.query('Order/GetWeekOverview', { WeekStart: st.week, BranchId: Store.ctx.branch }).then(x => {
      if (st.ovKey !== key || !el.isConnected) return;
      if (!x.ok || !x.data) { const b = el.querySelector('#owBody'); if (b) b.innerHTML = `<div class="notice warn">${I('alert')}<div>${esc(x.message || 'Không tải được tuần')}</div></div>`; return; }
      st.ov = x.data;
      if (st.init !== st.week) { st.init = st.week; st.closed = new Set(st.ov.listCustomer.length > 10 ? st.ov.listCustomer.map(c => c.customerId) : []); }
      draw(el, f);
    });
  }

  // ---------- Tab Tổng quan tuần ----------
  function chip(c) {
    const v = has(c.quantity) ? U.num(c.quantity) : '—';
    const hint = c.status === 'NONE' ? (has(c.forecast) ? 'DB ' + U.num(c.forecast) : 'chưa có dự báo') : c.status === 'NOCONTRACT' ? 'ngoài hợp đồng' : (ST_VI[c.status] || '').toLowerCase();
    return `<div class="fw-cell"><span class="ow-chip ${ST_CLS[c.status] || ''}">${v}</span><span class="fw-hint">${esc(hint)}</span></div>`;
  }
  function drawOv(el, f, box) {
    const d = st.ov, days = d.listDate.map(U.day), qq = st.q.trim().toLowerCase();
    const all = d.listCustomer, cus = qq ? all.filter(c => ((c.customerName || '') + ' ' + (c.customerCode || '') + ' ' + c.listPoint.map(p => p.locationName).join(' ')).toLowerCase().includes(qq)) : all;
    if (!all.length) { box.innerHTML = '<div class="empty">Tuần này không có khách nào có hợp đồng hiệu lực.</div>'; return; }
    const sum = a => a.reduce((s, v) => s + (Number(v) || 0), 0);
    const flat = points(), idxOf = p => flat.findIndex(x => x.deliveryLocationId === p.deliveryLocationId);
    const th = days.map(k => `<th>${DOW[(new Date(k + 'T00:00').getDay() + 6) % 7]} ${U.date(k).slice(0, 5)}</th>`).join('');
    const tools = `<div class="fw-tools"><input class="input" id="owQ" placeholder="Lọc khách hàng / điểm giao…" value="${esc(st.q)}"><button class="btn sm" id="owOpenAll">Mở tất cả</button><button class="btn sm" id="owCloseAll">Thu gọn tất cả</button><span class="small muted">${U.num(cus.length)} / ${U.num(all.length)} khách · bấm dòng khách để mở / thu gọn</span></div>`;
    const legend = `<div class="fw-legend">${['NONE', 'DRAFT', 'SUBMITTED', 'CONFIRMED', 'LOCKED', 'NOCONTRACT'].map(s => `<span><i class="ow-chip sw ${ST_CLS[s]}"></i>${ST_VI[s]}${s === 'NONE' ? ' (chữ nhỏ = dự báo)' : ''}</span>`).join('')}</div>`;
    const desk = `<div class="table-wrap"><table class="fw-table"><colgroup><col class="name">${days.map(() => '<col>').join('')}<col class="tot"><col style="width:78px"></colgroup><thead><tr><th class="l">Khách hàng / điểm giao</th>${th}<th>Cả tuần</th><th></th></tr></thead><tbody>${cus.map((c, ci) => {
      const closed = st.closed.has(c.customerId);
      return `<tr class="fw-cus${closed ? ' closed' : ''}" data-owc="${ci}"><td class="l fw-name"><span class="fw-chev">▾</span> ${esc(c.customerName || c.customerCode || '')} <span class="small muted" style="font-weight:400">· ${c.listPoint.length} điểm</span></td>${c.listTotal.map(v => `<td class="tot">${v ? U.num(v) : '<span class="muted">–</span>'}</td>`).join('')}<td class="tot">${U.num(sum(c.listTotal))}</td><td></td></tr>`
        + (closed ? '' : c.listPoint.map(p => `<tr class="fw-sub"><td class="l">${esc(p.locationName || '')}</td>${p.listCell.map(x => `<td title="${esc((ST_VI[x.status] || '') + (x.orderNumber ? ' · ' + x.orderNumber : '') + (has(x.forecast) ? ' · dự báo ' + U.num(x.forecast) : ''))}">${chip(x)}</td>`).join('')}<td class="tot">${U.num(p.totalWeek)}</td><td><button class="btn xs" data-owgo="${idxOf(p)}">Nhập</button></td></tr>`).join(''));
    }).join('')}</tbody><tfoot><tr class="fw-foot"><td class="l">Tổng</td>${d.listTotal.map(v => `<td>${U.num(v)}</td>`).join('')}<td>${U.num(d.totalWeek)}</td><td></td></tr></tfoot></table></div>`;
    const mob = `<div class="fw-mob">${cus.map((c, ci) => `<div class="fw-card${st.closed.has(c.customerId) ? ' closed' : ''}"><div class="fw-card-h" data-owm="${ci}"><b><span class="fw-chev">▾</span> ${esc(c.customerName || '')}</b><span class="small muted">${U.num(sum(c.listTotal))} suất</span></div>${st.closed.has(c.customerId) ? '' : c.listPoint.map(p => `<div class="fw-card-r"><div class="spread"><b class="small">${esc(p.locationName || '')}</b><button class="btn xs" data-owgo="${idxOf(p)}">Nhập</button></div><div class="fw-days">${p.listCell.map((x, di) => `<label><span class="small muted">${DOW[di]} ${U.date(days[di]).slice(0, 5)}</span>${chip(x)}</label>`).join('')}</div></div>`).join('')}</div>`).join('')}</div>`;
    box.innerHTML = tools + legend + `<div class="fw-desk">${desk}</div>${mob}`;
    const q = box.querySelector('#owQ'); q.oninput = () => { st.q = q.value; clearTimeout(st.t); st.t = setTimeout(() => { drawOv(el, f, box); const n = box.querySelector('#owQ'); n.focus(); n.setSelectionRange(n.value.length, n.value.length); }, 250); };
    box.querySelector('#owOpenAll').onclick = () => { cus.forEach(c => st.closed.delete(c.customerId)); drawOv(el, f, box); };
    box.querySelector('#owCloseAll').onclick = () => { cus.forEach(c => st.closed.add(c.customerId)); drawOv(el, f, box); };
    const toggle = i => { const id = cus[i].customerId; st.closed.has(id) ? st.closed.delete(id) : st.closed.add(id); drawOv(el, f, box); };
    box.querySelectorAll('tr.fw-cus').forEach(tr => tr.onclick = () => toggle(+tr.dataset.owc));
    box.querySelectorAll('[data-owm]').forEach(h => h.onclick = () => toggle(+h.dataset.owm));
    box.querySelectorAll('[data-owgo]').forEach(b => b.onclick = e => { e.stopPropagation(); st.cur = +b.dataset.owgo; st.tab = 'pt'; draw(el, f); });
  }

  // ---------- Tab Nhập số theo điểm giao ----------
  function curPoint() { const fl = points(); if (!fl.length) return null; if (st.wantLoc) { const i = fl.findIndex(x => x.deliveryLocationId === st.wantLoc); if (i >= 0) { st.cur = i; st.wantLoc = ''; st.dataKey = ''; } }if (st.cur < 0 || st.cur >= fl.length) st.cur = 0; return fl[st.cur]; }
  function loadPoint(el, f, p, force) {
    const key = [p.customerId, p.deliveryLocationId, st.week].join('|');
    if (!force && st.dataKey === key) return;
    st.dataKey = key; st.data = null;
    Store.query('Order/GetWeek', { customerId: p.customerId, deliveryLocationId: p.deliveryLocationId, weekStart: st.week }).then(x => {
      if (st.dataKey !== key || !el.isConnected) return;
      if (!x.ok || !x.data) { UI.toast(x.message || 'Không tải được lưới tuần', 'err'); return; }
      st.data = x.data; fill(); st.dirty = false; if (st.tab === 'pt') draw(el, f);
    });
  }
  function fill() { st.qty = {}; (st.data.listRow || []).forEach((r, ri) => r.listCell.forEach((c, di) => { st.qty[cellKey(ri, di)] = Number(c.quantity) || 0; })); }
  function groups() {
    const out = []; (st.data.listRow || []).forEach((r, ri) => { const k = r.contractMealId + '|' + (r.shiftId || ''); let g = out.find(x => x.k === k); if (!g) { g = { k, r, rows: [] }; out.push(g); } g.rows.push({ r, ri }); });
    return out;
  }
  function totals() {
    const day = Array(7).fill(0), money = Array(7).fill(0);
    (st.data.listRow || []).forEach((r, ri) => r.listCell.forEach((c, di) => { const v = Number(st.qty[cellKey(ri, di)]) || 0; day[di] += v; money[di] += v * (Number(c.unitPrice) || 0); }));
    return { day, money };
  }
  const warn = (v, avg) => has(avg) && avg > 0 && v > 0 && Math.abs(v - avg) / avg > 0.3;
  function drawPoint(el, f, box) {
    const p = curPoint(), canEdit = Store.perm(f.id).edit !== false;
    if (!p) { box.innerHTML = '<div class="empty">Tuần này không có điểm giao nào có hợp đồng hiệu lực.</div>'; return; }
    const fl = points();
    loadPoint(el, f, p, false);
    const nav = `<div class="spread mb ow-pt-head"><div><div class="row-flex"><select class="select" id="owPick" style="max-width:420px">${fl.map((x, i) => `<option value="${i}" ${i === st.cur ? 'selected' : ''}>${esc(x.customerName + ' · ' + x.locationName)}</option>`).join('')}</select><span class="ow-chip ${ST_CLS[p.status] || ''}">${esc(ST_VI[p.status] || '')}</span></div></div>
      <div class="row-flex"><button class="btn sm" id="owPrevPt" ${st.cur <= 0 ? 'disabled' : ''}>◀ Điểm trước</button><button class="btn sm" id="owNextPt" ${st.cur >= fl.length - 1 ? 'disabled' : ''}>Điểm sau ▶</button></div></div>
      <div class="row-flex mb">${canEdit ? `<button class="btn sm" id="owCopy">${I('history')} Chép số tuần trước</button><button class="btn sm" id="owFc">${I('chart')} Lấy số dự báo</button>` : ''}<button class="btn sm" id="owPrint">${I('down')} In phiếu</button>${canEdit ? `<button class="btn primary sm" id="owConfPt">${I('check')} Xác nhận điểm này</button>${st.dirty ? `<button class="btn orange sm" id="owSaveAll">${I('ok')} Lưu nháp các ô đã điền</button>` : ''}` : ''}</div>`;
    const d = st.data;
    if (!d) { box.innerHTML = nav + '<div class="muted small">Đang tải lưới tuần…</div>'; bindNav(el, f, box, p); return; }
    if (!d.listRow.length) { box.innerHTML = nav + `<div class="notice warn">${I('alert')}<div>${esc((d.listWarning || [])[0] || 'Khách chưa có hợp đồng hiệu lực trong tuần này.')}</div></div>`; bindNav(el, f, box, p); return; }
    const t = totals();
    let h = `<div class="table-wrap"><table class="table dc-table ow-grid"><colgroup><col class="ow-c0">${d.listDay.map(() => '<col>').join('')}</colgroup><thead><tr><th class="ow-first">Buổi / lựa chọn</th>${d.listDay.map((x, di) => `<th class="${x.locked ? 'ow-lock' : ''}" title="${esc(x.lockReason || '')}">${DOW[di]} ${U.date(x.mealDate).slice(0, 5)}${x.locked ? ' <span class="badge gray">Khóa</span>' : ''}${x.orderNumber ? `<span class="ow-ord"><a href="javascript:void 0" data-ow-order="${x.orderId}">${esc(x.orderNumber)}</a></span><span class="small muted">${esc(x.statusName || '')}</span>` : ''}</th>`).join('')}</tr></thead><tbody>`;
    groups().forEach(g => {
      const price = g.r.listCell.map(c => c.unitPrice).find(v => v != null), closed = st.collapsed.has(g.k);
      const gsum = di => g.rows.reduce((a, x) => a + (Number(st.qty[cellKey(x.ri, di)]) || 0), 0);
      h += `<tr class="ow-meal ow-meal-h${closed ? ' closed' : ''}" data-owg="${esc(g.k)}"><td class="ow-first"><span class="fw-chev">▾</span> ${esc(mealName(g.r))}${g.r.shiftName && g.r.shiftName.toLowerCase() !== mealName(g.r).toLowerCase() ? ` <span class="small muted">· ca ${esc(g.r.shiftName)}</span>` : ''}<div class="small muted" style="font-weight:400">${price != null ? U.money(price) + '/suất · ' : ''}${g.rows.length} lựa chọn</div></td>${d.listDay.map((_, di) => `<td class="num" data-ows="${esc(g.k)}|${di}"><b>${U.num(gsum(di))}</b></td>`).join('')}</tr>`;
      if (closed) return;
      g.rows.forEach(({ r, ri }) => {
        h += `<tr><td class="ow-first">${r.menuOptionNo ? (g.rows.length > 1 ? 'Lựa chọn ' + r.menuOptionNo : esc(mealName(r))) : 'Không theo lựa chọn'}</td>${r.listCell.map((c, di) => {
          if (!c.available) return '<td class="ow-cell ow-na"><span class="muted">–</span></td>';
          const v = st.qty[cellKey(ri, di)] || 0, w = warn(v, c.average4Weeks);
          const tip = (c.dishText || '') + (has(c.average4Weeks) ? ' · TB 4 tuần ' + U.num(c.average4Weeks) : '') + (w ? ' · ⚠ lệch > 30%' : '');
          return `<td class="ow-cell ${c.locked ? 'ow-lock' : ''}" title="${esc(tip)}"><div class="row-flex" style="flex-wrap:nowrap;gap:3px;justify-content:center">${w ? '<span class="ow-warn" aria-label="Lệch hơn 30% so trung bình 4 tuần">⚠</span>' : ''}<input class="input ow-qty" inputmode="numeric" data-ow="${cellKey(ri, di)}" value="${v || ''}" ${c.locked || !canEdit ? 'disabled' : ''}></div><div class="ow-dish">${esc(c.dishText || (r.menuOptionNo ? '' : 'Chưa có thực đơn'))}</div></td>`;
        }).join('')}</tr>`;
      });
    });
    h += `<tr class="ow-tot"><td class="ow-first">Tổng suất</td>${t.day.map((v, di) => `<td class="num" data-owt="${di}">${U.num(v)}</td>`).join('')}</tr>`;
    h += `<tr class="ow-tot"><td class="ow-first">Thành tiền</td>${t.money.map((v, di) => `<td class="num" data-owmn="${di}">${U.num(v, 0)}</td>`).join('')}</tr></tbody></table></div>`;
    box.innerHTML = nav + h + `<div class="small muted mt">Mỗi ô = số suất của lựa chọn; chữ nhỏ = món trong set (rê chuột xem đủ + TB 4 tuần). ⚠ lệch &gt; 30% so TB 4 tuần cùng thứ – chỉ cảnh báo. Gõ số rồi rời ô = lưu Nháp. Ngày đã chốt khóa – sửa qua "Yêu cầu thay đổi sau giờ chốt".</div>`;
    bindNav(el, f, box, p);
    bindGrid(el, f, box, p);
  }
  function cellsOf(filterFn) {
    const out = [];
    st.data.listRow.forEach((r, ri) => r.listCell.forEach((c, di) => { if (c.locked || !c.available || (filterFn && !filterFn(ri, di))) return; out.push({ mealDate: String(c.mealDate).slice(0, 10), contractMealId: r.contractMealId, shiftId: r.shiftId || null, menuOptionNo: r.menuOptionNo || null, quantity: Number(st.qty[cellKey(ri, di)]) || 0 }); }));
    return out;
  }
  function save(el, f, p, cells, confirm) {
    return Store.act('Order/SaveWeek', { json: { customerId: p.customerId, deliveryLocationId: p.deliveryLocationId, weekStart: st.week, confirm, listCell: cells }, tables: ['Order', 'OrderLine', 'ApprovalRequest'], quiet: !confirm }).then(r => {
      if (!r.ok || !r.data) { if (!confirm) UI.toast(esc(r.message || 'Không lưu được'), 'err'); return r; }
      const bad = (r.data.listDay || []).filter(x => ['ERROR', 'APPROVAL', 'SKIPPED'].includes(x.action) && x.message);
      if (r.data.week) { st.data = r.data.week; fill(); st.dirty = false; }
      if (bad.length) UI.toast(bad.map(x => U.date(x.mealDate).slice(0, 5) + ': ' + esc(x.message)).join('<br>'), 'warn');
      else UI.toast(confirm ? esc((r.res && r.res.responseMessage) || 'Đã xác nhận') : 'Đã lưu Nháp', 'ok');
      loadOv(el, f, true); st.hist = null; st.histKey = '';
      return r;
    });
  }
  function bindNav(el, f, box, p) {
    const fl = points(), b = s => box.querySelector(s);
    const to = i => { if (st.dirty && !window.confirm('Có số chưa lưu – bỏ qua và chuyển điểm?')) return; st.cur = i; st.dataKey = ''; st.dirty = false; draw(el, f); };
    b('#owPick').onchange = e => to(+e.target.value);
    b('#owPrevPt').onclick = () => to(Math.max(0, st.cur - 1));
    b('#owNextPt').onclick = () => to(Math.min(fl.length - 1, st.cur + 1));
    b('#owPrint').onclick = () => Store.download('Order/ExportWeekSlip', { customerId: p.customerId, deliveryLocationId: p.deliveryLocationId, weekStart: st.week });
    if (!st.data) return;
    const pull = (path, msg) => Store.query(path, { customerId: p.customerId, deliveryLocationId: p.deliveryLocationId, weekStart: st.week }).then(x => {
      if (!x.ok || !x.data) return UI.toast(esc(x.message || 'Không lấy được số'), 'err');
      let n = 0;
      (x.data.listRow || []).forEach(r => { const ri = st.data.listRow.findIndex(y => y.contractMealId === r.contractMealId && (y.shiftId || null) === (r.shiftId || null) && (y.menuOptionNo || null) === (r.menuOptionNo || null)); if (ri < 0) return;
        r.listCell.forEach((c, di) => { const own = st.data.listRow[ri].listCell[di]; if (own.locked || !own.available || !(Number(c.quantity) > 0) || (Number(st.qty[cellKey(ri, di)]) || 0) > 0) return; st.qty[cellKey(ri, di)] = Number(c.quantity); n++; }); });
      st.dirty = n > 0; UI.toast(n ? `${msg}: điền ${n} ô trống – bấm "Lưu nháp các ô đã điền"` : esc((x.data.listWarning || [])[0] || 'Không có số để điền vào ô trống'), n ? 'ok' : 'warn'); draw(el, f);
    });
    if (b('#owCopy')) b('#owCopy').onclick = () => pull('Order/CopyPreviousWeek', 'Chép tuần trước');
    if (b('#owFc')) b('#owFc').onclick = () => pull('Order/FillWeekFromForecast', 'Lấy số dự báo');
    if (b('#owSaveAll')) b('#owSaveAll').onclick = () => save(el, f, p, cellsOf(), false);
    if (b('#owConfPt')) b('#owConfPt').onclick = () => {
      const cells = cellsOf(), total = cells.reduce((a, c) => a + c.quantity, 0);
      UI.confirm(`Xác nhận <b>${esc(p.locationName)}</b>: ${U.num(total)} suất cả tuần. Mỗi ngày có số → 1 đơn <b>Đã xác nhận</b>; ngày đã chốt bỏ qua.`, () => save(el, f, p, cells, true), { ok: 'Xác nhận điểm này' });
    };
    box.querySelectorAll('[data-owg]').forEach(tr => tr.onclick = () => { const k = tr.dataset.owg; st.collapsed.has(k) ? st.collapsed.delete(k) : st.collapsed.add(k); draw(el, f); });
    box.querySelectorAll('[data-ow-order]').forEach(a => a.onclick = () => APP.openDetail('Order', a.dataset.owOrder));
  }
  function bindGrid(el, f, box, p) {
    box.querySelectorAll('[data-ow]').forEach(inp => {
      const [ri, di] = inp.dataset.ow.split('|').map(Number);
      inp.addEventListener('input', () => {
        inp.value = inp.value.replace(/\D/g, ''); st.qty[inp.dataset.ow] = Number(inp.value) || 0;
        const t = totals();
        t.day.forEach((v, k) => { const a = box.querySelector(`[data-owt="${k}"]`); if (a) a.textContent = U.num(v); const m = box.querySelector(`[data-owmn="${k}"]`); if (m) m.textContent = U.num(t.money[k], 0); });
        groups().forEach(g => { const c = box.querySelector(`[data-ows="${g.k}|${di}"]`); if (c) c.innerHTML = `<b>${U.num(g.rows.reduce((a, x) => a + (Number(st.qty[cellKey(x.ri, di)]) || 0), 0))}</b>`; });
      });
      inp.onkeydown = e => { if (e.key === 'Enter') { e.preventDefault(); inp.blur(); } };
      // Lưu Nháp khi rời ô: gửi cả ngày đó (mọi lựa chọn) để đơn trong ngày đúng số.
      inp.onchange = () => {
        const orig = Number(st.data.listRow[ri].listCell[di].quantity) || 0;
        if ((Number(st.qty[inp.dataset.ow]) || 0) === orig) return;
        save(el, f, p, cellsOf((r2, d2) => d2 === di), false);
      };
    });
  }

  // ---------- Tab Lịch sử ----------
  // Tóm tắt thay đổi đơn: trạng thái (Nháp → Đã gửi → Đã xác nhận…), tổng suất, số suất từng dòng – số trước → sau.
  const ch = (x, f) => (x.listChange || []).find(c => c.field === f);
  const stv = v => (UI.statusVi && UI.statusVi('Order', 'Status', v)) || v || '–';
  const qv = v => has(v) && v !== '' ? U.num(Number(v)) : '–';
  function histTitle(x) {
    const s = ch(x, 'Status');
    if (x.entityName === 'OrderLine') return (x.action === 'Added' ? 'Thêm dòng' : x.action === 'Deleted' ? 'Bỏ dòng' : 'Sửa số dòng');
    if (s && s.to === 'Confirmed') return 'Xác nhận';
    if (s && s.to === 'Submitted') return 'Gửi';
    if (s && s.to === 'Locked') return 'Chốt';
    if (s && s.to === 'Cancelled') return 'Hủy';
    return x.action === 'Added' ? 'Tạo đơn' : (x.actionName || x.action || '');
  }
  function histDetail(x) {
    const parts = [], s = ch(x, 'Status'), t = ch(x, 'TotalQuantity'), q = ch(x, 'Quantity'), mt = ch(x, 'MealType'), op = ch(x, 'MenuOptionNo'), n = ch(x, 'Note');
    if (s) parts.push('Trạng thái: ' + (s.from ? stv(s.from) + ' → ' : '') + stv(s.to));
    if (t) parts.push('Tổng suất: ' + qv(t.from) + ' → ' + qv(t.to));
    if (q) parts.push((mt ? (MEAL_LABEL[String(mt.to || '').toUpperCase()] || mt.to) + (op && op.to ? ' LC' + op.to : '') + ': ' : 'Số suất: ') + qv(q.from) + ' → ' + qv(q.to));
    if (n && n.to) parts.push('Ghi chú: ' + String(n.to).slice(0, 140));
    return parts.length ? '<div class="small">' + parts.map(esc).join(' · ') + '</div>' : '';
  }
  function drawHist(el, f, box) {
    const p = curPoint();
    if (!p) { box.innerHTML = '<div class="empty">Chưa có điểm giao.</div>'; return; }
    const fl = points();
    const ids = p.listCell.filter(c => c.orderId).map(c => ({ id: c.orderId, no: c.orderNumber, day: c.date }));
    box.innerHTML = `<div class="row-flex mb"><select class="select" id="owHPick" style="max-width:420px">${fl.map((x, i) => `<option value="${i}" ${i === st.cur ? 'selected' : ''}>${esc(x.customerName + ' · ' + x.locationName)}</option>`).join('')}</select><span class="small muted">Ai nhập / sửa / xác nhận / tải phiếu / khách gửi qua bot, thời điểm, số trước → sau (lịch sử đơn của tuần).</span></div><div id="owHist">${ids.length ? '<div class="muted small">Đang tải…</div>' : '<div class="empty">Điểm giao chưa có đơn nào trong tuần.</div>'}</div>`;
    box.querySelector('#owHPick').onchange = e => { st.cur = +e.target.value; st.dataKey = ''; draw(el, f); };
    if (!ids.length) return;
    const key = p.deliveryLocationId + '|' + st.week + '|' + ids.map(x => x.id).join(',');
    const render = list => { const h = box.querySelector('#owHist'); if (!h) return; h.innerHTML = list.length ? `<div class="timeline">${list.map(x => `<div class="tl ${/DELETE|REJECT|CANCEL/i.test(x.action || '') ? 'red' : /INSERT|CREATE/i.test(x.action || '') ? '' : 'orange'}"><b>${esc(x.no || '')} · ${U.date(U.day(x.day)).slice(0, 5)} – ${esc(histTitle(x))}</b>${histDetail(x)}<small>${esc(U.dt(String(x.at || '').replace(/\.\d+$/, '')))} · ${esc(x.byName || 'Hệ thống')}${x.comment ? ' · ' + esc(x.comment) : ''}</small></div>`).join('')}</div>` : '<div class="empty">Chưa có lịch sử.</div>'; };
    if (st.histKey === key && st.hist) return render(st.hist);
    st.histKey = key;
    Promise.all(ids.map(o => Store.query('Common/GetHistory', { resource: 'Order', id: o.id }).then(x => (x.ok ? x.list || [] : []).map(h => Object.assign({ no: o.no, day: o.day }, h))))).then(lists => {
      if (st.histKey !== key) return;
      st.hist = lists.flat().sort((a, b) => String(b.at || '').localeCompare(String(a.at || ''))); render(st.hist);
    });
  }
})();
