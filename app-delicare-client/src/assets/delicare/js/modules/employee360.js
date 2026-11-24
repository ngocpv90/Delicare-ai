/* UI-4 (D-68) – Màn chi tiết nhân viên cả trang: #/…/adm-employee-360?id=<EmployeeId>.
 * Mở khi bấm dòng ở danh sách nhân viên (adm-employee). Quyền = quyền xem adm-employee (Store.perm alias). Tab theo quyền xem màn hr-*;
 * thông tin cá nhân (ngày sinh, giới tính, địa chỉ) do máy chủ lọc theo quyền Sửa adm-employee (D-68 e). Khung chung: modules/detail360.js. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const ID = 'adm-employee-360';
  APP.FN[ID] = { ph: 'P0', grp: 'Quản trị hệ thống', fn: 'Nhân viên', id: ID, name: 'Nhân viên', types: ['DT'], table: null, rel: [], st: 'MAPPED', note: '' };
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
      <!-- UI-2R fix (D-80 d): 1 khung bọc dòng tên + hàng chỉ số gọn (.kpis.compact) --><div class="card d360-top"><div data-e360head>${local ? headHtml({ employee: local }, true) : loading}</div>
      <div class="kpis compact" data-e360kpi>${APP.employeeKpis(null)}</div></div>
      <div class="tabs c360-tabs" data-e360tabs></div><div data-e360pane></div>`;
    el.querySelector('[data-back]').onclick = () => APP.go('adm-employee');
    const ov = live() ? Store.query('Employee/GetOverview', { id }) : Promise.resolve({ ok: true, data: { employee: local || {}, listAlert: [] } });
    // UI-2R: màn có thể được vẽ lại trước khi API trả về – chỉ lần vẽ mới nhất dựng tab (tránh 2 bộ tab chồng nhau).
    const tok = el.dataset.d360 = String(Date.now() + Math.random());
    ov.then(x => {
      if (!el.isConnected || el.dataset.d360 !== tok) return;
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
    // Thông tin phụ 1 dòng: mã · chức vụ · đơn vị · vào làm (thâm niên) · SĐT · email (bỏ ô trống).
    const meta = [esc(e.Code || ''), d.positionName && esc(d.positionName), d.organizationUnitName && esc(d.organizationUnitName),
      e.HireDate && esc(`Vào làm ${dmy(e.HireDate)}${d.tenureMonths !== undefined && d.tenureMonths !== null ? ' (' + tenure(d.tenureMonths) + ')' : ''}`),
      e.Phone && `${I('phone')} ${esc(e.Phone)}`, e.Email && esc(e.Email)];
    const bot = partial || !live() ? '' : d.botLinked ? UI.tag('Đã liên kết bot', 'green') : UI.tag('Chưa liên kết bot', 'orange');
    const more = [p.edit !== false && live() && '<button data-h="tg">Tạo mã liên kết bot</button>', can('hr-leave', 'add') && live() && '<button data-h="leave">Tạo đơn nghỉ thay</button>',
      '<button data-h="print">In hồ sơ</button>', p.edit !== false && e.Status !== 'Resigned' && '<button data-h="resign">Cho nghỉ việc</button>'];
    return APP.D360.headRow({ avatar: initials(e.FullName), avatarCls: 'ind', actions: !partial, meta, more,
      title: `${esc(e.FullName || '')} ${e.Status ? UI.tag(st[0], st[1]) : ''} ${bot}`,
      buttons: [{ act: 'edit', icon: 'settings', label: 'Sửa', ok: p.edit !== false }, { act: 'assign', icon: 'calendar', label: 'Phân ca', ok: can('hr-shift-assign', 'add'), primary: true },
        { act: 'proxy', icon: 'check', label: 'Chấm công hộ', ok: can('hr-attendance', 'add') && live() }] });
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

  // ---------- Tab Tổng quan (D-68 d → UI-2R, D-69 g–h): trái 1/3 khối thông tin rút gọn, phải 2/3 khối nghiệp vụ ----------
  const GENDER_VI = { MALE: 'Nam', FEMALE: 'Nữ', OTHER: 'Khác', Male: 'Nam', Female: 'Nữ', M: 'Nam', F: 'Nữ' };
  function infoCfg(d, id) {
    const e = emp(d);
    const join = (...a) => a.filter(Boolean).join(' · ');
    const at = (dt, by) => join(dt && U.date(dt), by);
    const groups = (d.listUserGroupName || []).join(', ');
    const hire = e.HireDate ? U.day(e.HireDate).slice(5, 7) + '/' + U.day(e.HireDate).slice(0, 4) : '';
    const personal = live() && d.canViewPersonal;
    return {
      key: 'employee', title: 'Thông tin nhân viên', canEdit: Store.perm('adm-employee').edit !== false,
      onEdit: () => UI.form('Employee', Object.assign({}, e, Store.get('Employee', id) || {}), { title: 'Nhân viên', onSave: () => APP.refresh() }),
      core: [
        { icon: 'building', html: esc(join(d.positionName, d.organizationUnitName)) },
        { icon: 'phone', html: UI.tel(e.Phone) },
        { icon: 'mail', html: UI.mail(e.Email) },
        { icon: 'calendar', html: hire ? `Vào làm ${esc(hire)}${d.tenureMonths !== null && d.tenureMonths !== undefined ? ` <span class="ip-muted">· ${esc(tenure(d.tenureMonths))}</span>` : ''}` : '' },
        { icon: 'clock', html: d.usualShiftCode ? `Ca thường làm: <b>${esc(d.usualShiftCode)}</b> ${d.usualShiftStart ? `<span class="ip-muted">${tm(d.usualShiftStart)}–${tm(d.usualShiftEnd)}</span>` : ''}` : '' },
        { icon: 'msg', html: live() ? `${d.botLinked ? 'Đã liên kết bot' + (d.botUsername ? ' · ' + esc(d.botUsername) : '') : '<span class="dc-warn">Chưa liên kết bot</span>'}${d.userName ? ` <span class="ip-muted">· tài khoản web ${esc(d.userName)}${d.userIsActive === false ? ' (đã khóa)' : ''}</span>` : ''}` : '' },
      ],
      groups: [
        // D-68 e: máy chủ chỉ trả ngày sinh / giới tính / địa chỉ cho người có quyền Sửa adm-employee – không có quyền thì không hiện nhóm.
        ...(personal ? [{ key: 'personal', title: 'Cá nhân', locked: true, summary: join(GENDER_VI[e.Gender] || e.Gender, e.DateOfBirth && U.date(e.DateOfBirth)),
          rows: [['Ngày sinh', dmy(e.DateOfBirth)], ['Giới tính', esc(GENDER_VI[e.Gender] || e.Gender || '')], ['Địa chỉ', esc(e.Address)]] }] : []),
        { key: 'work', title: 'Công việc', open: true, summary: join(groups, d.organizationUnitName, d.tenureMonths !== null && d.tenureMonths !== undefined ? tenure(d.tenureMonths) : ''),
          rows: [['Chức danh', esc(d.positionName)], ['Đơn vị', esc(d.organizationUnitName)], ['Nhóm quyền', esc(groups)], ['Ngày vào làm', dmy(e.HireDate)]] },
        { key: 'docs', title: 'Giấy tờ', summary: 'Giấy khám sức khỏe, hợp đồng lao động',
          html: live() ? `Giấy khám sức khỏe, hợp đồng lao động lưu ở tab <a href="javascript:void 0" data-tabdocs>Tài liệu</a>.` : '' },
        { key: 'sys', title: 'Hệ thống', rows: [['Tạo lúc / bởi', esc(at(e.CreatedDate, d.createdByName))], ['Cập nhật gần nhất', esc(at(e.ModifiedDate, d.modifiedByName))]],
          summary: e.ModifiedDate ? 'Cập nhật ' + at(e.ModifiedDate, d.modifiedByName) : e.CreatedDate ? 'Tạo ' + at(e.CreatedDate, d.createdByName) : '' },
      ],
    };
  }
  function overview(box, d, id) {
    const join = (...a) => a.filter(Boolean).join(' · ');
    // Ca 7 ngày tới (ngày nghỉ phép tô cam).
    const leaveDays = new Set((d.listLeaveDate || []).map(x => U.day(x)));
    const shifts = d.listUpcomingShift || [];
    const days = []; for (let i = 0; i < 7; i++) days.push(U.addDays(today(), i));
    const WD = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
    const weekHtml = live() ? `<div class="e360-week seven">${days.map(dt => {
      const ss = shifts.filter(s => U.day(s.workDate) === dt), off = leaveDays.has(dt);
      return `<div class="e360-day ${off ? 'off' : ''} ${dt === today() ? 'today' : ''}"><b>${WD[new Date(dt).getDay()]} <span class="ip-muted">${esc(U.date(dt).slice(0, 5))}</span></b>${off ? '<span class="small">Nghỉ</span>' : ''}${ss.map(s => `<span class="small"><span class="scode ${esc(s.shiftCode)}">${esc(s.shiftCode || '')}</span>${s.checkInAt ? ' ✓' : ''}</span>`).join('') || (off ? '' : '<span class="small muted">–</span>')}</div>`;
    }).join('')}</div>` : demoNote;
    const now = shifts.find(s => U.day(s.workDate) === today());
    const todayLine = now ? `<div class="ip-muted mt">Hôm nay: <b>${esc(now.shiftCode || '')}</b> ${tm(now.plannedStart)}–${tm(now.plannedEnd)}${now.checkInAt ? ` · đã vào ca ${tm(now.checkInAt)}${now.checkInSource ? ' (' + esc(now.checkInSource) + (now.checkInValid === false ? ', ngoài bán kính' : ', hợp lệ') + ')' : ''}` : ' · chưa vào ca'}</div>` : '';
    // Nghỉ phép và đơn từ.
    const reqs = d.listRecentRequest || [];
    const reqHtml = !live() ? demoNote : (reqs.length ? reqs.map(r => `<div class="dc-it"><div class="top"><b>${r.kind === 'SWAP' ? 'Đổi ca' + (r.otherEmployeeName ? ' với ' + esc(r.otherEmployeeName) : '') : esc(leaveName(r.leaveTypeCode) || 'Nghỉ') + (r.fromDate ? ' · ' + esc(U.date(r.fromDate).slice(0, 5)) + (r.toDate && U.day(r.toDate) !== U.day(r.fromDate) ? '–' + esc(U.date(r.toDate).slice(0, 5)) : '') : '')}</b>${r.kind === 'LEAVE' && r.days ? `<span class="ip-muted">${U.num(r.days)} ngày</span>` : ''}</div>
        <div class="sub">${UI.statusVi(r.kind === 'SWAP' ? 'ShiftSwapRequest' : 'LeaveRequest', 'RequestStatus', r.requestStatus) || esc(r.requestStatus || '')} · gửi ${esc(U.date(r.submittedAt).slice(0, 5))}</div></div>`).join('') : '<div class="muted small">Chưa có đơn</div>')
      + (live() && (d.listLeaveByType || []).length ? `<div class="ip-muted mt">Đã nghỉ năm nay: ${(d.listLeaveByType || []).map(x => esc(leaveName(x.leaveTypeCode)).toLowerCase() + ' ' + U.num(x.days)).join(' · ')}</div>` : '');
    const cfg = infoCfg(d, id);
    box.innerHTML = `<div class="dc-two">${UI.infoPanel(cfg)}<div class="dc-col">${alertBox(d.listAlert)}
      <div class="dc-pair">
        <div class="card panel"><div class="spread"><h3>Ca 7 ngày tới</h3>${can('hr-shift-assign') ? `<button class="btn xs" data-go="hr-shift-assign">${I('calendar')} Phân ca</button>` : ''}</div>${weekHtml}${todayLine}</div>
        <div class="card panel"><div class="spread"><h3>Nghỉ phép và đơn từ</h3>${can('hr-leave') ? `<button class="btn xs" data-go="hr-leave">Xem đơn</button>` : ''}</div>${reqHtml}</div>
      </div>
      <div class="card panel"><h3>Giờ công 30 ngày</h3><div data-hours></div></div>
      <div class="card panel"><div class="spread"><h3>Chấm công gần đây</h3>${can('hr-attendance') ? `<button class="btn xs" data-go="hr-attendance">Xem bảng công</button>` : ''}</div><div data-att></div></div>
    </div></div>`;
    UI.wireInfoPanel(box, cfg);
    bindGo(box);
    const docs = box.querySelector('[data-tabdocs]'); if (docs) docs.onclick = () => { const b = [...document.querySelectorAll('.c360-tabs .tab')].find(x => /Tài liệu/.test(x.textContent)); if (b) b.click(); };
    const hrs = d.listDayHours || [], hBox = box.querySelector('[data-hours]');
    hBox.innerHTML = !live() ? demoNote : hrs.some(h => h.hours) ? `${UI.combo(hrs.map(h => U.date(h.date).slice(0, 5)), [{ name: 'Giờ công', type: 'bar', role: 'ok', values: hrs.map(h => h.hours || 0) }].concat(hrs.some(h => h.plannedHours) ? [{ name: 'Giờ chuẩn ca', type: 'line', role: 'target', dashed: true, values: hrs.map(h => h.plannedHours || null) }] : []), { h: 200, title: 'Giờ công 30 ngày', unit: 'giờ' })}<div class="ip-muted">${U.num(U.sum(hrs, 'hours'), 1)} giờ / 30 ngày · ${U.num(d.monthWorkedHours || 0, 1)} giờ / ${U.num(d.monthShifts || 0)} ca trong tháng</div>` : none;
    const att = d.listRecentAttendance || [], aBox = box.querySelector('[data-att]');
    const result = r => r.checkInValid === false || r.checkOutValid === false ? UI.tag('Ngoài bán kính', 'red') : r.lateMinutes ? UI.tag(`Muộn ${r.lateMinutes}′`, 'orange') : r.earlyLeaveMinutes ? UI.tag(`Về sớm ${r.earlyLeaveMinutes}′`, 'orange') : UI.tag('Hợp lệ', 'green');
    aBox.innerHTML = !live() ? demoNote : table(att, [{ label: 'Ngày', html: r => esc(U.date(r.workDate).slice(0, 5)) }, { label: 'Ca', html: r => `<span class="scode ${esc(r.shiftCode)}">${esc(r.shiftCode || '')}</span>` },
      { label: 'Vào – ra', html: r => `${tm(r.checkInAt)} – ${tm(r.checkOutAt)}` }, { label: 'Hình thức', html: r => esc(r.checkInSource || r.checkOutSource || '') },
      { label: 'Kết quả', num: true, html: result }]);
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
  // HR-ACC-1.8 (D-82 j): khối Tài khoản – Employee/GetAccount (liên kết thật User.PersonId ↔ Employee.PersonId); nút theo quyền:
  // NS [Đề nghị cấp] / [Đề nghị mở lại], IT [Tạo ngay] / [Khóa] / [Đặt lại mật khẩu], người duyệt [Mở yêu cầu].
  const ACC_ST = { None: ['Chưa có tài khoản', 'gray'], Pending: ['Đang chờ IT duyệt', 'orange'], Active: ['Hoạt động', 'green'], Locked: ['Đã khóa', 'gray'] };
  const tempModal = (title, a) => UI.modal({ title, size: 'md', body: `<div class="c360-rows"><div class="r"><label>Tên đăng nhập</label><span><b>${esc(a.userName || '')}</b></span></div>
      ${a.temporaryPassword ? `<div class="r"><label>Mật khẩu tạm</label><span><b class="mono">${esc(a.temporaryPassword)}</b></span></div>` : ''}</div>
      <div class="small muted mt">${a.temporaryPassword ? 'Chỉ hiện 1 lần – báo trực tiếp cho nhân viên. Hạn 24 giờ, nhân viên phải đổi mật khẩu khi đăng nhập lần đầu.' : 'Mật khẩu tạm đã gửi cho nhân viên qua bot Telegram.'}</div>` });
  const groupsModal = (title, okLabel, groups, send) => UI.modal({
    title, size: 'md',
    body: `<div class="field"><label for="accGroups">Nhóm quyền đề xuất (tên nhóm, cách nhau dấu phẩy)</label><input class="input" id="accGroups" value="${esc(groups.join(', '))}" placeholder="vd. OPS-KIT"></div>
      <div class="field mt"><label for="accReason">Lý do (tùy chọn)</label><input class="input" id="accReason" maxlength="500"></div>
      <div class="small muted mt">Trống = nhóm gợi ý theo chức danh. IT đổi được nhóm trước khi duyệt.</div>
      <div class="row-flex mt"><button class="btn primary" data-ok>${okLabel}</button></div>`,
    onMount: (m, close) => { m.querySelector('[data-ok]').onclick = () => send(m.querySelector('#accGroups').value.trim(), m.querySelector('#accReason').value.trim(), close); },
  });
  function accountTab(box, d, id) {
    if (!live()) { box.innerHTML = demoNote; return; }
    const r = list => `<div class="c360-rows">${list.map(([l, v]) => `<div class="r"><label>${esc(l)}</label><span>${v || '<span class="muted">–</span>'}</span></div>`).join('')}</div>`;
    box.innerHTML = loading;
    const reload = () => accountTab(box, d, id);
    Store.query('Employee/GetAccount', { id }).then(x => {
      if (!box.isConnected) return;
      if (!x.ok) { box.innerHTML = failBox(x.message); return; }
      const a = x.data || {}, st = ACC_ST[a.status] || [a.statusName || a.status, 'gray'], acts = a.actions || [];
      const btn = (k, label, cls = '') => acts.includes(k) ? `<button class="btn ${cls}" data-acc="${k}">${label}</button>` : '';
      box.innerHTML = `<div class="c360-grid two"><div class="card panel"><h3>Tài khoản web</h3>${r([
          ['Trạng thái', UI.tag(st[0], st[1])],
          ['Tên đăng nhập', esc(a.userName)],
          ['Nhóm quyền', esc((a.groups || []).join(', '))],
          ['Nhóm gợi ý theo chức danh', esc((a.suggestedGroups || []).join(', ')) || '<span class="muted">Chưa cấu hình</span>'],
          ['Lần đăng nhập cuối', a.lastLoginAt ? esc(U.dt(a.lastLoginAt)) : '<span class="muted">Chưa ghi nhận (GAP-ACC-01)</span>'],
          ['Gắn với nhân viên', a.userId ? (a.linked ? UI.tag('Đã gắn', 'green') : UI.tag('Đoán theo mã / bot – cần đối chiếu', 'orange')) : ''],
          ['Yêu cầu đang chờ', a.pendingRequestId ? `${esc(a.pendingTypeName || '')} · ${esc(a.pendingRequestNo || '')}` : ''],
        ])}<div class="row-flex mt">${btn('request', I('plus') + ' Đề nghị cấp tài khoản', 'primary')}${btn('create', I('plus') + ' Tạo ngay (IT)')}${btn('reopen', 'Đề nghị mở lại')}${btn('approve', 'Mở yêu cầu duyệt')}${btn('reset', 'Đặt lại mật khẩu')}${btn('lock', 'Khóa tài khoản')}
          ${can('adm-user') ? '<a href="javascript:void 0" data-go="adm-user" class="small">Mở màn Tài khoản</a>' : ''}</div></div>
        <div class="card panel"><h3>Bot Telegram</h3>${r([['Liên kết', d.botLinked ? UI.tag('Đã liên kết', 'green') : UI.tag('Chưa liên kết', 'orange')], ['Tài khoản Telegram', esc(d.botUsername)], ['Liên kết lúc', d.botLinkedAt ? esc(U.dt(d.botLinkedAt)) : ''], ['Trạng thái', esc(d.botStatus)]])}
          ${Store.perm('adm-employee').edit !== false ? `<div class="row-flex mt"><button class="btn primary" data-tg>${I('msg')} Tạo mã liên kết bot</button><span class="small muted">Mã hạn 72 giờ – nhân viên gửi mã cho bot để liên kết.</span></div>` : ''}</div></div>`;
      bindGo(box);
      const tg = box.querySelector('[data-tg]'); if (tg) tg.onclick = () => tgCode(id);
      const on = (k, fn) => { const el = box.querySelector(`[data-acc="${k}"]`); if (el) el.onclick = fn; };
      on('request', () => groupsModal('Đề nghị cấp tài khoản web', 'Gửi IT duyệt', a.suggestedGroups || [], (groups, reason, close) =>
        Store.act('Employee/RequestAccount', { json: { employeeId: id, groups, reason } }).then(z => { if (!z.ok) return; close(); UI.toast(esc((z.res && z.res.message) || 'Đã gửi yêu cầu cho IT duyệt'), 'ok'); reload(); })));
      on('create', () => groupsModal('Tạo tài khoản ngay (không qua duyệt)', 'Tạo tài khoản', a.suggestedGroups || [], (groups, _r, close) =>
        Store.act('User/CreateForEmployee', { json: { employeeId: id, groups } }).then(z => { if (!z.ok) return; close(); tempModal('Đã tạo tài khoản', z.data || {}); reload(); })));
      on('reopen', () => UI.confirm('Gửi IT yêu cầu mở lại tài khoản của nhân viên này?', () => Store.act('Employee/RequestReopen', { json: { employeeId: id } }).then(z => { if (z.ok) { UI.toast('Đã gửi yêu cầu mở lại tài khoản', 'ok'); reload(); } }), { ok: 'Gửi yêu cầu' }));
      on('approve', () => APP.go('wf-inbox'));
      on('reset', () => UI.confirm(`Đặt lại mật khẩu tạm cho <b>${esc(a.userName)}</b>?`, () => Store.query('User/ResetPassword', { id: a.userId }).then(z => { if (z.ok) tempModal('Mật khẩu tạm mới', { userName: a.userName, temporaryPassword: z.data }); else UI.toast(esc(z.message || 'Không đặt lại được'), 'err'); }), { ok: 'Đặt lại' }));
      on('lock', () => UI.confirm(`Khóa tài khoản <b>${esc(a.userName)}</b>? Phiên đang đăng nhập bị chặn ngay.`, () => Store.query('User/SaveIsActive', { id: a.userId }).then(z => { if (z.ok) { UI.toast('Đã khóa tài khoản', 'ok'); reload(); } else UI.toast(esc(z.message || 'Không khóa được'), 'err'); }), { ok: 'Khóa' }));
    });
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
