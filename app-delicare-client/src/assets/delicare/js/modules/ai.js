/* AI-21 (D-138): Trung tâm Agent – MỘT màn 7 tab trong nhóm Điều hành (#/ai-agents?tab=overview|chat|tasks|flow|schedule|log|usage), nền sáng theo
 * mockup docs/frontend-handoff/assets/ai-center/ (Overview, Chat, Tasks, Flow). Tab hiện theo quyền Xem của mục tương ứng (mục ẩn khỏi sidebar, giữ dòng quyền):
 * Tổng quan = ai-agents · Giao việc = ai-tasks · Luồng = ai-flow · Lịch chạy = ai-schedule · Nhật ký = ai-agent-log · Sử dụng & chi phí = ai-cost (CEO / COO / FIN-IT);
 * Chat mở cho người vào được màn (API Agent/Chat – quyền giao nhau agent ∩ người hỏi). #/ai-agent-log, #/ai-schedule, #/ai-flow, #/ai-tasks cũ → chuyển sang tab.
 * Chi tiết 1 agent: #/ai-agents?code=WH (AI-6). Bật / tắt agent = Sửa Trung tâm Agent (BotStatus, có hiệu lực ngay ở ToolGateway).
 * API: Agent/GetOverview · GetChatAgents · GetTasks · CloseTask · GetFlowDay · GetFlows · GetSchedules · SaveSchedule · GetActivity · GetCost · Chat · GetAll · GetById · SaveIsActive;
 * Knowledge/GetTree · GetNote · Search · GetHistory · Save. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const FN = APP.FN || {};
  // AI-22.1: vùng con "20 Kinh doanh & Hợp đồng/Hợp đồng khách hàng" → "20 › Hợp đồng khách hàng" (quyền riêng từng vùng).
  const kfName = nm => String(nm || '').replace(/^(\d+)[^/]*\//, '$1 › ');
  const def = (id, name, grp) => { if (!FN[id]) FN[id] = { extra: true, ph: 'AI', grp, fn: name, id, name, types: ['LS'], table: null, rel: [], st: 'MAPPED', note: '' }; };
  def('ai-agents', 'Trung tâm Agent', 'Điều hành'); def('knowledge', 'Kho tri thức', 'Tri thức');
  ['ai-agent-log', 'ai-flow', 'ai-tasks', 'ai-schedule', 'ai-cost'].forEach(id => def(id, id, 'Điều hành'));

  const TABS = [['overview', 'Tổng quan', 'ai-agents'], ['chat', 'Chat với agent', null], ['tasks', 'Giao việc', 'ai-tasks'], ['flow', 'Luồng liên phòng', 'ai-flow'],
    ['schedule', 'Lịch chạy', 'ai-schedule'], ['log', 'Nhật ký agent', 'ai-agent-log'], ['usage', 'Sử dụng & chi phí', 'ai-cost']];
  const SUB = {
    overview: '13 agent vận hành bếp ăn · lệnh ghi luôn có người xác nhận ở Phê duyệt',
    chat: 'Agent chỉ trả lời trong phạm vi quyền của anh / chị · đề xuất ghi chờ xác nhận ở Phê duyệt › Lệnh của agent',
    tasks: 'Sự cố agent phát hiện, kèm việc nên làm · chỉ hiện việc thuộc quyền của anh / chị · yêu cầu duyệt vẫn ở màn Phê duyệt',
    flow: 'Quy trình QT-SX-01 từ đơn hàng đến báo cáo · trạng thái từng bước tính từ dữ liệu · bước kẹt tự sinh việc cho agent phòng đó',
    schedule: 'Lịch lấy từ app-delicare-agents/schedules.json – cùng một tệp sinh cron cho Hermes',
    log: 'Mọi lời gọi công cụ của agent (kênh HERMES) – ai hỏi, kết quả, thời gian',
    usage: 'Chi phí mô hình theo token Hermes báo mỗi lượt × đơn giá · ngân sách Agent:MonthlyBudget',
  };
  const isOverview = () => { const p = Store.perm('ai-agents'); return p.access !== false && !p.tabsOnly; };
  const tabOk = ([key, , id]) => key === 'overview' ? isOverview() : !id || Store.perm(id).access !== false;
  const tabsFor = () => TABS.filter(tabOk);
  const go = (tab, extra) => { location.hash = '#/ai-agents?tab=' + tab + (extra ? '&' + Object.entries(extra).filter(([, v]) => v != null && v !== '').map(([k, v]) => k + '=' + encodeURIComponent(v)).join('&') : ''); };
  APP.agentChat = (code, q) => go('chat', { agent: code || 'VIVI', q });

  const STATUS = { Succeeded: ['Thành công', 'green'], Denied: ['Bị từ chối', 'orange'], Failed: ['Lỗi', 'red'] };
  const ST = { ok: ['Đang chạy', 'ok'], key: ['Thiếu khóa', 'key'], off: ['Chưa bật', 'off'] };
  const chip = (txt, color) => `<span class="badge ${color || ''}">${esc(txt)}</span>`;
  const stChip = s => `<span class="ac-chip ${(ST[s] || ST.off)[1]}">${esc((ST[s] || ST.off)[0])}</span>`;
  const when = d => (d ? U.dt(d) : '–');
  const hm = d => (d ? String(d).slice(11, 16) : '');
  const loading = '<div class="muted small">Đang tải…</div>';
  const TODAY = () => U.day(U.iso(new Date()));
  const money = v => U.num(Math.round(v || 0)) + ' đ';
  const shortMoney = v => Math.abs(v || 0) >= 1e6 ? U.num((v || 0) / 1e6, 2) + ' tr đ' : money(v);
  /** Avatar robot SVG theo màu agent (mockup ai-center); agent chưa bật → xám. */
  const robot = (c, on, size, mini) => `<span class="ac-ring ${on ? '' : 'off'}" style="--c:${esc(c || '#4F6B1F')};width:${size}px;height:${size}px"><svg width="${Math.round(size * .64)}" height="${Math.round(size * .64)}" viewBox="0 0 40 40" aria-hidden="true">${mini ? '' : `<line x1="20" y1="3" x2="20" y2="9" stroke="${esc(c)}" stroke-width="2"/><circle cx="20" cy="3" r="2.5" fill="${esc(c)}"/>`}<rect x="7" y="9" width="26" height="22" rx="9" fill="#fff" stroke="${esc(c)}" stroke-width="${mini ? 2 : 1.5}"/><rect x="11" y="14" width="18" height="10" rx="5" fill="#1F2A1A"/><path d="M14 19.5q2-2.5 4 0M22 19.5q2-2.5 4 0" stroke="${on ? '#7CE0A8' : '#9AA391'}" stroke-width="1.8" fill="none" stroke-linecap="round"/>${mini ? '' : `<circle cx="20" cy="28" r="1.8" fill="${esc(c)}"/>`}</svg></span>`;
  const cache = { agents: null, board: null, boardAt: 0 };
  const chatAgents = () => cache.agents ? Promise.resolve(cache.agents) : Store.query('Agent/GetChatAgents').then(x => (cache.agents = x.ok ? (x.list || []) : []));
  const loadBoard = force => (!force && cache.board && Date.now() - cache.boardAt < 30000) ? Promise.resolve(cache.board)
    : Store.query('Agent/GetTasks', { includeDone: true }).then(x => { if (x.ok && x.data) { cache.board = x.data; cache.boardAt = Date.now(); } return x.ok ? x.data : null; });

  /** Khung chung: tiêu đề + thanh tab (theo quyền) + vùng nội dung. */
  function frame(el, f, tab, actions) {
    const name = (TABS.find(t => t[0] === tab) || TABS[0])[1];
    el.innerHTML = APP.header(Object.assign({}, f || FN['ai-agents'], { name: 'Trung tâm Agent', grp: 'Điều hành' }), actions || '', { noNote: true, title: name, sub: esc(SUB[tab] || '') })
      + `<nav class="ac-tabs" aria-label="Trung tâm Agent">${tabsFor().map(([k, n]) => `<a href="#/ai-agents?tab=${k}" class="ac-tab ${k === tab ? 'on' : ''}" ${k === tab ? 'aria-current="page"' : ''}>${esc(n)}${k === 'tasks' ? '<span class="ac-cnt" data-taskcnt hidden></span>' : ''}</a>`).join('')}</nav><div id="acBody">${loading}</div>`;
    if (Store.perm('ai-tasks').access !== false) loadBoard(false).then(b => { const c = el.querySelector('[data-taskcnt]'); if (c && b && b.open) { c.textContent = b.open; c.hidden = false; } });
    return el.querySelector('#acBody');
  }

  APP.register('ai-agents', (el, f, p) => {
    p = p || {};
    if (p.code) return detail(el, f, p.code);
    const list = tabsFor();
    let tab = p.tab && list.some(t => t[0] === p.tab) ? p.tab : (list.find(t => t[0] !== 'chat') || list[0] || TABS[1])[0]; // không có Tổng quan → tab đầu có quyền (vd Giao việc), Chat sau cùng
    if (p.tab && p.tab !== tab && TABS.some(t => t[0] === p.tab)) UI.toast('Anh / chị chưa có quyền xem tab này', 'warn');
    ({ overview, chat, tasks, flow, schedule, log, usage })[tab](el, f, p);
  });
  // Mục cũ (AI-6 / AI-11 / AI-12) → tab của Trung tâm Agent.
  [['ai-agent-log', 'log'], ['ai-schedule', 'schedule'], ['ai-flow', 'flow'], ['ai-tasks', 'tasks'], ['ai-cost', 'usage']].forEach(([id, tab]) => APP.register(id, (el, f, p) => {
    const q = Object.entries(p || {}).filter(([k]) => k !== 'tab').map(([k, v]) => '&' + k + '=' + encodeURIComponent(v)).join('');
    location.replace('#/ai-agents?tab=' + tab + q);
  }));

  // ================= Tổng quan =================
  const ov = { cur: 'VIVI' };
  function overview(el, f) {
    const b = frame(el, f, 'overview', `<span class="ac-pill" id="acPill"><span class="dot"></span>Đang tải…</span><button class="btn" id="acRefresh">${I('refresh')} Làm mới</button><a class="btn primary" href="#/ai-agents?tab=chat&agent=VIVI">Hỏi Vivi</a>`);
    el.querySelector('#acRefresh').onclick = () => { cache.board = null; APP.refresh(); };
    const canCost = Store.perm('ai-cost').access !== false;
    Store.query('Agent/GetOverview').then(x => {
      if (!b.isConnected) return;
      if (!x.ok || !x.data) { b.innerHTML = `<div class="notice warn">${esc(x.message || 'Không tải được Trung tâm Agent')}</div>`; return; }
      const d = x.data, agents = d.listAgent || [];
      el.querySelector('#acPill').innerHTML = `<span class="dot"></span>${d.running} đang chạy · ${d.total} agent · cập nhật ${esc(hm(d.generatedAt))}`;
      if (!agents.some(a => a.code === ov.cur)) ov.cur = (agents[0] || {}).code;
      const group = (cls, title, href, link, items) => `<section class="ac-group ${cls}"><div class="ac-group-h"><h2>${esc(title)}</h2>${href ? `<a href="${href}">${esc(link)} ›</a>` : ''}</div><div class="ac-group-g">${items.map(k => `<a class="ac-kpi" href="${k.href || '#'}"><div class="l">${esc(k.label)}</div><div class="v" style="${k.color ? 'color:' + k.color : ''}">${esc(k.v)}<span>${esc(k.sub || '')}</span></div><div class="n">${esc(k.note || '')}</div></a>`).join('')}</div></section>`;
      const canTasks = Store.perm('ai-tasks').access !== false, canFlow = Store.perm('ai-flow').access !== false;
      const g1 = [];
      if (canTasks) g1.push({ label: 'Việc đang mở', v: U.num(d.tasksOpen), note: d.tasksHot ? `${d.tasksHot} cần xử lý ngay` : 'không có việc gấp', color: d.tasksHot ? '#A12B1F' : '', href: '#/ai-agents?tab=tasks' });
      if (canFlow) g1.push({ label: 'Luồng liên phòng', v: U.num(d.flowAttention), sub: ` / ${d.flowSteps} bước`, note: 'cần chú ý hôm nay', color: d.flowAttention ? '#8A5A00' : '', href: '#/ai-agents?tab=flow' });
      const logOk = Store.perm('ai-agent-log').access !== false, schOk = Store.perm('ai-schedule').access !== false;
      b.innerHTML = `<div class="ac-tiles">${agents.map(a => `<button class="ac-tile ${a.code === ov.cur ? 'on' : ''}" data-pick="${esc(a.code)}" title="${esc(a.name)}">${robot(a.color, a.state !== 'off', 58)}<span class="ell b">${esc(a.short)}</span><span class="ac-tile-n"><i class="ac-dot ${esc(a.state)}"></i>${U.num(a.callsToday)} lượt</span></button>`).join('')}</div>
        <div class="ac-groups">${g1.length ? group('hot', 'Cần xử lý hôm nay', canTasks ? '#/ai-agents?tab=tasks' : '', 'Giao việc', g1) : ''}
          ${group('', 'Hoạt động agent', logOk ? '#/ai-agents?tab=log' : '', 'Nhật ký agent', [
            { label: 'Agent đang chạy', v: U.num(d.running), sub: ` / ${d.total}`, note: `${d.total - d.running} chưa bật / thiếu khóa` },
            { label: 'Lượt hỏi hôm nay', v: U.num(d.callsToday), note: `${d.askers} người · ${d.failedToday} lỗi · ${d.deniedToday} từ chối`, href: logOk ? '#/ai-agents?tab=log' : '' },
            { label: 'Lịch tự động', v: U.num(d.schedulesOn), sub: ` / ${d.schedulesTotal}`, note: 'việc đang bật', href: schOk ? '#/ai-agents?tab=schedule' : '' },
            { label: 'Kho tri thức', v: U.num(d.knowledgeNotes), note: 'ghi chú, SOP anh / chị xem được', href: '#/knowledge' }])}
          ${canCost ? '<a class="ac-group cost" id="acCostTile" href="#/ai-agents?tab=usage"><div class="ac-group-h"><h2>Chi phí AI</h2><span class="small">Chi tiết ›</span></div><div class="muted small">Đang tải…</div></a>' : ''}</div>
        <div class="ac-main"><section class="card ac-list"><div class="ac-list-h"><h2>Agent</h2><span class="small muted">Bấm để xem nhanh · lần gần nhất lấy từ nhật ký</span></div>
          ${agents.map(a => `<button class="ac-row ${a.code === ov.cur ? 'on' : ''}" data-pick="${esc(a.code)}">${robot(a.color, a.state !== 'off', 30, true)}<span class="ac-row-t"><span class="ell"><b>${esc(a.name)}</b> <span class="small muted">${a.toolCount} chức năng · ${a.scheduleCount} lịch</span></span><span class="ell small muted">${esc(a.lastLine || (a.state === 'off' ? `Đợt ${a.wave} – ${a.configured ? 'đang tắt' : 'chưa bật'}` : 'Chưa có lượt nào'))}</span></span><span class="small muted nowrap">${U.num(a.callsToday)} lượt</span>${stChip(a.state)}</button>`).join('')}</section>
          <div class="ac-side"><div id="acCur"></div><div id="acCost"></div>
            <section class="card ac-feed"><div class="ac-list-h"><h2>Hoạt động gần đây</h2></div>${(d.listFeed || []).length ? d.listFeed.map(r => `<div class="ac-feed-r"><i class="ac-dot ${r.status === 'Succeeded' ? 'ok' : r.status === 'Denied' ? 'key' : 'bad'}"></i><div class="ac-row-t"><div class="ell"><b>${esc(r.agent || '')}</b> · ${esc(r.text)}</div><div class="small muted ell">${esc(r.who || '')}</div></div><span class="small muted nowrap">${esc(hm(r.at))}</span></div>`).join('') : '<div class="muted small" style="padding:12px 16px">Chưa có hoạt động trong 7 ngày.</div>'}</section></div></div>`;
      const paintCur = () => {
        const a = agents.find(z => z.code === ov.cur); const box = b.querySelector('#acCur'); if (!a || !box) return;
        const canEdit = Store.perm('ai-agents').edit === true && a.configured;
        box.innerHTML = `<section class="card ac-cur"><div class="row-flex" style="gap:12px;align-items:center">${robot(a.color, a.state !== 'off', 52)}<div style="flex:1;min-width:0"><b class="ell" style="font-size:16px;display:block">${esc(a.name)}</b><div class="small muted ell">Phục vụ: ${esc(a.serves)}</div></div>${stChip(a.state)}</div>
          <div class="ac-desc">${esc(a.purpose)}</div>
          <div class="ac-mini"><div><div class="l">Lượt hôm nay</div><b>${U.num(a.callsToday)}</b></div><div><div class="l">Chức năng</div><b>${U.num(a.toolCount)}</b></div><div><div class="l">Việc đang theo</div><b>${U.num(a.openTasks)}</b></div></div>
          ${a.listAsk.length && a.state !== 'off' ? `<div class="small muted">Hỏi nhanh</div><div class="ac-asks">${a.listAsk.map(q => `<a href="#/ai-agents?tab=chat&agent=${esc(a.code)}&q=${encodeURIComponent(q)}">${esc(q)}</a>`).join('')}</div>` : ''}
          <div class="row-flex" style="gap:8px">${a.state !== 'off' ? `<a class="btn primary" style="flex:1;justify-content:center" href="#/ai-agents?tab=chat&agent=${esc(a.code)}">Chat với agent</a>` : `<span class="muted small" style="flex:1">Agent đợt ${a.wave} – chưa bật</span>`}<a class="btn" href="#/ai-agents?code=${esc(a.code)}">Chi tiết</a>${canEdit ? `<label class="ai-switch" title="${a.active ? 'Tắt' : 'Bật'} agent"><input type="checkbox" data-code="${esc(a.code)}" ${a.active ? 'checked' : ''}><span>${a.active ? 'Bật' : 'Tắt'}</span></label>` : ''}</div></section>`;
        const sw = box.querySelector('.ai-switch input'); if (sw) sw.onchange = () => toggle(sw.dataset.code, sw.checked, () => APP.refresh());
      };
      b.querySelectorAll('[data-pick]').forEach(x => x.onclick = () => { ov.cur = x.dataset.pick; b.querySelectorAll('[data-pick]').forEach(y => y.classList.toggle('on', y.dataset.pick === ov.cur)); paintCur(); });
      paintCur();
      if (canCost) Store.query('Agent/GetCost').then(c => costPanels(b, c.ok ? c.data : null, agents));
    });
  }

  /** Nhóm "Chi phí AI · tháng" + khung chi tiết (D-138 g) – chỉ người có quyền ai-cost; chưa có lượt nào báo chi phí → "Chưa có dữ liệu chi phí". */
  function costPanels(b, d, agents) {
    const tile = b.querySelector('#acCostTile'), box = b.querySelector('#acCost'); if (!tile || !box) return;
    if (!d) { tile.remove(); return; }
    const mm = d.month.slice(5) + '/' + d.month.slice(0, 4), dayPct = d.daysInMonth ? Math.round(d.daysPassed / d.daysInMonth * 1000) / 10 : 0;
    const pct = d.pct == null ? null : d.pct, change = d.prevTotalVnd > 0 ? Math.round((d.totalVnd - d.prevTotalVnd) / d.prevTotalVnd * 100) : null;
    const none = !d.turns;
    const bar = cls => `<div class="ac-bar ${cls || ''}"><div style="width:${Math.min(100, pct || 0)}%"></div>${d.budget ? `<span title="Mốc ngày ${d.daysPassed}/${d.daysInMonth}" style="left:${Math.min(100, dayPct)}%"></span>` : ''}</div>`;
    tile.innerHTML = `<div class="ac-group-h"><h2>Chi phí AI · tháng ${esc(mm)}</h2><span class="small">Chi tiết ›</span></div>${none ? '<div class="muted small">Chưa có dữ liệu chi phí (Hermes chưa báo token)</div>'
      : `<div class="ac-cost-v"><b>${esc(shortMoney(d.totalVnd))}</b><span>${d.budget ? '/ ' + esc(shortMoney(d.budget)) : 'chưa đặt ngân sách'}</span></div>${d.budget ? bar('amber') : ''}<div class="spread small muted"><span>${pct == null ? U.num(d.turns) + ' lượt' : U.num(pct, 1) + ' % ngân sách'}</span><span>${change == null ? '' : (change >= 0 ? '+' : '') + change + ' % so T' + esc(d.prevMonth.slice(5))}</span></div>`}`;
    const name = c => (agents.find(a => a.code === c) || {}).short || c, color = c => (agents.find(a => a.code === c) || {}).color || '#4F6B1F';
    const max = Math.max(1, ...(d.listAgent || []).map(a => a.costVnd));
    box.innerHTML = `<section class="card ac-costd" id="chi-phi"><div class="spread"><h2>Chi phí Trợ lý AI · tháng ${esc(mm)}</h2><span class="small muted">đến ${esc(String(d.daysPassed).padStart(2, '0'))}/${esc(d.month.slice(5))}</span></div>
      ${none ? '<div class="notice info small">Chưa có dữ liệu chi phí – số liệu có khi Hermes báo token / chi phí mỗi lượt (công cụ bao_chi_phi, AI-13).</div>' : `<div class="ac-cost-v big"><b>${esc(money(d.totalVnd))}</b><span>${d.budget ? '/ ngân sách ' + esc(money(d.budget)) : '· chưa đặt ngân sách (Agent:MonthlyBudget)'}</span></div>${d.budget ? bar() : ''}
      <div class="spread small muted"><span>${pct == null ? 'Chưa đặt ngân sách' : 'Đã dùng ' + U.num(pct, 1) + ' %'}</span><span>Mốc thời gian ${U.num(dayPct, 0)} % tháng</span></div>
      <div class="ac-mini"><div><div class="l">Dự báo cuối tháng</div><b>≈ ${esc(shortMoney(d.forecastVnd))}</b></div><div><div class="l">Tháng ${esc(d.prevMonth.slice(5))}</div><b>${esc(shortMoney(d.prevTotalVnd))}</b></div><div><div class="l">Bình quân</div><b>${d.perTurnVnd == null ? '–' : '≈ ' + esc(money(d.perTurnVnd)) + ' / lượt'}</b></div></div>
      <div class="small muted">Theo agent · ${U.num(d.turns)} lượt</div>${d.listAgent.slice(0, 6).map(a => `<div class="ac-cost-r"><span class="ell">${esc(name(a.agent))}</span><span class="ac-cost-t"><span style="width:${Math.round(a.costVnd / max * 100)}%;background:${esc(color(a.agent))}"></span></span><span class="num nowrap">${esc(money(a.costVnd))}</span></div>`).join('')}`}
      <div class="small muted ac-foot">Tính từ số token trong nhật ký agent × đơn giá model · cảnh báo khi vượt ${U.num(d.alertPct)} % ngân sách · <a href="#/ai-agents?tab=usage">Xem Sử dụng &amp; chi phí</a></div></section>`;
  }

  function toggle(code, active, after) {
    const run = () => Store.act('Agent/SaveIsActive', { form: { code, active } }).then(r => { if (r.ok) UI.toast(esc(r.res && r.res.responseMessage || 'Đã lưu'), 'ok'); cache.agents = null; after(); });
    if (active) return run();
    UI.modal({
      title: `Tắt agent ${esc(code)}?`, body: '<p>Agent sẽ bị từ chối ngay từ lời gọi công cụ tiếp theo (lịch chạy của agent cũng dừng). Nghiệp vụ lõi và job vẫn chạy bình thường.</p>',
      foot: '<button class="btn" data-close>Hủy</button><button class="btn danger" id="aiOff">Tắt agent</button>',
      onMount: (m, close) => { m.querySelector('#aiOff').onclick = () => { close(); run(); }; m.querySelectorAll('[data-close]').forEach(x => x.addEventListener('click', after)); },
    });
  }

  function detail(el, f, code) {
    el.innerHTML = APP.header(Object.assign({}, f || FN['ai-agents'], { name: 'Trung tâm Agent', grp: 'Điều hành' }), '', { noNote: true, title: 'Chi tiết agent', sub: 'Công cụ, thư mục tri thức, lịch chạy và hoạt động gần đây' }) + `<div id="aiD">${loading}</div>`;
    Store.query('Agent/GetById', { code }).then(x => {
      const box = el.querySelector('#aiD'); if (!box) return;
      if (!x.ok || !x.data) { box.innerHTML = `<div class="notice warn">${esc(x.message || 'Không tải được agent')}</div>`; return; }
      const d = x.data, a = d.agent;
      box.innerHTML = `<a href="#/ai-agents?tab=overview" class="small">← Trung tâm Agent</a>
        <div class="card mt"><div class="row-flex" style="gap:14px;align-items:center">${robot(a.color, a.active, 56)}<div style="flex:1;min-width:0"><h2 style="margin:0">${esc(a.name)} <span class="muted">(${esc(a.code)})</span></h2>
          <div class="muted">Đợt ${a.wave} · phục vụ ${esc(a.serves)}</div></div>${chip(a.active ? 'Đang bật' : 'Đang tắt', a.active ? 'green' : '')}</div>
          <div class="grid2 mt"><div><div class="muted small">Mục đích</div><div>${esc(a.purpose)}</div></div><div><div class="muted small">Ghi (luôn có người xác nhận)</div><div>${esc(a.writes)}</div></div></div>
          ${a.active ? `<div class="mt"><a class="btn primary" href="#/ai-agents?tab=chat&agent=${esc(a.code)}">Chat với agent</a></div>` : ''}</div>
        ${UI.kpis([UI.kpi('Lời gọi hôm nay', U.num(a.callsToday), `${U.num(a.okToday)} thành công`, 'chart', 'blue'), UI.kpi('Bị từ chối', U.num(a.deniedToday), 'quyền / tắt / giới hạn', 'shield', a.deniedToday ? 'orange' : 'green'),
          UI.kpi('Công cụ', U.num(d.listTool.length), 'quyền theo màn web tương ứng', 'settings', 'blue'), UI.kpi('Lịch chạy', U.num(d.listSchedule.filter(s => s.enabled).length), 'việc theo giờ đang bật', 'history', 'blue')], 'mt')}
        <div class="card mt"><h3>Công cụ được dùng</h3><div class="table-wrap"><table class="table dc-table"><thead><tr><th>Công cụ</th><th>Làm gì</th><th>Quyền theo màn</th></tr></thead><tbody>
          ${d.listTool.map(t => `<tr><td><code>${esc(t.name)}</code>${t.write ? ' ' + chip('ghi', 'orange') : ''}</td><td class="small">${esc(t.description)}</td><td>${t.screenId ? `<code>${esc(t.screenId)}</code>` : '<span class="muted small">theo người hỏi / dữ liệu chung</span>'}</td></tr>`).join('')}</tbody></table></div></div>
        <div class="card mt"><h3>Thư mục tri thức được đọc</h3><div class="row-flex" style="flex-wrap:wrap;gap:6px">${d.listFolder.map(z => chip(z)).join('')}</div><div class="muted small mt">Không bao giờ đọc BOD Bảo mật; phạm vi lọc cứng ở máy chủ (§7.8) và giao với quyền của người hỏi.</div></div>
        <div class="card mt"><h3>Lịch chạy</h3>${schedTable(d.listSchedule, false)}</div>
        ${Store.perm('ai-agent-log').access !== false ? `<div class="card mt"><div class="spread"><h3>Hoạt động gần đây</h3><a class="small" href="#/ai-agents?tab=log&agent=${esc(a.code)}">Xem nhật ký</a></div>${logTable(d.listActivity, false)}</div>` : ''}`;
    });
  }

  // ================= Chat với agent (D-138 f – thay ngăn chat nổi AI-14) =================
  // Lịch sử hiển thị giữ trong phiên trình duyệt; Hermes dùng chung lịch sử với bot Trợ lý theo người dùng. Hermes tắt → API trả thông báo tiếng Việt.
  const CHAT_KEY = 'dc-agent-chat';
  const chatStore = () => { try { return JSON.parse(sessionStorage.getItem(CHAT_KEY) || '{}'); } catch (e) { return {}; } };
  const chatSave = h => { try { sessionStorage.setItem(CHAT_KEY, JSON.stringify(h)); } catch (e) { /* đầy bộ nhớ – bỏ qua */ } };
  function chat(el, f, p) {
    const b = frame(el, f, 'chat');
    chatAgents().then(list => {
      if (!b.isConnected) return;
      if (!list.length) { b.innerHTML = '<div class="notice warn">Không tải được danh sách agent.</div>'; return; }
      const live = list.filter(a => a.state !== 'off');
      let cur = (list.find(a => a.code === (p.agent || '').toUpperCase() && a.state !== 'off') || live[0] || list[0]).code;
      const draw = () => {
        const a = list.find(z => z.code === cur), h = chatStore()[cur] || [];
        b.innerHTML = `<div class="ac-chat"><aside class="card ac-chat-list"><div class="ac-chat-lh">Agent của tôi</div><div class="ac-chat-ag">${list.map(z => `<button class="ac-ag ${z.code === cur ? 'on' : ''} ${z.state === 'off' ? 'off' : ''}" data-ag="${esc(z.code)}" ${z.state === 'off' ? 'disabled title="Agent chưa bật"' : ''}>${robot(z.color, z.state !== 'off', 34, true)}<span class="ac-row-t"><b class="ell">${esc(z.short)}</b><span class="small muted ell">${esc(z.state === 'off' ? `Đợt ${z.wave} – chưa bật` : z.serves)}</span></span><i class="ac-dot ${esc(z.state)}"></i></button>`).join('')}</div></aside>
          <section class="card ac-chat-main"><div class="ac-chat-h">${robot(a.color, a.state !== 'off', 30, true)}<b class="ell">${esc(a.name)}</b><span class="small muted ell">${esc(a.serves)}</span><span class="ac-chip off" style="margin-left:auto">Ghi qua Phê duyệt</span></div>
            <div class="ac-msgs" id="acMsgs">${h.length ? h.map(m => `<div class="ac-msg ${m.role === 'user' ? 'me' : ''}">${m.role === 'assistant' ? APP.knowledgeMd(m.content, []) : esc(m.content)}${m.role === 'assistant' && !m.err ? `<div class="ac-src"><span>Nguồn: số liệu lõi trong quyền của anh / chị</span>${m.corr ? `<span>${esc(m.corr.slice(0, 8))}</span>` : ''}</div>` : ''}</div>`).join('') : `<div class="muted small ac-empty">Hỏi ${esc(a.short)} về số liệu anh / chị được xem. Câu trả lời ghi nguồn; đề xuất ghi (nếu có) chờ người có quyền xác nhận ở Phê duyệt › Lệnh của agent.</div>`}</div>
            ${a.listAsk.length ? `<div class="ac-asks">${a.listAsk.map(q => `<button type="button" data-q="${esc(q)}">${esc(q)}</button>`).join('')}</div>` : ''}
            <form class="ac-input" id="acForm"><textarea class="input" id="acText" rows="1" maxlength="2000" placeholder="Hỏi ${esc(a.short)}… (Enter gửi, Shift+Enter xuống dòng)" aria-label="Câu hỏi"></textarea><button class="btn primary" type="submit">Gửi</button></form></section>
          <aside class="ac-chat-side"><section class="card"><h3>Agent này làm được</h3>${a.listCan.map(c => `<div class="small">✓ ${esc(c)}</div>`).join('')}<div class="small muted mt">${a.toolCount} chức năng · ${a.scheduleCount} lịch tự động</div></section>
            <section class="card"><h3>Giới hạn</h3><div class="small">Không xem số liệu ngoài quyền của anh / chị (giá vốn, lương…). Lệnh ghi (tạo phiếu, đổi trạng thái) chỉ là đề xuất – người có quyền xác nhận ở Phê duyệt › Lệnh của agent.</div></section>
            <section class="card"><h3>Cũng có trên bot</h3><div class="small">Telegram (bot Trợ lý) · lịch sử chat đồng bộ theo người dùng</div></section></aside></div>`;
        const msgs = b.querySelector('#acMsgs'); msgs.scrollTop = msgs.scrollHeight;
        b.querySelectorAll('[data-ag]').forEach(x => x.onclick = () => { cur = x.dataset.ag; draw(); });
        const ta = b.querySelector('#acText');
        if (p.q && cur === (p.agent || '').toUpperCase()) { ta.value = p.q; p.q = ''; }
        ta.onkeydown = e => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); b.querySelector('#acForm').requestSubmit(); } };
        b.querySelectorAll('[data-q]').forEach(x => x.onclick = () => { ta.value = x.dataset.q; b.querySelector('#acForm').requestSubmit(); });
        b.querySelector('#acForm').onsubmit = e => {
          e.preventDefault();
          const text = ta.value.trim(); if (!text) return;
          const all = chatStore(), hist = all[cur] || [], who = cur;
          hist.push({ role: 'user', content: text }); all[cur] = hist.slice(-40); chatSave(all); draw();
          const wait = document.createElement('div'); wait.className = 'ac-msg muted'; wait.textContent = 'Đang trả lời…'; b.querySelector('#acMsgs').appendChild(wait);
          Store.act('Agent/Chat', { json: { agent: who, message: text, history: hist.slice(-21, -1).map(m => ({ role: m.role, content: m.content })) }, quiet: true }).then(r => {
            const a2 = chatStore(), h2 = a2[who] || [];
            h2.push(r.ok ? { role: 'assistant', content: (r.data || {}).reply || '', corr: (r.data || {}).correlationId } : { role: 'assistant', err: true, content: '⚠ ' + (r.message || 'Trợ lý không phản hồi') });
            a2[who] = h2.slice(-40); chatSave(a2); if (b.isConnected && cur === who) draw();
          });
        };
        if (!('ontouchstart' in window)) ta.focus();
      };
      draw();
    });
  }

  // ================= Giao việc (D-138 d) =================
  const tk = { f: 'all', mine: false };
  const SEV = { HOT: ['Cần xử lý ngay', 'hot'], WARN: ['Cần chú ý', 'warn'] };
  function tasks(el, f) {
    const b = frame(el, f, 'tasks', `<button class="btn" id="acRefresh">${I('refresh')} Làm mới</button>`);
    el.querySelector('#acRefresh').onclick = () => { cache.board = null; APP.refresh(); };
    const canEdit = Store.perm('ai-tasks').edit === true;
    loadBoard(true).then(d => {
      if (!b.isConnected) return;
      if (!d) { b.innerHTML = '<div class="notice warn">Không tải được việc.</div>'; return; }
      const all = d.listTask || [], open = all.filter(t => t.status === 'OPEN');
      const paint = () => {
        const pass = t => (tk.f === 'all' ? t.status === 'OPEN' : tk.f === 'done' ? t.status !== 'OPEN' : t.status === 'OPEN' && SEV[t.severity][1] === tk.f) && (!tk.mine || t.mine);
        const F = [['hot', 'Cần xử lý ngay', open.filter(t => t.severity === 'HOT').length], ['warn', 'Cần chú ý', open.filter(t => t.severity !== 'HOT').length], ['all', 'Đang mở', open.length], ['done', 'Đã xử lý hôm nay', all.length - open.length]];
        const rows = all.filter(pass);
        b.innerHTML = `<div class="ac-filters">${F.map(([k, l, n]) => `<button class="ac-fchip ${k} ${tk.f === k ? 'on' : ''}" data-f="${k}" aria-pressed="${tk.f === k}"><i></i>${esc(l)} <b>${n}</b></button>`).join('')}<span class="grow"></span><label class="small row-flex" style="gap:6px"><input type="checkbox" id="tkMine" ${tk.mine ? 'checked' : ''}> Chỉ việc tôi phụ trách</label></div>
          <div class="ac-tasks"><div class="ac-tlist">${rows.length ? rows.map(t => card(t, canEdit)).join('') : `<div class="card panel empty">${tk.f === 'done' ? 'Chưa có việc nào đóng hôm nay.' : 'Không có việc đang mở trong phạm vi quyền của anh / chị.'}</div>`}</div>
            <aside class="ac-taside">${d.approvalsPending ? `<a class="card ac-apr" href="#/wf-inbox"><b>${U.num(d.approvalsPending)} yêu cầu chờ anh / chị duyệt</b><span class="small">Duyệt ở màn Phê duyệt ›</span></a>` : ''}
              <section class="card"><h3>Theo bộ phận</h3>${(d.listDept || []).length ? d.listDept.map(x => `<div class="spread small ac-dept"><span>${esc(x.dept)}</span><b>${x.count}</b></div>`).join('') : '<div class="small muted">Không có việc mở.</div>'}</section>
              <section class="card"><h3>Cách việc được tạo</h3><div class="small">Agent nhận sự kiện (lô cận hạn, thiếu nguyên liệu, nhiệt độ, phản ánh…) và bước luồng liên phòng bị kẹt, viết việc nên làm, gán phụ trách theo vai trò. <b>Việc tự đóng khi nguồn đã xử lý</b>; bấm "Đã xử lý" để ghi chú lại – ghi chú lưu vào kho tri thức (98 Công việc).</div></section></aside></div>`;
        b.querySelectorAll('[data-f]').forEach(x => x.onclick = () => { tk.f = x.dataset.f; paint(); });
        b.querySelector('#tkMine').onchange = e => { tk.mine = e.target.checked; paint(); };
        b.querySelectorAll('[data-done]').forEach(x => x.onclick = () => UI.confirm('Ghi nhận đã xử lý việc này?', note => Store.act('Agent/CloseTask', { json: { key: x.dataset.done, note } }).then(r => {
          if (r.ok) { UI.toast(esc((r.res && r.res.responseMessage) || 'Đã lưu'), 'ok'); cache.board = null; APP.refresh(); }
        }), { input: 'Ghi chú (đã làm gì – tùy chọn)', ok: 'Đã xử lý' }));
      };
      paint();
    });
  }
  function card(t, canEdit) {
    const s = SEV[t.severity] || SEV.WARN, done = t.status !== 'OPEN';
    return `<article class="card ac-task ${done ? 't-done' : 't-' + s[1]}"><div class="ac-task-h"><i class="ac-sev ${done ? 'ok' : s[1]}"></i><div class="ac-row-t"><div><b>${esc(t.title)}</b> ${done ? `<span class="ac-chip ok">${t.status === 'AUTO' ? 'Tự đóng' : 'Đã xử lý'}</span>` : `<span class="ac-chip ${s[1]}">${esc(s[0])}</span>`}</div>${t.detail ? `<div class="small muted">${esc(t.detail)}</div>` : ''}</div><span class="ac-agent">${esc(t.agent)}</span></div>
      ${!done && t.listTodo.length ? `<div class="ac-todo"><div class="small muted">Việc nên làm (agent đề xuất)</div>${t.listTodo.map(x => `<div>• ${esc(x)}</div>`).join('')}</div>` : ''}
      <div class="ac-meta small">${t.sourceName ? `<span>Nguồn: <b>${esc(t.sourceName)}</b></span>` : ''}${t.owner ? `<span>Phụ trách: <b>${esc(t.owner)}</b></span>` : ''}${t.due && !done ? `<span>Hạn: <b class="${t.severity === 'HOT' ? 'txt-red' : ''}">${esc(t.due)}</b></span>` : ''}${t.closeWhen ? `<span>Tự đóng khi: ${esc(t.closeWhen)}</span>` : ''}
        ${done ? `<span>Đóng lúc ${esc(hm(t.closedAt))}${t.closedBy ? ' · ' + esc(t.closedBy) : ''}${t.note ? ' · ' + esc(t.note) : ''}</span>` : t.openedAt ? `<span>Mở lúc ${esc(hm(t.openedAt))}</span>` : ''}</div>
      ${done ? '' : `<div class="ac-task-a">${t.route ? `<a class="btn sm" href="${esc(t.route)}">Mở chứng từ ↗</a>` : ''}<a class="btn sm" href="#/ai-agents?tab=chat&agent=${esc(t.agent)}&q=${encodeURIComponent(t.title)}">Hỏi agent</a>${canEdit ? `<button class="btn sm primary" data-done="${esc(t.key)}">Đã xử lý</button>` : ''}</div>`}</article>`;
  }

  // ================= Luồng liên phòng (D-138 e) =================
  const FL = { OK: ['Đạt', 'ok'], RUN: ['Đang chạy', 'run'], WARN: ['Cần chú ý', 'warn'], BAD: ['Có vấn đề', 'hot'], WAIT: ['Chờ dữ liệu', 'off'] };
  const fl = { day: null, cur: 1 };
  const DOW = ['Chủ nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'];
  function flow(el, f, p) {
    if (p.date) fl.day = p.date; if (!fl.day) fl.day = TODAY();
    const dd = new Date(fl.day + 'T00:00:00');
    const b = frame(el, f, 'flow', `<div class="ac-date"><button class="btn" data-dd="-1" aria-label="Ngày trước">‹</button><span>${DOW[dd.getDay()]}, ${esc(U.date(fl.day))}</span><button class="btn" data-dd="1" aria-label="Ngày sau">›</button>${fl.day !== TODAY() ? '<button class="btn sm" data-dd="0">Hôm nay</button>' : ''}</div>`);
    el.querySelectorAll('[data-dd]').forEach(x => x.onclick = () => { fl.day = x.dataset.dd === '0' ? TODAY() : U.addDays(fl.day, +x.dataset.dd); APP.refresh(); });
    Store.query('Agent/GetFlowDay', { date: fl.day }).then(x => {
      if (!b.isConnected) return;
      if (!x.ok || !x.data) { b.innerHTML = `<div class="notice warn">${esc(x.message || 'Không tải được luồng')}</div>`; return; }
      const d = x.data, steps = d.listStep || [];
      const paint = () => {
        const s = steps.find(z => z.no === fl.cur) || steps[0], st = FL[s.status] || FL.WAIT;
        b.innerHTML = `<div class="ac-legend">${['OK', 'RUN', 'WARN', 'BAD', 'WAIT'].map(k => `<span><i class="ac-sev ${FL[k][1]}"></i>${esc(FL[k][0])} <b>${steps.filter(z => z.status === k).length}</b></span>`).join('')}<span class="grow"></span><span class="small muted">Cập nhật ${esc(hm(d.generatedAt))}</span></div>
          <div class="ac-flow"><div class="ac-steps">${steps.map(z => `<button class="ac-step ${z.no === fl.cur ? 'on' : ''} ${(FL[z.status] || FL.WAIT)[1]}" data-step="${z.no}"><span class="spread"><span class="small muted">Bước ${z.no}</span><span class="ac-chip ${(FL[z.status] || FL.WAIT)[1]}">${esc((FL[z.status] || FL.WAIT)[0])}</span></span><b>${esc(z.name)}</b><span class="small muted ell">${esc(z.dept)} · agent ${esc(z.agentName)}</span><span class="ac-step-n ${(FL[z.status] || FL.WAIT)[1]}">${esc(z.note)}</span></button>`).join('')}</div>
            <aside class="card ac-fdet"><div class="spread" style="align-items:flex-start"><div><div class="small muted">Bước ${s.no} · ${esc(s.dept)}</div><b style="font-size:17px">${esc(s.name)}</b></div><span class="ac-chip ${st[1]}">${esc(st[0])}</span></div>
              <div class="ac-step-n ${st[1]}">${esc(s.note)}</div><div class="small muted">Chuỗi xử lý</div>
              <div class="ac-chain">${s.listChain.map(c => `<div><span class="small muted nowrap">${esc(c.at ? hm(c.at) : '—')}</span><i class="ac-sev ${c.status === 'OK' ? 'ok' : c.status === 'FAIL' ? 'hot' : c.status === 'WAIT' ? 'warn' : 'run'}"></i><span><b>${esc(c.kind)}</b> · <span class="small">${esc(c.text)}</span></span></div>`).join('')}</div>
              <div class="row-flex" style="gap:8px;flex-wrap:wrap">${s.canOpen ? `<a class="btn" href="#/${esc(s.screen)}">Mở màn ${esc(s.screenName)} ↗</a>` : ''}<a class="btn" href="#/ai-agents?tab=chat&agent=${esc(s.agent)}&q=${encodeURIComponent('Bước ' + s.name + ' ngày ' + U.date(fl.day) + ' đang thế nào?')}">Hỏi agent ${esc(s.agentName)}</a>${(s.status === 'BAD' || s.status === 'WARN') && Store.perm('ai-tasks').access !== false ? '<a class="btn primary" href="#/ai-agents?tab=tasks">Xem việc đã sinh</a>' : ''}</div></aside></div>`;
        b.querySelectorAll('[data-step]').forEach(z => z.onclick = () => { fl.cur = +z.dataset.step; paint(); if (window.innerWidth < 900) { const det = b.querySelector('.ac-fdet'); if (det) det.scrollIntoView({ behavior: 'smooth', block: 'start' }); } });
      };
      paint();
    });
  }

  // ================= Nhật ký agent =================
  function logTable(rows, showAgent = true) {
    if (!rows || !rows.length) return '<div class="muted small">Chưa có lời gọi nào trong khoảng này.</div>';
    return `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Thời gian</th>${showAgent ? '<th>Agent</th>' : ''}<th>Công cụ</th><th>Người hỏi</th><th>Kết quả</th><th class="num">ms</th><th class="num">Dòng</th><th>Ghi chú</th></tr></thead><tbody>
      ${rows.map(r => { const s = STATUS[r.status] || [r.status || '', '']; return `<tr><td class="nowrap">${esc(when(r.at))}</td>${showAgent ? `<td class="nowrap">${esc(r.agent)}</td>` : ''}<td><code>${esc(r.tool || '')}</code></td>
        <td class="small">${esc(r.askingUserName || (r.onBehalfOf ? 'Telegram ' + r.onBehalfOf : 'theo lịch / không rõ'))}</td><td>${chip(s[0] + (r.httpStatus && r.httpStatus !== 200 ? ' · ' + r.httpStatus : ''), s[1])}</td>
        <td class="num">${r.ms != null ? U.num(r.ms) : ''}</td><td class="num">${r.rows != null ? U.num(r.rows) : ''}</td><td class="small">${esc(r.error || '')}${r.correlationId && /^sched:/.test(r.correlationId) ? ` <span class="muted">(${esc(r.correlationId.split(':')[1])})</span>` : ''}</td></tr>`; }).join('')}</tbody></table></div>`;
  }
  function log(el, f, p) {
    const st = { agent: p.agent || '', status: p.status || '', tool: '', from: U.addDays(TODAY(), -6), to: TODAY(), page: 1, rows: [] };
    const b = frame(el, f, 'log');
    b.innerHTML = `<div class="card"><div class="row-flex" style="gap:8px;flex-wrap:wrap;align-items:flex-end">
          <label class="field" style="min-width:140px"><span>Agent</span><select class="input" id="lgA"><option value="">Mọi agent</option>${['VIVI', 'WH', 'KITCHEN', 'QA', 'KNOW', 'SALES', 'PLAN', 'PROC', 'LOG', 'PKG', 'HR', 'ACC', 'CSKH'].map(c => `<option ${c === st.agent ? 'selected' : ''}>${c}</option>`).join('')}</select></label>
          <label class="field" style="min-width:140px"><span>Kết quả</span><select class="input" id="lgS"><option value="">Mọi kết quả</option>${Object.entries(STATUS).map(([k, v]) => `<option value="${k}" ${k === st.status ? 'selected' : ''}>${v[0]}</option>`).join('')}</select></label>
          <label class="field"><span>Công cụ</span><input class="input" id="lgT" placeholder="vd get_ton_kho" style="max-width:170px"></label>
          <label class="field"><span>Từ ngày</span><input type="date" class="input" id="lgF" value="${esc(st.from)}"></label>
          <label class="field"><span>Đến ngày</span><input type="date" class="input" id="lgTo" value="${esc(st.to)}"></label>
          <button class="btn primary" id="lgGo">${I('search')} Lọc</button></div></div>
        <div id="lgK" class="mt"></div><div class="card mt" id="lgB">${loading}</div>`;
    const load = more => {
      if (!more) { st.page = 1; st.rows = []; }
      Store.query('Agent/GetActivity', { agent: st.agent, status: st.status, tool: st.tool, from: st.from, to: st.to, page: st.page, pageSize: 50 }).then(x => {
        const box = b.querySelector('#lgB'); if (!box) return;
        if (!x.ok || !x.data) { box.innerHTML = `<div class="notice warn">${esc(x.message || 'Không tải được nhật ký')}</div>`; return; }
        const d = x.data; st.rows = st.rows.concat(d.listRow || []);
        b.querySelector('#lgK').innerHTML = UI.kpis([UI.kpi('Lời gọi', U.num(d.total), 'trong khoảng đã lọc', 'chart', 'blue'), UI.kpi('Thành công', U.num(d.ok), '', 'check', 'green'),
          UI.kpi('Bị từ chối', U.num(d.denied), 'quyền / khóa / tắt / giới hạn', 'shield', d.denied ? 'orange' : 'green'), UI.kpi('Lỗi', U.num(d.failed), '', 'alert', d.failed ? 'red' : 'green')]);
        box.innerHTML = logTable(st.rows) + (st.rows.length < d.total ? `<div class="mt"><button class="btn sm" id="lgMore">Xem thêm (${U.num(d.total - st.rows.length)})</button></div>` : '');
        const m = box.querySelector('#lgMore'); if (m) m.onclick = () => { st.page++; load(true); };
      });
    };
    b.querySelector('#lgGo').onclick = () => {
      st.agent = b.querySelector('#lgA').value; st.status = b.querySelector('#lgS').value; st.tool = b.querySelector('#lgT').value.trim();
      st.from = b.querySelector('#lgF').value; st.to = b.querySelector('#lgTo').value; load(false);
    };
    load(false);
  }

  // ================= Lịch chạy =================
  function schedTable(rows, showAgent = true) {
    if (!rows || !rows.length) return '<div class="muted small">Chưa có lịch.</div>';
    return `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Giờ</th>${showAgent ? '<th>Agent</th>' : ''}<th>Việc</th><th>Công cụ</th><th>Job lõi</th><th>Trạng thái</th><th>Lần chạy gần nhất</th></tr></thead><tbody>
      ${rows.map(s => `<tr class="${s.enabled && s.agentActive ? '' : 'muted'}"><td class="nowrap"><b>${esc(s.at || s.cron || '')}</b></td>${showAgent ? `<td class="nowrap">${esc(s.agentName || s.agent)}</td>` : ''}
        <td>${esc(s.title || '')}</td><td class="small">${(s.listTool || []).map(t => `<code>${esc(t)}</code>`).join(' ')}</td><td class="small">${esc(s.coreJob ? (UI.JOB_VI && UI.JOB_VI[s.coreJob]) || s.coreJob : '–')}</td>
        <td class="nowrap">${!s.enabled ? chip((s.listTool || []).length ? 'Đang tắt' : `Chờ đợt ${s.wave}`) : s.agentActive ? chip('Đang chạy', 'green') : chip('Agent đang tắt', 'orange')}${showAgent && Store.perm('ai-schedule').edit && (s.listTool || []).length ? ` <label class="ai-switch" title="Bật / tắt việc này"><input type="checkbox" data-sched="${esc(s.id)}" ${s.enabled ? 'checked' : ''}></label>` : ''}</td>
        <td class="small">${s.lastRunAt ? esc(when(s.lastRunAt)) + ' ' + chip((STATUS[s.lastRunStatus] || [s.lastRunStatus || ''])[0], (STATUS[s.lastRunStatus] || ['', ''])[1]) : '<span class="muted">chưa chạy</span>'}</td></tr>`).join('')}</tbody></table></div>`;
  }
  function schedule(el, f) {
    const b = frame(el, f, 'schedule');
    Store.query('Agent/GetSchedules').then(x => {
      if (!b.isConnected) return;
      if (!x.ok) { b.innerHTML = `<div class="notice warn">${esc(x.message || 'Không tải được lịch')}</div>`; return; }
      const list = x.list || [];
      b.innerHTML = `<div class="notice info mb">${I('help')}<div>Job lõi (khóa đơn, MRP, bản tin…) vẫn chạy đúng giờ dù agent tắt; agent chỉ đọc kết quả và báo. Người có quyền Sửa Lịch chạy bật / tắt từng việc bằng công tắc ở cột Trạng thái – ghi lại tệp lịch, Hermes đọc lại.</div></div>`
        + `<div class="card">${list.length ? schedTable(list) : `<div class="muted">${esc((x.res && x.res.responseMessage) || 'Chưa có lịch')}</div>`}</div>`;
      b.querySelectorAll('[data-sched]').forEach(cb => cb.onchange = () => Store.act('Agent/SaveSchedule', { form: { id: cb.dataset.sched, enabled: cb.checked } }).then(r => { if (r.ok) UI.toast(esc((r.res && r.res.responseMessage) || 'Đã lưu'), 'ok'); APP.refresh(); }));
    });
  }

  // ================= Sử dụng & chi phí (D-138 g – CEO / COO / FIN-IT) =================
  const us = { month: null };
  function usage(el, f, p) {
    if (p.month) us.month = p.month; if (!us.month) us.month = TODAY().slice(0, 7);
    const b = frame(el, f, 'usage', `<label class="field" style="margin:0"><span class="sr-only">Tháng</span><input type="month" class="input" id="usM" value="${esc(us.month)}"></label>`);
    el.querySelector('#usM').onchange = e => { if (e.target.value) { us.month = e.target.value; APP.refresh(); } };
    Promise.all([Store.query('Agent/GetCost', { month: us.month }), chatAgents()]).then(([x, agents]) => {
      if (!b.isConnected) return;
      if (!x.ok || !x.data) { b.innerHTML = `<div class="notice warn">${esc(x.message || 'Không tải được chi phí')}</div>`; return; }
      const d = x.data, mm = d.month.slice(5) + '/' + d.month.slice(0, 4), name = c => (agents.find(a => a.code === c) || {}).short || c;
      if (!d.turns) { b.innerHTML = `<div class="card panel empty">${I('chart')}<h2 class="mt">Chưa có dữ liệu chi phí tháng ${esc(mm)}</h2><p class="muted">Số liệu có khi Hermes báo token / chi phí mỗi lượt trả lời (công cụ bao_chi_phi – AI-13). Ngân sách: ${d.budget ? esc(money(d.budget)) : 'chưa đặt (Agent:MonthlyBudget)'}.</p></div>`; return; }
      const color = d.pct == null ? 'blue' : d.pct >= 100 ? 'red' : d.pct >= d.alertPct ? 'orange' : 'green';
      b.innerHTML = UI.kpis([UI.kpi('Đã dùng', shortMoney(d.totalVnd), d.budget ? 'ngân sách ' + money(d.budget) : 'chưa đặt ngân sách', 'chart', color), UI.kpi('% ngân sách', d.pct == null ? '–' : U.num(d.pct, 1) + ' %', `cảnh báo từ ${U.num(d.alertPct)} %`, 'shield', color),
        UI.kpi('Dự báo cuối tháng', shortMoney(d.forecastVnd), 'theo bình quân ngày', 'trend', d.budget && d.forecastVnd > d.budget ? 'red' : 'blue'), UI.kpi('Tháng ' + d.prevMonth.slice(5), shortMoney(d.prevTotalVnd), 'tháng trước', 'history', 'blue'),
        UI.kpi('Bình quân / lượt', d.perTurnVnd == null ? '–' : money(d.perTurnVnd), U.num(d.turns) + ' lượt', 'clock', 'blue'), UI.kpi('Token', U.num((d.tokensIn + d.tokensOut) / 1000, 1) + 'k', 'vào ' + U.num(d.tokensIn / 1000, 1) + 'k · ra ' + U.num(d.tokensOut / 1000, 1) + 'k', 'layers', 'blue')])
        + `<div class="ac-usage mt"><div class="card"><h3>Chi phí theo ngày · tháng ${esc(mm)}</h3>${UI.chart({ type: 'bar', title: 'Chi phí AI theo ngày', unit: 'đ', labels: d.listDay.map(z => String(z.date).slice(8, 10)), fullLabels: d.listDay.map(z => U.date(z.date)), series: [{ name: 'Chi phí (đ)', values: d.listDay.map(z => z.costVnd), role: 'deliver' }], h: 260 })}</div>
          <div class="card"><h3>Theo agent</h3>${UI.chart({ type: 'hbar', title: 'Chi phí theo agent', unit: 'đ', labels: d.listAgent.map(a => name(a.agent)), series: [{ name: 'Chi phí (đ)', values: d.listAgent.map(a => a.costVnd) }], h: Math.max(160, d.listAgent.length * 34) })}</div></div>
        <div class="card mt"><h3>Chi tiết theo agent</h3><div class="table-wrap"><table class="table dc-table"><thead><tr><th>Agent</th><th class="num">Lượt</th><th class="num">Token vào</th><th class="num">Token ra</th><th class="num">Chi phí (đ)</th><th class="num">%</th></tr></thead><tbody>
          ${d.listAgent.map(a => `<tr><td>${esc(name(a.agent))}</td><td class="num">${U.num(a.turns)}</td><td class="num">${U.num(a.tokensIn)}</td><td class="num">${U.num(a.tokensOut)}</td><td class="num">${U.num(Math.round(a.costVnd))}</td><td class="num">${d.totalVnd ? U.num(a.costVnd / d.totalVnd * 100, 1) : '–'}</td></tr>`).join('')}</tbody></table></div></div>`;
    });
  }

  // ================= Kho tri thức =================
  const DOC = { PO: 'scm-po', PR: 'scm-pr', GR: 'scm-gr', PN: 'scm-gr', PX: 'scm-issue', PA: 'log-complaint', ME: 'prod-batch', LO: 'scm-lot', KK: 'scm-stockcount', KH: 'crm-customer', KHB: 'crm-customer', NCC: 'scm-supplier', HD: 'crm-contract', TD: 'menu-menu' };
  const docRx = /\b(PO|PR|GR|PN|PX|PA|ME|LO|KK|KHB|KH|NCC|HD|TD)-[0-9A-Z]{2,}(?:-[0-9A-Z]+)*\b/g;

  /** Markdown → HTML an toàn (thoát trước, dựng sau): tiêu đề, danh sách, bảng, đậm / nghiêng / code, [[liên kết]], mã chứng từ → link. */
  function md(src, notes) {
    const inline = s => esc(s)
      .replace(/`([^`]+)`/g, '<code>$1</code>').replace(/\*\*([^*]+)\*\*/g, '<b>$1</b>').replace(/(^|[^*])\*([^*\s][^*]*)\*/g, '$1<i>$2</i>')
      .replace(/\[\[([^\]|#]+)(?:[#|]([^\]]*))?\]\]/g, (m0, target, label) => {
        const t = target.trim(), n = (notes || []).find(x => x.title === t || x.path === t || x.path === t + '.md' || x.path.endsWith('/' + t + '.md') || x.id === t);
        return n ? `<a href="#/knowledge?path=${encodeURIComponent(n.path)}" class="kn-link">${esc(label || t)}</a>` : `<span class="kn-link missing" title="Chưa có ghi chú này">${esc(label || t)}</span>`;
      })
      .replace(docRx, code => { const scr = DOC[code.split('-')[0]]; return scr ? `<a href="#/${scr}?q=${encodeURIComponent(code)}" class="kn-doc" title="Mở chứng từ">${code}</a>` : code; });
    const lines = String(src || '').replace(/\r/g, '').split('\n'); let out = '', i = 0;
    while (i < lines.length) {
      const l = lines[i];
      if (/^\s*$/.test(l)) { i++; continue; }
      let m = l.match(/^(#{1,4})\s+(.*)$/);
      if (m) { const h = Math.min(6, m[1].length + 1); out += `<h${h}>${inline(m[2])}</h${h}>`; i++; continue; }
      if (/^\|/.test(l)) {
        const rows = []; while (i < lines.length && /^\|/.test(lines[i])) rows.push(lines[i++]);
        const cells = r => r.replace(/^\||\|$/g, '').split('|').map(c => c.trim());
        const body = rows.filter((r, k) => !(k === 1 && /^\|[\s:|-]+\|?$/.test(r)));
        out += `<div class="table-wrap"><table class="table dc-table"><thead><tr>${cells(body[0]).map(c => `<th>${inline(c)}</th>`).join('')}</tr></thead><tbody>${body.slice(1).map(r => `<tr>${cells(r).map(c => `<td>${inline(c)}</td>`).join('')}</tr>`).join('')}</tbody></table></div>`;
        continue;
      }
      if (/^\s*([-*]|\d+\.)\s+/.test(l)) {
        const ordered = /^\s*\d+\./.test(l); const items = [];
        while (i < lines.length && /^\s*([-*]|\d+\.)\s+/.test(lines[i])) items.push(lines[i++].replace(/^\s*([-*]|\d+\.)\s+/, ''));
        out += `<${ordered ? 'ol' : 'ul'}>${items.map(x => `<li>${inline(x)}</li>`).join('')}</${ordered ? 'ol' : 'ul'}>`; continue;
      }
      if (/^>\s?/.test(l)) { const q = []; while (i < lines.length && /^>\s?/.test(lines[i])) q.push(lines[i++].replace(/^>\s?/, '')); out += `<blockquote>${inline(q.join(' '))}</blockquote>`; continue; }
      const para = []; while (i < lines.length && lines[i].trim() && !/^(#{1,4}\s|\||\s*([-*]|\d+\.)\s|>)/.test(lines[i])) para.push(lines[i++]);
      out += `<p>${inline(para.join(' '))}</p>`;
    }
    return out;
  }
  APP.knowledgeMd = md;

  const TYPE = { sop: 'SOP', 'su-co': 'Sự cố', 'bai-hoc': 'Bài học', 'bao-cao': 'Báo cáo', 'quyet-dinh': 'Quyết định', 'ho-so': 'Hồ sơ', 'nhat-ky': 'Nhật ký', 'huong-dan': 'Hướng dẫn',
    // AI-22.2 (D-141): loại tài liệu gốc (tầng Lưu trữ).
    'cong-van-den': 'Công văn đến', 'cong-van-di': 'Công văn đi', 'bien-ban-hop': 'Biên bản họp', 'hop-dong-kh': 'Hợp đồng khách hàng', 'hop-dong-ncc': 'Hợp đồng NCC',
    'hop-dong-ld': 'Hợp đồng lao động', 'ho-so-nv': 'Hồ sơ nhân viên', 'chung-tu': 'Chứng từ', hdsd: 'Hướng dẫn sử dụng', 'tai-lieu': 'Tài liệu' };
  // Loại gợi ý theo vùng (tải file gốc).
  const AREA_TYPES = { '05': ['cong-van-den', 'cong-van-di'], '15': ['bien-ban-hop', 'bao-cao'], '20-HD': ['hop-dong-kh'], '40-HD': ['hop-dong-ncc'], '60-CT': ['chung-tu'], '70-HS': ['ho-so-nv', 'hop-dong-ld'] };
  const SOPT = ['sop', 'huong-dan', 'quyet-dinh']; // AI-12: loại qua Duyệt SOP (khôi phục – mất khi gộp ai.js ở AI-21)
  const fold = s => String(s || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();
  const typeName = t => TYPE[fold(t).replace(/\s+/g, '-')] || t || '';

  APP.register('knowledge', (el, f, p) => {
    p = p || {};
    const st = { tree: [], folder: p.folder || '', path: p.path || '', q: p.q || '' };
    el.innerHTML = APP.header(f || FN.knowledge, `<a class="btn" href="#/knowledge?view=my-tasks">${I('check')} Việc tôi được giao</a>`, { noNote: true }) // AI-22.4
      + `<div class="notice info mb">${I('help')}<div>Kho chứa tri thức: cách làm, quy định, bài học, bản chụp báo cáo có ngày. Số liệu hiện tại luôn xem trên màn nghiệp vụ. Ghi chú tự sinh (lõi xuất) chỉ đọc; SOP / quyết định có hiệu lực sau khi duyệt.</div></div>
        <div class="kn-wrap"><aside class="card kn-side"><div class="row-flex mb" style="gap:6px" data-help="kn-search"><input class="input" id="knQ" placeholder="Tìm trong kho (gõ không dấu được)…" value="${esc(st.q)}"><button class="btn" id="knGo" aria-label="Tìm">${I('search')}</button></div><div id="knTree" data-help="kn-tree">${loading}</div></aside>
        <section class="card kn-main" id="knMain">${loading}</section></div>`;
    const allNotes = () => st.tree.flatMap(x => x.listNote || []);
    const main = el.querySelector('#knMain');
    const tree = () => {
      el.querySelector('#knTree').innerHTML = st.tree.length ? st.tree.map(fd => `<div class="kn-folder ${fd.code === st.folder ? 'open' : ''}">
          <button class="kn-fold" data-folder="${esc(fd.code)}">${I('chevron')} ${esc(kfName(fd.name))} <span class="muted small">${U.num(fd.noteCount)}</span></button>
          ${fd.code === st.folder ? `<div class="kn-notes">${(fd.listNote || []).slice(0, 300).map(n => `<a href="#/knowledge?folder=${esc(fd.code)}&path=${encodeURIComponent(n.path)}" class="kn-note ${n.path === st.path ? 'active' : ''}">${esc(n.title)}${n.readOnly ? ' <span class="muted small">· tự sinh</span>' : ''}</a>`).join('') || '<div class="muted small">Chưa có ghi chú</div>'}${fd.noteCount > 300 ? `<div class="muted small">… và ${U.num(fd.noteCount - 300)} ghi chú khác – dùng ô tìm</div>` : ''}</div>` : ''}</div>`).join('')
        : '<div class="muted small">Anh/chị chưa được cấp thư mục nào của kho tri thức.</div>';
      el.querySelectorAll('.kn-fold').forEach(b => b.onclick = () => { st.folder = st.folder === b.dataset.folder ? '' : b.dataset.folder; tree(); if (!st.path) folderView(); });
    };
    const folderView = () => {
      const fd = st.tree.find(x => x.code === st.folder);
      if (fd && fd.code === '80-HDSD' && APP.helpLibrary) { APP.helpLibrary(main); return; } // AI-22.3: thư viện Hướng dẫn sử dụng
      if (fd && (fd.code === '05' || fd.code === '15') && APP.kbRegister) { APP.kbRegister(main, fd); return; } // AI-22.4: sổ công văn / biên bản họp
      if (!fd) { main.innerHTML = `<h2>Kho tri thức</h2><p class="muted">Chọn thư mục bên trái hoặc tìm theo từ khóa.</p>${UI.kpis(st.tree.slice(0, 4).map(x => UI.kpi(x.name, U.num(x.noteCount), x.owner, 'file', 'blue')))}`; return; }
      main.innerHTML = `<div class="spread"><h2 style="margin:0">${esc(kfName(fd.name))}</h2>${fd.canEdit ? `<div class="row-flex" style="gap:6px">${fd.code !== '80-HDSD' ? `<button class="btn sm" id="knUp" data-help="kn-upload">${I('up')} Tải tài liệu lên</button>` : ''}<button class="btn sm" id="knImp">${I('up')} Nhập Word</button><button class="btn primary sm" id="knNew">${I('plus')} Ghi chú mới</button></div>` : ''}</div><div class="muted small">Chủ thư mục: ${esc(fd.owner)} · ${U.num(fd.noteCount)} ghi chú</div>
        <div class="table-wrap mt"><table class="table dc-table"><thead><tr><th>Ghi chú</th><th>Loại</th><th>Trạng thái</th><th>Hiệu lực từ</th><th>Nguồn</th></tr></thead><tbody>
        ${(fd.listNote || []).slice(0, 200).map(n => `<tr><td><a href="#/knowledge?folder=${esc(fd.code)}&path=${encodeURIComponent(n.path)}">${esc(n.title)}</a>${n.needsReview ? ' ' + chip('cần rà soát', 'orange') : ''}</td><td>${esc(typeName(n.type))}</td><td>${esc(n.status || '')}</td><td>${esc(U.date(n.validFrom))}</td><td class="small">${esc(n.source || '')}</td></tr>`).join('')}</tbody></table></div>`;
      const nb = main.querySelector('#knNew'); if (nb) nb.onclick = () => editor(fd, null);
      // AI-22.2 (D-141): tải file gốc (pdf / docx / xlsx / jpg / png) + phiếu mô tả – tên trên máy chủ do hệ thống đặt.
      const ub = main.querySelector('#knUp'); if (ub) ub.onclick = () => {
        const types = AREA_TYPES[fd.code] || ['tai-lieu', 'bao-cao', 'cong-van-den', 'cong-van-di', 'bien-ban-hop'];
        UI.modal({
          title: 'Tải tài liệu lên – ' + esc(kfName(fd.name)), size: 'md', sub: 'PDF, Word, Excel, ảnh JPG / PNG. Tệp gốc chỉ tải lại được qua nút "Tải file gốc" (cần quyền Xuất). Xóa = chuyển Lưu trữ.',
          body: `<label class="field"><span>Tệp</span><input type="file" class="input" id="kuFile" accept=".pdf,.docx,.xlsx,.jpg,.jpeg,.png"></label>
            <div class="grid2"><label class="field"><span>Loại</span><select class="select" id="kuType">${types.map(t => `<option value="${t}">${esc(TYPE[t] || t)}</option>`).join('')}</select></label>
            <label class="field"><span>Số hiệu</span><input class="input" id="kuNo" placeholder="vd 125/CV-DLC, HĐ-2026-014"></label></div>
            <label class="field"><span>Tên / trích yếu</span><input class="input" id="kuTitle"></label>
            <div class="grid2"><label class="field"><span>Ngày văn bản</span><input type="date" class="input" id="kuDay" value="${TODAY()}"></label>
            <label class="field"><span>Hạn lưu giữ (nhắc, không tự xóa)</span><input type="date" class="input" id="kuKeep"></label></div>
            <label class="field"><span>Mã đối tượng (khách / NCC / nhân viên / chứng từ)</span><input class="input" id="kuObj" placeholder="vd KH00012"></label>
            <label class="field"><span>Mô tả / tóm tắt</span><textarea class="input" id="kuSum" rows="3"></textarea></label>`,
          foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="kuGo">Tải lên</button>',
          onMount: (m, close) => m.querySelector('#kuGo').onclick = () => {
            const file = m.querySelector('#kuFile').files[0], v = id => m.querySelector(id).value.trim();
            if (!file) return UI.toast('Chọn tệp', 'warn');
            if (!v('#kuTitle')) return UI.toast('Nhập tên / trích yếu', 'warn');
            const form = new FormData(); form.append('file', file); form.append('folder', fd.code); form.append('type', v('#kuType')); form.append('title', v('#kuTitle'));
            form.append('soHieu', v('#kuNo')); form.append('ngay', v('#kuDay')); form.append('retainUntil', v('#kuKeep')); form.append('doiTuong', v('#kuObj')); form.append('summary', v('#kuSum'));
            Store.act('Knowledge/UploadFile', { json: form }).then(r => { if (!r.ok) return; close(); UI.toast('Đã tải tài liệu lên', 'ok'); APP.go('knowledge?folder=' + fd.code + '&path=' + encodeURIComponent((r.data || {}).path || '')); });
          },
        });
      };
      // AI-12 (D-133): nhập Word .docx → ghi chú Nháp (PDF chưa hỗ trợ – GAP-KB-PDF).
      const ib = main.querySelector('#knImp'); if (ib) ib.onclick = () => UI.modal({
        title: 'Nhập tài liệu Word – ' + esc(fd.name), size: 'sm', sub: 'Tiêu đề, danh sách, bảng, chữ đậm được giữ. Ghi chú vào ở trạng thái Nháp; SOP sửa xong bấm Gửi duyệt.',
        body: '<label class="field"><span>Tệp (.docx, .md, .txt)</span><input type="file" class="input" id="knFile" accept=".docx,.md,.txt"></label><label class="field"><span>Loại</span><select class="select" id="knType"><option value="sop">SOP</option><option value="huong-dan">Hướng dẫn</option><option value="quyet-dinh">Quyết định</option><option value="ho-so">Hồ sơ</option><option value="bai-hoc">Bài học</option></select></label>',
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="knImpGo">Nhập</button>',
        onMount: (m, close) => m.querySelector('#knImpGo').onclick = () => {
          const file = m.querySelector('#knFile').files[0]; if (!file) return UI.toast('Chọn tệp', 'warn');
          const form = new FormData(); form.append('file', file); form.append('folder', fd.code); form.append('type', m.querySelector('#knType').value);
          Store.act('Knowledge/Import', { json: form }).then(r => { if (!r.ok) return; close(); UI.toast(esc((r.res && r.res.responseMessage) || 'Đã nhập'), 'ok'); APP.go('knowledge?folder=' + fd.code + '&path=' + encodeURIComponent((r.data || {}).path || '')); });
        },
      });
    };
    const noteView = () => Store.query('Knowledge/GetNote', { path: st.path }).then(x => {
      if (!main.isConnected) return;
      if (!x.ok || !x.data) { main.innerHTML = `<div class="notice warn">${esc(x.message || 'Không mở được ghi chú')}</div>`; return; }
      const n = x.data;
      main.innerHTML = `<div class="spread"><div><div class="muted small">${esc(n.path)}</div><h2 style="margin:4px 0">${esc(n.title)}</h2></div>
          <div class="row-flex" style="gap:6px">${n.canEdit && SOPT.includes(n.type) && /^nh[aá]p$/i.test(n.status || '') ? `<button class="btn sm orange" id="knSubmit">Gửi duyệt</button>` : ''}${n.canEdit && SOPT.includes(n.type) && /hi[eệ]u l[uự]c/i.test(n.status || '') ? `<button class="btn sm" id="knVer">${I('plus')} Phiên bản mới</button>` : ''}${n.canEdit && !/ch[oờ] duy[eệ]t/i.test(n.status || '') && !(SOPT.includes(n.type) && /hi[eệ]u l[uự]c/i.test(n.status || '')) ? `<button class="btn sm" id="knEdit">${I('edit')} Sửa</button>` : ''}${n.canDownload ? `<button class="btn sm primary" id="knDl">${I('down')} Tải file gốc</button>` : ''}${n.canArchive ? `<button class="btn sm" id="knArc">Lưu trữ</button>` : ''}${n.fileName && n.canEdit ? `<button class="btn sm" id="knAcc">Nhật ký truy cập</button>` : ''}<button class="btn sm" id="knHist">${I('history')} Lịch sử</button></div></div>
        <div class="row-flex mb" style="gap:6px;flex-wrap:wrap">${chip(typeName(n.type))}${chip(n.status || '', /hi[eệ]u l[uự]c/i.test(n.status || '') ? 'green' : '')}${chip('Hiệu lực từ ' + U.date(n.validFrom))}${n.needsReview ? chip('Quá hạn rà soát ' + U.date(n.reviewBy), 'orange') : chip('Rà soát trước ' + U.date(n.reviewBy))}${chip(n.confidentiality || '')}${n.readOnly ? chip('Tự sinh – chỉ đọc', 'blue') : ''}</div>
        ${n.fileName ? `<div class="notice ${n.canDownload ? 'info' : 'warn'} mb">${I('file')}<div><b>Tệp gốc:</b> ${esc(n.fileName)}${n.meta && n.meta.so_hieu ? ' · số hiệu ' + esc(n.meta.so_hieu) : ''}${n.meta && n.meta.retain_until ? ' · lưu giữ đến ' + esc(U.date(n.meta.retain_until)) : ''}${n.canDownload ? '' : ' – cần quyền Xuất của vùng để tải file gốc'}</div></div>` : ''}
        ${n.refs && n.refs.length ? `<div class="small mb">Liên quan: ${md(n.refs.join(', '), allNotes()).replace(/^<p>|<\/p>$/g, '')}</div>` : ''}
        <article class="kn-body">${md(n.body, allNotes())}</article>
        <div class="kn-back mt"><h4>Liên kết ngược</h4>${(n.listBacklink || []).length ? n.listBacklink.map(b => `<a href="#/knowledge?path=${encodeURIComponent(b.path)}" class="kn-link">${esc(b.title)}</a>`).join(' · ') : '<span class="muted small">Chưa có ghi chú nào liên kết tới đây.</span>'}</div>`;
      if (/^hdsd$/i.test(n.type || '') && APP.helpGuidePage) APP.helpGuidePage(main, n); // AI-22.3: Mở màn / In / Hỏi Vivi + mục lục
      const e = main.querySelector('#knEdit'); if (e) e.onclick = () => editor(st.tree.find(t => t.code === n.folder) || { code: n.folder, name: n.folder }, n);
      // AI-12 (D-133): SOP / hướng dẫn / quyết định – Nháp → Gửi duyệt (Phê duyệt › Duyệt SOP); Hiệu lực → Phiên bản mới (bản cũ giữ tới khi bản mới được duyệt, rồi Lưu trữ).
      const sb = main.querySelector('#knSubmit'); if (sb) sb.onclick = () => UI.confirm('Gửi duyệt <b>' + esc(n.title) + '</b>? Sau khi gửi không sửa được cho tới khi duyệt / từ chối.', note => Store.act('Knowledge/SubmitSop', { json: { path: n.path, prevHash: n.hash, note } }).then(r => { if (r.ok) { UI.toast(esc((r.res && r.res.responseMessage) || 'Đã gửi duyệt'), 'ok'); APP.refresh(); } }), { input: 'Ghi chú cho người duyệt', ok: 'Gửi duyệt' });
      const vb = main.querySelector('#knVer'); if (vb) vb.onclick = () => UI.confirm('Tạo phiên bản mới (Nháp) từ bản đang hiệu lực? Bản hiện tại vẫn dùng cho tới khi bản mới được duyệt.', () => Store.act('Knowledge/NewVersion', { json: { path: n.path } }).then(r => { if (r.ok) { UI.toast('Đã tạo phiên bản mới', 'ok'); APP.go('knowledge?path=' + encodeURIComponent((r.data || {}).path || '')); } }), { ok: 'Tạo phiên bản' });
      // AI-22.2: tải file gốc (qua API, kiểm quyền Xuất – vùng hạn chế ghi nhật ký), chuyển Lưu trữ (thay xóa), xem nhật ký truy cập.
      const db = main.querySelector('#knDl'); if (db) db.onclick = () => Store.download('Knowledge/DownloadFile', { path: n.path });
      const ab = main.querySelector('#knArc'); if (ab) ab.onclick = () => UI.confirm('Chuyển <b>' + esc(n.title) + '</b> sang Lưu trữ? Tài liệu không bị xóa, không còn hiện trong kết quả tìm.', () => Store.act('Knowledge/Archive', { json: { path: n.path } }).then(r => { if (r.ok) { UI.toast('Đã chuyển Lưu trữ', 'ok'); APP.refresh(); } }), { ok: 'Lưu trữ' });
      const lb = main.querySelector('#knAcc'); if (lb) lb.onclick = () => Store.query('Knowledge/GetAccessLog', { path: n.path }).then(h => UI.modal({
        title: 'Nhật ký truy cập', sub: esc(n.title),
        body: (h.list || []).length ? `<table class="table dc-table"><thead><tr><th>Lúc</th><th>Thao tác</th><th>Người</th></tr></thead><tbody>${h.list.map(r => `<tr><td class="nowrap">${esc(when(r.at))}</td><td>${esc(r.action || '')}</td><td class="small mono">${esc(r.by || '')}</td></tr>`).join('')}</tbody></table>` : `<p class="muted">${esc(h.message && !h.ok ? h.message : 'Chưa có lần xem / tải nào (chỉ ghi ở vùng hợp đồng, chứng từ, hồ sơ cá nhân, BOD).')}</p>`,
      }));
      main.querySelector('#knHist').onclick = () => Store.query('Knowledge/GetHistory', { path: n.path }).then(h => UI.modal({
        title: 'Lịch sử ghi chú', sub: esc(n.path),
        body: (h.list || []).length ? `<table class="table dc-table"><thead><tr><th>Lúc</th><th>Thao tác</th><th>Chi tiết</th></tr></thead><tbody>${h.list.map(r => { let c = {}; try { c = JSON.parse(r.changes || '{}'); } catch (err) { /* bỏ qua */ } return `<tr><td class="nowrap">${esc(when(r.at))}</td><td>${esc(r.action || '')}</td><td class="small">${esc(c.actor || '')}${c.note ? ' · ' + esc(c.note) : ''}</td></tr>`; }).join('')}</tbody></table>` : '<p class="muted">Chưa có lần lưu nào qua ứng dụng.</p>',
      }));
    });
    const editor = (fd, n) => {
      const today = TODAY(), next = U.addDays(today, 180);
      const tpl = `---\nid: \ntype: huong-dan\nsource: human\nowner: ${fd.owner || ''}\nstatus: nháp\nvalid_from: ${today}\nreview_by: ${next}\nconfidentiality: nội bộ\nrefs: []\n---\n\n# Tiêu đề\n\n`;
      UI.modal({
        title: n ? 'Sửa ghi chú' : 'Ghi chú mới – ' + esc(fd.name), size: 'lg',
        body: `${n ? '' : `<label class="field"><span>Tên tệp (không dấu cũng được)</span><input class="input" id="knName" placeholder="vd SOP nhận hàng lạnh"></label>`}
          <div class="muted small mb">Frontmatter bắt buộc: id, type (sop / su-co / bai-hoc / bao-cao / quyet-dinh / ho-so / nhat-ky / huong-dan), source: human, owner, status (nháp / chờ duyệt / hiệu lực / lưu trữ), valid_from, review_by, confidentiality, refs. Ngưỡng ghi bằng mã cấu hình, vd <code>{{BusinessRules:PurchaseOrderDirectorApprovalAmount}}</code>. Không ghi số điện thoại / CCCD / số tài khoản.</div>
          <textarea class="input kn-edit" id="knText" rows="20">${esc(n ? n.raw || '' : tpl)}</textarea>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="knSave">Lưu</button>',
        onMount: (m, close) => m.querySelector('#knSave').onclick = () => {
          const name = n ? null : (m.querySelector('#knName').value || '').trim();
          if (!n && !name) return UI.toast('Nhập tên tệp', 'warn');
          const path = n ? n.path : `${fd.name}/${name.replace(/[\\/:*?"<>|#^[\]]/g, '-')}.md`;
          Store.act('Knowledge/Save', { json: { path, content: m.querySelector('#knText').value, prevHash: n ? n.hash : null } }).then(r => {
            if (!r.ok) return;
            UI.toast('Đã lưu ghi chú', 'ok'); close(); location.hash = '#/knowledge?folder=' + encodeURIComponent(fd.code) + '&path=' + encodeURIComponent(path);
          });
        },
      });
    };
    const search = () => {
      st.q = el.querySelector('#knQ').value.trim();
      if (!st.q) return UI.toast('Nhập từ khóa', 'warn');
      Store.query('Knowledge/Search', { keyword: st.q, folder: st.folder || '' }).then(x => {
        if (!x.ok) { main.innerHTML = `<div class="notice warn">${esc(x.message || 'Không tìm được')}</div>`; return; }
        const list = x.list || [];
        main.innerHTML = `<h2>Kết quả cho «${esc(st.q)}»</h2>${list.length ? list.map(h => `<div class="kn-hit"><a href="#/knowledge?folder=${esc(h.folder)}&path=${encodeURIComponent(h.path)}"><b>${esc(h.title)}</b></a> <span class="muted small">${esc(h.path)} · ${esc(typeName(h.type))} · hiệu lực ${esc(U.date(h.validFrom))}</span>${h.needsReview ? ' ' + chip('cần rà soát', 'orange') : ''}<div class="small muted mt">${esc(h.snippet)}</div></div>`).join('') : '<p class="muted">Kho chưa có ghi chú phù hợp.</p>'}`;
      });
    };
    el.querySelector('#knGo').onclick = search;
    el.querySelector('#knQ').onkeydown = e => { if (e.key === 'Enter') search(); };
    Store.query('Knowledge/GetTree').then(x => {
      if (!el.isConnected) return;
      if (!x.ok) { el.querySelector('#knTree').innerHTML = `<div class="notice warn">${esc(x.message || 'Không tải được kho')}</div>`; return; }
      st.tree = x.list || [];
      if (st.path) { const fd = st.tree.find(t => (t.listNote || []).some(n => n.path === st.path)); if (fd) st.folder = fd.code; }
      tree();
      if (p.view === 'my-tasks' && APP.kbMyTasks) APP.kbMyTasks(main); else if (st.path) noteView(); else if (st.q) search(); else folderView();
    });
  });
})();
