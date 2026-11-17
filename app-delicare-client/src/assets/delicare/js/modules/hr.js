/* P8 – Phân ca (lưới nhân sự × ngày), Hộp phê duyệt tập trung (ApprovalRequest/Step/Decision) */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  // ---------- Phân ca ----------
  const hs = { ou: '', start: null };
  APP.register('hr-shift-assign', (el, f) => {
    const start = hs.start || U.addDays(Store.ctx.date, -3); hs.start = start;
    const days = Array.from({ length: 7 }, (_, i) => U.addDays(start, i));
    const ous = Store.rows('OrganizationUnit').filter(o => Store.by('Employee', 'OrganizationUnitId', o.Id).length);
    const emps = Store.rows('Employee').filter(e => !hs.ou || e.OrganizationUnitId === hs.ou);
    const shifts = Store.rows('Shift');
    const sa = Store.rows('ShiftAssignment');
    const today = sa.filter(a => U.day(a.WorkDate) === Store.ctx.date);
    el.innerHTML = APP.header(f, `<button class="btn" data-w="-7">‹ Tuần trước</button><button class="btn" data-w="7">Tuần sau ›</button>`, { sub: 'Lưới phân ca nhân sự × ngày (ShiftAssignment). Bấm ô để phân/đổi ca. Ràng buộc UNIQUE (EmployeeId, ShiftId, WorkDate) theo SQL.' }) + `
    <div class="kpis">${shifts.map((s, i) => { const a = today.filter(x => x.ShiftId === s.Id); return UI.kpi(esc(s.Name), `${a.filter(x => ['CheckedIn', 'Completed'].includes(x.AssignmentStatus)).length}/${a.length}`, `Hôm nay: đã chấm công / được phân · ${U.time(s.StartTime)}–${U.time(s.EndTime)}`, 'users', ['blue', 'green', 'orange', 'purple'][i]); }).join('')}</div>
    <div class="card panel"><div class="row-flex mb"><select class="select" id="hOu"><option value="">Tất cả đơn vị (${Store.rows('Employee').length} NV)</option>${ous.map(o => `<option value="${o.Id}" ${o.Id === hs.ou ? 'selected' : ''}>${esc(o.Name)}</option>`).join('')}</select><span class="grow"></span>${shifts.map(s => `<span class="scode ${s.Code}">${s.Code}</span> <span class="small muted">${esc(s.Name.split('·')[1] || '')}</span>`).join(' ')}<span class="scode Absent">Vắng</span><span class="scode off">Nghỉ</span></div>
      <div class="table-wrap" style="max-height:560px"><table class="matrix shiftgrid" style="width:100%"><thead><tr><th class="l">Nhân viên</th><th class="l">Chức vụ</th>${days.map(d => `<th>${['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'][new Date(d + 'T00:00').getDay()]} ${U.date(d).slice(0, 5)}${d === Store.ctx.date ? ' ●' : ''}</th>`).join('')}</tr></thead><tbody>${emps.map(e => `<tr><td class="l"><a href="javascript:void 0" data-open="Employee" data-id="${e.Id}">${esc(e.FullName)}</a></td><td class="l small muted">${esc(Store.label('Position', e.PositionId))}</td>${days.map(d => { const a = sa.find(x => x.EmployeeId === e.Id && U.day(x.WorkDate) === d); const s = (a && Store.get('Shift', a.ShiftId)) || { Code: '?' }; const lv = Store.rows('LeaveRequest').find(l => l.EmployeeId === e.Id && l.RequestStatus === 'Approved' && U.day(l.FromDate) <= d && d <= U.day(l.ToDate)); return `<td class="sc" data-e="${e.Id}" data-d="${d}" title="${a ? esc(a.AssignmentStatus) : 'Chưa phân ca'}">${lv ? '<span class="scode off">Phép</span>' : a ? `<span class="scode ${a.AssignmentStatus === 'Absent' ? 'Absent' : s.Code}">${s.Code}</span>${a.AssignmentStatus === 'CheckedIn' ? ' ✓' : ''}` : '<span class="scode off">–</span>'}</td>`; }).join('')}</tr>`).join('')}</tbody></table></div></div>`;
    el.querySelectorAll('[data-w]').forEach(b => b.onclick = () => { hs.start = U.addDays(start, +b.dataset.w); APP.refresh(); });
    el.querySelector('#hOu').onchange = e => { hs.ou = e.target.value; APP.refresh(); };
    el.querySelectorAll('td.sc').forEach(td => td.onclick = e => { if (e.target.closest('a')) return; assign(td.dataset.e, td.dataset.d); });
  });
  function assign(eid, d) {
    const e = Store.get('Employee', eid), a = Store.rows('ShiftAssignment').find(x => x.EmployeeId === eid && U.day(x.WorkDate) === d);
    const shifts = Store.rows('Shift');
    UI.modal({ title: 'Phân ca – ' + esc(e.FullName), sub: U.date(d) + ' · dbo.ShiftAssignment', size: 'sm',
      body: `<div class="grid">${shifts.map(s => `<button class="btn ${a && a.ShiftId === s.Id ? 'primary' : ''}" data-s="${s.Id}"><span class="scode ${s.Code}">${s.Code}</span> ${esc(s.Name)} <span class="small muted">${U.time(s.StartTime)}–${U.time(s.EndTime)}</span></button>`).join('')}${a ? `<button class="btn danger" data-s="">Bỏ phân ca</button>` : ''}</div>${a ? `<div class="small muted mt">Trạng thái hiện tại: ${UI.badge(a.AssignmentStatus, 'ShiftAssignment', 'AssignmentStatus')} · chấm công: ${Store.by('AttendanceEvent', 'ShiftAssignmentId', a.Id).map(x => x.EventType + ' ' + U.time(x.EventAt)).join(', ') || 'chưa có'}</div>` : ''}`,
      onMount: (m, close) => m.querySelectorAll('[data-s]').forEach(b => b.onclick = () => {
        const sid = b.dataset.s;
        if (a && Store.by('AttendanceEvent', 'ShiftAssignmentId', a.Id).length) return UI.toast('Ca đã có chấm công – không thể đổi. Dùng Đơn đổi ca.', 'err');
        // Chế độ API: ShiftAssignment/Assign – phân / đổi / bỏ ca (ShiftId trống = bỏ); backend kiểm tra trùng ca, nghỉ phép, chấm công.
        if (Store.isLive) return Store.act('ShiftAssignment/Assign', { method: 'PUT', form: { EmployeeId: eid, WorkDate: d, ShiftId: sid || null }, tables: ['ShiftAssignment'] }).then(r => { if (!r.ok) return; close(); const w = (r.data && r.data.listWarning) || []; UI.toast('Đã cập nhật phân ca' + (w.length ? '<br><span class="small">' + w.map(esc).join('<br>') + '</span>' : ''), w.length ? 'warn' : 'ok'); APP.refresh(); });
        if (!sid) { Store.remove('ShiftAssignment', a.Id); }
        else if (a) Store.update('ShiftAssignment', a.Id, { ShiftId: sid });
        else { const s = Store.get('Shift', sid); Store.add('ShiftAssignment', { EmployeeId: eid, ShiftId: sid, WorkDate: d, PlannedStart: d + 'T' + U.time(s.StartTime), PlannedEnd: d + 'T' + U.time(s.EndTime), AssignmentStatus: 'Planned', AssignedBy: Store.user.id }); }
        close(); UI.toast('Đã cập nhật phân ca (ShiftAssignment · DEMO)', 'ok'); APP.refresh();
      }) });
  }
  // ---------- Hộp phê duyệt ----------
  const BT = { PURCHASE_REQUEST: ['PurchaseRequest', 'Status', 'Approved', 'Rejected', 'scm-pr'], LEAVE_REQUEST: ['LeaveRequest', 'RequestStatus', 'Approved', 'Rejected', 'hr-leave'], SHIFT_SWAP: ['ShiftSwapRequest', 'RequestStatus', 'Approved', 'Rejected', 'hr-swap'], RECIPE_VERSION: ['Recipe', 'Status', 'Approved', 'Draft', 'menu-recipe'], STOCK_COUNT: ['StockCount', 'Status', 'PendingApproval', 'Draft', 'scm-stockcount'], CONTRACT: ['Contract', 'Status', 'Active', 'Draft', 'crm-contract'], MENU_CHANGE: ['MenuChangeRequest', 'RequestStatus', 'Approved', 'Rejected', 'menu-change-request'] };
  const ws = { tab: 'mine' };
  APP.register('wf-inbox', (el, f) => {
    const all = Store.rows('ApprovalRequest').slice().sort((a, b) => (b.RequestedAt || '').localeCompare(a.RequestedAt || ''));
    const pend = all.filter(a => ['Pending', 'InProgress'].includes(a.RequestStatus));
    const cur = a => Store.by('ApprovalStep', 'ApprovalRequestId', a.Id).find(s => s.DecisionStatus === 'Waiting');
    // Chế độ API: "Chờ tôi duyệt" lấy từ ApprovalRequest/GetMyPending (đúng người duyệt của bước hiện tại), làm mới sau 30 giây.
    if (Store.isLive && (!ws.mineIds || Date.now() - ws.mineAt > 30000) && !ws.mineBusy) {
      ws.mineBusy = true;
      Store.query('ApprovalRequest/GetMyPending').then(x => { ws.mineBusy = false; ws.mineAt = Date.now(); const had = ws.mineIds; ws.mineIds = new Set(x.ok ? x.list.map(p => p.id) : []); if (!had || had.size !== ws.mineIds.size) { if (APP.current === 'wf-inbox') APP.refresh(); } });
    }
    const mine = Store.isLive ? pend.filter(a => ws.mineIds && ws.mineIds.has(a.Id)) : pend.filter(a => { const s = cur(a); return s && (Store.user.group === 'Quản trị hệ thống' || Store.user.group === s.ApproverRoleCode); });
    const list = ws.tab === 'mine' ? mine : ws.tab === 'pending' ? pend : all;
    el.innerHTML = APP.header(f, '', { sub: 'Hộp phê duyệt tập trung cho mọi nghiệp vụ: ApprovalRequest (BusinessType, BusinessId) → ApprovalStep (StepNo, ApproverRoleCode) → ApprovalDecision. Duyệt bước cuối sẽ cập nhật trạng thái bản ghi nghiệp vụ.' }) + `
    <div class="notice warn">${I('alert')}<div><b>GAP-F-08:</b> SQL không có bảng mẫu quy trình (số cấp, vai trò, hạn mức theo BusinessType). Mockup dùng cấu hình cố định: Đề nghị mua 2 cấp (Quản lý kho → Ban giám đốc), các nghiệp vụ khác 1 cấp.</div></div>
    <div class="kpis">${UI.kpi('Chờ tôi duyệt', mine.length, 'Theo vai trò: ' + esc(Store.user.group), 'approve', 'orange')}${UI.kpi('Đang chờ (tất cả)', pend.length, 'RequestStatus = Pending/InProgress', 'clipboard', 'blue')}${UI.kpi('Đã duyệt', all.filter(a => a.RequestStatus === 'Approved').length, 'Approved', 'ok', 'green')}${UI.kpi('Từ chối', all.filter(a => a.RequestStatus === 'Rejected').length, 'Rejected', 'x', 'red')}</div>
    <div class="card panel"><div class="row-flex mb">${[['mine', 'Chờ tôi duyệt'], ['pending', 'Đang chờ'], ['all', 'Tất cả']].map(([k, l]) => `<button class="chip ${ws.tab === k ? 'active' : ''}" data-tab2="${k}">${l}</button>`).join('')}</div><div id="wT"></div></div>`;
    el.querySelectorAll('[data-tab2]').forEach(b => b.onclick = () => { ws.tab = b.dataset.tab2; APP.refresh(); });
    UI.table(el.querySelector('#wT'), { table: 'ApprovalRequest', rows: list, onRow: id => openReq(id),
      cols: [{ k: 'RequestNo', label: 'Số yêu cầu' }, { k: 'BusinessType', label: 'Loại nghiệp vụ' }, { k: 'BusinessId', label: 'Bản ghi nghiệp vụ', render: r => { const m = BT[r.BusinessType]; const row = m && Store.get(m[0], r.BusinessId); return row ? `<a href="javascript:void 0" data-open="${m[0]}" data-id="${row.Id}">${esc(Store.label(m[0], row))}</a><span class="sub">dbo.${m[0]}</span>` : '<span class="muted">–</span>'; } }, { k: 'RequestedBy', label: 'Người yêu cầu', render: r => esc(Store.label('User', r.RequestedBy)) }, { k: 'RequestedAt', label: 'Gửi lúc' }, { k: 'step', label: 'Bước / Vai trò duyệt', col: false, nosort: true, render: r => { const st = Store.by('ApprovalStep', 'ApprovalRequestId', r.Id).sort((a, b) => a.StepNo - b.StepNo); return st.map(s => `<span class="tag ${s.DecisionStatus === 'Approved' ? 'green' : s.DecisionStatus === 'Rejected' ? 'red' : 'orange'}">${s.StepNo}. ${esc(s.ApproverRoleCode)}</span>`).join(' '); } }, { k: 'RequestStatus', label: 'Trạng thái' }],
      actions: r => mine.includes(r) ? `<button class="btn xs primary" data-ap="${r.Id}">Duyệt</button> <button class="btn xs danger" data-rj="${r.Id}">Từ chối</button>` : '',
      after: t => { t.querySelectorAll('[data-ap]').forEach(b => b.onclick = () => decide(b.dataset.ap, true)); t.querySelectorAll('[data-rj]').forEach(b => b.onclick = () => decide(b.dataset.rj, false)); } });
  });
  function openReq(id) {
    const r = Store.get('ApprovalRequest', id), m = BT[r.BusinessType];
    const steps = Store.by('ApprovalStep', 'ApprovalRequestId', id).sort((a, b) => a.StepNo - b.StepNo);
    const biz = m && Store.get(m[0], r.BusinessId);
    UI.detail('ApprovalRequest', id, { screen: 'wf-inbox', noEdit: true,
      top: `${UI.stepper(steps.map(s => ({ l: s.ApproverRoleCode, s: s.DecisionStatus })), steps.findIndex(s => s.DecisionStatus === 'Waiting') < 0 ? steps.length : steps.findIndex(s => s.DecisionStatus === 'Waiting'), steps.some(s => s.DecisionStatus === 'Rejected'))}${biz ? `<div class="card panel mt mb"><div class="eyebrow">Bản ghi nghiệp vụ · dbo.${m[0]}</div><h3>${esc(Store.label(m[0], biz))}</h3>${UI.kvHtml(m[0], biz, Object.keys(biz).filter(k => !Store.AUDIT.has(k) && k !== 'Id' && !k.endsWith('Json')).slice(0, 10))}${m[0] === 'PurchaseRequest' ? `<table class="dt mt"><thead><tr><th>Nguyên liệu</th><th class="num">SL</th></tr></thead><tbody>${Store.by('PurchaseRequestLine', 'PurchaseRequestId', biz.Id).map(l => `<tr><td>${esc(Store.label('Ingredient', l.IngredientId))}</td><td class="num">${U.num(l.Quantity)}</td></tr>`).join('')}</tbody></table>` : ''}<a class="btn sm mt" href="#/${m[4]}">Mở màn hình ${esc(APP.FN[m[4]].name)} →</a></div>` : ''}`,
      tabs: [{ label: 'Quyết định', html: `<div class="timeline">${Store.by('ApprovalDecision', 'ApprovalRequestId', id).map(dc => `<div class="tl ${dc.DecisionCode === 'REJECT' ? 'red' : ''}"><b>${esc(dc.DecisionCode)} · ${esc(Store.label('User', dc.DecidedBy))}</b><small>${U.dt(dc.DecidedAt)} · ${esc(dc.Comment || '')}</small></div>`).join('') || '<div class="empty">Chưa có quyết định</div>'}</div>` }],
      foot: () => ['Pending', 'InProgress'].includes(r.RequestStatus) ? `<button class="btn danger" data-act="rj">Từ chối</button><button class="btn primary" data-act="ap">${I('approve')} Duyệt bước hiện tại</button>` : '',
      onAct: (a, row, close) => { close(); decide(id, a === 'ap'); } });
  }
  function decide(id, ok) {
    const r = Store.get('ApprovalRequest', id); const steps = Store.by('ApprovalStep', 'ApprovalRequestId', id).sort((a, b) => a.StepNo - b.StepNo);
    if (Store.isLive) {
      // ApprovalRequest/Decide: backend kiểm tra người duyệt của bước hiện tại, ghi quyết định và áp kết quả vào bản ghi nghiệp vụ.
      const biz = BT[r.BusinessType];
      return UI.confirm(`${ok ? 'Duyệt' : 'Từ chối'} <b>${esc(r.RequestNo)}</b>`, note => Store.act('ApprovalRequest/Decide', { form: { Id: id, Approve: ok, Note: note }, tables: ['ApprovalRequest', 'ApprovalStep', 'ApprovalDecision', 'ApprovalItem'].concat(biz ? [biz[0]] : []) })
        .then(x => { if (!x.ok) return; ws.mineIds = null; UI.toast(ok ? 'Đã duyệt' : 'Đã từ chối', ok ? 'ok' : 'warn'); APP.refresh(); }), { input: 'Ý kiến phê duyệt', required: !ok, ok: ok ? 'Duyệt' : 'Từ chối', danger: !ok });
    }
    const s = steps.find(x => x.DecisionStatus === 'Waiting'); if (!s) return;
    if (Store.user.group !== 'Quản trị hệ thống' && Store.user.group !== s.ApproverRoleCode) return UI.toast(`Bước ${s.StepNo} cần vai trò "${esc(s.ApproverRoleCode)}" – đổi vai trò bằng avatar góc phải`, 'err');
    UI.confirm(`${ok ? 'Duyệt' : 'Từ chối'} <b>${esc(r.RequestNo)}</b> – bước ${s.StepNo}/${steps.length} (${esc(s.ApproverRoleCode)})`, note => {
      const now = U.iso(new Date());
      Store.update('ApprovalStep', s.Id, { DecisionStatus: ok ? 'Approved' : 'Rejected', DecidedAt: now, DecisionNote: note });
      Store.add('ApprovalDecision', { ApprovalRequestId: id, ApprovalStepId: s.Id, DecisionCode: ok ? 'APPROVE' : 'REJECT', DecidedBy: Store.user.id || (Store.rows('User')[0] || {}).Id, DecidedAt: now, Comment: note });
      const last = s.StepNo === steps[steps.length - 1].StepNo;
      const fin = !ok ? 'Rejected' : last ? 'Approved' : 'InProgress';
      Store.update('ApprovalRequest', id, { RequestStatus: fin, CurrentStepNo: ok && !last ? s.StepNo + 1 : s.StepNo, CompletedAt: fin === 'InProgress' ? null : now });
      Store.by('ApprovalItem', 'ApprovalRequestId', id).forEach(it => Store.update('ApprovalItem', it.Id, { ItemStatus: fin }));
      const m = BT[r.BusinessType]; let msg = '';
      if (m && fin !== 'InProgress') { const patch = { [m[1]]: fin === 'Approved' ? m[2] : m[3] }; if (r.BusinessType === 'STOCK_COUNT' && fin === 'Approved') Object.assign(patch, { ApprovedBy: Store.user.id, ApprovedAt: now }); if (r.BusinessType === 'RECIPE_VERSION' && fin === 'Approved') Object.assign(patch, { ApprovedBy: Store.user.id, ApprovedAt: now }); if (r.BusinessType === 'MENU_CHANGE' && fin === 'Approved') Object.assign(patch, { ApprovedBy: Store.user.id, ApprovedAt: now }); Store.update(m[0], r.BusinessId, patch, 'Theo phê duyệt ' + r.RequestNo); msg = ` · ${m[0]}.${m[1]} → ${patch[m[1]]}`; }
      UI.toast(`${ok ? 'Đã duyệt' : 'Đã từ chối'} bước ${s.StepNo}${fin === 'InProgress' ? ' – chuyển bước ' + (s.StepNo + 1) : ''}${msg}`, ok ? 'ok' : 'warn'); APP.refresh();
    }, { input: 'Ý kiến phê duyệt', required: !ok, ok: ok ? 'Duyệt' : 'Từ chối', danger: !ok });
  }
  // ---------- Xin nghỉ / Đổi ca: tạo đơn kèm phê duyệt ----------
  ['hr-leave', 'hr-swap'].forEach(id => APP.register(id, (el, f) => {
    const t = f.table;
    APP.genericList(el, f, { noTransit: true, noAdd: true, top: `<div class="card panel mb spread"><div><div class="panel-title">${id === 'hr-leave' ? 'Tạo đơn xin nghỉ' : 'Tạo đơn đổi ca'}</div><div class="panel-desc">Đơn sinh kèm ApprovalRequest (BusinessType = ${id === 'hr-leave' ? 'LEAVE_REQUEST' : 'SHIFT_SWAP'}) → duyệt tại Hộp phê duyệt</div></div><button class="btn primary" id="nReq">${I('plus')} Tạo đơn</button></div>` });
    // Chế độ API: LeaveRequest/Create · ShiftSwapRequest/Create (backend sinh số đơn + yêu cầu duyệt), hủy đơn chờ duyệt qua .../Cancel.
    const ctrl = id === 'hr-leave' ? 'LeaveRequest' : 'ShiftSwapRequest', RT = [t, 'ApprovalRequest', 'ApprovalStep', 'ApprovalItem', 'ShiftAssignment'];
    if (Store.isLive) {
      APP.rowExtra(el, tr => { const r = Store.get(t, tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td || r.RequestStatus !== 'Pending') return; td.insertAdjacentHTML('afterbegin', '<button class="btn xs danger" data-cx>Hủy đơn</button> '); td.querySelector('[data-cx]').onclick = () => UI.confirm('Hủy đơn <b>' + esc(r.RequestNo || '') + '</b>?', note => Store.act(ctrl + '/Cancel', { form: { Id: r.Id, Reason: note }, tables: RT }).then(x => { if (x.ok) { UI.toast('Đã hủy đơn', 'ok'); APP.refresh(); } }), { input: 'Lý do', required: true, danger: true, ok: 'Hủy đơn' }); });
      el.querySelector('#nReq').onclick = () => UI.form(t, null, { title: f.name, fields: id === 'hr-leave' ? ['EmployeeId', 'FromDate', 'ToDate', 'LeaveTypeCode', 'Reason'] : ['TargetEmployeeId', 'CurrentShiftAssignmentId', 'ProposedShiftAssignmentId', 'Reason'],
        validate: o => id === 'hr-leave' && o.FromDate && o.ToDate && o.ToDate < o.FromDate ? 'Đến ngày phải ≥ Từ ngày' : null,
        submit: (o, close) => Store.act(ctrl + '/Create', { form: o, tables: RT }).then(x => { if (!x.ok) return; close(); UI.toast('Đã tạo đơn và gửi duyệt', 'ok'); APP.refresh(); }) });
      return;
    }
    el.querySelector('#nReq').onclick = () => UI.form(t, null, { title: f.name, defaults: { RequestNo: (id === 'hr-leave' ? 'NP' : 'DC') + '-2611-' + String(Store.rows(t).length + 1).padStart(3, '0'), RequestStatus: 'Pending', SubmittedAt: U.iso(new Date()) }, fields: id === 'hr-leave' ? ['RequestNo', 'EmployeeId', 'FromDate', 'ToDate', 'LeaveTypeCode', 'Reason'] : ['RequestNo', 'RequesterEmployeeId', 'TargetEmployeeId', 'CurrentShiftAssignmentId', 'ProposedShiftAssignmentId', 'Reason'],
      validate: o => id === 'hr-leave' && o.FromDate && o.ToDate && o.ToDate < o.FromDate ? 'Đến ngày phải ≥ Từ ngày' : null,
      onSave: row => { const ar = APP.submitApproval(id === 'hr-leave' ? 'LEAVE_REQUEST' : 'SHIFT_SWAP', t, row.Id, [[id === 'hr-leave' ? 'Quản lý bếp' : 'Bếp trưởng']], row.RequestNo); Store.update(t, row.Id, { ApprovalRequestId: ar.Id }); APP.refresh(); } });
  }));
})();
