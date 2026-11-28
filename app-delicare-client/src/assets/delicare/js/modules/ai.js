/* AI-6 (D-133): nhóm "Trợ lý AI" + "Tri thức" theo mockup docs/frontend-handoff/assets/ai-agent/ (Main, AgentDetail, AgentLog, Schedule, Knowledge).
 * Trung tâm agent (#/ai-agents, chi tiết #/ai-agents?code=WH): thẻ 13 agent (icon img/agents/<MÃ>.svg, trạng thái, công cụ, lời gọi hôm nay), bật / tắt
 * (= BotStatus, có hiệu lực ngay ở ToolGateway – quyền Sửa). Nhật ký agent (#/ai-agent-log): mọi lời gọi công cụ (IntegrationLog kênh HERMES).
 * Lịch chạy (#/ai-schedule): đọc agents/schedules.json (một nguồn cho cron Hermes). Kho tri thức (#/knowledge, #/knowledge?folder=45&path=…): cây thư mục
 * theo quyền, đọc markdown, [[liên kết]], liên kết ngược, mã chứng từ thành link, tìm không dấu, soạn ghi chú (quyền Sửa thư mục, kiểm băm chống đè).
 * API: Agent/GetAll · GetById · SaveIsActive · GetActivity · GetSchedules; Knowledge/GetTree · GetNote · Search · GetHistory · Save. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const FN = APP.FN || {};
  const def = (id, name, grp) => { if (!FN[id]) FN[id] = { extra: true, ph: 'AI', grp, fn: name, id, name, types: ['LS'], table: null, rel: [], st: 'MAPPED', note: '' }; };
  def('ai-agents', 'Trung tâm agent', 'Trợ lý AI'); def('ai-agent-log', 'Nhật ký agent', 'Trợ lý AI'); def('ai-flow', 'Luồng liên phòng', 'Trợ lý AI'); def('ai-tasks', 'Việc agent theo dõi', 'Trợ lý AI'); def('ai-schedule', 'Lịch chạy', 'Trợ lý AI'); def('knowledge', 'Kho tri thức', 'Tri thức');

  const NAV = [['ai-agents', 'Trung tâm agent'], ['ai-agent-log', 'Nhật ký agent'], ['ai-flow', 'Luồng liên phòng'], ['ai-tasks', 'Việc agent theo dõi'], ['ai-schedule', 'Lịch chạy']];
  const nav = cur => `<div class="acc-nav mb">${NAV.filter(([id]) => Store.perm(id).access !== false).map(([id, n]) => `<a href="#/${id}" class="acc-nav-i ${id === cur ? 'active' : ''}">${esc(n)}</a>`).join('')}</div>`;
  const icon = (code, size = 40) => `<img class="ai-ico" src="assets/delicare/img/agents/${esc(code)}.svg" width="${size}" height="${size}" alt="" onerror="this.style.visibility='hidden'">`;
  const STATUS = { Succeeded: ['Thành công', 'green'], Denied: ['Bị từ chối', 'orange'], Failed: ['Lỗi', 'red'] };
  const chip = (txt, color) => `<span class="badge ${color || ''}">${esc(txt)}</span>`;
  const when = d => (d ? U.dt(d) : '–');
  const loading = '<div class="muted small">Đang tải…</div>';
  const TODAY = () => U.day(U.iso(new Date()));

  // ================= Trung tâm agent =================
  APP.register('ai-agents', (el, f, p) => {
    if (p && p.code) return detail(el, f, p.code);
    el.innerHTML = APP.header(f || FN['ai-agents'], '', { noNote: true }) + nav('ai-agents')
      + `<div class="notice info mb">${I('help')}<div><b>Đợt 1 – chỉ đọc.</b> Agent trả lời bằng số liệu lõi (công cụ đọc) và kho tri thức; chưa tạo được đề xuất ghi. Tắt một agent → cổng công cụ từ chối ngay từ lời gọi sau.</div></div>`
      + `<div id="aiKpi">${loading}</div><div id="aiGrid" class="ai-grid mt"></div>`;
    const canEdit = Store.perm('ai-agents').edit !== false;
    const paint = list => {
      const on = list.filter(a => a.active), calls = U.sum(list.map(a => a.callsToday)), denied = U.sum(list.map(a => a.deniedToday)), failed = U.sum(list.map(a => a.failedToday));
      el.querySelector('#aiKpi').innerHTML = UI.kpis([
        UI.kpi('Agent đang bật', `${on.length} / ${list.length}`, `${list.filter(a => a.wave > 1).length} agent chờ đợt 2–4`, 'check', on.length ? 'green' : 'orange'),
        UI.kpi('Lời gọi hôm nay', U.num(calls), 'mọi công cụ, mọi agent', 'chart', 'blue', '#/ai-agent-log'),
        UI.kpi('Bị từ chối quyền', U.num(denied), 'agent ∩ người hỏi không đủ quyền / tắt / vượt giới hạn', 'shield', denied ? 'orange' : 'green', '#/ai-agent-log?status=Denied'),
        UI.kpi('Lỗi', U.num(failed), 'lỗi khi chạy công cụ', 'alert', failed ? 'red' : 'green', '#/ai-agent-log?status=Failed'),
      ]);
      el.querySelector('#aiGrid').innerHTML = list.map(a => `<div class="card ai-card ${a.active ? '' : 'off'}">
          <div class="row-flex" style="gap:10px;align-items:flex-start">${icon(a.code)}<div style="flex:1;min-width:0"><div class="spread"><b>${esc(a.name)}</b>${chip(a.active ? 'Đang bật' : a.status ? 'Đang tắt' : 'Chưa cấu hình', a.active ? 'green' : '')}</div>
            <div class="muted small">${esc(a.code)} · đợt ${a.wave} · phục vụ ${esc(a.serves)}</div></div></div>
          <p class="small mt">${esc(a.purpose)}</p>
          <div class="muted small">${U.num(a.listTool.length)} công cụ · hôm nay ${U.num(a.callsToday)} lời gọi${a.deniedToday ? ` · ${U.num(a.deniedToday)} bị từ chối` : ''}${a.lastCallAt ? ` · gần nhất ${esc(U.time(a.lastCallAt))}` : ''}${a.active && !a.hasKey ? ' · <span class="txt-orange">máy chủ chưa có khóa agent</span>' : ''}</div>
          <div class="spread mt"><a class="btn sm" href="#/ai-agents?code=${esc(a.code)}">Chi tiết</a>${canEdit && a.status ? `<label class="ai-switch" title="${a.active ? 'Tắt' : 'Bật'} agent"><input type="checkbox" data-code="${esc(a.code)}" ${a.active ? 'checked' : ''}><span>${a.active ? 'Bật' : 'Tắt'}</span></label>` : ''}</div>
        </div>`).join('');
      el.querySelectorAll('.ai-switch input').forEach(cb => cb.onchange = () => toggle(cb.dataset.code, cb.checked, load));
    };
    const load = () => Store.query('Agent/GetAll').then(x => {
      if (!el.isConnected) return;
      if (!x.ok) { el.querySelector('#aiKpi').innerHTML = `<div class="notice warn">${esc(x.message || 'Không tải được danh sách agent')}</div>`; return; }
      paint(x.list || []);
    });
    load();
  });

  function toggle(code, active, after) {
    const go = () => Store.act('Agent/SaveIsActive', { form: { code, active } }).then(r => { if (r.ok) UI.toast(esc(r.res && r.res.responseMessage || 'Đã lưu'), 'ok'); after(); });
    if (active) return go();
    UI.modal({
      title: `Tắt agent ${esc(code)}?`, body: '<p>Agent sẽ bị từ chối ngay từ lời gọi công cụ tiếp theo (lịch chạy của agent cũng dừng). Nghiệp vụ lõi và job vẫn chạy bình thường.</p>',
      foot: '<button class="btn" data-close>Hủy</button><button class="btn danger" id="aiOff">Tắt agent</button>',
      onMount: (m, close) => { m.querySelector('#aiOff').onclick = () => { close(); go(); }; m.querySelectorAll('[data-close]').forEach(b => b.addEventListener('click', after)); },
    });
  }

  function detail(el, f, code) {
    el.innerHTML = APP.header(f || FN['ai-agents'], '', { noNote: true }) + nav('ai-agents') + `<div id="aiD">${loading}</div>`;
    Store.query('Agent/GetById', { code }).then(x => {
      const box = el.querySelector('#aiD'); if (!box) return;
      if (!x.ok || !x.data) { box.innerHTML = `<div class="notice warn">${esc(x.message || 'Không tải được agent')}</div>`; return; }
      const d = x.data, a = d.agent;
      box.innerHTML = `<a href="#/ai-agents" class="small">← Trung tâm agent</a>
        <div class="card mt"><div class="row-flex" style="gap:14px;align-items:center">${icon(a.code, 56)}<div style="flex:1"><h2 style="margin:0">${esc(a.name)} <span class="muted">(${esc(a.code)})</span></h2>
          <div class="muted">Đợt ${a.wave} · phục vụ ${esc(a.serves)}</div></div>${chip(a.active ? 'Đang bật' : 'Đang tắt', a.active ? 'green' : '')}</div>
          <div class="grid2 mt"><div><div class="muted small">Mục đích</div><div>${esc(a.purpose)}</div></div><div><div class="muted small">Ghi (từ đợt 2, luôn có người xác nhận)</div><div>${esc(a.writes)}</div></div></div></div>
        ${UI.kpis([UI.kpi('Lời gọi hôm nay', U.num(a.callsToday), `${U.num(a.okToday)} thành công`, 'chart', 'blue'), UI.kpi('Bị từ chối', U.num(a.deniedToday), 'quyền / tắt / giới hạn', 'shield', a.deniedToday ? 'orange' : 'green'),
          UI.kpi('Công cụ', U.num(d.listTool.length), 'quyền theo màn web tương ứng', 'settings', 'blue'), UI.kpi('Lịch chạy', U.num(d.listSchedule.filter(s => s.enabled).length), 'việc theo giờ đang bật', 'history', 'blue')], 'mt')}
        <div class="card mt"><h3>Công cụ được dùng</h3><div class="table-wrap"><table class="table dc-table"><thead><tr><th>Công cụ</th><th>Làm gì</th><th>Quyền theo màn</th></tr></thead><tbody>
          ${d.listTool.map(t => `<tr><td><code>${esc(t.name)}</code>${t.write ? ' ' + chip('ghi', 'orange') : ''}</td><td class="small">${esc(t.description)}</td><td>${t.screenId ? `<code>${esc(t.screenId)}</code>` : '<span class="muted small">theo người hỏi / dữ liệu chung</span>'}</td></tr>`).join('')}</tbody></table></div></div>
        <div class="card mt"><h3>Thư mục tri thức được đọc</h3><div class="row-flex" style="flex-wrap:wrap;gap:6px">${d.listFolder.map(x => chip(x)).join('')}</div><div class="muted small mt">Không bao giờ đọc BOD Bảo mật; phạm vi lọc cứng ở máy chủ (§7.8) và giao với quyền của người hỏi.</div></div>
        <div class="card mt"><h3>Lịch chạy</h3>${schedTable(d.listSchedule, false)}</div>
        <div class="card mt"><div class="spread"><h3>Hoạt động gần đây</h3><a class="small" href="#/ai-agent-log?agent=${esc(a.code)}">Xem nhật ký</a></div>${logTable(d.listActivity, false)}</div>`;
    });
  }

  // ================= Nhật ký agent =================
  function logTable(rows, showAgent = true) {
    if (!rows || !rows.length) return '<div class="muted small">Chưa có lời gọi nào trong khoảng này.</div>';
    return `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Thời gian</th>${showAgent ? '<th>Agent</th>' : ''}<th>Công cụ</th><th>Người hỏi</th><th>Kết quả</th><th class="num">ms</th><th class="num">Dòng</th><th>Ghi chú</th></tr></thead><tbody>
      ${rows.map(r => { const s = STATUS[r.status] || [r.status || '', '']; return `<tr><td class="nowrap">${esc(when(r.at))}</td>${showAgent ? `<td class="nowrap">${icon(r.agent, 20)} ${esc(r.agent)}</td>` : ''}<td><code>${esc(r.tool || '')}</code></td>
        <td class="small">${esc(r.askingUserName || (r.onBehalfOf ? 'Telegram ' + r.onBehalfOf : 'theo lịch / không rõ'))}</td><td>${chip(s[0] + (r.httpStatus && r.httpStatus !== 200 ? ' · ' + r.httpStatus : ''), s[1])}</td>
        <td class="num">${r.ms != null ? U.num(r.ms) : ''}</td><td class="num">${r.rows != null ? U.num(r.rows) : ''}</td><td class="small">${esc(r.error || '')}${r.correlationId && /^sched:/.test(r.correlationId) ? ` <span class="muted">(${esc(r.correlationId.split(':')[1])})</span>` : ''}</td></tr>`; }).join('')}</tbody></table></div>`;
  }

  APP.register('ai-agent-log', (el, f, p) => {
    const st = { agent: (p && p.agent) || '', status: (p && p.status) || '', tool: '', from: U.addDays(TODAY(), -6), to: TODAY(), page: 1, rows: [] };
    el.innerHTML = APP.header(f || FN['ai-agent-log'], '', { noNote: true }) + nav('ai-agent-log')
      + `<div class="card"><div class="row-flex" style="gap:8px;flex-wrap:wrap;align-items:flex-end">
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
        const b = el.querySelector('#lgB'); if (!b) return;
        if (!x.ok || !x.data) { b.innerHTML = `<div class="notice warn">${esc(x.message || 'Không tải được nhật ký')}</div>`; return; }
        const d = x.data; st.rows = st.rows.concat(d.listRow || []);
        el.querySelector('#lgK').innerHTML = UI.kpis([UI.kpi('Lời gọi', U.num(d.total), 'trong khoảng đã lọc', 'chart', 'blue'), UI.kpi('Thành công', U.num(d.ok), '', 'check', 'green'),
          UI.kpi('Bị từ chối', U.num(d.denied), 'quyền / khóa / tắt / giới hạn', 'shield', d.denied ? 'orange' : 'green'), UI.kpi('Lỗi', U.num(d.failed), '', 'alert', d.failed ? 'red' : 'green')]);
        b.innerHTML = logTable(st.rows) + (st.rows.length < d.total ? `<div class="mt"><button class="btn sm" id="lgMore">Xem thêm (${U.num(d.total - st.rows.length)})</button></div>` : '');
        const m = b.querySelector('#lgMore'); if (m) m.onclick = () => { st.page++; load(true); };
      });
    };
    el.querySelector('#lgGo').onclick = () => {
      st.agent = el.querySelector('#lgA').value; st.status = el.querySelector('#lgS').value; st.tool = el.querySelector('#lgT').value.trim();
      st.from = el.querySelector('#lgF').value; st.to = el.querySelector('#lgTo').value; load(false);
    };
    load(false);
  });

  // ================= Lịch chạy =================
  function schedTable(rows, showAgent = true) {
    if (!rows || !rows.length) return '<div class="muted small">Chưa có lịch.</div>';
    return `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Giờ</th>${showAgent ? '<th>Agent</th>' : ''}<th>Việc</th><th>Công cụ</th><th>Job lõi</th><th>Trạng thái</th><th>Lần chạy gần nhất</th></tr></thead><tbody>
      ${rows.map(s => `<tr class="${s.enabled && s.agentActive ? '' : 'muted'}"><td class="nowrap"><b>${esc(s.at || s.cron || '')}</b></td>${showAgent ? `<td class="nowrap">${icon(s.agent, 20)} ${esc(s.agentName || s.agent)}</td>` : ''}
        <td>${esc(s.title || '')}</td><td class="small">${(s.listTool || []).map(t => `<code>${esc(t)}</code>`).join(' ')}</td><td class="small">${esc(s.coreJob ? (UI.JOB_VI && UI.JOB_VI[s.coreJob]) || s.coreJob : '–')}</td>
        <td class="nowrap">${!s.enabled ? chip((s.listTool || []).length ? 'Đang tắt' : `Chờ đợt ${s.wave}`) : s.agentActive ? chip('Đang chạy', 'green') : chip('Agent đang tắt', 'orange')}${showAgent && Store.perm('ai-schedule').edit && (s.listTool || []).length ? ` <label class="ai-switch" title="Bật / tắt việc này"><input type="checkbox" data-sched="${esc(s.id)}" ${s.enabled ? 'checked' : ''}></label>` : ''}</td>
        <td class="small">${s.lastRunAt ? esc(when(s.lastRunAt)) + ' ' + chip((STATUS[s.lastRunStatus] || [s.lastRunStatus || ''])[0], (STATUS[s.lastRunStatus] || ['', ''])[1]) : '<span class="muted">chưa chạy</span>'}</td></tr>`).join('')}</tbody></table></div>`;
  }

  APP.register('ai-schedule', (el, f) => {
    el.innerHTML = APP.header(f || FN['ai-schedule'], '', { noNote: true }) + nav('ai-schedule')
      + `<div class="notice info mb">${I('help')}<div>Lịch lấy từ <code>agents/schedules.json</code> – cùng một tệp sinh cron cho Hermes. Job lõi (khóa đơn, MRP, bản tin…) vẫn chạy đúng giờ dù agent tắt; agent chỉ đọc kết quả và báo. Người có quyền Sửa Lịch chạy bật / tắt từng việc bằng công tắc ở cột Trạng thái – ghi lại tệp lịch, Hermes đọc lại.</div></div>
        <div class="card" id="scB">${loading}</div>`;
    Store.query('Agent/GetSchedules').then(x => {
      const b = el.querySelector('#scB'); if (!b) return;
      if (!x.ok) { b.innerHTML = `<div class="notice warn">${esc(x.message || 'Không tải được lịch')}</div>`; return; }
      const list = x.list || [];
      b.innerHTML = list.length ? schedTable(list) : `<div class="muted">${esc((x.res && x.res.responseMessage) || 'Chưa có lịch')}</div>`;
      // AI-11 (D-133): bật / tắt từng việc → agents/schedules.json (Hermes đọc lại khi sinh cron).
      b.querySelectorAll('[data-sched]').forEach(cb => cb.onchange = () => Store.act('Agent/SaveSchedule', { form: { id: cb.dataset.sched, enabled: cb.checked } }).then(r => { if (r.ok) UI.toast(esc((r.res && r.res.responseMessage) || 'Đã lưu'), 'ok'); APP.refresh(); }));
    });
  });

  // ================= AI-11 (D-133): Luồng liên phòng – dựng từ CorrelationId (sự kiện → agent → đề xuất → duyệt → kết quả → tin) =================
  const KIND = { EVENT: ['Sự kiện', 'alert'], CALL: ['Agent đọc', 'eye'], PROPOSAL: ['Đề xuất', 'edit'], DECISION: ['Người duyệt', 'check'], RESULT: ['Kết quả', 'ok'], MESSAGE: ['Tin gửi', 'bell'] };
  const FST = { DONE: ['Xong', 'green'], WAITING: ['Chờ duyệt', 'orange'], FAILED: ['Lỗi / từ chối', 'red'], INFO: ['Đang theo dõi', 'blue'] };
  const SST = { OK: 'green', WAIT: 'orange', FAIL: 'red' };
  const fl = { day: null };
  const SOPT = ['sop', 'huong-dan', 'quyet-dinh'];
  APP.register('ai-flow', (el, f) => {
    if (!fl.day) fl.day = Store.ctx.date || U.iso(new Date());
    const today = U.iso(new Date()), yest = U.addDays(today, -1);
    el.innerHTML = APP.header(f || FN['ai-flow'], `<div class="row-flex"><button class="btn sm ${fl.day === today ? 'primary' : ''}" data-day="${today}">Hôm nay</button><button class="btn sm ${fl.day === yest ? 'primary' : ''}" data-day="${yest}">Hôm qua</button><input type="date" class="input" id="flDay" value="${fl.day}" style="width:auto"></div>`, { noNote: true, sub: 'Mỗi luồng nối các bước cùng mã tương quan: sự kiện → agent nhận xét → đề xuất → người duyệt → kết quả ghi nghiệp vụ → tin bot gửi người phụ trách.' })
      + nav('ai-flow') + `<div id="flB">${loading}</div>`;
    el.querySelectorAll('[data-day]').forEach(b => b.onclick = () => { fl.day = b.dataset.day; APP.refresh(); });
    el.querySelector('#flDay').onchange = e => { if (e.target.value) { fl.day = e.target.value; APP.refresh(); } };
    Store.query('Agent/GetFlows', { date: fl.day }).then(x => {
      const b = el.querySelector('#flB'); if (!b) return;
      if (!x.ok) { b.innerHTML = `<div class="notice warn">${esc(x.message || 'Không tải được luồng')}</div>`; return; }
      const d = x.data || {}, list = d.listFlow || [];
      b.innerHTML = UI.kpis([UI.kpi('Luồng', U.num(d.total || 0), '', 'route', 'blue'), UI.kpi('Xong', U.num(d.done || 0), '', 'ok', 'green'), UI.kpi('Chờ duyệt', U.num(d.waiting || 0), '', 'clock', d.waiting ? 'orange' : 'green'), UI.kpi('Lỗi / từ chối', U.num(d.failed || 0), '', 'alert', d.failed ? 'red' : 'green'), UI.kpi('Tin đã gửi', U.num(d.messages || 0), '', 'bell', 'blue')])
        + (list.length ? list.map(fw => `<div class="card panel mb"><div class="spread"><div><b>${esc(fw.title || '')}</b><div class="small muted">${(fw.agents || []).map(a => icon(a, 18) + ' ' + esc(a)).join(' · ')} · ${esc(when(fw.startedAt))} → ${esc(when(fw.lastAt))} · <code>${esc(fw.correlationId)}</code></div></div>${chip((FST[fw.status] || [fw.status])[0], (FST[fw.status] || ['', ''])[1])}</div>
          <div class="ai-flow-steps">${(fw.steps || []).map((st, i) => `<div class="ai-flow-step ${SST[st.status] || ''}"><div class="small muted">BƯỚC ${i + 1} · ${esc((KIND[st.kind] || [st.kind])[0])}</div><b>${esc(st.title || '')}</b>${st.detail ? `<div class="small">${esc(st.detail)}</div>` : ''}<div class="small muted mt">${st.agent ? icon(st.agent, 16) + ' ' + esc(st.agent) + ' · ' : ''}${esc(when(st.at))}${st.link ? ` · <a href="${esc(st.link)}">Mở</a>` : ''}</div></div>`).join('')}</div></div>`).join('')
          : '<div class="card panel empty">Chưa có luồng nào trong ngày. Luồng xuất hiện khi agent nhận sự kiện, đề xuất ghi hoặc gửi cảnh báo.</div>');
    });
  });

  // ================= AI-12 (D-133): Việc agent theo dõi – thư mục 98 kho tri thức =================
  const tk = { done: false };
  APP.register('ai-tasks', (el, f) => {
    el.innerHTML = APP.header(f || FN['ai-tasks'], `<label class="row-flex small"><input type="checkbox" id="tkDone" ${tk.done ? 'checked' : ''}> Hiện cả việc đã xong</label>`, { noNote: true, sub: 'Việc agent ghi lại để theo dõi tới khi xong (hạn, người phụ trách) – thấy theo quyền thư mục kho. Đề xuất ghi chờ xác nhận ở Phê duyệt › Lệnh của agent.' })
      + nav('ai-tasks') + `<div id="tkB">${loading}</div>`;
    el.querySelector('#tkDone').onchange = e => { tk.done = e.target.checked; APP.refresh(); };
    Store.query('Agent/GetTasks', { includeDone: tk.done }).then(x => {
      const b = el.querySelector('#tkB'); if (!b) return;
      if (!x.ok) { b.innerHTML = `<div class="notice warn">${esc(x.message || 'Không tải được việc')}</div>`; return; }
      const list = x.list || [], open = list.filter(t => t.status !== 'xong'), today = U.iso(new Date());
      const canEdit = Store.perm('ai-tasks').edit;
      b.innerHTML = UI.kpis([UI.kpi('Việc đang mở', U.num(open.length), '', 'clipboard', 'blue'), UI.kpi('Quá hạn', U.num(open.filter(t => t.overdue).length), '', 'alert', open.some(t => t.overdue) ? 'red' : 'green'),
        UI.kpi('Hạn hôm nay', U.num(open.filter(t => t.due && String(t.due).slice(0, 10) === today).length), '', 'clock', 'orange'), UI.kpi('Agent đang theo dõi', U.num(new Set(open.map(t => t.agent)).size), 'sẽ nhắc lại khi tới hạn', 'eye', 'green')])
        + (list.length ? list.map(t => `<div class="card panel mb ${t.status === 'xong' ? 'muted' : ''}"><div class="spread"><div>${icon(t.agent, 22)} <b>${esc(t.title)}</b> ${t.overdue ? chip('Quá hạn', 'red') : t.status === 'xong' ? chip('Xong', 'green') : chip('Đang theo dõi', 'blue')}</div>
            ${canEdit ? `<div class="row-flex" style="gap:6px">${t.status === 'xong' ? `<button class="btn sm" data-reopen="${esc(t.path)}">Mở lại</button>` : `<button class="btn sm" data-give="${esc(t.path)}">Giao lại</button><button class="btn sm primary" data-done="${esc(t.path)}">Xong</button>`}</div>` : ''}</div>
            <div class="small mt" style="white-space:pre-line">${esc((t.content || '').slice(0, 600))}</div>
            <div class="small muted mt">Hạn: ${esc(t.due ? U.date(t.due) : '–')} · người phụ trách: ${esc(t.owner || '–')} · agent ${esc(t.agent)} · <a href="#/knowledge?path=${encodeURIComponent(t.path)}">Mở ghi chú</a></div></div>`).join('')
          : '<div class="card panel empty">Chưa có việc agent theo dõi.</div>');
      const set = (path, body) => Store.act('Agent/SetTask', { json: Object.assign({ path }, body) }).then(r => { if (r.ok) { UI.toast('Đã lưu', 'ok'); APP.refresh(); } });
      b.querySelectorAll('[data-done]').forEach(x => x.onclick = () => set(x.dataset.done, { status: 'xong' }));
      b.querySelectorAll('[data-reopen]').forEach(x => x.onclick = () => set(x.dataset.reopen, { status: 'dang-theo-doi' }));
      b.querySelectorAll('[data-give]').forEach(x => x.onclick = () => UI.confirm('Giao việc này cho ai?', who => set(x.dataset.give, { owner: who, note: 'giao lại' }), { input: 'Người phụ trách mới (tên / vai trò)', required: true, ok: 'Giao lại' }));
    });
  });

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

  const TYPE = { sop: 'SOP', 'su-co': 'Sự cố', 'bai-hoc': 'Bài học', 'bao-cao': 'Báo cáo', 'quyet-dinh': 'Quyết định', 'ho-so': 'Hồ sơ', 'nhat-ky': 'Nhật ký', 'huong-dan': 'Hướng dẫn' };
  const fold = s => String(s || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();
  const typeName = t => TYPE[fold(t).replace(/\s+/g, '-')] || t || '';

  APP.register('knowledge', (el, f, p) => {
    p = p || {};
    const st = { tree: [], folder: p.folder || '', path: p.path || '', q: p.q || '' };
    el.innerHTML = APP.header(f || FN.knowledge, '', { noNote: true })
      + `<div class="notice info mb">${I('help')}<div>Kho chứa tri thức: cách làm, quy định, bài học, bản chụp báo cáo có ngày. Số liệu hiện tại luôn xem trên màn nghiệp vụ. Ghi chú tự sinh (lõi xuất) chỉ đọc; SOP / quyết định có hiệu lực sau khi duyệt.</div></div>
        <div class="kn-wrap"><aside class="card kn-side"><div class="row-flex mb" style="gap:6px"><input class="input" id="knQ" placeholder="Tìm trong kho (gõ không dấu được)…" value="${esc(st.q)}"><button class="btn" id="knGo" aria-label="Tìm">${I('search')}</button></div><div id="knTree">${loading}</div></aside>
        <section class="card kn-main" id="knMain">${loading}</section></div>`;
    const allNotes = () => st.tree.flatMap(x => x.listNote || []);
    const main = el.querySelector('#knMain');
    const tree = () => {
      el.querySelector('#knTree').innerHTML = st.tree.length ? st.tree.map(fd => `<div class="kn-folder ${fd.code === st.folder ? 'open' : ''}">
          <button class="kn-fold" data-folder="${esc(fd.code)}">${I('chevron')} ${esc(fd.name)} <span class="muted small">${U.num(fd.noteCount)}</span></button>
          ${fd.code === st.folder ? `<div class="kn-notes">${(fd.listNote || []).slice(0, 300).map(n => `<a href="#/knowledge?folder=${esc(fd.code)}&path=${encodeURIComponent(n.path)}" class="kn-note ${n.path === st.path ? 'active' : ''}">${esc(n.title)}${n.readOnly ? ' <span class="muted small">· tự sinh</span>' : ''}</a>`).join('') || '<div class="muted small">Chưa có ghi chú</div>'}${fd.noteCount > 300 ? `<div class="muted small">… và ${U.num(fd.noteCount - 300)} ghi chú khác – dùng ô tìm</div>` : ''}</div>` : ''}</div>`).join('')
        : '<div class="muted small">Anh/chị chưa được cấp thư mục nào của kho tri thức.</div>';
      el.querySelectorAll('.kn-fold').forEach(b => b.onclick = () => { st.folder = st.folder === b.dataset.folder ? '' : b.dataset.folder; tree(); if (!st.path) folderView(); });
    };
    const folderView = () => {
      const fd = st.tree.find(x => x.code === st.folder);
      if (!fd) { main.innerHTML = `<h2>Kho tri thức</h2><p class="muted">Chọn thư mục bên trái hoặc tìm theo từ khóa.</p>${UI.kpis(st.tree.slice(0, 4).map(x => UI.kpi(x.name, U.num(x.noteCount), x.owner, 'file', 'blue')))}`; return; }
      main.innerHTML = `<div class="spread"><h2 style="margin:0">${esc(fd.name)}</h2>${fd.canEdit ? `<div class="row-flex" style="gap:6px"><button class="btn sm" id="knImp">${I('up')} Nhập Word</button><button class="btn primary sm" id="knNew">${I('plus')} Ghi chú mới</button></div>` : ''}</div><div class="muted small">Chủ thư mục: ${esc(fd.owner)} · ${U.num(fd.noteCount)} ghi chú</div>
        <div class="table-wrap mt"><table class="table dc-table"><thead><tr><th>Ghi chú</th><th>Loại</th><th>Trạng thái</th><th>Hiệu lực từ</th><th>Nguồn</th></tr></thead><tbody>
        ${(fd.listNote || []).slice(0, 200).map(n => `<tr><td><a href="#/knowledge?folder=${esc(fd.code)}&path=${encodeURIComponent(n.path)}">${esc(n.title)}</a>${n.needsReview ? ' ' + chip('cần rà soát', 'orange') : ''}</td><td>${esc(typeName(n.type))}</td><td>${esc(n.status || '')}</td><td>${esc(U.date(n.validFrom))}</td><td class="small">${esc(n.source || '')}</td></tr>`).join('')}</tbody></table></div>`;
      const nb = main.querySelector('#knNew'); if (nb) nb.onclick = () => editor(fd, null);
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
          <div class="row-flex" style="gap:6px">${n.canEdit && SOPT.includes(n.type) && /^nh[aá]p$/i.test(n.status || '') ? `<button class="btn sm orange" id="knSubmit">Gửi duyệt</button>` : ''}${n.canEdit && SOPT.includes(n.type) && /hi[eệ]u l[uự]c/i.test(n.status || '') ? `<button class="btn sm" id="knVer">${I('plus')} Phiên bản mới</button>` : ''}${n.canEdit && !/ch[oờ] duy[eệ]t/i.test(n.status || '') && !(SOPT.includes(n.type) && /hi[eệ]u l[uự]c/i.test(n.status || '')) ? `<button class="btn sm" id="knEdit">${I('edit')} Sửa</button>` : ''}<button class="btn sm" id="knHist">${I('history')} Lịch sử</button></div></div>
        <div class="row-flex mb" style="gap:6px;flex-wrap:wrap">${chip(typeName(n.type))}${chip(n.status || '', /hi[eệ]u l[uự]c/i.test(n.status || '') ? 'green' : '')}${chip('Hiệu lực từ ' + U.date(n.validFrom))}${n.needsReview ? chip('Quá hạn rà soát ' + U.date(n.reviewBy), 'orange') : chip('Rà soát trước ' + U.date(n.reviewBy))}${chip(n.confidentiality || '')}${n.readOnly ? chip('Tự sinh – chỉ đọc', 'blue') : ''}</div>
        ${n.refs && n.refs.length ? `<div class="small mb">Liên quan: ${md(n.refs.join(', '), allNotes()).replace(/^<p>|<\/p>$/g, '')}</div>` : ''}
        <article class="kn-body">${md(n.body, allNotes())}</article>
        <div class="kn-back mt"><h4>Liên kết ngược</h4>${(n.listBacklink || []).length ? n.listBacklink.map(b => `<a href="#/knowledge?path=${encodeURIComponent(b.path)}" class="kn-link">${esc(b.title)}</a>`).join(' · ') : '<span class="muted small">Chưa có ghi chú nào liên kết tới đây.</span>'}</div>`;
      const e = main.querySelector('#knEdit'); if (e) e.onclick = () => editor(st.tree.find(t => t.code === n.folder) || { code: n.folder, name: n.folder }, n);
      // AI-12 (D-133): SOP / hướng dẫn / quyết định – Nháp → Gửi duyệt (Phê duyệt › Duyệt SOP); Hiệu lực → Phiên bản mới (bản cũ giữ tới khi bản mới được duyệt, rồi Lưu trữ).
      const sb = main.querySelector('#knSubmit'); if (sb) sb.onclick = () => UI.confirm('Gửi duyệt <b>' + esc(n.title) + '</b>? Sau khi gửi không sửa được cho tới khi duyệt / từ chối.', note => Store.act('Knowledge/SubmitSop', { json: { path: n.path, prevHash: n.hash, note } }).then(r => { if (r.ok) { UI.toast(esc((r.res && r.res.responseMessage) || 'Đã gửi duyệt'), 'ok'); APP.refresh(); } }), { input: 'Ghi chú cho người duyệt', ok: 'Gửi duyệt' });
      const vb = main.querySelector('#knVer'); if (vb) vb.onclick = () => UI.confirm('Tạo phiên bản mới (Nháp) từ bản đang hiệu lực? Bản hiện tại vẫn dùng cho tới khi bản mới được duyệt.', () => Store.act('Knowledge/NewVersion', { json: { path: n.path } }).then(r => { if (r.ok) { UI.toast('Đã tạo phiên bản mới', 'ok'); APP.go('knowledge?path=' + encodeURIComponent((r.data || {}).path || '')); } }), { ok: 'Tạo phiên bản' });
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
      if (st.path) noteView(); else if (st.q) search(); else folderView();
    });
  });
})();
