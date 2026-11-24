/* UI-6.23 (D-104): Kế hoạch sản xuất – màn "7 ngày tới" (mô phỏng docs/frontend-handoff/assets/production-plan-days-mockup.html).
 * Một kế hoạch / ngày; hệ thống tự lập, tự duyệt và ra lệnh sau chốt đơn – màn này chỉ cho thấy ngày nào cần người bấm.
 * 3 tab: 7 ngày tới (mặc định) · Danh sách kế hoạch (APP.prodPlanList – modules/production.js) · Nhật ký tự động (Note.AutoRun).
 * API: ProductionPlan/GetDays (From, To ≤ 14 ngày) · Recalculate (Lập / Tính lại – máy chủ tự quyết) · GetShortage (chỉ đọc) · Approve (Vẫn duyệt) · Release (Ra lệnh n ca).
 * Ô KHÔNG sửa trực tiếp (sửa tay làm hệ thống không tự ra lệnh – BIZ-2.4); bấm ô → hộp chi tiết ngày. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const pd = { tab: 'days', from: null, data: null, log: null, open: { order: true, plan: true, prod: false } };
  const DOW = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
  const dd = v => { const s = U.day(v); return s ? s.slice(8, 10) + '/' + s.slice(5, 7) : ''; };
  const dow = v => DOW[new Date(U.day(v) + 'T00:00').getDay()];
  // Màu theo trạng thái ngày / ca (chú thích màu bên dưới): đã ra lệnh · đã duyệt · nháp · bị giữ lại · chưa có kế hoạch.
  const CLS = { released: 'pub', approved: 'reg', draft: 'draft', held: 'held', none: 'miss', open: 'miss', empty: 'miss' };
  const TAG = { released: 'green', approved: 'blue', draft: 'orange', held: 'red', none: '', open: '', empty: '' };
  const chip = x => UI.tag(x.dayStatusName, TAG[x.dayStatus] || '');
  const canEdit = () => Store.perm('prod-plan').edit !== false;
  const canApprove = () => Store.canApprove('prod-plan');
  const relLabel = x => { const n = x.shiftTotal - x.shiftReleased; return n > 0 ? `Ra lệnh ${n} ca` : 'Ra lệnh'; };
  const planHref = id => '#/prod-plan-detail?id=' + encodeURIComponent(id);

  function load(el, f) {
    const q = pd.from ? { From: pd.from, To: U.addDays(pd.from, 6) } : {};
    return Store.query('ProductionPlan/GetDays', q).then(r => {
      if (!el.isConnected) return;
      if (!r.ok) { const b = el.querySelector('#pdBody'); if (b) b.innerHTML = `<div class="muted small">${esc((r.res && r.res.responseMessage) || 'Không tải được kế hoạch 7 ngày tới')}</div>`; return; }
      pd.data = r.data; pd.from = U.day(r.data.from); draw(el, f);
    });
  }

  // ----- Việc gợi ý của 1 ngày (đúng 1 nút) -----
  function actionHtml(x, lockAt) {
    const pending = x.shiftTotal - x.shiftReleased;
    if (x.action === 'viewReason') return `<button class="btn xs danger-o" data-view="${x._i}">Xem lý do</button>`;
    if (x.action === 'recalc') return canEdit() ? `<button class="btn xs orange" data-recalc="${x._i}">${x.planId ? 'Tính lại' : 'Lập trước'}</button>` : '<span class="small muted">Chờ lập kế hoạch</span>';
    if (x.action === 'release') return canEdit() ? `<button class="btn xs primary" data-release="${x._i}">${relLabel(x)}</button>` : `<span class="small muted">Chờ ra lệnh</span>`;
    if (x.action === 'view') return `<button class="btn xs" data-view="${x._i}">Xem</button>`;
    return x.dayStatus === 'open' ? `<span class="small muted">Tự lập sau chốt ${esc(lockAt)}</span>` : '<span class="small muted">–</span>';
  }
  function hintHtml(x) {
    if (x.dayStatus === 'held') return '<span class="fw-hint bad">Hệ thống giữ lại</span>';
    if (x.dayStatus === 'draft' && x.lateMeals > 0) return `<span class="fw-hint">+${U.num(x.lateMeals)} suất chốt sau lần lập</span>`;
    if (x.dayStatus === 'none' && x.openMeals > 0) return `<span class="fw-hint">Còn ${U.num(x.openMeals)} suất chưa chốt</span>`;
    if (x.dayStatus === 'approved') return x.shiftTotal ? `<span class="fw-hint">${x.shiftReleased}/${x.shiftTotal} ca đã ra lệnh</span>` : '<span class="fw-hint">Chưa có suất chốt theo ca</span>';
    return '<span class="fw-hint">&nbsp;</span>';
  }

  function head(d) {
    const days = d.listDay, sum = k => U.sum(days, k);
    const recalc = days.filter(x => x.action === 'recalc'), held = days.filter(x => x.dayStatus === 'held'), rel = days.filter(x => x.action === 'release');
    const pendingShifts = U.sum(rel, x => x.shiftTotal - x.shiftReleased), openDays = days.filter(x => x.openMeals > 0).length;
    const todo = days.filter(x => ['recalc', 'release', 'viewReason'].includes(x.action)).length;
    const step = (n, cls, title, sub, btn) => `<div class="fw-step"><span class="n ${cls}">${n}</span><div><b>${title}</b><span class="small muted">${sub}</span></div>${btn || ''}</div>`;
    return `<div class="spread"><div class="fw-week"><button class="btn sm" id="pdPrev" title="7 ngày trước" aria-label="7 ngày trước">◀</button><span>${dd(d.from)} – ${U.date(d.to)}</span>
        <button class="btn sm" id="pdNext" title="7 ngày sau" aria-label="7 ngày sau">▶</button>${U.day(d.from) !== U.day(d.today) ? '<button class="btn sm" id="pdNow">7 ngày tới</button>' : '<span class="small muted" style="font-weight:400">(7 ngày tới)</span>'}</div></div>
      <div class="fw-steps">
        ${step(1, 's1', 'Chốt đơn', `Tự động ${esc(d.lockAt)} hôm trước${openDays ? ` · ${openDays} ngày còn đơn chưa chốt` : ''}`)}
        ${step(2, recalc.length ? 's2' : 's1', 'Lập kế hoạch', d.autoRelease ? (recalc.length ? `${recalc.length} ngày cần lập / tính lại` : 'Tự động – không có ngày cần xử lý') : `${recalc.length} ngày cần lập`,
          recalc.length && canEdit() ? `<button class="btn sm orange" data-recalc="${recalc[0]._i}">${recalc[0].planId ? 'Tính lại' : 'Lập'} ${dd(recalc[0].date)}</button>` : '')}
        ${step(3, held.length ? 's4' : 's1', 'Duyệt', held.length ? `${held.length} ngày bị giữ lại` : 'Không có ngày bị giữ', held.length ? `<button class="btn sm danger-o" data-view="${held[0]._i}">Xem lý do</button>` : '')}
        ${step(4, 's3', 'Ra lệnh SX theo ca', pendingShifts ? `${pendingShifts} ca chờ ra lệnh` : rel.length ? `${rel.length} ngày chờ ra lệnh` : 'Mọi ca đã ra lệnh',
          rel.length && canEdit() ? `<button class="btn sm primary" data-release="${rel[0]._i}">${relLabel(rel[0])}</button>` : '')}
      </div>
      ${UI.kpis([UI.kpi('Suất đã chốt (7 ngày)', U.num(sum('lockedMeals')), '', 'check', 'green'), UI.kpi('Suất chưa chốt', U.num(sum('openMeals')), '', 'clock', sum('openMeals') ? 'orange' : 'green'),
        UI.kpi('Ngày cần xử lý', U.num(todo), '', 'alert', todo ? 'red' : 'green'), UI.kpi('Ca đã ra lệnh SX', `${sum('shiftReleased')} / ${sum('shiftTotal')}`, '', 'flow', 'blue')], 'compact')}`;
  }

  function grid(d) {
    const days = d.listDay, n = days.length, total = a => U.sum(a, v => v || 0);
    const qs = (v, cls, i, tip) => v ? `<span class="fw-q pp ${cls}" ${i !== undefined ? `data-day="${i}"` : ''} title="${esc(tip || '')}">${U.num(v)}</span>` : '<span class="fw-q lock">–</span>';
    const grp = (k, name, vals, plain) => `<tr class="fw-cus ${pd.open[k] ? '' : 'closed'}" data-grp="${k}"><td class="l"><span class="fw-chev">▾</span> ${name}</td>${vals.map(v => `<td>${v ? U.num(v) : '<span class="muted">–</span>'}</td>`).join('')}<td class="tot">${plain ? '' : U.num(total(vals))}</td></tr>`;
    let h = `<colgroup><col class="name">${days.map(() => '<col>').join('')}<col class="tot"></colgroup>
      <thead><tr><th class="l">Ngày sản xuất</th>${days.map(x => `<th class="${U.day(x.date) === U.day(d.today) ? 'pp-today' : ''}"><b>${dow(x.date)} ${dd(x.date)}</b>${U.day(x.date) === U.day(d.today) ? '<span class="small">hôm nay</span>' : ''}<div>${chip(x)}</div></th>`).join('')}<th>Cả ${n} ngày</th></tr></thead><tbody>
      <tr class="pp-act"><td class="l">Việc cần làm</td>${days.map(x => `<td><div class="fw-cell">${actionHtml(x, d.lockAt)}${hintHtml(x)}</div></td>`).join('')}<td></td></tr>`;
    h += grp('order', 'Đơn hàng (suất)', days.map(x => x.lockedMeals + x.openMeals));
    if (pd.open.order) {
      h += `<tr class="fw-sub"><td class="l">Đã chốt</td>${days.map(x => `<td>${qs(x.lockedMeals, 'pub')}</td>`).join('')}<td class="tot">${U.num(U.sum(days, 'lockedMeals'))}</td></tr>`;
      h += `<tr class="fw-sub"><td class="l">Chưa chốt</td>${days.map(x => `<td>${qs(x.openMeals, 'open')}</td>`).join('')}<td class="tot" ${U.sum(days, 'openMeals') ? 'style="color:#C2571A"' : ''}>${U.num(U.sum(days, 'openMeals'))}</td></tr>`;
    }
    h += grp('plan', 'Kế hoạch theo ca (suất)', days.map(x => U.sum(x.listShift, 'meals')));
    if (pd.open.plan) {
      (d.listShift || []).forEach(s => {
        const cells = days.map(x => (x.listShift || []).find(c => c.shiftId === s.shiftId));
        h += `<tr class="fw-sub"><td class="l">${esc(s.shiftName || s.shiftCode || '')}</td>${cells.map((c, i) => `<td>${c ? qs(c.meals, CLS[c.status] || 'miss', days[i]._i, `${s.shiftName || s.shiftCode} · ${dow(days[i].date)} ${dd(days[i].date)}: ${U.num(c.meals)} suất${c.productionOrderCount ? ' · ' + c.productionOrderCount + ' lệnh SX' : ''}`) : '<span class="fw-q lock">–</span>'}</td>`).join('')}<td class="tot">${U.num(U.sum(cells, c => c ? c.meals : 0))}</td></tr>`;
      });
      if (!(d.listShift || []).length) h += `<tr class="fw-sub"><td class="l muted">Chưa có ca</td>${days.map(() => '<td><span class="fw-q lock">–</span></td>').join('')}<td></td></tr>`;
    }
    h += grp('prod', 'Món · lệnh sản xuất', days.map(x => x.productionOrderCount), true);
    if (pd.open.prod) {
      h += `<tr class="fw-sub"><td class="l">Số món</td>${days.map(x => `<td>${x.dishCount || '<span class="muted">–</span>'}</td>`).join('')}<td></td></tr>`;
      h += `<tr class="fw-sub"><td class="l">Lệnh SX đã ra</td>${days.map(x => `<td>${x.productionOrderCount || '<span class="muted">–</span>'}</td>`).join('')}<td class="tot">${U.num(U.sum(days, 'productionOrderCount'))}</td></tr>`;
    }
    return h + '</tbody>';
  }

  function draw(el, f) {
    const d = pd.data; if (!d) return;
    d.listDay.forEach((x, i) => { x._i = i; });
    el.querySelector('#pdHead').innerHTML = head(d);
    el.querySelector('#pdBody').innerHTML = `<div class="fw-legend"><span><span class="fw-sw pub"></span>Đã ra lệnh SX</span><span><span class="fw-sw reg"></span>Đã duyệt – chờ ra lệnh</span>
        <span><span class="fw-sw draft"></span>Nháp / cần tính lại</span><span><span class="fw-sw held"></span>Hệ thống giữ lại (có lý do)</span><span><span class="fw-sw miss"></span>Chưa có kế hoạch</span><span>Bấm ô để xem chi tiết ngày</span></div>
      <div class="table-wrap pp-wrap"><table class="fw-table pp-table">${grid(d)}</table></div>`;
    const g = s => el.querySelector(s);
    const go = from => { pd.from = from; load(el, f); };
    if (g('#pdPrev')) g('#pdPrev').onclick = () => go(U.addDays(pd.from, -7));
    if (g('#pdNext')) g('#pdNext').onclick = () => go(U.addDays(pd.from, 7));
    if (g('#pdNow')) g('#pdNow').onclick = () => go(U.day(d.today));
    el.querySelectorAll('[data-grp]').forEach(t => t.onclick = () => { pd.open[t.dataset.grp] = !pd.open[t.dataset.grp]; draw(el, f); });
    const day = i => d.listDay[+i];
    el.querySelectorAll('[data-recalc]').forEach(b => b.onclick = () => recalc(el, f, day(b.dataset.recalc)));
    el.querySelectorAll('[data-release]').forEach(b => b.onclick = () => release(el, f, day(b.dataset.release)));
    el.querySelectorAll('[data-view]').forEach(b => b.onclick = () => dayBox(el, f, day(b.dataset.view)));
    el.querySelectorAll('[data-day]').forEach(b => b.onclick = () => dayBox(el, f, day(b.dataset.day)));
  }

  // ----- Thao tác -----
  function recalc(el, f, x, close) {
    const label = x.planId ? 'Tính lại' : 'Lập';
    const msg = x.planId ? `Tính lại kế hoạch <b>${esc(x.planNumber || '')}</b> ngày ${dd(x.date)} theo đơn đã chốt?` : `Lập kế hoạch ngày ${dd(x.date)} từ ${U.num(x.lockedMeals)} suất đã chốt?`;
    const note = x.openMeals > 0 ? `<br><span class="small muted">Còn ${U.num(x.openMeals)} suất chưa chốt – kế hoạch lập Nháp và tự tính lại khi chốt đơn ${esc(pd.data.lockAt)}.</span>`
      : pd.data.autoRelease ? '<br><span class="small muted">Mọi đơn đã chốt: đủ công thức và nguyên liệu thì hệ thống tự duyệt và ra lệnh, không đủ thì giữ lại kèm lý do.</span>' : '';
    UI.confirm(msg + note, () => Store.act('ProductionPlan/Recalculate', { form: { ProductionDate: U.day(x.date) }, tables: ['ProductionPlan', 'ProductionPlanLine', 'ProductionOrder'] }).then(r => {
      if (!r.ok) return; const m = r.data || {};
      UI.toast(esc(m.message || 'Đã lập kế hoạch'), m.status === 'RELEASED' ? 'ok' : 'warn');
      if (close) close();
      if ((m.status === 'DRAFT' || m.status === 'HELD') && m.planId) { location.hash = planHref(m.planId); return; }
      load(el, f);
    }), { ok: label });
  }
  function release(el, f, x, close) {
    const n = x.shiftTotal - x.shiftReleased;
    UI.confirm(`Ra lệnh sản xuất ${n > 0 ? n + ' ca còn lại' : 'các ca'} của kế hoạch <b>${esc(x.planNumber || '')}</b> ngày ${dd(x.date)}? Hệ thống chia mẻ theo ca, giữ chỗ nguyên liệu và lập phiếu xuất nháp.`, () =>
      Store.act('ProductionPlan/Release', { form: { Id: x.planId }, tables: ['ProductionPlan', 'ProductionOrder', 'ProductionBatche', 'MaterialReservation'] }).then(r => {
        if (!r.ok) return; const w = (r.data && r.data.listWarning) || [];
        UI.toast(`Đã ra lệnh ${n > 0 ? n + ' ca' : 'sản xuất'} của ${esc(x.planNumber || '')}${w.length ? ' · ' + w.length + ' cảnh báo (xem trang kế hoạch)' : ''}`, w.length ? 'warn' : 'ok');
        if (close) close(); load(el, f);
      }), { ok: 'Ra lệnh' });
  }
  function approveAnyway(el, f, x, close) {
    UI.confirm(`Duyệt kế hoạch <b>${esc(x.planNumber || '')}</b> khi thiếu nguyên liệu? Lệnh sản xuất vẫn ra; phần thiếu kho giữ chỗ bổ sung khi hàng về.`, () =>
      Store.act('ProductionPlan/Approve', { form: { Id: x.planId, Note: 'Duyệt khi thiếu nguyên liệu (màn 7 ngày tới)' }, tables: ['ProductionPlan', 'ApprovalRequest'] }).then(r => {
        if (!r.ok) return; UI.toast(`Đã duyệt ${esc(x.planNumber || '')}. Bấm "Ra lệnh" khi sẵn sàng.`, 'ok'); if (close) close(); load(el, f);
      }), { ok: 'Vẫn duyệt' });
  }

  // ----- Hộp chi tiết ngày -----
  function dayBox(el, f, x) {
    const held = x.dayStatus === 'held';
    const shifts = (x.listShift || []).map(s => `<tr><td>${esc(s.shiftName || s.shiftCode || '')}</td><td class="num">${U.num(s.meals)}</td><td class="num">${s.productionOrderCount || '–'}</td><td>${UI.tag(({ released: 'Đã ra lệnh', approved: 'Đã duyệt', draft: 'Nháp', held: 'Bị giữ lại' })[s.status] || 'Chưa có kế hoạch', TAG[s.status] || '')}</td></tr>`).join('');
    const shiftTbl = shifts ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Ca</th><th class="num">Suất</th><th class="num">Lệnh SX</th><th>Trạng thái</th></tr></thead><tbody>${shifts}</tbody></table></div>` : '<div class="muted small">Chưa có suất đã chốt theo ca</div>';
    const body = `<div class="small muted mb">${U.num(x.lockedMeals)} suất đã chốt${x.openMeals ? ` · ${U.num(x.openMeals)} suất chưa chốt` : ''}${x.dishCount ? ` · ${x.dishCount} món` : ''}${x.productionOrderCount ? ` · ${x.productionOrderCount} lệnh SX` : ''} · ${chip(x)}</div>
      ${held ? `<div class="notice danger mb">${I('alert')}<div><b>Hệ thống giữ lại, chưa tự duyệt:</b><br>${esc(x.heldReason || '')}</div></div><div id="pdShort" class="mb">${'<div class="muted small">Đang kiểm nguyên liệu…</div>'}</div>
        <div class="small muted mb">Duyệt khi chấp nhận thiếu: lệnh sản xuất vẫn ra, phần thiếu kho giữ chỗ bổ sung khi hàng về.</div>` : ''}
      <h3>Theo ca</h3>${shiftTbl}`;
    const btn = [];
    if (held) btn.push(`<button class="btn" data-go="#/scm-requirement">Mở tính nhu cầu nguyên liệu</button>`);
    if (x.planId) btn.push(`<button class="btn" data-go="${planHref(x.planId)}">Mở kế hoạch</button>`);
    if (held && canApprove()) btn.push('<button class="btn primary" data-ok="approve">Vẫn duyệt</button>');
    else if (x.action === 'recalc' && canEdit()) btn.push(`<button class="btn orange" data-ok="recalc">${x.planId ? 'Tính lại' : 'Lập trước'}</button>`);
    else if (x.action === 'release' && canEdit()) btn.push(`<button class="btn primary" data-ok="release">${relLabel(x)}</button>`);
    UI.modal({ title: `${esc(x.planNumber || 'Chưa có kế hoạch')} · ${dow(x.date)} ${U.date(x.date)}`, size: 'md', body,
      foot: `<button class="btn" data-close>Đóng</button>${btn.join('')}`,
      onMount: (m, close) => {
        m.querySelectorAll('[data-go]').forEach(b => b.onclick = () => { close(); location.hash = b.dataset.go; });
        const ok = m.querySelector('[data-ok]');
        if (ok) ok.onclick = () => ({ approve: approveAnyway, recalc, release })[ok.dataset.ok](el, f, x, close);
        if (held && x.planId) Store.query('ProductionPlan/GetShortage', { id: x.planId }).then(r => {
          const box = m.querySelector('#pdShort'); if (!box) return;
          if (!r.ok) { box.innerHTML = `<div class="muted small">${esc((r.res && r.res.responseMessage) || 'Không kiểm được nguyên liệu')}</div>`; return; }
          const s = r.data || {}, lines = s.listLine || [], warn = s.listWarning || [];
          box.innerHTML = (lines.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Nguyên liệu</th><th class="num">Cần</th><th class="num">Giữ được</th><th class="num">Thiếu</th><th>Đang về</th></tr></thead><tbody>${lines.map(l => `<tr><td>${esc(l.ingredientName || '')}</td><td class="num">${U.num(l.required, 3)} ${esc(l.unitName || '')}</td><td class="num">${U.num(l.reserved, 3)}</td><td class="num"><b style="color:var(--red)">${U.num(l.short, 3)}</b></td><td>${(l.listIncoming || []).length ? l.listIncoming.map(p => `${esc(p.purchaseOrderNumber || '')} · ${U.num(p.quantity, 3)} ${esc(p.unitName || '')}${p.expectedDate ? ' · ' + esc(U.dt(p.expectedDate)) : ''}`).join('<br>') : '<span class="muted">Chưa có đơn mua</span>'}</td></tr>`).join('')}</tbody></table></div>`
            : '<div class="notice ok">Đủ nguyên liệu nếu ra lệnh lúc này.</div>') + (warn.length ? `<div class="small muted mt">${warn.map(esc).join('<br>')}</div>` : '');
        });
      } });
  }

  // ----- Tab Nhật ký tự động: Note.AutoRun của kế hoạch 14 ngày (7 ngày trước + kỳ đang xem) -----
  function logTab(el) {
    const body = el.querySelector('#pdBody'); body.innerHTML = '<div class="muted small">Đang tải…</div>';
    const from = U.addDays(pd.from || U.day(new Date().toISOString()), -7);
    Store.query('ProductionPlan/GetDays', { From: from, To: U.addDays(from, 13) }).then(r => {
      if (!body.isConnected) return;
      const rows = ((r.ok && r.data && r.data.listDay) || []).filter(x => x.autoRun).reverse();
      body.innerHTML = rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Ngày SX</th><th>Kế hoạch</th><th>Trạng thái</th><th>Hệ thống đã làm</th></tr></thead><tbody>${rows.map(x => `<tr><td>${dow(x.date)} ${U.date(x.date)}</td><td><a href="${planHref(x.planId)}">${esc(x.planNumber || '')}</a></td><td>${chip(x)}</td><td style="white-space:normal">${esc(x.autoRun)}</td></tr>`).join('')}</tbody></table></div>`
        : '<div class="muted small">Chưa có lần tự lập / tự ra lệnh nào trong 14 ngày</div>';
    });
  }

  const tabs = () => `<div class="tabs pp-tabs mb">${[['days', '7 ngày tới'], ['list', 'Danh sách kế hoạch'], ['log', 'Nhật ký tự động']].map(([k, l]) => `<button class="tab ${pd.tab === k ? 'active' : ''}" data-pt="${k}">${l}</button>`).join('')}</div>`;
  const purpose = '<div class="small muted mb">Hệ thống tự lập, tự duyệt và ra lệnh sản xuất sau khi chốt đơn – màn này cho thấy 7 ngày tới, chỉ cần xử lý những ngày có nút.</div>';

  APP.register('prod-plan', (el, f, p) => {
    if (!Store.isLive) return APP.prodPlanList(el, f);
    if (p && p.tab && ['days', 'list', 'log'].includes(p.tab)) pd.tab = p.tab;
    if (p && /^\d{4}-\d{2}-\d{2}$/.test(p.from || '') && p.from !== pd.from) { pd.from = p.from; pd.data = null; }
    const bind = () => el.querySelectorAll('[data-pt]').forEach(b => b.onclick = () => { pd.tab = b.dataset.pt; APP.SCREENS['prod-plan'](el, f); });
    if (pd.tab === 'list') { APP.prodPlanList(el, f, purpose + tabs()); bind(); return; }
    el.innerHTML = APP.header(f, '', { noNote: true }) + purpose + tabs()
      + (pd.tab === 'days' ? '<div class="card panel mb" id="pdHead"><div class="muted small">Đang tải kế hoạch 7 ngày tới…</div></div>' : '')
      + `<div class="card panel" id="pdBody">${pd.tab === 'days' ? '' : ''}</div>`;
    bind();
    if (pd.tab === 'log') return logTab(el);
    if (pd.data && pd.from) draw(el, f);
    load(el, f);
  });
})();
