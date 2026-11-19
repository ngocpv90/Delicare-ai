/* UI-4 (D-68) – Màn chi tiết nhân viên cả trang: #/…/adm-employee-360?id=<EmployeeId>.
 * Mở khi bấm dòng ở danh sách nhân viên (adm-employee). Quyền = quyền xem adm-employee (Store.perm alias). Tab theo quyền xem màn hr-*;
 * thông tin cá nhân (ngày sinh, giới tính, địa chỉ) do máy chủ lọc theo quyền Sửa adm-employee (D-68 e). Khung chung: modules/detail360.js. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const ID = 'adm-employee-360';
  APP.FN[ID] = { ph: 'P0', grp: 'Quản trị hệ thống', fn: 'Hồ sơ nhân viên', id: ID, name: 'Hồ sơ nhân viên', types: ['DT'], table: null, rel: [], st: 'MAPPED', note: '' };
  const { pas, failBox, loading, none, can, initials, dmy, pct, table, bindRows, filterBar, statusOpts, run, demoNote, alertBox, bindGo } = APP.D360;
  const live = () => Store.isLive;
  const today = () => Store.ctx.date;
  const LEAVE = { ANNUAL: 'Phép năm', SICK: 'Ốm', UNPAID: 'Không lương', PERSONAL: 'Việc riêng', MATERNITY: 'Thai sản', WEDDING: 'Cưới', FUNERAL: 'Tang' };
  const leaveName = c => LEAVE[c] || c || '';
  const EMP_ST = { Active: ['Đang làm việc', 'green'], Inactive: ['Tạm nghỉ', 'gray'], Resigned: ['Đã nghỉ việc', 'gray'] };
  const tenure = n => (n === null || n === undefined ? '' : n >= 12 ? `${Math.floor(n / 12)} năm${n % 12 ? ' ' + (n % 12) + ' tháng' : ''}` : `${n} tháng`);
  const tm = v => (v ? U.time(v) : '–');
  const emp = d => (d.employee ? (d.employee.FullName !== undefined || d.employee.Code !== undefined ? d.employee : pas(d.employee)) : {});

  // 4 thẻ chỉ số (D-68 b).
  APP.employeeKpis = d => UI.kpi('Ca tháng này', d ? U.num(d.monthShifts || 0) : '–', d ? `${U.num(d.monthWorkedHours || 0, 1)} giờ công` : '', 'calendar', 'blue', can('hr-shift-assign') ? 'hr-shift-assign' : '')
    + UI.kpi('Đúng giờ (tháng)', d ? pct(d.monthOnTimePct) : '–', '', 'check-circle', 'green')
    + UI.kpi('Ngày nghỉ năm nay', d ? U.num(d.yearLeaveDays || 0) : '–', d && d.pendingRequestCount ? `${U.num(d.pendingRequestCount)} đơn chờ duyệt` : '', 'history', 'orange', can('hr-leave') ? 'hr-leave' : '')
    + UI.kpi('Vắng / muộn (tháng)', d ? `${U.num(d.monthAbsent || 0)} / ${U.num(d.monthLate || 0)}` : '–', '', 'alert', d && (d.monthAbsent || d.monthLate) ? 'red' : 'green', can('hr-attendance') ? 'hr-attendance' : '');

  APP.register(ID, (el, f, params) => {
    const id = params && params.id ? String(params.id).toLowerCase() : '';
    if (!id) { el.innerHTML = APP.header(f, '', { noNote: true }) + failBox('Chưa chọn nhân viên – mở từ danh sách nhân viên.'); return; }
    const local = Store.get('Employee', id);
    el.innerHTML = `<div class="page-head"><div><div class="breadcrumb small"><a href="javascript:void 0" data-back>${I('down')} Danh sách nhân viên</a> / <span data-e360code>${esc(local ? local.Code || '' : '')}</span></div></div></div>
      <div data-e360head>${local ? headHtml({ employee: local }, true) : loading}</div>
      <div class="kpis" data-e360kpi>${APP.employeeKpis(null)}</div>
      <div class="tabs c360-tabs" data-e360tabs></div><div data-e360pane></div>`;
    el.querySelector('[data-back]').onclick = () => APP.go('adm-employee');
    const ov = live() ? Store.query('Employee/GetOverview', { id }) : Promise.resolve({ ok: true, data: { employee: local || {}, listAlert: [] } });
    ov.then(x => {
      if (!el.isConnected) return;
      if (!x.ok || !x.data) { el.querySelector('[data-e360head]').innerHTML = failBox(x.message); return; }
      const d = x.data;
      el.querySelector('[data-e360head]').innerHTML = headHtml(d, false);
      el.querySelector('[data-e360code]').textContent = emp(d).Code || '';
      el.querySelector('[data-e360kpi]').innerHTML = APP.employeeKpis(live() ? d : null);
      bindGo(el.querySelector('[data-e360kpi]'));
      bindHead(el, d, id);
      APP.D360.buildTabs(el.querySelector('[data-e360tabs]'), el.querySelector('[data-e360pane]'), TABS, d, id);
    });
  });

  // ---------- Phần đầu (D-68 b) ----------
  function headHtml(d, partial) {
    const e = emp(d), p = Store.perm('adm-employee'), st = EMP_ST[e.Status] || [e.Status, 'gray'];
    const meta = [e.Code, d.positionName, d.organizationUnitName, e.HireDate && `Vào làm ${dmy(e.HireDate)}${d.tenureMonths !== undefined && d.tenureMonths !== null ? ' · ' + tenure(d.tenureMonths) : ''}`].filter(Boolean).map(esc).join(' · ');
    const contact = [e.Phone && `<span>${I('msg')}${esc(e.Phone)}</span>`, e.Email && `<span>${esc(e.Email)}</span>`].filter(Boolean).join('');
    const bot = partial || !live() ? '' : d.botLinked ? UI.tag('Đã liên kết bot', 'green') : UI.tag('Chưa liên kết bot', 'orange');
    const btn = (act, icon, label, ok, primary) => ok ? `<button class="btn ${primary ? 'primary' : ''}" data-h="${act}">${I(icon)} ${label}</button>` : '';
    const more = [p.edit !== false && live() && '<button data-h="tg">Tạo mã liên kết bot</button>', can('hr-leave', 'add') && live() && '<button data-h="leave">Tạo đơn nghỉ thay</button>',
      '<button data-h="print">In hồ sơ</button>', p.edit !== false && e.Status !== 'Resigned' && '<button data-h="resign">Cho nghỉ việc</button>'].filter(Boolean).join('');
    return `<div class="card panel"><div class="c360-head">
      <div class="c360-avatar ind">${esc(initials(e.FullName))}</div>
      <div class="c360-id"><h1>${esc(e.FullName || '')} ${e.Status ? UI.tag(st[0], st[1]) : ''} ${bot}</h1>
        <div class="c360-meta">${meta}</div><div class="c360-meta">${contact}</div></div>
      ${partial ? '' : `<div class="c360-actions">${btn('edit', 'settings', 'Sửa', p.edit !== false)}${btn('assign', 'calendar', 'Phân ca', can('hr-shift-assign', 'add'), true)}${btn('proxy', 'check', 'Chấm công hộ', can('hr-attendance', 'add') && live())}
        <button class="btn" data-h="more" aria-label="Thêm thao tác">⋯</button><div class="c360-more hide" data-hmore>${more}</div></div>`}
    </div></div>`;
  }
  const tgCode = id => Store.act('TelegramLink/CreateCodeForEmployee', { form: { EmployeeId: id } }).then(r => { if (r.ok && !r.demo && APP.showTelegramCode) APP.showTelegramCode(r.data); });
  const leaveFor = id => UI.form('LeaveRequest', null, { title: 'Tạo đơn nghỉ thay nhân viên', fields: ['EmployeeId', 'FromDate', 'ToDate', 'LeaveTypeCode', 'Reason'], defaults: { EmployeeId: id },
    validate: o => o.FromDate && o.ToDate && o.ToDate < o.FromDate ? 'Đến ngày phải ≥ Từ ngày' : null,
    submit: (o, close) => Store.act('LeaveRequest/Create', { form: o, tables: ['LeaveRequest'] }).then(x => { if (!x.ok) return; close(); UI.toast('Đã tạo đơn nghỉ – chờ duyệt', 'ok'); APP.refresh(); }) });
  function bindHead(el, d, id) {
    APP.D360.bindMore(el.querySelector('[data-e360head]'), act => {
      const row = Object.assign({}, emp(d), Store.get('Employee', id) || {});
      if (act === 'edit') UI.form('Employee', row, { title: 'Nhân viên', onSave: () => APP.refresh() });
      else if (act === 'assign') APP.go('hr-shift-assign');
      else if (act === 'proxy') { if (APP.proxyCheck) APP.proxyCheck(id); }
      else if (act === 'tg') tgCode(id);
      else if (act === 'leave') leaveFor(id);
      else if (act === 'print') window.print();
      else if (act === 'resign') UI.confirm(`Cho nhân viên <b>${esc(row.FullName || '')}</b> nghỉ việc? Nhân viên đã nghỉ việc không được phân ca mới.`, () => {
        Store.save('Employee', 'Update', Object.assign({}, row, { Status: 'Resigned' })).then(r => { if (!r.ok) { UI.toast(esc(r.message || 'Không lưu được'), 'err'); return; } UI.toast('Đã cập nhật trạng thái nghỉ việc', 'ok'); APP.refresh(); });
      }, { danger: true, ok: 'Cho nghỉ việc' });
    });
  }

  // ---------- Tab Tổng quan (D-68 d) ----------
  function overview(box, d, id) {
    const e = emp(d);
    const rows = list => `<div class="c360-rows">${list.filter(x => x[1]).map(([l, v]) => `<div class="r"><label>${esc(l)}</label><span>${v}</span></div>`).join('') || none}</div>`;
    const info = [['Họ tên', esc(e.FullName)], ['Mã nhân viên', esc(e.Code || e.EmployeeCode)], ['Chức danh', esc(d.positionName)], ['Đơn vị', esc(d.organizationUnitName)],
      ['Ngày vào làm', e.HireDate ? `${dmy(e.HireDate)}${d.tenureMonths !== null && d.tenureMonths !== undefined ? ' · ' + tenure(d.tenureMonths) : ''}` : ''], ['Điện thoại', esc(e.Phone)], ['Email', esc(e.Email)]]
      .concat(d.canViewPersonal ? [['Ngày sinh', dmy(e.DateOfBirth)], ['Giới tính', esc(e.Gender)], ['Địa chỉ', esc(e.Address)]] : []);
    // Ca 7 ngày tới (ngày nghỉ phép tô màu).
    const leaveDays = new Set((d.listLeaveDate || []).map(x => U.day(x)));
    const shifts = d.listUpcomingShift || [];
    const days = []; for (let i = 0; i <= 7; i++) days.push(U.addDays(today(), i));
    const weekHtml = live() ? `<div class="e360-week">${days.map(dt => {
      const ss = shifts.filter(s => U.day(s.workDate) === dt), off = leaveDays.has(dt);
      return `<div class="e360-day ${off ? 'off' : ''} ${dt === today() ? 'today' : ''}"><b>${esc(U.date(dt).slice(0, 5))}</b>${off ? '<span class="small">Nghỉ phép</span>' : ''}${ss.map(s => `<span class="small"><span class="scode ${esc(s.shiftCode)}">${esc(s.shiftCode || '')}</span> ${tm(s.plannedStart)}–${tm(s.plannedEnd)}${s.checkInAt ? ' ✓' : ''}</span>`).join('') || (off ? '' : '<span class="small muted">–</span>')}</div>`;
    }).join('')}</div>` : demoNote;
    const acc = live() ? rows([['Tài khoản web', d.userName ? `${esc(d.userName)}${d.userIsActive === false ? ' ' + UI.tag('Đã khóa', 'gray') : ''}` : '<span class="muted">Chưa có</span>'],
      ['Nhóm quyền', esc((d.listUserGroupName || []).join(', '))], ['Bot Telegram', d.botLinked ? `${UI.tag('Đã liên kết', 'green')}${d.botUsername ? ' ' + esc(d.botUsername) : ''}${d.botLinkedAt ? ' · ' + dmy(d.botLinkedAt) : ''}` : UI.tag('Chưa liên kết', 'orange')]])
      + (Store.perm('adm-employee').edit !== false ? `<div class="row-flex mt"><button class="btn xs" data-tg>${I('msg')} Tạo mã liên kết bot</button></div>` : '') : demoNote;
    box.innerHTML = `${alertBox(d.listAlert)}
      <div class="c360-grid">
        <div class="card panel"><div class="spread"><h3>Thông tin nhân viên</h3>${Store.perm('adm-employee').edit !== false ? `<button class="btn xs" data-edit>${I('settings')} Sửa</button>` : ''}</div>${rows(info)}${live() && !d.canViewPersonal ? '<div class="small muted mt">Ngày sinh, giới tính, địa chỉ chỉ hiện với người có quyền sửa hồ sơ nhân viên.</div>' : ''}</div>
        <div class="card panel"><div class="spread"><h3>Ca 7 ngày tới</h3>${can('hr-shift-assign') ? `<a href="javascript:void 0" data-go="hr-shift-assign" class="small">Phân ca</a>` : ''}</div>${weekHtml}</div>
        <div class="card panel"><h3>Tài khoản và bot</h3>${acc}</div>
      </div>
      <div class="c360-grid two">
        <div class="card panel"><h3>Giờ công 30 ngày</h3><div data-hours></div></div>
        <div class="card panel"><div class="spread"><h3>Chấm công gần đây</h3>${can('hr-attendance') ? `<a href="javascript:void 0" data-go="hr-attendance" class="small">Bảng công</a>` : ''}</div><div data-att></div></div>
      </div>`;
    bindGo(box);
    const ed = box.querySelector('[data-edit]'); if (ed) ed.onclick = () => UI.form('Employee', Object.assign({}, e, Store.get('Employee', id) || {}), { title: 'Nhân viên', onSave: () => APP.refresh() });
    const tg = box.querySelector('[data-tg]'); if (tg) tg.onclick = () => tgCode(id);
    const hrs = d.listDayHours || [], hBox = box.querySelector('[data-hours]');
    hBox.innerHTML = !live() ? demoNote : hrs.some(h => h.hours) ? `${UI.bars(hrs.map(h => U.date(h.date).slice(0, 5)), [{ name: 'Giờ công', values: hrs.map(h => h.hours || 0), color: '#91AF42' }], { h: 200, title: 'Giờ công 30 ngày' })}<div class="small muted">Tổng ${U.num(U.sum(hrs, 'hours'), 1)} giờ</div>` : none;
    const att = d.listRecentAttendance || [], aBox = box.querySelector('[data-att]');
    aBox.innerHTML = !live() ? demoNote : table(att, [{ label: 'Ngày', html: r => dmy(r.workDate) }, { label: 'Ca', html: r => `<span class="scode ${esc(r.shiftCode)}">${esc(r.shiftCode || '')}</span>` },
      { label: 'Vào', html: r => `${tm(r.checkInAt)}${r.checkInValid === false ? ' <span class="bad">ngoài bán kính</span>' : ''}${r.lateMinutes ? ` <span class="bad">muộn ${r.lateMinutes}′</span>` : ''}` },
      { label: 'Ra', html: r => `${tm(r.checkOutAt)}${r.earlyLeaveMinutes ? ` <span class="bad">sớm ${r.earlyLeaveMinutes}′</span>` : ''}` }, { label: 'Hình thức', html: r => esc(r.checkInSource || r.checkOutSource || '') },
      { label: 'Giờ công', num: true, html: r => U.num((r.workedMinutes || 0) / 60, 1) }]);
  }

  // ---------- Các tab còn lại (D-68 c, UI-4.4) ----------
  // Lịch ca: lưới 1 tuần, ‹ › đổi tuần; bấm ô → màn phân ca.
  function scheduleTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    let start = (() => { const t = new Date(today()); const wd = (t.getDay() + 6) % 7; return U.addDays(today(), -wd); })();
    const draw = () => {
      const end = U.addDays(start, 6);
      box.innerHTML = `<div class="c360-filter"><button class="btn xs" data-w="-7">‹</button><b>${U.date(start)} – ${U.date(end)}</b><button class="btn xs" data-w="7">›</button><button class="btn xs" data-w="0">Tuần này</button></div><div data-grid>${loading}</div>`;
      box.querySelectorAll('[data-w]').forEach(b => b.onclick = () => { const n = +b.dataset.w; start = n ? U.addDays(start, n) : (() => { const t = new Date(today()); return U.addDays(today(), -((t.getDay() + 6) % 7)); })(); draw(); });
      const grid = box.querySelector('[data-grid]');
      Promise.all([Store.query('ShiftAssignment/GetAll', { EmployeeId: id, FromDate: start, ToDate: end, Page: 1, PageSize: 100 }), Store.query('LeaveRequest/GetAll', { EmployeeId: id, RequestStatus: 'Approved', Page: 1, PageSize: 200 })]).then(([x, lv]) => {
        if (!grid.isConnected) return;
        if (!x.ok) { grid.innerHTML = failBox(x.message); return; }
        const rows = x.list || [], leaves = lv.ok ? lv.list || [] : [];
        const off = dt => leaves.some(l => U.day(l.fromDate) <= dt && U.day(l.toDate) >= dt);
        const days = []; for (let i = 0; i < 7; i++) days.push(U.addDays(start, i));
        grid.innerHTML = `<div class="e360-week seven">${days.map(dt => { const ss = rows.filter(r => U.day(r.workDate) === dt); return `<div class="e360-day ${off(dt) ? 'off' : ''} ${dt === today() ? 'today' : ''}" data-day="${dt}"><b>${['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'][days.indexOf(dt)]} ${esc(U.date(dt).slice(0, 5))}</b>${off(dt) ? '<span class="small">Nghỉ phép</span>' : ''}${ss.map(s => `<span class="small"><span class="scode ${esc(s.shiftName || '')}">${esc(s.shiftName || '')}</span> ${tm(s.plannedStart)}–${tm(s.plannedEnd)} · ${esc(UI.statusVi('ShiftAssignment', 'AssignmentStatus', s.assignmentStatus))}</span>`).join('') || '<span class="small muted">–</span>'}</div>`; }).join('')}</div>`
          + (can('hr-shift-assign') ? '<div class="small muted mt">Bấm một ngày để mở màn phân ca.</div>' : '');
        if (can('hr-shift-assign')) grid.querySelectorAll('[data-day]').forEach(c => { c.style.cursor = 'pointer'; c.onclick = () => APP.go('hr-shift-assign'); });
      });
    };
    draw();
  }
  function attendanceTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    filterBar(box, { from: U.addDays(today(), -30), to: today() }, (st, body) => {
      body.innerHTML = loading;
      run(body, Store.query('AttendanceEvent/GetTimesheetDetail', { EmployeeId: id, FromDate: st.from, ToDate: st.to, Page: 1, PageSize: 500 }), x => {
        const rows = (x.list || []).slice().sort((a, b) => String(b.workDate).localeCompare(a.workDate));
        const sum = k => rows.filter(r => r[k]).length;
        body.innerHTML = `<div class="small muted mb">${U.num(rows.length)} ca · có mặt ${U.num(sum('checkInAt'))} · muộn ${U.num(sum('lateMinutes'))} · vắng ${U.num(sum('isAbsent'))} · ${U.num(U.sum(rows, 'workedMinutes') / 60, 1)} giờ công</div>` + table(rows, [
          { label: 'Ngày', html: r => dmy(r.workDate) }, { label: 'Ca', html: r => `<span class="scode ${esc(r.shiftCode)}">${esc(r.shiftCode || '')}</span> ${tm(r.plannedStart)}–${tm(r.plannedEnd)}` },
          { label: 'Vào ca', html: r => `${tm(r.checkInAt)}${r.checkInValid === false ? ' <span class="bad">ngoài bán kính</span>' : ''}${r.lateMinutes ? ` <span class="bad">muộn ${r.lateMinutes}′</span>` : ''}` },
          { label: 'Ra ca', html: r => `${tm(r.checkOutAt)}${r.earlyLeaveMinutes ? ` <span class="bad">sớm ${r.earlyLeaveMinutes}′</span>` : ''}${r.missingCheckOut ? ' <span class="bad">thiếu ra ca</span>' : ''}` },
          { label: 'Hình thức', html: r => esc([r.checkInSource, r.checkOutSource].filter(Boolean).join(' / ')) }, { label: 'Giờ công', num: true, html: r => U.num((r.workedMinutes || 0) / 60, 1) },
          { label: 'Trạng thái', html: r => (r.isAbsent ? UI.tag('Vắng', 'red') : esc(r.statusName || '')) }]);
      });
    });
  }
  function leaveTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    const byType = (d.listLeaveByType || []).map(x => `${esc(leaveName(x.leaveTypeCode))} ${U.num(x.days)} ngày`).join(' · ');
    filterBar(box, { dates: false, statuses: statusOpts('LeaveRequest', 'RequestStatus', ['Pending', 'Approved', 'Rejected', 'Cancelled']), tools: can('hr-leave', 'add') ? `<span class="grow"></span><button class="btn primary" data-newl>${I('plus')} Tạo đơn nghỉ thay</button>` : '' }, (st, body) => {
      body.innerHTML = loading;
      run(body, Store.query('LeaveRequest/GetAll', { EmployeeId: id, RequestStatus: st.status, Page: 1, PageSize: 200, SortField: 'fromDate', SortOrder: 'descend' }), x => {
        const rows = x.list || [];
        body.innerHTML = (byType ? `<div class="small muted mb">Năm nay (đã duyệt): ${byType}</div>` : '') + table(rows, [{ label: 'Số đơn', html: r => `<b>${esc(r.requestNo || '')}</b>` },
          { label: 'Từ – đến', html: r => `${dmy(r.fromDate)} – ${dmy(r.toDate)}` }, { label: 'Loại', html: r => esc(leaveName(r.leaveTypeCode)) }, { label: 'Lý do', html: r => esc(r.reason || '') },
          { label: 'Gửi lúc', html: r => esc(U.dt(r.submittedAt)) }, { label: 'Trạng thái', html: r => UI.badge(r.requestStatus, 'LeaveRequest', 'RequestStatus') }], true);
        bindRows(body, rows, r => APP.openHrRequest && APP.openHrRequest(true, r.id));
      });
    });
    const nb = box.querySelector('[data-newl]'); if (nb) nb.onclick = () => leaveFor(id);
  }
  function swapTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    filterBar(box, { dates: false, statuses: statusOpts('ShiftSwapRequest', 'RequestStatus', ['Pending', 'Approved', 'Rejected', 'Cancelled']) }, (st, body) => {
      body.innerHTML = loading;
      const q = p => Store.query('ShiftSwapRequest/GetAll', Object.assign({ RequestStatus: st.status, Page: 1, PageSize: 200 }, p));
      Promise.all([q({ RequesterEmployeeId: id }), q({ TargetEmployeeId: id })]).then(([a, b]) => {
        if (!body.isConnected) return;
        if (!a.ok) { body.innerHTML = failBox(a.message); return; }
        const seen = new Set(), rows = [...(a.list || []), ...(b.ok ? b.list || [] : [])].filter(r => !seen.has(r.id) && seen.add(r.id)).sort((x, y) => String(y.submittedAt).localeCompare(x.submittedAt));
        body.innerHTML = table(rows, [{ label: 'Số đơn', html: r => `<b>${esc(r.requestNo || '')}</b>` },
          { label: 'Vai trò', html: r => (String(r.requesterEmployeeId).toLowerCase() === id ? 'Người đề nghị' : 'Người nhận ca') }, { label: 'Người đề nghị', html: r => esc(Store.label('Employee', r.requesterEmployeeId) || '') },
          { label: 'Người nhận', html: r => esc(Store.label('Employee', r.targetEmployeeId) || '') }, { label: 'Lý do', html: r => esc(r.reason || '') }, { label: 'Gửi lúc', html: r => esc(U.dt(r.submittedAt)) },
          { label: 'Trạng thái', html: r => UI.badge(r.requestStatus, 'ShiftSwapRequest', 'RequestStatus') }], true);
        bindRows(body, rows, r => APP.openHrRequest && APP.openHrRequest(false, r.id));
      });
    });
  }
  // Công việc theo vai trò (chuyến xe, mẻ, phiếu… gắn nhân viên) – Common/GetRelated.
  function workTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    run(box, Store.query('Common/GetRelated', { resource: 'Employee', id }), x => {
      const groups = (x.list || []).filter(g => g.count);
      box.innerHTML = groups.length ? groups.map(g => `<div class="card panel mb"><div class="spread"><b>${esc(g.title)} · ${U.num(g.count)}</b>${g.route && can(g.route) ? `<a href="javascript:void 0" data-go="${esc(g.route)}" class="small">Mở màn</a>` : ''}</div>
        <div class="small mt">${(g.items || []).slice(0, 12).map(i => `${esc(i.label || '')}${i.statusName ? ' (' + esc(i.statusName) + ')' : ''}`).join(' · ')}</div></div>`).join('') : '<div class="muted small">Chưa có công việc gắn với nhân viên này.</div>';
      bindGo(box);
    });
  }
  function accountTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    const r = list => `<div class="c360-rows">${list.map(([l, v]) => `<div class="r"><label>${esc(l)}</label><span>${v || '<span class="muted">–</span>'}</span></div>`).join('')}</div>`;
    box.innerHTML = `<div class="c360-grid two"><div class="card panel"><h3>Tài khoản web</h3>${r([['Tên đăng nhập', esc(d.userName)], ['Trạng thái', d.userName ? (d.userIsActive === false ? UI.tag('Đã khóa', 'gray') : UI.tag('Đang hoạt động', 'green')) : ''],
        ['Nhóm quyền', esc((d.listUserGroupName || []).join(', '))]])}${!d.userName ? '<div class="small muted mt">Tài khoản web gắn theo quy tắc tên đăng nhập = mã nhân viên, hoặc qua liên kết bot.</div>' : ''}${can('adm-user') ? `<div class="row-flex mt"><a href="javascript:void 0" data-go="adm-user" class="small">Mở màn người dùng</a></div>` : ''}</div>
      <div class="card panel"><h3>Bot Telegram</h3>${r([['Liên kết', d.botLinked ? UI.tag('Đã liên kết', 'green') : UI.tag('Chưa liên kết', 'orange')], ['Tài khoản Telegram', esc(d.botUsername)], ['Liên kết lúc', d.botLinkedAt ? esc(U.dt(d.botLinkedAt)) : ''], ['Trạng thái', esc(d.botStatus)]])}
        ${Store.perm('adm-employee').edit !== false ? `<div class="row-flex mt"><button class="btn primary" data-tg>${I('msg')} Tạo mã liên kết bot</button><span class="small muted">Mã hạn 72 giờ – nhân viên gửi mã cho bot để liên kết.</span></div>` : ''}</div></div>`;
    bindGo(box);
    const tg = box.querySelector('[data-tg]'); if (tg) tg.onclick = () => tgCode(id);
  }

  const TABS = [
    { label: 'Tổng quan', render: overview },
    { label: 'Lịch ca', screen: 'hr-shift-assign', render: scheduleTab },
    { label: 'Chấm công', screen: 'hr-attendance', render: attendanceTab },
    { label: 'Nghỉ phép', screen: 'hr-leave', render: leaveTab },
    { label: 'Đổi ca', screen: 'hr-swap', render: swapTab },
    { label: 'Công việc', render: workTab },
    { label: 'Tài khoản và bot', render: accountTab },
    { label: 'Tài liệu', render: (box, d, id) => APP.D360.documentsTab('Employee', Store.perm('adm-employee').edit !== false)(box, d, id) },
    { label: 'Lịch sử', render: APP.D360.historyTab('Employee', true) },
  ];
})();
