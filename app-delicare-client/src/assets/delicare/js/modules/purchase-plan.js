/* UI-6.51 (D-137 h): màn "Kế hoạch mua" – thay khối Kế hoạch mua 7 ngày trong màn Tính nhu cầu nguyên liệu (giữ route / quyền scm-requirement – UI-6.44).
 * Ma trận nguyên liệu (theo nhóm) × 7 ngày cần hàng (mẫu D-84 g): ô = số lượng + trạng thái (Đã đặt / Tự duyệt – chờ gửi NCC / Chờ duyệt / Đặt hôm nay / Quá hạn đặt / Dự kiến)
 * + dòng chia NCC; nguyên liệu có thay thế gắn nhãn ⇄, dòng thay thế nền xanh nhạt ngay dưới. Bấm ô → chi tiết 5 bước (công thức → so giá dùng được → trừ hàng đã có
 * → chia NCC + lũy kế → kết quả đơn mua) với Giữ 100% chuẩn / Đổi tỷ lệ… / Sửa chia NCC… / Đặt ngay. Điện thoại: danh sách việc phải đặt hôm nay.
 * API: PurchasePlan/GetBoard, GetCell, Run, OrderToday, OverrideSubstitute, OverrideSplit. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const SCREEN = 'scm-requirement';
  const PUR_TABLES = ['PurchaseRequest', 'PurchaseRequestLine', 'PurchaseOrder', 'PurchaseOrderLine'];
  const st = { from: null, data: null, busy: false, host: null };
  const CLS = { ORDERED: 'ok', APPROVED: 'auto', PENDING: 'wait', DRAFT: 'today', NEED_PICK: 'wait', DUE_TODAY: 'today', LATE: 'late', PLANNED: 'plan', ENOUGH: 'enough' };
  const LEGEND = [['ok', 'Đã đặt'], ['auto', 'Tự duyệt – chờ gửi NCC'], ['wait', 'Chờ người duyệt'], ['today', 'Phải đặt hôm nay'], ['late', 'Quá hạn đặt'], ['plan', 'Dự kiến – chưa tới hạn']];
  const WD = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
  const ymd = v => String(v || '').slice(0, 10);
  const dd = v => U.date(ymd(v)).slice(0, 5);
  const wd = v => WD[new Date(ymd(v) + 'T00:00').getDay()];
  const num = (v, d = 2) => U.num(Math.round((v || 0) * 100) / 100, d);
  const money = v => U.num(Math.round(v || 0));
  const tr = v => (v >= 1e6 ? U.num(Math.round(v / 1e5) / 10, 1) + ' tr' : money(v));
  const perm = () => Store.perm(SCREEN) || {};

  function style() {
    if (document.getElementById('ppStyle')) return;
    const s = document.createElement('style'); s.id = 'ppStyle';
    s.textContent = `
.pp-head{display:flex;gap:16px;align-items:flex-start;justify-content:space-between;flex-wrap:wrap}
.pp-nav{display:flex;gap:8px;align-items:center;flex-wrap:wrap}.pp-nav .today{font-weight:700;font-size:16px;margin:0 4px}
.pp-legend{display:flex;gap:14px;flex-wrap:wrap;font-size:12.5px;color:#4b5563;margin-bottom:8px}.pp-legend i{display:inline-block;width:11px;height:11px;border-radius:3px;margin-right:5px;vertical-align:-1px;border:1px solid}
.pp-tbl{width:100%;border-collapse:collapse}.pp-tbl th{font-size:13px;padding:8px 6px;text-align:center;background:#F6F8F3;border-bottom:1px solid #E5E9E0}.pp-tbl th.l{text-align:left}
.pp-tbl th.td{color:#c2410c}.pp-th-sub{display:block;font-weight:500;font-size:11.5px}.pp-tbl td{padding:6px 4px;border-bottom:1px solid #EEF1EA;text-align:center;vertical-align:middle}.pp-tbl td.l{text-align:left;padding-left:8px;min-width:200px}
.pp-tbl tr.grp td{background:#F6F8F3;font-weight:700;text-align:left;padding:8px}.pp-tbl tr.sub td{background:#F2F8FD}
.pp-cell{display:inline-flex;flex-direction:column;align-items:center;min-width:58px;padding:4px 8px;border-radius:8px;border:1px solid;cursor:pointer;background:#fff;font:inherit}
.pp-cell b{font-size:15px}.pp-cell span{font-size:10.5px;font-weight:600;white-space:nowrap}.pp-cell:focus-visible{outline:2px solid #2563eb}
.pp-under{display:block;font-size:11.5px;color:#6b7280;margin-top:2px;max-width:190px;margin-left:auto;margin-right:auto}
.pp-ok,i.pp-ok{background:#EEF6E6;border-color:#A6C98A;color:#3f6b1f}.pp-auto,i.pp-auto{background:#EAF2FD;border-color:#9DBFEA;color:#1d4f91}
.pp-wait,i.pp-wait{background:#FFF7E0;border-color:#E9C66B;color:#7a5a00}.pp-today,i.pp-today{background:#FFF1E6;border-color:#F0B488;color:#b4500e}
.pp-late,i.pp-late{background:#FDECEC;border-color:#EBA0A0;color:#b42318}.pp-plan,i.pp-plan{background:#fff;border-color:#CBD2C6;border-style:dashed;color:#4b5563}
.pp-enough{background:#fff;border-color:#E5E9E0;color:#9ca3af}.pp-tag{font-size:11px;background:#EAF2FD;color:#1d4f91;border-radius:6px;padding:1px 6px;margin-left:6px}
.pp-steps{display:grid;grid-template-columns:1fr 1fr;gap:14px}.pp-step{border:1px solid #E5E9E0;border-radius:12px;padding:14px}.pp-step h6{display:flex;gap:8px;align-items:center;margin:0 0 10px;font-size:15px}
.pp-step h6 .n{display:inline-flex;width:22px;height:22px;border-radius:50%;background:#6E9F3A;color:#fff;align-items:center;justify-content:center;font-size:12px}
.pp-step.full{grid-column:1/-1}.pp-step th{text-transform:none}.pp-step td,.pp-step th{white-space:normal}.pp-price{display:grid;grid-template-columns:1fr 1fr;gap:10px}.pp-price>div{border:1px solid #E5E9E0;border-radius:10px;padding:10px}.pp-price>div.win{border-color:#6E9F3A;background:#F6FAF1}
.pp-price b{font-size:20px;display:block}.pp-bar{display:flex;height:22px;border-radius:6px;overflow:hidden;font-size:11.5px;color:#fff;font-weight:700}.pp-bar div{display:flex;align-items:center;justify-content:center;white-space:nowrap}
.pp-cum{position:relative;height:6px;background:#E5E9E0;border-radius:3px;width:110px}.pp-cum i{position:absolute;left:0;top:0;bottom:0;background:#6E9F3A;border-radius:3px}.pp-cum em{position:absolute;top:-3px;width:2px;height:12px;background:#111}
.pp-phone .item{border:1px solid #E5E9E0;border-radius:12px;padding:12px;margin-bottom:10px;display:flex;justify-content:space-between;gap:10px;align-items:center}
@media (max-width:900px){.pp-steps{grid-template-columns:1fr}}`;
    document.head.appendChild(s);
  }

  function load(force) {
    if (st.busy) return; if (st.data && !force) return draw();
    st.busy = true; if (force) { st.data = null; draw(); }
    Store.query('PurchasePlan/GetBoard', { From: st.from, Days: 7, OrganizationUnitId: Store.ctx.branch }).then(x => { st.busy = false; st.data = x; draw(); });
  }

  function cellHtml(r, c) {
    if (!c || c.status === 'NONE') return '<span class="muted">–</span>';
    const k = CLS[c.status] || 'plan';
    return `<button class="pp-cell pp-${k}" data-ing="${r.ingredientId}" data-d="${ymd(c.date)}" title="${esc(c.statusName || '')}"><b>${num(c.quantity, 1)}</b><span>${esc(c.statusName || '')}</span></button>${c.sub ? `<span class="pp-under">${esc(c.sub)}</span>` : ''}`;
  }

  function draw() {
    const host = st.host; if (!host || !host.isConnected) return;
    style();
    const x = st.data;
    if (!x) { host.innerHTML = `<div class="card panel mb"><div class="panel-title">Kế hoạch mua</div><div class="muted small mt">Đang tính…</div></div>`; return; }
    if (!x.ok) { host.innerHTML = `<div class="notice gap">${I('alert')}<div><b>Không tính được kế hoạch mua</b>${esc(x.message || '')}</div></div>`; return; }
    const d = x.data || {}, k = d.kpi || {}, days = d.listDay || [], rows = d.listRow || [], p = perm();
    const phone = window.matchMedia('(max-width: 640px)').matches || document.documentElement.clientWidth < 640;
    const head = `<div class="card panel mb"><div class="pp-head"><div><div class="panel-title" style="font-size:22px">Kế hoạch mua</div>
        <div class="panel-desc">Nhu cầu 7 ngày theo đơn đã chốt / đã xác nhận / dự báo – hệ thống tự tính lúc ${esc((d.runTimes || []).join(' và '))}, tự lập đề nghị mua cho phần đến hạn đặt${d.lastRunAt ? ` · lần gần nhất ${esc(U.dt(String(d.lastRunAt).replace(/\.\d+$/, '')))}` : ''}.</div></div>
        <div class="pp-nav"><button class="btn sm" id="ppPrev" aria-label="Tuần trước">◀</button><span class="today">${ymd(d.fromDate) === ymd(d.today) ? 'Hôm nay ' : ''}${wd(d.fromDate)} ${dd(d.fromDate)}</span><button class="btn sm" id="ppNext" aria-label="Tuần sau">▶</button>
        ${p.add ? `<button class="btn sm" id="ppRun">${I('reset')} Tính lại ngay</button><button class="btn sm primary" id="ppToday" ${d.dueCount ? '' : 'disabled'}>Đặt phần hôm nay (${d.dueCount || 0})</button>` : ''}</div></div>
      ${UI.kpis([
        UI.kpi('Phải đặt hôm nay', U.num(k.dueToday || 0), `nguyên liệu · ${tr(k.dueTodayAmount)}`, 'clipboard', k.dueToday ? 'orange' : 'green'),
        UI.kpi('Quá hạn đặt', U.num(k.late || 0), esc(k.lateNote || ''), 'alert', k.late ? 'red' : 'green'),
        UI.kpi('Chờ người duyệt', U.num(k.pendingApproval || 0), esc(k.pendingNote || ''), 'check', k.pendingApproval ? 'orange' : 'green'),
        UI.kpi('Chưa gửi NCC', U.num(k.unsentPurchaseOrder || 0), `tự gửi Zalo / email lúc ${esc(d.sendTime || '15:00')}`, 'up', 'blue'),
        UI.kpi('Tiết kiệm nhờ thay thế · tháng', tr(k.savingMonth), esc(k.savingNote || ''), 'chart', 'green'),
      ])}</div>`;
    const warn = (d.listWarning || []).slice(0, 4).map(w => `<div class="notice warn small">${I('alert')}<div>${esc(w)}</div></div>`).join('');
    let body;
    if (phone) {
      const todo = rows.flatMap(r => (r.listCell || []).filter(c => ['DUE_TODAY', 'LATE'].includes(c.status)).map(c => ({ r, c })));
      body = `<div class="card panel pp-phone"><div class="panel-title mb">Việc phải đặt hôm nay (${todo.length})</div>${todo.map(({ r, c }) => `<div class="item"><div><b>${esc(r.ingredientName || '')}</b> ${num(c.quantity, 1)} ${esc(r.unitName || '')}<span class="sub">cần ${wd(c.date)} ${dd(c.date)} · ${esc(c.sub || '')}</span></div><div class="row-flex"><span class="tag ${c.status === 'LATE' ? 'red' : 'orange'}">${esc(c.statusName || '')}</span>${p.add ? `<button class="btn sm primary" data-order="${r.ingredientId}" data-d="${ymd(c.date)}">Đặt</button>` : ''}<button class="btn sm" data-ing="${r.ingredientId}" data-d="${ymd(c.date)}">Xem</button></div></div>`).join('') || '<div class="muted">Không có nguyên liệu phải đặt hôm nay</div>'}</div>`;
    } else {
      let lastCat = null;
      const tbody = rows.map(r => {
        let g = '';
        if (!r.isSubstitute && (r.categoryName || 'Khác') !== lastCat) { lastCat = r.categoryName || 'Khác'; g = `<tr class="grp"><td colspan="${days.length + 1}">${esc(lastCat)}</td></tr>`; }
        const cells = days.map(dy => cellHtml(r, (r.listCell || []).find(c => ymd(c.date) === ymd(dy.date)))).map(h => `<td>${h}</td>`).join('');
        return g + `<tr class="${r.isSubstitute ? 'sub' : ''}"><td class="l"><b>${esc(r.ingredientName || '')}</b> <span class="muted small">${esc(r.unitName || '')}</span>${r.hasSubstitute ? '<span class="pp-tag">⇄ thay thế</span>' : ''}${r.note ? `<span class="sub">${esc(r.note)}</span>` : ''}</td>${cells}</tr>`;
      }).join('');
      body = `<div class="card panel"><div class="spread"><div class="pp-legend">${LEGEND.map(([c, l]) => `<span><i class="pp-${c}"></i>${l}</span>`).join('')}</div><div class="small muted">⇄ = có nguyên liệu thay thế · dòng dưới ô = chia NCC</div></div>
        <div class="table-wrap"><table class="pp-tbl"><thead><tr><th class="l">Nguyên liệu (theo ngày cần hàng)</th>${days.map(dy => `<th class="${dy.isToday ? 'td' : ''}">${wd(dy.date)} ${dd(dy.date)}${dy.isToday ? '<span class="pp-th-sub">hôm nay</span>' : ''}</th>`).join('')}</tr></thead>
        <tbody>${tbody || `<tr><td colspan="${days.length + 1}" class="empty">Không có nhu cầu nguyên liệu trong 7 ngày</td></tr>`}</tbody></table></div>
        <div class="small muted mt">Bấm ô để xem cách hệ thống tính: công thức → chọn tỷ lệ thay thế theo giá dùng được → trừ tồn / đơn mua sắp về / đề nghị đang mở → chia NCC theo tỷ lệ và lũy kế tháng.</div></div>`;
    }
    host.innerHTML = head + warn + body;
    const shift = n => { st.from = U.addDays(ymd(d.fromDate), n); load(true); };
    host.querySelector('#ppPrev').onclick = () => shift(-7);
    host.querySelector('#ppNext').onclick = () => shift(7);
    const run = host.querySelector('#ppRun');
    if (run) run.onclick = () => runPlan({ onlyDue: true }, 'Đã tính lại kế hoạch mua');
    const today = host.querySelector('#ppToday');
    if (today) today.onclick = () => UI.confirm(`Đặt phần đến hạn hôm nay cho <b>${d.dueCount}</b> nguyên liệu? Hệ thống lập / cập nhật đề nghị mua theo ngày cần; đúng định mức tự duyệt + lập đơn mua, vượt định mức gửi Trưởng mua hàng.`,
      () => Store.act('PurchasePlan/OrderToday', { json: { organizationUnitId: Store.ctx.branch || null }, tables: PUR_TABLES }).then(after), { ok: 'Đặt phần hôm nay' });
    host.querySelectorAll('button.pp-cell[data-ing], .pp-phone [data-ing]').forEach(b => b.onclick = () => openCell(b.dataset.ing, b.dataset.d));
    host.querySelectorAll('[data-order]').forEach(b => b.onclick = () => runPlan({ onlyDue: false, ingredientId: b.dataset.order, needDate: b.dataset.d }, 'Đã đặt'));
  }

  function after(r) {
    if (!r || !r.ok) return;
    UI.toast(esc((r.res && r.res.responseMessage) || 'Đã chạy kế hoạch mua'), 'ok');
    load(true);
  }

  function runPlan(json, ok) {
    return Store.act('PurchasePlan/Run', { json: Object.assign({ organizationUnitId: Store.ctx.branch || null }, json), tables: PUR_TABLES }).then(r => { if (r.ok) { UI.toast(esc((r.res && r.res.responseMessage) || ok), 'ok'); load(true); } return r; });
  }

  // ---------- Chi tiết ô: 5 bước ----------
  function openCell(ing, date) {
    UI.modal({
      title: 'Đang tải…', size: 'lg', foot: false, body: '<div class="muted">Đang tính chi tiết…</div>',
      onMount: (m, close) => Store.query('PurchasePlan/GetCell', { IngredientId: ing, NeedDate: date, OrganizationUnitId: Store.ctx.branch }).then(x => {
        if (!m.isConnected) return;
        if (!x.ok) { m.querySelector('.modal-body').innerHTML = `<div class="notice gap">${I('alert')}<div>${esc(x.message || 'Không tải được chi tiết')}</div></div>`; return; }
        renderCell(m, close, x.data || {}, ing, date);
      }),
    });
  }

  function renderCell(m, close, c, ing, date) {
    const p = perm(), s = c.substitution;
    m.querySelector('.modal-title').textContent = c.title || '';
    const hdr = m.querySelector('.modal-header > div'); if (hdr && !hdr.querySelector('.panel-desc')) hdr.insertAdjacentHTML('beforeend', `<div class="panel-desc">${esc(c.subtitle || '')}</div>`);
    const step = (n, title, html, full) => `<div class="pp-step ${full ? 'full' : ''}"><h6><span class="n">${n}</span>${title}</h6>${html}</div>`;
    const s1 = `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Món</th><th class="num">Suất</th><th>Chuẩn</th><th>Thay được</th></tr></thead><tbody>${(c.listDish || []).map(x => `<tr${x.standardOnly ? ' class="muted"' : ''}><td>${esc(x.dishName || '')}</td><td class="num">${U.num(x.portions)}</td><td>${esc(x.standardText || '')}</td><td>${x.substituteText ? esc(x.substituteText) : '<span class="muted">–</span>'}</td></tr>`).join('') || '<tr><td colspan="4" class="empty">Chưa có món dùng nguyên liệu này trong ngày (hoặc ngày đã qua)</td></tr>'}</tbody></table></div>`;
    const s2 = !s ? '<div class="muted">Nguyên liệu không có thay thế trong công thức.</div>'
      : `<div class="pp-price"><div class="${s.sharePct > 0 ? '' : 'win'}"><span class="small muted">${esc(s.stdIngredientName || '')} – chuẩn</span><b>${s.stdUsablePrice ? money(s.stdUsablePrice) + ' đ' : '–'}</b><span class="small">mua ${s.stdUnitPrice ? money(s.stdUnitPrice) : '–'} đ · hao ${num(s.stdWastePct, 1)}%</span></div>
         <div class="${s.sharePct > 0 ? 'win' : ''}"><span class="small muted">${esc(s.subIngredientName || '')} – thay thế</span><b>${s.subUsablePrice ? money(s.subUsablePrice) + ' đ' : '–'}</b><span class="small">mua ${s.subUnitPrice ? money(s.subUnitPrice) : '–'} đ · hao ${num(s.subWastePct, 1)}%${s.savingPct != null ? ` · <b style="display:inline;font-size:inherit">${s.savingPct > 0 ? 'rẻ hơn' : 'đắt hơn'} ${num(Math.abs(s.savingPct), 1)}%</b>` : ''}</span></div></div>
         <div class="notice info small mt">${I('info') || ''}<div>${esc(s.reason || '')}${s.savingAmount ? ` · tiết kiệm dự kiến <b>${money(s.savingAmount)} đ</b>` : ''}${s.source === 'OVERRIDE' ? ' · <b>người mua đã sửa</b>' : ''}</div></div>
         ${p.edit ? `<div class="row-flex mt" style="justify-content:flex-end"><button class="btn sm" id="ppHold">Giữ 100% ${esc(s.stdIngredientName || 'chuẩn')} ngày này</button><button class="btn sm" id="ppRatio">Đổi tỷ lệ…</button></div>` : ''}`;
    const nets = c.listNet || [];
    const s3 = `<div class="table-wrap"><table class="table dc-table"><thead><tr><th></th>${nets.map(n => `<th class="num">${esc(n.ingredientName || '')}</th>`).join('')}</tr></thead><tbody>
      ${[['Cần lũy kế (sau thay thế)', 'required'], ['+ Tồn an toàn', 'safetyStock'], ['− Tồn khả dụng', 'available'], ['− Đơn mua sắp về', 'incoming'], ['− Đề nghị mua đang mở', 'requested']].map(([l, f]) => `<tr><td>${l}</td>${nets.map(n => `<td class="num">${num(n[f])}</td>`).join('')}</tr>`).join('')}
      <tr><td><b>Phải mua</b></td>${nets.map(n => `<td class="num"><b>${num(n.toBuy)} ${esc(n.unitName || '')}</b></td>`).join('')}</tr></tbody></table></div>`;
    const COLORS = ['#6E9F3A', '#4A86D0', '#D08A3A', '#8A63C9'];
    const s4 = (c.listSplit || []).map(sp => `<div class="mb"><div class="small"><b>${esc(sp.ingredientName || '')} ${num(sp.quantity)} ${esc(sp.unitName || '')}</b>${sp.listPart.length > 1 ? ' – tỷ lệ mục tiêu ' + sp.listPart.map(x => `${esc(x.supplierName || '')} ${num(x.targetPct, 0)}%`).join(' · ') : ''}</div>
        ${sp.listPart.length > 1 ? `<div class="pp-bar mt">${sp.listPart.map((x, i) => `<div style="width:${x.pct}%;background:${COLORS[i % 4]}">${esc(x.supplierName || '')} ${num(x.quantity, 1)}</div>`).join('')}</div>` : ''}
        <div class="table-wrap mt"><table class="table dc-table"><thead><tr><th>NCC</th><th class="num">Giá</th><th class="num">Mục tiêu</th><th>Lũy kế tháng</th><th class="num">Lần này</th></tr></thead><tbody>${sp.listPart.map(x => `<tr><td><b>${esc(x.supplierName || '')}</b><span class="sub">${x.isPreferred ? 'ưu tiên · ' : ''}${x.minQuantity ? 'tối thiểu ' + num(x.minQuantity) + ' · ' : ''}${x.leadTimeDays ? 'giao ' + x.leadTimeDays + ' ngày' : ''}</span></td><td class="num">${x.unitPrice ? money(x.unitPrice) : '–'}</td><td class="num">${num(x.targetPct, 0)}%</td><td>${x.cumPct != null ? `${num(x.cumPct, 0)}%<div class="pp-cum"><i style="width:${Math.min(100, x.cumPct)}%"></i><em style="left:${Math.min(100, x.targetPct)}%"></em></div>` : '<span class="muted">chưa có</span>'}</td><td class="num"><b>${num(x.quantity)}</b></td></tr>`).join('') || '<tr><td colspan="5" class="empty">Chưa có NCC đạt</td></tr>'}</tbody></table></div>
        ${sp.note ? `<div class="small muted">${esc(sp.note)}</div>` : ''}</div>`).join('') || '<div class="muted">Không còn phần phải mua – đã đặt đủ (xem bước 5).</div>';
    const orders = c.listOrder || [];
    const s5 = `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>NCC</th><th>Nội dung</th><th class="num">Thành tiền</th><th>Duyệt</th><th>Gửi NCC</th></tr></thead><tbody>${orders.map(o => `<tr><td><b>${esc(o.supplierName || o.purchaseRequestNumber || '')}</b>${o.purchaseOrderNumber ? `<span class="sub">${esc(o.purchaseOrderNumber)}</span>` : o.planned ? '<span class="sub">dự kiến</span>' : ''}</td><td>${esc(o.content || '')}</td><td class="num">${o.amount ? money(o.amount) : '–'}</td><td><span class="tag ${o.planned ? '' : 'blue'}">${esc(o.approvalText || '')}</span></td><td>${esc(o.sendText || '')}</td></tr>`).join('') || '<tr><td colspan="5" class="empty">Chưa có đơn mua</td></tr>'}</tbody></table></div>
      ${c.message ? `<div class="notice info small mt">${I('info') || ''}<div>${esc(c.message)}</div></div>` : ''}`;
    m.querySelector('.modal-body').innerHTML = `<div class="pp-steps">${step(1, 'Nhu cầu theo công thức', s1)}${step(2, 'So giá trên 1 kg dùng được (đã trừ hao sơ chế)', s2)}${step(3, 'Trừ hàng đã có', s3)}${step(4, 'Chia nhà cung cấp', s4)}${step(5, `Kết quả – ${orders.length} đơn mua`, s5, true)}</div>
      <div class="row-flex mt" style="justify-content:flex-end"><button class="btn" data-close>Đóng</button>${p.edit && (c.listSplit || []).some(x => x.listPart.length) ? '<button class="btn" id="ppSplit">Sửa chia NCC…</button>' : ''}${p.add && c.canOrder ? '<button class="btn primary" id="ppOrder">Đặt ngay</button>' : ''}</div>`;
    m.querySelectorAll('[data-close]').forEach(b => b.onclick = close);
    const reopen = () => { close(); openCell(ing, date); };
    const ov = (pct, reason) => Store.act('PurchasePlan/OverrideSubstitute', { json: { needDate: date, stdIngredientId: s.stdIngredientId, subIngredientId: s.subIngredientId, sharePct: pct, reason } })
      .then(r => { if (r.ok) { UI.toast(esc((r.res && r.res.responseMessage) || 'Đã lưu'), 'ok'); load(true); reopen(); } });
    const hold = m.querySelector('#ppHold');
    if (hold) hold.onclick = () => UI.confirm(`Giữ 100% <b>${esc(s.stdIngredientName)}</b> cho ngày ${dd(date)}? Lý do được lưu vào lịch sử và ghi chú dòng đề nghị.`, v => ov(0, v), { input: 'Lý do', required: true, ok: 'Giữ chuẩn' });
    const ratio = m.querySelector('#ppRatio');
    if (ratio) ratio.onclick = () => UI.modal({
      title: 'Đổi tỷ lệ thay thế', size: 'sm', sub: `${esc(s.subIngredientName)} thay ${esc(s.stdIngredientName)} · ngày ${dd(date)} · công thức cho tối đa ${num(s.maxSharePct, 0)}%`,
      body: `<div class="field"><label>Tỷ lệ thay (%) <span class="req">*</span></label><input class="input" type="number" min="0" max="100" step="5" id="ppPct" value="${num(s.sharePct, 0)}"><span class="small muted">Vượt ${num(s.maxSharePct, 0)}% → đề nghị không tự duyệt</span></div><div class="field"><label>Lý do <span class="req">*</span></label><textarea class="input" rows="2" id="ppWhy"></textarea></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="ppSave">Lưu</button>',
      onMount: (mm, cl) => mm.querySelector('#ppSave').onclick = () => { const v = Number(mm.querySelector('#ppPct').value), why = mm.querySelector('#ppWhy').value.trim(); if (!(v >= 0 && v <= 100) || !why) return UI.toast('Nhập tỷ lệ 0–100 và lý do', 'warn'); cl(); ov(v, why); },
    });
    const split = m.querySelector('#ppSplit');
    if (split) split.onclick = () => {
      const sp = (c.listSplit || []).find(x => x.listPart.length) || {};
      UI.modal({
        title: 'Sửa chia NCC', size: 'sm', sub: `${esc(sp.ingredientName || '')} · ngày cần ${dd(date)} – tổng phải 100%; đề nghị có phần chia sửa tay cần Trưởng mua hàng duyệt`,
        body: sp.listPart.map(x => `<div class="field"><label>${esc(x.supplierName || '')}</label><input class="input" type="number" min="0" max="100" data-sup="${x.supplierId}" value="${num(x.pct, 0)}"></div>`).join('') + '<div class="field"><label>Lý do <span class="req">*</span></label><textarea class="input" rows="2" id="spWhy"></textarea></div>',
        foot: '<button class="btn" data-close>Hủy</button><button class="btn" id="spReset">Về tỷ lệ tính</button><button class="btn primary" id="spSave">Lưu</button>',
        onMount: (mm, cl) => {
          const send = pct => Store.act('PurchasePlan/OverrideSplit', { json: { needDate: date, ingredientId: sp.ingredientId, pct, reason: mm.querySelector('#spWhy').value.trim() } }).then(r => { if (r.ok) { cl(); UI.toast(esc((r.res && r.res.responseMessage) || 'Đã lưu'), 'ok'); reopen(); } });
          mm.querySelector('#spReset').onclick = () => send(null);
          mm.querySelector('#spSave').onclick = () => { const pct = {}; mm.querySelectorAll('[data-sup]').forEach(i => { pct[i.dataset.sup] = Number(i.value) || 0; }); send(pct); };
        },
      });
    };
    const order = m.querySelector('#ppOrder');
    if (order) order.onclick = () => runPlan({ onlyDue: false, ingredientId: ing, needDate: date }, 'Đã đặt').then(r => { if (r && r.ok) reopen(); });
  }

  /** Vẽ màn Kế hoạch mua vào khung <paramref name="host"/> (màn scm-requirement, tab mặc định). */
  APP.purchasePlan = host => { st.host = host; if (!st.from) st.from = Store.ctx.date || U.iso(new Date()); draw(); load(false); };
  APP.purchasePlanReload = () => load(true);
  // Đổi khổ màn (xoay điện thoại / thu cửa sổ) → vẽ lại: ma trận ↔ danh sách việc phải đặt.
  let rz = null, wasPhone = null;
  window.addEventListener('resize', () => { clearTimeout(rz); rz = setTimeout(() => { const p = window.matchMedia('(max-width: 640px)').matches; if (p !== wasPhone && st.host && st.host.isConnected) draw(); wasPhone = p; }, 200); });
})();
