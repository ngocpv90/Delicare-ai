/* APR-UI-1 (D-81, anh chốt 2026-11-23) – màn PHÊ DUYỆT TỔNG (wf-inbox, chế độ dữ liệu thật).
 * Mockup: docs/frontend-handoff/assets/apr-ui-1/Main.dc.html (web), Mobile.dc.html (≤ 768px). Một bộ vẽ cho mọi loại theo khuôn tóm tắt chung của máy chủ
 * (summary: title, typeName, moduleName, documentNo, mainValue, reason, flags ≤ 3, info, lineHeaders, lines, changes, openLink).
 * Tab: Chờ tôi duyệt (GetMyPending – gồm kế hoạch SX giữ Nháp chờ COO) · Tôi đã gửi (GetMySent + Nhắc qua bot) · Tôi đã duyệt (GetMyDecided) ·
 * Tất cả (chỉ người có Xem #/wf-inbox – CEO / CEO-PMO / COO / FIN-IT). Lọc nhanh theo phân hệ có số, tìm tại chỗ, ?type= cũ áp làm bộ lọc.
 * Xem nhanh: ≥ 1200px ngăn bên phải; < 1200px lớp phủ (điện thoại: toàn màn, nút cố định cuối). Duyệt & sang việc tiếp, ‹ Trước / Tiếp theo ›,
 * từ chối bắt buộc lý do; duyệt nhiều chỉ dòng canBulk (máy chủ kiểm lại). Chế độ DEMO giữ màn cũ (modules/hr.js). */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const legacy = APP.SCREENS['wf-inbox'];
  const LOADING = '<div class="empty">Đang tải…</div>';
  const MODULES = ['Kinh doanh', 'Thực đơn', 'Mua hàng', 'Kho', 'Sản xuất', 'Nhân sự', 'Khác'];
  // Tên loại cho tab Tất cả (máy chủ trả typeName ở các tab khác).
  const TYPE_VI = { PURCHASE_ORDER: 'Đơn mua hàng', PURCHASE_REQUEST: 'Đề nghị mua', ORDER_CHANGE_AFTER_CUTOFF: 'Sửa đơn sau chốt', ORDER_DEVIATION: 'Lệch suất đơn đặt', CONTRACT: 'Hợp đồng',
    RECIPE_VERSION: 'Công thức chế biến', MENU_CHANGE: 'Yêu cầu đổi món', STOCK_COUNT: 'Kiểm kê', STOCK_ADJUSTMENT: 'Điều chỉnh tồn', LEAVE_REQUEST: 'Đơn xin nghỉ', SHIFT_SWAP: 'Đơn đổi ca' };
  const STATUS_VI = { Pending: 'Chờ duyệt', InProgress: 'Đang duyệt', Approved: 'Đã duyệt', Rejected: 'Từ chối', Cancelled: 'Đã hủy' };
  const H = { tab: 'mine', g: '', q: '', cur: null, picked: new Set(), mine: null, mineAt: 0, sent: null, done: null, all: null, busy: false, note: '' };
  APP.aprState = H;

  const live = () => Store.isLive;
  const canAll = () => !!(window.DC_HOST && (window.DC_HOST.user && window.DC_HOST.user.isAdmin || window.DC_HOST.perm('wf-inbox').access === true));
  const hoursTo = d => (new Date(d) - Date.now()) / 36e5;
  const hrs = h => (Math.abs(h) >= 48 ? Math.round(Math.abs(h) / 24) + ' ngày' : Math.max(1, Math.round(Math.abs(h))) + ' giờ');
  const dueLabel = i => !i.dueAt ? { t: 'Không hạn', c: 'ok' } : i.isOverdue ? { t: 'Quá hạn ' + hrs(hoursTo(i.dueAt)), c: 'over' } : { t: 'Còn ' + hrs(hoursTo(i.dueAt)), c: i.dueSoon ? 'soon' : 'ok' };
  const sum = i => i.summary || {};
  const flags = i => (sum(i).flags || []).filter(f => f && f.text);
  const typeOf = i => sum(i).typeName || i.stepName || TYPE_VI[i.businessType] || 'Yêu cầu duyệt';

  // ---------- dữ liệu ----------
  function loadMine(force) {
    if (!live() || H.busy || (!force && H.mine && Date.now() - H.mineAt < 30000)) return Promise.resolve();
    H.busy = true;
    return Store.query('ApprovalRequest/GetMyPending').then(x => {
      H.busy = false; H.mineAt = Date.now();
      const before = H.mine ? H.mine.map(i => i.id).join() : null;
      H.mine = x.ok ? x.list : [];
      APP.aprCount = H.mine.length; if (APP.updateCounts) APP.updateCounts();
      const keep = new Set(H.mine.map(i => i.id)); H.picked.forEach(id => { if (!keep.has(id)) H.picked.delete(id); });
      return before !== H.mine.map(i => i.id).join();
    });
  }
  const loadSent = () => Store.query('ApprovalRequest/GetMySent').then(x => { H.sent = x.ok ? x.list : []; window.DC_APR_SENT = H.sent.length > 0; });
  const loadDone = () => Store.query('ApprovalRequest/GetMyDecided', { page: 1, pageSize: 50 }).then(x => { H.done = x.ok ? x.list : []; });
  const loadAll = () => Store.query('ApprovalRequest/GetAll', { Page: 1, PageSize: 200, SortField: 'RequestedAt', SortOrder: 'desc' }).then(x => { H.all = x.ok ? x.list : []; });

  // Số "chờ tôi duyệt" trên sidebar + quyền vào màn của người chỉ gửi yêu cầu: làm mới 30 giây (D-81 a).
  function tick() {
    if (!live() || !window.DC_HOST) return;
    const wasSent = window.DC_APR_SENT;
    const access = Store.perm('wf-inbox').access !== false;
    (access ? loadMine(true) : Promise.resolve(false)).then(changed => { if (changed && APP.current === 'wf-inbox' && H.tab === 'mine' && !document.querySelector('.apr-full')) APP.refresh(); });
    if (H.sent === null || !access) loadSent().then(() => { if (!wasSent && window.DC_APR_SENT && APP._buildNav) APP._buildNav(); });
  }
  setTimeout(tick, 2500); setInterval(tick, 30000);

  // ---------- lọc ----------
  function filtered(p) {
    const typ = p && p.type && p.type !== 'wf-inbox' ? String(p.type) : '';
    const q = H.q.trim().toLowerCase();
    return (H.mine || []).filter(i => (!typ || i.screenId === typ || String(i.screenId || '').replace(/-(l\d+|director)$/, '') === typ)
      && (!H.g || (sum(i).moduleName || 'Khác') === H.g)
      && (!q || [i.requestNo, sum(i).documentNo, sum(i).title, i.title, i.requestedByName, sum(i).counterparty, typeOf(i)].some(v => String(v || '').toLowerCase().includes(q))));
  }

  // ---------- vẽ ----------
  APP.register('wf-inbox', (el, f, p) => {
    if (!live()) return legacy && legacy(el, f, p);
    if (p && p.type && p.type !== 'wf-inbox' && H.lastType !== p.type) { H.lastType = p.type; H.tab = 'mine'; H.g = ''; }
    if (H.mine === null) { el.innerHTML = APP.header(f, '', { noNote: true }) + `<div class="card panel">${LOADING}</div>`; loadMine(true).then(() => APP.current === 'wf-inbox' && APP.refresh()); if (H.sent === null) loadSent(); return; }
    if (H.sent === null) { loadSent().then(() => APP.current === 'wf-inbox' && APP.refresh()); }
    const mine = H.mine, list = filtered(p), sent = H.sent || [];
    if (!H.cur || !list.some(i => i.id === H.cur)) H.cur = list[0] ? list[0].id : null;
    const flagged = mine.filter(i => flags(i).length).length, over = mine.filter(i => i.isOverdue).length, soon = mine.filter(i => i.dueSoon);
    const maxWait = sent.reduce((m, s) => Math.max(m, s.waitingHours || 0), 0);
    const user = (window.DC_HOST && window.DC_HOST.user) || {};
    const typ = p && p.type && p.type !== 'wf-inbox' ? String(p.type) : '';
    const tabs = [['mine', `Chờ tôi duyệt (${mine.length})`], ['sent', `Tôi đã gửi (${sent.length})`], ['done', 'Tôi đã duyệt']].concat(canAll() ? [['all', 'Tất cả']] : []);
    if (!tabs.some(t => t[0] === H.tab)) H.tab = 'mine';
    const counts = {}; mine.forEach(i => { const m = sum(i).moduleName || 'Khác'; counts[m] = (counts[m] || 0) + 1; });
    el.innerHTML = APP.header(f, `<label class="apr-search">${I('search')}<input type="search" id="aprQ" placeholder="Số chứng từ, khách, NCC, người gửi" value="${esc(H.q)}" aria-label="Tìm yêu cầu duyệt"></label>`,
      { noNote: true, sub: `${esc(user.fullName || user.userName || '')}${user.fullName || user.userName ? ' · ' : ''}cập nhật ${new Date(H.mineAt || Date.now()).toTimeString().slice(0, 5)} · tự làm mới 30 giây` })
      + `<div class="kpis">${UI.kpi('Chờ tôi duyệt', U.num(mine.length), `${flagged} cảnh báo · ${mine.length - flagged} bình thường`, 'approve', 'orange')}${UI.kpi('Quá hạn', U.num(over), over ? 'đã leo thang theo hạn' : '', 'alert', over ? 'red' : 'green')}${UI.kpi('Hết hạn trong 8 giờ', U.num(soon.length), soon[0] ? typeOf(soon[0]) : '', 'clock', soon.length ? 'orange' : 'green')}${UI.kpi('Tôi gửi đang chờ', U.num(sent.length), sent.length ? 'lâu nhất ' + hrs(maxWait) : '', 'export', 'blue')}</div>`
      + `<div class="apr-layout"><section class="card apr-main"><div class="apr-tabs" role="tablist">${tabs.map(([k, l]) => `<button role="tab" aria-selected="${H.tab === k}" class="${H.tab === k ? 'on' : ''}" data-tab="${k}">${esc(l)}</button>`).join('')}</div><div id="aprBody"></div></section>`
      + `<aside class="card apr-side" id="aprSide" aria-label="Xem nhanh yêu cầu"></aside></div>`;
    const body = el.querySelector('#aprBody');
    if (H.tab === 'mine') {
      body.innerHTML = `<div class="apr-chips">${typ ? `<span class="tag orange">Đang lọc: ${esc((document.querySelector(`#navGroups [data-nav="wf-inbox?type=${typ}"]`) || {}).title || (APP.FN[typ] && APP.FN[typ].fn) || typ)}</span><a class="btn xs" href="${APP.href('wf-inbox')}">Bỏ lọc</a>` : ''}
          <button class="chip ${H.g ? '' : 'active'}" data-g="" aria-pressed="${!H.g}">Tất cả <b>${mine.length}</b></button>${MODULES.filter(m => counts[m]).map(m => `<button class="chip ${H.g === m ? 'active' : ''}" data-g="${esc(m)}" aria-pressed="${H.g === m}">${esc(m)} <b>${counts[m]}</b></button>`).join('')}
          <span class="grow"></span><span class="small muted apr-sortnote">Xếp: quá hạn → sắp hết hạn → cũ nhất</span></div>
        ${H.picked.size ? `<div class="apr-bulk"><b>Đã chọn ${H.picked.size} yêu cầu bình thường</b><span class="small muted">Chỉ chọn được dòng không có cảnh báo, loại rủi ro thấp</span><span class="grow"></span><button class="btn primary" id="aprBulk">${I('approve')} Duyệt các mục đã chọn</button></div>` : ''}
        <div class="apr-rows">${list.length ? list.map(rowHtml).join('') : `<div class="empty">${I('ok')}<div class="mt">${mine.length ? 'Không có yêu cầu khớp bộ lọc' : 'Không có yêu cầu nào chờ bạn duyệt'}</div></div>`}</div>`;
      body.querySelectorAll('[data-g]').forEach(b => b.onclick = () => { H.g = b.dataset.g; APP.refresh(); });
      body.querySelectorAll('[data-open]').forEach(b => b.onclick = () => open(b.dataset.open, list));
      body.querySelectorAll('input[data-pick]').forEach(c => c.onchange = () => { if (c.checked) H.picked.add(c.dataset.pick); else H.picked.delete(c.dataset.pick); APP.refresh(); });
      const bulk = body.querySelector('#aprBulk'); if (bulk) bulk.onclick = bulkApprove;
      side(el.querySelector('#aprSide'), list);
    } else if (H.tab === 'sent') { sentTab(body); el.querySelector('#aprSide').remove(); }
    else if (H.tab === 'done') { doneTab(body); el.querySelector('#aprSide').remove(); }
    else { allTab(body); el.querySelector('#aprSide').remove(); }
    el.querySelectorAll('[data-tab]').forEach(b => b.onclick = () => { H.tab = b.dataset.tab; APP.refresh(); });
    const q = el.querySelector('#aprQ'); if (q) q.oninput = () => { H.q = q.value; clearTimeout(H.qt); H.qt = setTimeout(() => { APP.refresh(); const n = document.getElementById('aprQ'); if (n) { n.focus(); n.setSelectionRange(n.value.length, n.value.length); } }, 250); };
  });

  function rowHtml(i) {
    const fl = flags(i), d = dueLabel(i), s = sum(i);
    return `<div class="apr-row ${H.cur === i.id ? 'cur' : ''}">
      <label class="apr-pick">${i.canBulk ? `<input type="checkbox" data-pick="${i.id}" ${H.picked.has(i.id) ? 'checked' : ''} aria-label="Chọn ${esc(s.documentNo || i.requestNo)} để duyệt nhanh">` : `<input type="checkbox" disabled aria-label="Không duyệt nhanh được – mở từng yêu cầu" title="Có cảnh báo hoặc loại phải mở từng yêu cầu">`}</label>
      <button type="button" class="apr-open" data-open="${i.id}">
        <span class="apr-type"><b>${esc(typeOf(i))}</b><span class="small muted">${esc(s.documentNo || i.requestNo || '')}</span></span>
        <span class="apr-what"><span class="apr-title">${esc(s.title || i.title || '')}</span><span class="small muted">Vì sao cần duyệt: ${esc(s.reason || '')}</span>
          ${fl.length ? `<span class="apr-flag warn">⚠ ${fl.length} cảnh báo: ${esc(fl[0].text)}</span>` : '<span class="apr-flag okf">✓ Bình thường</span>'}</span>
        <span class="apr-by small muted">${esc(i.requestedByName || '–')}<br>gửi ${esc(U.dt(i.requestedAt))} · cấp ${i.stepNo}/${i.stepCount}</span>
        <span class="apr-due ${d.c}">${d.t}</span>
      </button></div>`;
  }

  // ---------- xem nhanh ----------
  function detailHtml(i, list, mobile) {
    const s = sum(i), fl = flags(i), idx = list.findIndex(x => x.id === i.id), d = dueLabel(i);
    const plan = i.source === 'PLAN';
    const lines = s.lines || [], heads = s.lineHeaders || [];
    return `${mobile ? '' : `<div class="apr-nav"><span class="small muted">Xem nhanh · ${idx + 1} / ${list.length}</span><span class="grow"></span><button class="btn sm" data-nav="-1" ${list.length < 2 ? 'disabled' : ''}>‹ Trước</button><button class="btn sm" data-nav="1" ${list.length < 2 ? 'disabled' : ''}><b>Tiếp theo ›</b></button></div>`}
      <div><div class="small muted">${esc(typeOf(i))} · ${s.openLink ? `<a href="${APP.href(s.openLink)}">${esc(s.documentNo || i.requestNo || '')} ↗</a>` : esc(s.documentNo || i.requestNo || '')}${mobile ? '' : ` · <span class="apr-due ${d.c}">${d.t}</span>`}</div><h2 class="apr-h">${esc(s.title || i.title || '')}</h2>${s.mainValue ? `<div class="apr-main-v">${esc(s.mainValue)}</div>` : ''}</div>
      <div class="apr-why"><div class="apr-cap">Vì sao cần duyệt</div><div>${esc(s.reason || '')}</div></div>
      <div class="apr-box ${fl.length ? 'warn' : 'okb'}"><div class="apr-cap">${fl.length ? `Cảnh báo (${fl.length})` : 'Bình thường'}</div>${fl.length ? fl.map(x => `<div>• ${esc(x.text)}</div>`).join('') : '<div>Không có điểm bất thường so với dữ liệu hiện có</div>'}</div>
      ${(s.info || []).length ? `<div class="apr-info">${s.info.map(x => `<span class="muted">${esc(x.label)}</span><span>${esc(x.value == null ? '–' : x.value)}</span>`).join('')}</div>` : ''}
      ${lines.length ? `<div class="apr-lines"><table><thead><tr>${heads.map(h => `<th>${esc(h)}</th>`).join('')}</tr></thead><tbody>${lines.map(l => `<tr class="${l.highlight ? 'hl' : ''}">${(l.cells || []).map(c => `<td>${esc(c == null ? '–' : c)}</td>`).join('')}</tr>`).join('')}</tbody></table>${s.lineCount > lines.length ? `<div class="small muted apr-more">${s.openLink ? `<a href="${APP.href(s.openLink)}">Xem đủ ${s.lineCount} dòng và chứng từ gốc ↗</a>` : `Còn ${s.lineCount - lines.length} dòng`}</div>` : ''}</div>` : ''}
      ${(s.changes || []).length && !lines.length ? `<div class="apr-info">${s.changes.map(c => `<span class="muted">${esc(c.label)}</span><span>${esc(c.before || '–')} → <b>${esc(c.after || '–')}</b></span>`).join('')}</div>` : ''}
      <div class="small muted" data-progress>Tiến trình: ${esc(i.stepName || '')} – cấp ${i.stepNo}/${i.stepCount}, đang chờ bạn${i.requestedByName ? ' · người gửi ' + esc(i.requestedByName) : ''}</div>
      <label class="apr-note">Ý kiến ${plan ? '' : '(bắt buộc khi từ chối)'}<textarea rows="2" id="aprNote" maxlength="1000">${esc(H.note)}</textarea></label>
      <div class="apr-acts">${plan ? `<a class="btn" href="${APP.href(s.openLink || 'prod-plan')}">Sửa kế hoạch</a>` : `<button class="btn danger" data-act="no">Từ chối</button>`}<button class="btn primary" data-act="ok">${I('approve')} ${mobile ? 'Duyệt · việc tiếp ›' : 'Duyệt &amp; sang việc tiếp'}</button></div>`;
  }
  function bindDetail(box, i, list, close) {
    const note = box.querySelector('#aprNote'); if (note) note.oninput = () => { H.note = note.value; };
    box.querySelectorAll('[data-nav]').forEach(b => b.onclick = () => { const idx = list.findIndex(x => x.id === i.id), n = list[(idx + +b.dataset.nav + list.length) % list.length]; H.cur = n.id; H.note = ''; APP.refresh(); });
    box.querySelectorAll('[data-act]').forEach(b => b.onclick = () => decide(i, b.dataset.act === 'ok', list, close));
    // Tiến trình các cấp + người quyết định (GetDetail) – nạp sau, không chặn duyệt.
    Store.query('ApprovalRequest/GetDetail', { id: i.id }).then(x => {
      const pg = box.querySelector('[data-progress]'); if (!pg || !x.ok || !x.data) return;
      const steps = x.data.listStepInfo || [];
      if (steps.length) pg.innerHTML = 'Tiến trình: ' + steps.map(st => `${st.stepNo}. ${esc(st.stepName || '')} – ${esc(st.decisionStatusName || '')}${st.approverName ? ' (' + esc(st.approverName) + ')' : ''}${st.isCurrent ? ' <b>← đang chờ</b>' : ''}`).join(' → ');
    });
  }
  function side(box, list) {
    if (!box) return;
    const i = list.find(x => x.id === H.cur);
    if (!i) { box.innerHTML = `<div class="empty">${I('approve')}<div class="mt">Chọn một yêu cầu để xem nhanh</div></div>`; return; }
    box.innerHTML = detailHtml(i, list, false); bindDetail(box, i, list, null);
  }
  // < 1200px: lớp phủ (điện thoại toàn màn – nút Duyệt / Từ chối cố định cuối, vùng bấm ≥ 44px).
  function open(id, list) {
    H.cur = id; H.note = '';
    if (innerWidth >= 1200) { APP.refresh(); return; }
    const i = list.find(x => x.id === id); if (!i) return;
    document.querySelectorAll('.apr-full').forEach(x => x.remove());
    const wrap = document.createElement('div'); wrap.className = 'apr-full'; wrap.setAttribute('role', 'dialog'); wrap.setAttribute('aria-modal', 'true');
    const d = dueLabel(i), idx = list.findIndex(x => x.id === id);
    const close = () => { wrap.remove(); APP.refresh(); };
    wrap.innerHTML = `<div class="apr-full-in"><div class="apr-full-head"><button class="btn apr-back" aria-label="Quay lại danh sách">‹</button><div class="grow"><div class="small muted">${esc(typeOf(i))} · ${idx + 1} / ${list.length}</div><b>${esc(sum(i).documentNo || i.requestNo || '')}</b></div><span class="apr-due ${d.c}">${d.t}</span></div><div class="apr-full-body">${detailHtml(i, list, true)}</div></div>`;
    document.body.appendChild(wrap);
    wrap.querySelector('.apr-back').onclick = close;
    wrap.onclick = e => { if (e.target === wrap) close(); };
    bindDetail(wrap, i, list, () => wrap.remove());
    const next = () => { const n = filtered(APP.params)[0]; if (n) open(n.id, filtered(APP.params)); else close(); };
    wrap._next = next;
  }

  // ---------- duyệt ----------
  function decide(i, ok, list, close) {
    const note = (H.note || '').trim();
    if (!ok && !note) { UI.toast('Nhập lý do từ chối', 'err'); const n = document.getElementById('aprNote'); if (n) n.focus(); return; }
    Store.act('ApprovalRequest/Decide', { form: { Id: i.id, Approve: ok, Note: note || null }, tables: [] }).then(x => {
      if (!x.ok) return;
      UI.toast(ok ? `Đã duyệt ${esc(sum(i).documentNo || i.requestNo || '')}` : 'Đã từ chối', ok ? 'ok' : 'warn');
      const idx = list.findIndex(y => y.id === i.id);
      H.mine = (H.mine || []).filter(y => y.id !== i.id); H.picked.delete(i.id); H.note = '';
      APP.aprCount = H.mine.length; if (APP.updateCounts) APP.updateCounts();
      const rest = list.filter(y => y.id !== i.id), nx = rest[Math.min(idx, rest.length - 1)];
      H.cur = nx ? nx.id : null;
      // "Duyệt & sang việc tiếp": mở ngay việc tiếp (lớp phủ) hoặc tô dòng tiếp (ngăn bên phải).
      if (close) { close(); if (nx && innerWidth < 1200) open(nx.id, rest); else APP.refresh(); } else APP.refresh();
      setTimeout(() => loadMine(true), 1500);
    });
  }
  function bulkApprove() {
    const ids = [...H.picked];
    UI.confirm(`Duyệt <b>${ids.length}</b> yêu cầu bình thường đã chọn?<div class="small muted mt">Máy chủ kiểm lại từng yêu cầu – yêu cầu có cảnh báo hoặc không còn chờ bạn sẽ bị bỏ qua.</div>`, () =>
      Store.act('ApprovalRequest/DecideBulk', { json: { listId: ids, note: null }, tables: [] }).then(x => {
        if (!x.ok) return;
        const res = (x.res && (x.res.listData || x.res.ListData)) || [];
        const bad = res.filter(r => !r.ok);
        UI.toast(esc((x.res && x.res.responseMessage) || 'Đã duyệt') + (bad.length ? '<br>' + bad.map(r => esc((r.requestNo || '') + ': ' + (r.message || ''))).join('<br>') : ''), bad.length ? 'warn' : 'ok');
        H.picked.clear(); loadMine(true).then(() => APP.refresh());
      }), { ok: 'Duyệt ' + ids.length + ' mục' });
  }

  // ---------- tab khác ----------
  function sentTab(body) {
    const sent = H.sent || [];
    body.innerHTML = `<div class="apr-rows">${sent.length ? sent.map(s => `<div class="apr-row sent"><span class="apr-type"><b>${esc(s.summary && s.summary.typeName || TYPE_VI[s.businessType] || '')}</b><span class="small muted">${esc(s.summary && s.summary.documentNo || s.requestNo || '')}</span></span>
        <span class="apr-what"><span class="apr-title">${esc(s.summary && s.summary.title || '')}</span>${s.isOverdue ? '<span class="apr-flag warn">Quá hạn</span>' : ''}</span>
        <span class="small muted">Đang chờ: <b class="ink">${esc(s.holderNames || '–')}</b><br>đã chờ ${hrs(s.waitingHours || 0)} · cấp ${s.stepNo}/${s.stepCount}${s.lastRemindAt ? ' · đã nhắc ' + esc(U.dt(s.lastRemindAt)) : ''}</span>
        <span class="apr-sent-acts"><button class="btn sm" data-remind="${s.id}" ${s.canRemind ? '' : 'disabled title="Mỗi yêu cầu nhắc tối đa 1 lần / 4 giờ"'}>${I('bell')} Nhắc qua bot</button><button class="btn xs" data-cancel="${s.id}" data-no="${esc(s.requestNo || '')}">Hủy</button></span></div>`).join('')
      : `<div class="empty">${I('ok')}<div class="mt">Bạn không có yêu cầu nào đang chờ duyệt</div></div>`}</div>
      <div class="small muted apr-foot">Nhắc tối đa 1 lần / 4 giờ cho mỗi yêu cầu. Yêu cầu do bạn gửi không hiện ở "Chờ tôi duyệt" (người gửi không tự duyệt – D-79).</div>`;
    body.querySelectorAll('[data-remind]').forEach(b => b.onclick = () => Store.act('ApprovalRequest/Remind', { form: { Id: b.dataset.remind }, tables: [] }).then(x => { if (!x.ok) return; UI.toast(esc((x.res && x.res.responseMessage) || 'Đã nhắc'), 'ok'); loadSent().then(() => APP.refresh()); }));
    body.querySelectorAll('[data-cancel]').forEach(b => b.onclick = () => UI.confirm(`Hủy yêu cầu duyệt <b>${esc(b.dataset.no)}</b>?<div class="small muted mt">Chứng từ về Nháp, không còn chờ duyệt. Không hoàn tác được.</div>`,
      note => Store.act('ApprovalRequest/Cancel', { form: { Id: b.dataset.cancel, Reason: note }, tables: [] }).then(x => { if (!x.ok) return; UI.toast('Đã hủy yêu cầu duyệt', 'ok'); loadSent().then(() => APP.refresh()); }),
      { input: 'Lý do hủy', required: true, danger: true, ok: 'Hủy yêu cầu' }));
  }
  function doneTab(body) {
    if (H.done === null) { body.innerHTML = LOADING; loadDone().then(() => APP.current === 'wf-inbox' && APP.refresh()); return; }
    body.innerHTML = `<div class="apr-rows">${H.done.length ? H.done.map(d => `<div class="apr-row done"><span class="apr-type"><b>${esc(d.typeName || '')}</b><span class="small muted">${esc(d.requestNo || '')}</span></span>
        <span class="apr-what"><span class="apr-title">${esc(d.title || '')}</span>${d.comment ? `<span class="small muted">Ý kiến: ${esc(d.comment)}</span>` : ''}</span>
        <span class="small muted">${esc(U.dt(d.decidedAt))} · cấp ${d.stepNo}</span><span class="apr-res ${d.decisionCode === 'APPROVE' ? 'okf' : 'nof'}">${esc(d.decisionName || '')}</span></div>`).join('')
      : `<div class="empty">Chưa có quyết định nào</div>`}</div><div class="small muted apr-foot">Hậu kiểm (hệ thống tự duyệt việc bình thường, hoàn tác 24 giờ) chỉ bật sau UAT khi anh chốt danh sách loại – D-81 k.</div>`;
  }
  function allTab(body) {
    if (H.all === null) { body.innerHTML = LOADING; loadAll().then(() => APP.current === 'wf-inbox' && APP.refresh()); return; }
    const q = H.q.trim().toLowerCase();
    const rows = H.all.filter(a => !q || [a.requestNo, a.businessType, TYPE_VI[a.businessType]].some(v => String(v || '').toLowerCase().includes(q)));
    body.innerHTML = `<div class="apr-rows">${rows.length ? rows.map(a => `<div class="apr-row done"><span class="apr-type"><b>${esc(TYPE_VI[a.businessType] || a.businessType || '')}</b><span class="small muted">${esc(a.requestNo || '')}</span></span>
        <span class="apr-what"><span class="apr-title">${a.currentStepNo ? 'Cấp ' + a.currentStepNo : ''}</span></span><span class="small muted">gửi ${esc(U.dt(a.requestedAt))}</span>
        <span class="apr-res ${a.requestStatus === 'Approved' ? 'okf' : a.requestStatus === 'Rejected' ? 'nof' : ''}">${esc(STATUS_VI[a.requestStatus] || a.requestStatus || '')}</span></div>`).join('') : '<div class="empty">Không có yêu cầu</div>'}</div>
      <div class="small muted apr-foot">Tab Tất cả chỉ xem (CEO / CEO-PMO / COO / FIN-IT) – có quyền xem không làm người có nó duyệt được mọi loại.</div>`;
  }
})();
