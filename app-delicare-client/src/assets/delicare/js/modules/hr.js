/* P8 – Phân ca (lưới nhân sự × ngày), Chấm công, Hộp phê duyệt tập trung (ApprovalRequest/Step/Decision) */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  // ---------- Tiện ích cục bộ ----------
  const list = a => Array.isArray(a) ? a : [];
  const LOADING = '<div class="empty">Đang tải…</div>';
  const kv = rows => `<div class="kv">${rows.filter(Boolean).map(([l, v]) => `<div><label>${l}</label><span>${v || '<span class="muted">–</span>'}</span></div>`).join('')}</div>`;
  const warnBox = w => list(w).length ? `<div class="notice warn">${I('alert')}<div>${list(w).map(esc).join('<br>')}</div></div>` : '';
  const closeDrawerOf = node => { const w = node.closest('.drawer-wrap'); if (w) w.remove(); };
  // Đọc qua action riêng có nhớ tạm (màn hình vẽ lại nhiều lần): có kết quả cũ thì vẽ ngay, quá hạn thì gọi lại và vẽ vào khung mới nhất.
  const qc = {};
  function feed(path, params, box, draw, ttl) {
    const k = path + '?' + JSON.stringify(params || {}), c = qc[k] || (qc[k] = { at: 0 });
    c.sink = { box, draw };
    if (c.val) draw(c.val); else box.innerHTML = LOADING;
    if (c.busy || Date.now() - c.at < (ttl || 30000)) return;
    c.busy = true;
    Store.query(path, params).then(x => { c.busy = false; c.at = Date.now(); c.val = x; const s = c.sink; if (s && s.box.isConnected) s.draw(x); });
  }
  const bust = prefix => Object.keys(qc).forEach(k => { if (k.startsWith(prefix)) delete qc[k]; });

  // ---------- Phân ca ----------
  const hs = { ou: '', start: null };
  // Mô hình lưới dùng chung cho 2 nguồn: dữ liệu cục bộ (DEMO) và ShiftAssignment/GetGrid (chế độ API).
  function localGrid(days) {
    const shifts = Store.rows('Shift'), sa = Store.rows('ShiftAssignment'), lr = Store.rows('LeaveRequest');
    const today = sa.filter(a => U.day(a.WorkDate) === Store.ctx.date);
    return {
      days, shifts,
      kpi: shifts.map(s => { const a = today.filter(x => x.ShiftId === s.Id); return { s, assigned: a.length, checkedIn: a.filter(x => ['CheckedIn', 'Completed'].includes(x.AssignmentStatus)).length }; }),
      emps: Store.rows('Employee').filter(e => !hs.ou || e.OrganizationUnitId === hs.ou).map(e => ({
        Id: e.Id, FullName: e.FullName, Position: Store.label('Position', e.PositionId),
        cells: Object.fromEntries(days.map(d => {
          const a = sa.find(x => x.EmployeeId === e.Id && U.day(x.WorkDate) === d); const s = (a && Store.get('Shift', a.ShiftId)) || { Code: '?' };
          const lv = lr.find(l => l.EmployeeId === e.Id && l.RequestStatus === 'Approved' && U.day(l.FromDate) <= d && d <= U.day(l.ToDate));
          return [d, { id: a ? a.Id : null, shiftId: a ? a.ShiftId : null, code: a ? s.Code : null, status: a ? a.AssignmentStatus : null, onLeave: !!lv }];
        }))
      }))
    };
  }
  function apiGrid(g) {
    const shifts = list(g.shifts).map(s => ({ Id: s.id, Code: s.code, Name: s.name, StartTime: s.startTime, EndTime: s.endTime }));
    const k = {}; list(g.kpiToday).forEach(x => { k[x.shiftId] = x; });
    return {
      days: list(g.days).map(d => U.day(d)), shifts,
      kpi: shifts.map(s => ({ s, assigned: (k[s.Id] || {}).assigned || 0, checkedIn: (k[s.Id] || {}).checkedIn || 0 })),
      emps: list(g.employees).map(e => ({
        Id: e.id, FullName: e.fullName, Position: e.positionName || '',
        cells: Object.fromEntries(Object.entries(e.cells || {}).map(([d, c]) => [U.day(d), { id: c.assignmentId || null, shiftId: c.shiftId || null, code: c.shiftCode || null, status: c.status || null, statusName: c.statusName, onLeave: !!c.onLeave, leavePending: !!c.leavePending, leaveNo: c.leaveRequestNo, pendingSwap: !!c.pendingSwap, hasAttendance: !!c.hasAttendance }]))
      }))
    };
  }
  const cellTitle = c => Store.isLive
    ? [c.code ? (c.statusName || UI.statusVi('ShiftAssignment', 'AssignmentStatus', c.status) || '') : 'Chưa phân ca', c.onLeave && 'Nghỉ phép' + (c.leaveNo ? ' ' + c.leaveNo : ''), c.leavePending && 'Có đơn nghỉ chờ duyệt', c.pendingSwap && 'Đang có đơn đổi ca chờ duyệt'].filter(Boolean).join(' · ')
    : (c.code ? c.status : 'Chưa phân ca');
  const cellHtml = c => c.onLeave ? '<span class="scode off">Phép</span>'
    : c.code ? `<span class="scode ${c.status === 'Absent' ? 'Absent' : esc(c.code)}">${esc(c.code)}</span>${c.status === 'CheckedIn' ? ' ✓' : ''}${c.pendingSwap ? ' ⇄' : ''}${c.leavePending ? ' …' : ''}`
      : '<span class="scode off">–</span>' + (c.leavePending ? ' …' : '');
  APP.register('hr-shift-assign', (el, f) => {
    const start = hs.start || U.addDays(Store.ctx.date, -3); hs.start = start;
    const days = Array.from({ length: 7 }, (_, i) => U.addDays(start, i));
    const ous = Store.rows('OrganizationUnit').filter(o => Store.by('Employee', 'OrganizationUnitId', o.Id).length);
    const canEdit = Store.isLive && Store.perm(f.id).edit !== false;
    el.innerHTML = APP.header(f, `<button class="btn" data-w="-7">‹ Tuần trước</button><button class="btn" data-w="7">Tuần sau ›</button>${canEdit ? `<button class="btn" data-bulk>${I('plus')} Phân ca hàng loạt</button><button class="btn primary" data-cfm>${I('ok')} Xác nhận lịch</button>` : ''}`, { sub: Store.isLive ? 'Lưới phân ca nhân viên theo ngày. Bấm vào ô để phân, đổi hoặc bỏ ca.' : 'Lưới phân ca nhân sự × ngày (ShiftAssignment). Bấm ô để phân/đổi ca. Ràng buộc UNIQUE (EmployeeId, ShiftId, WorkDate) theo SQL.' }) + '<div id="hGrid"></div>';
    const box = el.querySelector('#hGrid');
    let model = null;
    const paint = m => {
      model = m;
      box.innerHTML = `
    <div class="kpis">${m.kpi.map((k, i) => UI.kpi(esc(k.s.Name), `${k.checkedIn}/${k.assigned}`, `Hôm nay: đã chấm công / được phân · ${U.time(k.s.StartTime)}–${U.time(k.s.EndTime)}`, 'users', ['blue', 'green', 'orange', 'purple'][i])).join('')}</div>
    <div class="card panel"><div class="row-flex mb"><select class="select" id="hOu"><option value="">Tất cả đơn vị (${Store.rows('Employee').length} NV)</option>${ous.map(o => `<option value="${o.Id}" ${o.Id === hs.ou ? 'selected' : ''}>${esc(o.Name)}</option>`).join('')}</select><span class="grow"></span>${m.shifts.map(s => `<span class="scode ${esc(s.Code)}">${esc(s.Code)}</span> <span class="small muted">${esc(String(s.Name || '').split('·')[1] || '')}</span>`).join(' ')}<span class="scode Absent">Vắng</span><span class="scode off">Nghỉ</span></div>
      <div class="table-wrap" style="max-height:560px"><table class="matrix shiftgrid" style="width:100%"><thead><tr><th class="l">Nhân viên</th><th class="l">Chức vụ</th>${m.days.map(d => `<th>${['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'][new Date(d + 'T00:00').getDay()]} ${U.date(d).slice(0, 5)}${d === Store.ctx.date ? ' ●' : ''}</th>`).join('')}</tr></thead><tbody>${m.emps.map((e, i) => `<tr><td class="l"><a href="javascript:void 0" data-open="Employee" data-id="${e.Id}">${esc(e.FullName)}</a></td><td class="l small muted">${esc(e.Position)}</td>${m.days.map(d => { const c = e.cells[d] || {}; return `<td class="sc" data-i="${i}" data-d="${d}" title="${esc(cellTitle(c))}">${cellHtml(c)}</td>`; }).join('')}</tr>`).join('') || `<tr><td class="l muted" colspan="${m.days.length + 2}">Không có nhân viên phù hợp</td></tr>`}</tbody></table></div></div>`;
      box.querySelector('#hOu').onchange = e => { hs.ou = e.target.value; APP.refresh(); };
      box.querySelectorAll('td.sc').forEach(td => td.onclick = e => { if (e.target.closest('a')) return; assign(m.emps[+td.dataset.i], td.dataset.d, m); });
    };
    // Chế độ API: ShiftAssignment/GetGrid trả sẵn lưới nhân viên × ngày (ca, nghỉ phép, đơn chờ duyệt, số liệu hôm nay). Lỗi → dùng dữ liệu bảng đã tải.
    if (Store.isLive) feed('ShiftAssignment/GetGrid', { FromDate: start, Days: 7, OrganizationUnitId: hs.ou }, box, x => paint(x.ok && x.data && x.data.employees ? apiGrid(x.data) : localGrid(days)));
    else paint(localGrid(days));
    el.querySelectorAll('[data-w]').forEach(b => b.onclick = () => { hs.start = U.addDays(start, +b.dataset.w); APP.refresh(); });
    const bb = el.querySelector('[data-bulk]'); if (bb) bb.onclick = () => { if (model) bulkAssign(model); };
    const cb = el.querySelector('[data-cfm]'); if (cb) cb.onclick = () => confirmWeek(start);
  });
  function assign(emp, d, m) {
    const eid = emp.Id, live = Store.isLive, c = emp.cells[d] || {};
    const a = live ? null : Store.rows('ShiftAssignment').find(x => x.EmployeeId === eid && U.day(x.WorkDate) === d);
    const has = live ? !!c.shiftId : !!a, curShift = live ? c.shiftId : a && a.ShiftId, status = live ? c.status : a && a.AssignmentStatus;
    const attended = () => live ? !!c.hasAttendance : !!(a && Store.by('AttendanceEvent', 'ShiftAssignmentId', a.Id).length);
    const shifts = m.shifts;
    UI.modal({ title: 'Phân ca – ' + esc(emp.FullName), sub: U.date(d) + (live ? '' : ' · dbo.ShiftAssignment'), size: 'sm',
      body: `<div class="grid">${shifts.map(s => `<button class="btn ${has && curShift === s.Id ? 'primary' : ''}" data-s="${s.Id}"><span class="scode ${esc(s.Code)}">${esc(s.Code)}</span> ${esc(s.Name)} <span class="small muted">${U.time(s.StartTime)}–${U.time(s.EndTime)}</span></button>`).join('')}${has ? `<button class="btn danger" data-s="">Bỏ phân ca</button>` : ''}</div>${has ? `<div class="small muted mt">Trạng thái hiện tại: ${UI.badge(status, 'ShiftAssignment', 'AssignmentStatus')} · chấm công: ${live ? (c.hasAttendance ? 'đã có' : 'chưa có') : (Store.by('AttendanceEvent', 'ShiftAssignmentId', a.Id).map(x => x.EventType + ' ' + U.time(x.EventAt)).join(', ') || 'chưa có')}</div>` : ''}${live ? warnBox([c.onLeave && 'Nhân viên đang nghỉ phép ngày này' + (c.leaveNo ? ' (đơn ' + c.leaveNo + ')' : ''), c.leavePending && 'Nhân viên có đơn nghỉ chờ duyệt ngày này', c.pendingSwap && 'Ca đang nằm trong đơn đổi ca chờ duyệt'].filter(Boolean)) : ''}`,
      onMount: (md, close) => md.querySelectorAll('[data-s]').forEach(b => b.onclick = () => {
        const sid = b.dataset.s;
        if (attended()) return UI.toast('Ca đã có chấm công – không thể đổi. Dùng Đơn đổi ca.', 'err');
        // Chế độ API: ShiftAssignment/Assign – phân / đổi / bỏ ca (ShiftId trống = bỏ); backend kiểm tra trùng ca, nghỉ phép, chấm công.
        if (live) return Store.act('ShiftAssignment/Assign', { method: 'PUT', form: { EmployeeId: eid, WorkDate: d, ShiftId: sid || null }, tables: ['ShiftAssignment'] }).then(r => { if (!r.ok) return; close(); bust('ShiftAssignment/GetGrid'); const w = (r.data && r.data.listWarning) || []; UI.toast('Đã cập nhật phân ca' + (w.length ? '<br><span class="small">' + w.map(esc).join('<br>') + '</span>' : ''), w.length ? 'warn' : 'ok'); APP.refresh(); });
        if (!sid) { Store.remove('ShiftAssignment', a.Id); }
        else if (a) Store.update('ShiftAssignment', a.Id, { ShiftId: sid });
        else { const s = Store.get('Shift', sid); Store.add('ShiftAssignment', { EmployeeId: eid, ShiftId: sid, WorkDate: d, PlannedStart: d + 'T' + U.time(s.StartTime), PlannedEnd: d + 'T' + U.time(s.EndTime), AssignmentStatus: 'Planned', AssignedBy: Store.user.id }); }
        close(); UI.toast('Đã cập nhật phân ca (ShiftAssignment · DEMO)', 'ok'); APP.refresh();
      }) });
  }
  // Chế độ API: ShiftAssignment/BulkAssign – phân 1 ca cho nhiều nhân viên trong khoảng ngày (không ghi đè ô đã có ca / ngày nghỉ phép).
  function bulkAssign(m) {
    const today = Store.ctx.date, from = hs.start < today ? today : hs.start, end = U.addDays(hs.start, 6), to = end < from ? from : end;
    const WD = [[1, 'T2'], [2, 'T3'], [3, 'T4'], [4, 'T5'], [5, 'T6'], [6, 'T7'], [7, 'CN']];
    UI.modal({ title: 'Phân ca hàng loạt', sub: 'Ô đã có ca hoặc ngày nghỉ phép được giữ nguyên', size: 'lg',
      body: `<div class="form-grid">
        <div class="field"><label>Ca làm việc <span class="req">*</span></label><select class="select" id="bS"><option value="">— Chọn ca —</option>${m.shifts.map(s => `<option value="${s.Id}">${esc(s.Code)} · ${esc(s.Name)} (${U.time(s.StartTime)}–${U.time(s.EndTime)})</option>`).join('')}</select></div>
        <div class="field"><label>Ghi chú</label><input class="input" id="bN" maxlength="1000"></div>
        <div class="field"><label>Từ ngày <span class="req">*</span></label><input type="date" class="input" id="bF" value="${from}" min="${today}"></div>
        <div class="field"><label>Đến ngày <span class="req">*</span></label><input type="date" class="input" id="bT" value="${to}" min="${today}"></div>
        <div class="field full"><label>Không phân vào các thứ</label><div class="row-flex">${WD.map(([v, l]) => `<label class="small"><input type="checkbox" data-wd="${v}"> ${l}</label>`).join('')}</div></div>
        <div class="field full"><label>Nhân viên <span class="req">*</span> · <a href="javascript:void 0" id="bAll">Chọn tất cả</a> · <span id="bCnt" class="muted">0</span>/${m.emps.length} đã chọn</label><div class="table-wrap" style="max-height:240px;padding:8px 12px">${m.emps.map(e => `<label style="display:block;margin:4px 0"><input type="checkbox" data-be value="${e.Id}"> ${esc(e.FullName)} <span class="small muted">${esc(e.Position)}</span></label>`).join('') || '<span class="muted">Không có nhân viên</span>'}</div></div>
      </div>`,
      foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="bOk">${I('ok')} Phân ca</button>`,
      onMount: (md, close) => {
        const boxes = () => [...md.querySelectorAll('[data-be]')], count = () => { md.querySelector('#bCnt').textContent = boxes().filter(x => x.checked).length; };
        boxes().forEach(x => x.onchange = count);
        md.querySelector('#bAll').onclick = () => { const all = boxes().every(x => x.checked); boxes().forEach(x => { x.checked = !all; }); count(); };
        md.querySelector('#bOk').onclick = () => {
          const sid = md.querySelector('#bS').value, f = md.querySelector('#bF').value, t = md.querySelector('#bT').value, note = md.querySelector('#bN').value.trim();
          const ids = boxes().filter(x => x.checked).map(x => x.value), skip = [...md.querySelectorAll('[data-wd]:checked')].map(x => x.dataset.wd);
          if (!sid) return UI.toast('Chưa chọn ca làm việc', 'err');
          if (!f || !t || t < f) return UI.toast('Đến ngày phải ≥ Từ ngày', 'err');
          if ((new Date(t + 'T00:00') - new Date(f + 'T00:00')) / 864e5 + 1 > 31) return UI.toast('Khoảng ngày tối đa 31 ngày', 'err');
          if (!ids.length) return UI.toast('Chưa chọn nhân viên', 'err');
          if (skip.length === 7) return UI.toast('Đã loại hết các thứ trong tuần', 'err');
          // Danh sách lặp khóa (EmployeeIds, SkipWeekdays) → gửi FormData trực tiếp.
          const fd = new FormData();
          ids.forEach(v => fd.append('EmployeeIds', v)); fd.append('ShiftId', sid); fd.append('FromDate', f); fd.append('ToDate', t);
          skip.forEach(v => fd.append('SkipWeekdays', v)); if (note) fd.append('Note', note);
          const btn = md.querySelector('#bOk'); btn.disabled = true;
          Store.act('ShiftAssignment/BulkAssign', { json: fd, tables: ['ShiftAssignment'] }).then(r => {
            btn.disabled = false; if (!r.ok) return;
            close(); bust('ShiftAssignment/GetGrid');
            const d = r.data || {}, sk = list(d.listSkipped);
            UI.toast(`Đã phân ${U.num(d.created || 0)} ca` + (d.skipped ? `, bỏ qua ${U.num(d.skipped)} ô` : ''), d.skipped ? 'warn' : 'ok'); APP.refresh();
            if (sk.length) UI.modal({ title: 'Các ô không được phân', sub: `${sk.length} ô giữ nguyên`, size: 'lg', body: `<div class="table-wrap" style="max-height:420px"><table class="table dc-table dt"><thead><tr><th>Nhân viên</th><th>Ngày</th><th>Lý do</th></tr></thead><tbody>${sk.map(x => `<tr><td>${esc(x.employeeName)}</td><td>${U.date(x.workDate)}</td><td>${esc(x.reason)}</td></tr>`).join('')}</tbody></table></div>` });
          });
        };
      } });
  }
  // Chế độ API: ShiftAssignment/Confirm – chốt lịch tuần đang xem (Kế hoạch → Xác nhận).
  function confirmWeek(start) {
    const to = U.addDays(start, 6), ou = hs.ou && Store.get('OrganizationUnit', hs.ou);
    UI.confirm(`Xác nhận lịch ca từ <b>${U.date(start)}</b> đến <b>${U.date(to)}</b>${ou ? ' – ' + esc(ou.Name) : ''}?<div class="small muted mt">Các ca đang ở trạng thái Kế hoạch sẽ chuyển sang Xác nhận.</div>`,
      () => Store.act('ShiftAssignment/Confirm', { form: { WorkDate: start, ToDate: to, OrganizationUnitId: hs.ou }, tables: ['ShiftAssignment'] }).then(r => {
        if (!r.ok) return; bust('ShiftAssignment/GetGrid');
        const d = r.data || {};
        UI.toast(`Đã xác nhận ${U.num(d.confirmed || 0)} ca` + (d.alreadyConfirmed ? ` (${U.num(d.alreadyConfirmed)} ca đã xác nhận từ trước)` : ''), 'ok'); APP.refresh();
      }), { ok: 'Xác nhận lịch' });
  }

  // ---------- Chấm công ----------
  // Chế độ API: ca hôm nay của tôi (AttendanceEvent/GetMine) + vào / ra ca, chấm hộ, bảng công (GetTimesheet) và chốt vắng (ShiftAssignment/MarkAbsent).
  // Sổ chấm công chỉ ghi thêm: không có sửa / xóa.
  const at = { from: null, to: null, ou: '' };
  const AT = ['AttendanceEvent', 'ShiftAssignment'];
  const attendanceChanged = () => { bust('AttendanceEvent/'); bust('ShiftAssignment/GetGrid'); };
  function checkDone(d, isIn) {
    d = d || {}; attendanceChanged();
    const w = list(d.listWarning), bad = w.length || d.isValid === false;
    const parts = [d.isDuplicate ? 'Lần chấm này đã được ghi trước đó' : `${esc(d.eventTypeName || (isIn ? 'Vào ca' : 'Ra ca'))}${d.employeeName ? ' – ' + esc(d.employeeName) : ''}${d.eventAt ? ' lúc ' + U.time(d.eventAt) : ''}`];
    if (d.lateMinutes) parts.push(`Đi muộn ${d.lateMinutes} phút`);
    if (d.earlyLeaveMinutes) parts.push(`Về sớm ${d.earlyLeaveMinutes} phút`);
    UI.toast(parts.join(' · ') + (w.length ? '<br><span class="small">' + w.map(esc).join('<br>') + '</span>' : ''), bad ? 'warn' : 'ok'); APP.refresh();
  }
  function selfCheck(isIn, btn) {
    if (!navigator.geolocation) return UI.toast('Thiết bị không hỗ trợ định vị nên không tự chấm công được', 'err');
    UI.confirm(`Ghi nhận <b>${isIn ? 'vào ca' : 'ra ca'}</b> ngay lúc này?<div class="small muted mt">Hệ thống dùng vị trí hiện tại của thiết bị. Lần chấm đã ghi không sửa, không xóa được.</div>`, () => {
      btn.disabled = true;
      navigator.geolocation.getCurrentPosition(pos => {
        const form = { Latitude: pos.coords.latitude.toFixed(6), Longitude: pos.coords.longitude.toFixed(6), SourceChannel: 'GPS' };
        (isIn ? Store.act('AttendanceEvent/CheckIn', { form, tables: AT }) : Store.act('AttendanceEvent/CheckOut', { form, tables: AT })).then(r => { btn.disabled = false; if (r.ok) checkDone(r.data, isIn); });
      }, () => { btn.disabled = false; UI.toast('Không lấy được vị trí – hãy cho phép trình duyệt dùng định vị rồi thử lại', 'err'); }, { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 });
    }, { ok: isIn ? 'Vào ca' : 'Ra ca' });
  }
  function proxyCheck() {
    UI.modal({ title: 'Chấm công hộ', sub: 'Ghi thêm một lần chấm cho nhân viên – không sửa, không xóa được', size: 'sm',
      body: `<div class="field"><label>Nhân viên <span class="req">*</span></label><select class="select" id="pE"><option value="">— Chọn nhân viên —</option>${Store.rows('Employee').map(e => `<option value="${e.Id}">${esc(Store.label('Employee', e))}</option>`).join('')}</select></div>
        <div class="field"><label>Loại <span class="req">*</span></label><select class="select" id="pT"><option value="in">Vào ca</option><option value="out">Ra ca</option></select></div>
        <div class="field"><label>Lý do chấm hộ <span class="req">*</span></label><textarea class="input" rows="3" id="pN" maxlength="500"></textarea></div>`,
      foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="pOk">${I('ok')} Ghi nhận</button>`,
      onMount: (md, close) => md.querySelector('#pOk').onclick = () => {
        const eid = md.querySelector('#pE').value, isIn = md.querySelector('#pT').value === 'in', note = md.querySelector('#pN').value.trim();
        if (!eid) return UI.toast('Chưa chọn nhân viên', 'err');
        if (!note) return UI.toast('Vui lòng nhập lý do chấm hộ', 'err');
        const form = { EmployeeId: eid, SourceChannel: 'PROXY', Note: note }, btn = md.querySelector('#pOk'); btn.disabled = true;
        (isIn ? Store.act('AttendanceEvent/CheckIn', { form, tables: AT }) : Store.act('AttendanceEvent/CheckOut', { form, tables: AT })).then(r => { btn.disabled = false; if (!r.ok) return; close(); checkDone(r.data, isIn); });
      } });
  }
  function markAbsent() {
    const ou = at.ou && Store.get('OrganizationUnit', at.ou);
    UI.modal({ title: 'Chốt vắng', size: 'sm',
      body: `<p style="margin-top:0">Các ca đã hết giờ mà nhân viên không vào ca trong ngày được chọn${ou ? ' (' + esc(ou.Name) + ')' : ''} sẽ chuyển sang <b>Vắng</b>. Thao tác này không hoàn tác được.</p><div class="field"><label>Ngày <span class="req">*</span></label><input type="date" class="input" id="mD" value="${Store.ctx.date}" max="${Store.ctx.date}"></div>`,
      foot: `<button class="btn" data-close>Hủy</button><button class="btn danger" id="mOk">Đánh vắng</button>`,
      onMount: (md, close) => md.querySelector('#mOk').onclick = () => {
        const d = md.querySelector('#mD').value; if (!d) return UI.toast('Chưa chọn ngày', 'err');
        close();
        Store.act('ShiftAssignment/MarkAbsent', { form: { WorkDate: d, OrganizationUnitId: at.ou }, tables: ['ShiftAssignment'] }).then(r => { if (!r.ok) return; attendanceChanged(); const n = (r.data && r.data.marked) || 0; UI.toast(n ? `Đã đánh vắng ${U.num(n)} ca ngày ${U.date(d)}` : `Không có ca nào cần đánh vắng ngày ${U.date(d)}`, 'ok'); APP.refresh(); });
      } });
  }
  const validTag = v => v === false ? ' <span class="tag red">Ngoài bán kính</span>' : '';
  function timesheetDetail(row) {
    UI.modal({ title: 'Bảng công chi tiết – ' + esc(row.fullName || ''), sub: `${U.date(at.from)} → ${U.date(at.to)}`, size: 'lg', body: '<div id="tsD">' + LOADING + '</div>',
      onMount: md => Store.query('AttendanceEvent/GetTimesheetDetail', { FromDate: at.from, ToDate: at.to, EmployeeId: row.employeeId, Page: 1, PageSize: 200 }).then(x => {
        const b = md.querySelector('#tsD'); if (!b || !b.isConnected) return;
        if (!x.ok) { b.innerHTML = `<div class="empty">${esc(x.message || 'Không tải được bảng công chi tiết')}</div>`; return; }
        b.innerHTML = x.list.length ? `<div class="table-wrap" style="max-height:460px"><table class="table dc-table dt"><thead><tr><th>Ngày</th><th>Ca</th><th>Giờ kế hoạch</th><th>Vào ca</th><th>Ra ca</th><th class="num">Muộn (phút)</th><th class="num">Sớm (phút)</th><th class="num">Giờ công</th><th>Trạng thái</th></tr></thead><tbody>${x.list.map(r => `<tr><td>${U.date(r.workDate)}</td><td><span class="scode ${esc(r.shiftCode)}">${esc(r.shiftCode)}</span></td><td>${r.plannedStart ? U.time(r.plannedStart) + '–' + U.time(r.plannedEnd) : '–'}</td><td>${r.checkInAt ? U.time(r.checkInAt) + validTag(r.checkInValid) : '–'}</td><td>${r.checkOutAt ? U.time(r.checkOutAt) + validTag(r.checkOutValid) : (r.missingCheckOut ? '<span class="tag orange">Thiếu ra ca</span>' : '–')}</td><td class="num">${r.lateMinutes || ''}</td><td class="num">${r.earlyLeaveMinutes || ''}</td><td class="num">${r.workedMinutes ? U.num(r.workedMinutes / 60, 1) : ''}</td><td>${r.isAbsent ? UI.badge('Absent', 'ShiftAssignment', 'AssignmentStatus') : UI.badge(r.status, 'ShiftAssignment', 'AssignmentStatus')}</td></tr>`).join('')}</tbody></table></div>` : '<div class="empty">Không có ca nào trong khoảng ngày này</div>';
      }) });
  }
  APP.register('hr-attendance', (el, f) => {
    if (!Store.isLive) return APP.genericList(el, f);
    const perm = Store.perm(f.id), today = Store.ctx.date, canAbsent = !!Store.perm('hr-shift-assign').edit;
    at.from = at.from || U.addDays(today, -6); at.to = at.to || today;
    const ous = Store.rows('OrganizationUnit');
    APP.genericList(el, f, { noAdd: true, noTransit: true, top: `
      <div class="card panel mb"><div class="spread"><div><div class="panel-title">Ca hôm nay của tôi</div><div class="panel-desc">Vào ca / ra ca bằng vị trí hiện tại của thiết bị</div></div>${perm.add ? `<button class="btn" id="aProxy">${I('plus')} Chấm công hộ</button>` : ''}</div><div id="aMine" class="mt"></div></div>
      <div class="card panel mb"><div class="row-flex mb"><div class="panel-title">Bảng công</div><span class="grow"></span><input type="date" class="input" id="aF" value="${at.from}" style="width:auto"><span class="muted">→</span><input type="date" class="input" id="aT" value="${at.to}" style="width:auto"><select class="select" id="aOu"><option value="">Tất cả đơn vị</option>${ous.map(o => `<option value="${o.Id}" ${o.Id === at.ou ? 'selected' : ''}>${esc(o.Name)}</option>`).join('')}</select>${canAbsent ? '<button class="btn" id="aAbs">Chốt vắng</button>' : ''}</div><div id="aTs"></div></div>` });
    // Sổ chỉ ghi thêm: bỏ nút sửa / xóa trên dòng, chi tiết chỉ xem.
    APP.rowExtra(el, tr => {
      tr.querySelectorAll('[data-edit],[data-del]').forEach(b => b.remove());
      const open = () => APP.openDetail('AttendanceEvent', tr.dataset.row, f, { noEdit: true });
      tr.onclick = e => { if (e.target.closest('button,a,input,select')) return; open(); };
      const v = tr.querySelector('[data-view]'); if (v) v.onclick = open;
    });
    const mine = el.querySelector('#aMine'), ts = el.querySelector('#aTs');
    feed('AttendanceEvent/GetMine', null, mine, x => {
      if (!x.ok || !x.data) { mine.innerHTML = `<div class="small muted">${esc(x.message || 'Tài khoản chưa gắn với nhân viên nên không tự chấm công được.')}</div>`; return; }
      const d = x.data, acts = list(d.allowedActions), rows = list(d.listAssignment);
      mine.innerHTML = `<div class="row-flex"><b>${esc(d.employeeName || '')}</b>${d.employeeCode ? `<span class="small muted">${esc(d.employeeCode)}</span>` : ''}<span class="grow"></span>${acts.includes('checkIn') ? `<button class="btn primary" data-ck="in">${I('ok')} Vào ca</button>` : ''}${acts.includes('checkOut') ? `<button class="btn primary" data-ck="out">${I('ok')} Ra ca</button>` : ''}</div>`
        + (rows.length ? `<div class="table-wrap mt"><table class="table dc-table dt"><thead><tr><th>Ngày</th><th>Ca</th><th>Giờ kế hoạch</th><th>Vào ca</th><th>Ra ca</th><th>Trạng thái</th></tr></thead><tbody>${rows.map(r => `<tr><td>${U.date(r.workDate)}</td><td><span class="scode ${esc(r.shiftCode)}">${esc(r.shiftCode)}</span> ${esc(r.shiftName || '')}</td><td>${r.plannedStart ? U.time(r.plannedStart) + '–' + U.time(r.plannedEnd) : '–'}</td><td>${r.checkInAt ? U.time(r.checkInAt) + validTag(r.checkInValid) + (r.lateMinutes ? ` <span class="small muted">muộn ${r.lateMinutes}′</span>` : '') : '–'}</td><td>${r.checkOutAt ? U.time(r.checkOutAt) + validTag(r.checkOutValid) + (r.earlyLeaveMinutes ? ` <span class="small muted">sớm ${r.earlyLeaveMinutes}′</span>` : '') : '–'}</td><td>${UI.badge(r.status, 'ShiftAssignment', 'AssignmentStatus')}</td></tr>`).join('')}</tbody></table></div>` : '<div class="small muted mt">Hôm nay bạn không có ca được phân.</div>');
      mine.querySelectorAll('[data-ck]').forEach(b => b.onclick = () => selfCheck(b.dataset.ck === 'in', b));
    }, 15000);
    feed('AttendanceEvent/GetTimesheet', { FromDate: at.from, ToDate: at.to, OrganizationUnitId: at.ou, Page: 1, PageSize: 1000 }, ts, x => {
      if (!x.ok) { ts.innerHTML = `<div class="empty">${esc(x.message || 'Không tải được bảng công')}</div>`; return; }
      const rows = x.list.map(r => Object.assign({ Id: r.employeeId }, r)), n = k => ({ k, num: true, render: r => r[k] ? U.num(r[k]) : '<span class="muted">0</span>' });
      UI.table(ts, { rows, pageSize: 10, onRow: id => { const r = rows.find(y => y.Id === id); if (r) timesheetDetail(r); },
        cols: [{ k: 'employeeCode', label: 'Mã NV' }, { k: 'fullName', label: 'Nhân viên' }, { k: 'organizationUnitName', label: 'Đơn vị' },
          Object.assign(n('assignedShifts'), { label: 'Ca được phân' }), Object.assign(n('present'), { label: 'Có mặt' }), Object.assign(n('late'), { label: 'Đi muộn' }), Object.assign(n('earlyLeave'), { label: 'Về sớm' }),
          Object.assign(n('absent'), { label: 'Vắng' }), Object.assign(n('missingCheckOut'), { label: 'Thiếu ra ca' }), Object.assign(n('invalidEvents'), { label: 'Ngoài bán kính' }), Object.assign(n('leaveDays'), { label: 'Ngày nghỉ' }), Object.assign(n('workedHours'), { label: 'Giờ công' })] });
    });
    const range = (k, input) => {
      const v = input.value, from = k === 'from' ? v : at.from, to = k === 'to' ? v : at.to;
      if (!v || to < from) { input.value = at[k]; return UI.toast('Đến ngày phải ≥ Từ ngày', 'err'); }
      if ((new Date(to + 'T00:00') - new Date(from + 'T00:00')) / 864e5 + 1 > 62) { input.value = at[k]; return UI.toast('Khoảng ngày bảng công tối đa 62 ngày', 'err'); }
      at[k] = v; APP.refresh();
    };
    el.querySelector('#aF').onchange = e => range('from', e.target);
    el.querySelector('#aT').onchange = e => range('to', e.target);
    el.querySelector('#aOu').onchange = e => { at.ou = e.target.value; APP.refresh(); };
    const pb = el.querySelector('#aProxy'); if (pb) pb.onclick = proxyCheck;
    const ab = el.querySelector('#aAbs'); if (ab) ab.onclick = markAbsent;
  });

  // ---------- Hộp phê duyệt ----------
  const BT = { PURCHASE_REQUEST: ['PurchaseRequest', 'Status', 'Approved', 'Rejected', 'scm-pr'], LEAVE_REQUEST: ['LeaveRequest', 'RequestStatus', 'Approved', 'Rejected', 'hr-leave'], SHIFT_SWAP: ['ShiftSwapRequest', 'RequestStatus', 'Approved', 'Rejected', 'hr-swap'], RECIPE_VERSION: ['Recipe', 'Status', 'Approved', 'Draft', 'menu-recipe'], STOCK_COUNT: ['StockCount', 'Status', 'PendingApproval', 'Draft', 'scm-stockcount'], CONTRACT: ['Contract', 'Status', 'Active', 'Draft', 'crm-contract'], MENU_CHANGE: ['MenuChangeRequest', 'RequestStatus', 'Approved', 'Rejected', 'menu-change-request'] };
  const ws = { tab: 'mine' };
  const isOpen = r => ['Pending', 'InProgress'].includes(r.RequestStatus);
  const approvalTables = r => ['ApprovalRequest', 'ApprovalStep', 'ApprovalDecision', 'ApprovalItem'].concat(BT[r.BusinessType] ? [BT[r.BusinessType][0]] : []);
  APP.register('wf-inbox', (el, f) => {
    const all = Store.rows('ApprovalRequest').slice().sort((a, b) => (b.RequestedAt || '').localeCompare(a.RequestedAt || ''));
    const pend = all.filter(isOpen);
    const cur = a => Store.by('ApprovalStep', 'ApprovalRequestId', a.Id).find(s => s.DecisionStatus === 'Waiting');
    // Chế độ API: "Chờ tôi duyệt" lấy từ ApprovalRequest/GetMyPending (đúng người duyệt của bước hiện tại), làm mới sau 30 giây.
    if (Store.isLive && (!ws.mineIds || Date.now() - ws.mineAt > 30000) && !ws.mineBusy) {
      ws.mineBusy = true;
      Store.query('ApprovalRequest/GetMyPending').then(x => { ws.mineBusy = false; ws.mineAt = Date.now(); const had = ws.mineIds; ws.mineIds = new Set(x.ok ? x.list.map(p => p.id) : []); if (!had || had.size !== ws.mineIds.size) { if (APP.current === 'wf-inbox') APP.refresh(); } });
    }
    const mine = Store.isLive ? pend.filter(a => ws.mineIds && ws.mineIds.has(a.Id)) : pend.filter(a => { const s = cur(a); return s && (Store.user.group === 'Quản trị hệ thống' || Store.user.group === s.ApproverRoleCode); });
    // Chế độ API: người gửi được hủy yêu cầu còn chờ duyệt (ApprovalRequest/Cancel) – backend kiểm tra lại.
    const canCancel = r => Store.isLive && isOpen(r) && !!Store.user.id && r.RequestedBy === Store.user.id;
    const rowsOf = ws.tab === 'mine' ? mine : ws.tab === 'pending' ? pend : all;
    el.innerHTML = APP.header(f, '', { sub: 'Hộp phê duyệt tập trung cho mọi nghiệp vụ: ApprovalRequest (BusinessType, BusinessId) → ApprovalStep (StepNo, ApproverRoleCode) → ApprovalDecision. Duyệt bước cuối sẽ cập nhật trạng thái bản ghi nghiệp vụ.' }) + `
    <div class="notice warn">${I('alert')}<div><b>GAP-F-08:</b> SQL không có bảng mẫu quy trình (số cấp, vai trò, hạn mức theo BusinessType). Mockup dùng cấu hình cố định: Đề nghị mua 2 cấp (Quản lý kho → Ban giám đốc), các nghiệp vụ khác 1 cấp.</div></div>
    <div class="kpis">${UI.kpi('Chờ tôi duyệt', mine.length, 'Theo vai trò: ' + esc(Store.user.group), 'approve', 'orange')}${UI.kpi('Đang chờ (tất cả)', pend.length, 'RequestStatus = Pending/InProgress', 'clipboard', 'blue')}${UI.kpi('Đã duyệt', all.filter(a => a.RequestStatus === 'Approved').length, 'Approved', 'ok', 'green')}${UI.kpi('Từ chối', all.filter(a => a.RequestStatus === 'Rejected').length, 'Rejected', 'x', 'red')}</div>
    <div class="card panel"><div class="row-flex mb">${[['mine', 'Chờ tôi duyệt'], ['pending', 'Đang chờ'], ['all', 'Tất cả']].map(([k, l]) => `<button class="chip ${ws.tab === k ? 'active' : ''}" data-tab2="${k}">${l}</button>`).join('')}</div><div id="wT"></div></div>`;
    el.querySelectorAll('[data-tab2]').forEach(b => b.onclick = () => { ws.tab = b.dataset.tab2; APP.refresh(); });
    UI.table(el.querySelector('#wT'), { table: 'ApprovalRequest', rows: rowsOf, onRow: id => openReq(id),
      cols: [{ k: 'RequestNo', label: 'Số yêu cầu' }, { k: 'BusinessType', label: 'Loại nghiệp vụ' }, { k: 'BusinessId', label: 'Bản ghi nghiệp vụ', render: r => { const m = BT[r.BusinessType]; const row = m && Store.get(m[0], r.BusinessId); return row ? `<a href="javascript:void 0" data-open="${m[0]}" data-id="${row.Id}">${esc(Store.label(m[0], row))}</a><span class="sub">dbo.${m[0]}</span>` : '<span class="muted">–</span>'; } }, { k: 'RequestedBy', label: 'Người yêu cầu', render: r => esc(Store.label('User', r.RequestedBy)) }, { k: 'RequestedAt', label: 'Gửi lúc' }, { k: 'step', label: 'Bước / Vai trò duyệt', col: false, nosort: true, render: r => { const st = Store.by('ApprovalStep', 'ApprovalRequestId', r.Id).sort((a, b) => a.StepNo - b.StepNo); return st.map(s => `<span class="tag ${s.DecisionStatus === 'Approved' ? 'green' : s.DecisionStatus === 'Rejected' ? 'red' : 'orange'}">${s.StepNo}. ${esc(s.ApproverRoleCode)}</span>`).join(' '); } }, { k: 'RequestStatus', label: 'Trạng thái' }],
      actions: r => (mine.includes(r) ? `<button class="btn xs primary" data-ap="${r.Id}">Duyệt</button> <button class="btn xs danger" data-rj="${r.Id}">Từ chối</button>` : '') + (canCancel(r) ? ` <button class="btn xs" data-cc="${r.Id}">Hủy yêu cầu</button>` : ''),
      after: t => { t.querySelectorAll('[data-ap]').forEach(b => b.onclick = () => decide(b.dataset.ap, true)); t.querySelectorAll('[data-rj]').forEach(b => b.onclick = () => decide(b.dataset.rj, false)); t.querySelectorAll('[data-cc]').forEach(b => b.onclick = () => cancelReq(b.dataset.cc)); } });
  });
  function openReq(id) {
    const r = Store.get('ApprovalRequest', id), m = BT[r.BusinessType];
    const steps = Store.by('ApprovalStep', 'ApprovalRequestId', id).sort((a, b) => a.StepNo - b.StepNo);
    const biz = m && Store.get(m[0], r.BusinessId);
    UI.detail('ApprovalRequest', id, { screen: 'wf-inbox', noEdit: true,
      top: `${UI.stepper(steps.map(s => ({ l: s.ApproverRoleCode, s: s.DecisionStatus })), steps.findIndex(s => s.DecisionStatus === 'Waiting') < 0 ? steps.length : steps.findIndex(s => s.DecisionStatus === 'Waiting'), steps.some(s => s.DecisionStatus === 'Rejected'))}${biz ? `<div class="card panel mt mb"><div class="eyebrow">Bản ghi nghiệp vụ · dbo.${m[0]}</div><h3>${esc(Store.label(m[0], biz))}</h3>${UI.kvHtml(m[0], biz, Object.keys(biz).filter(k => !Store.AUDIT.has(k) && k !== 'Id' && !k.endsWith('Json')).slice(0, 10))}${m[0] === 'PurchaseRequest' ? `<table class="table dc-table dt mt"><thead><tr><th>Nguyên liệu</th><th class="num">SL</th></tr></thead><tbody>${Store.by('PurchaseRequestLine', 'PurchaseRequestId', biz.Id).map(l => `<tr><td>${esc(Store.label('Ingredient', l.IngredientId))}</td><td class="num">${U.num(l.Quantity)}</td></tr>`).join('')}</tbody></table>` : ''}<a class="btn sm mt" href="#/${m[4]}">Mở màn hình ${esc(APP.FN[m[4]].name)} →</a></div>` : ''}`,
      // Chế độ API: tab "Tiến trình duyệt" đọc ApprovalRequest/GetDetail (các cấp, người quyết định, hạn xử lý, quyền duyệt / hủy của tôi).
      tabs: (Store.isLive ? [{ label: 'Tiến trình duyệt', html: p => { loadApproval(id, p); return LOADING; } }] : []).concat([{ label: 'Quyết định', html: `<div class="timeline">${Store.by('ApprovalDecision', 'ApprovalRequestId', id).map(dc => `<div class="tl ${dc.DecisionCode === 'REJECT' ? 'red' : ''}"><b>${esc(dc.DecisionCode)} · ${esc(Store.label('User', dc.DecidedBy))}</b><small>${U.dt(dc.DecidedAt)} · ${esc(dc.Comment || '')}</small></div>`).join('') || '<div class="empty">Chưa có quyết định</div>'}</div>` }]),
      foot: () => isOpen(r) ? `${Store.isLive && !!Store.user.id && r.RequestedBy === Store.user.id ? '<button class="btn" data-act="cx">Hủy yêu cầu</button>' : ''}<button class="btn danger" data-act="rj">Từ chối</button><button class="btn primary" data-act="ap">${I('approve')} Duyệt bước hiện tại</button>` : '',
      onAct: (a, row, close) => { close(); if (a === 'cx') cancelReq(id); else decide(id, a === 'ap'); } });
  }
  const DECISION_VI = { APPROVE: 'Duyệt', REJECT: 'Từ chối', CANCEL: 'Hủy' };
  function loadApproval(id, p) {
    Store.query('ApprovalRequest/GetDetail', { id }).then(x => {
      if (!p.isConnected) return;
      if (!x.ok || !x.data) { p.innerHTML = `<div class="empty">${esc(x.message || 'Không tải được chi tiết yêu cầu duyệt')}</div>`; return; }
      const d = x.data, steps = list(d.listStepInfo), decs = list(d.listDecision), b = d.business || {};
      p.innerHTML = (d.title ? `<h3 style="margin-top:0">${esc(d.title)}</h3>` : '') + kv([
        ['Người gửi', esc(d.requestedByName || '')], ['Trạng thái', esc(d.statusName || '')],
        ['Cấp duyệt', d.currentStepNo ? `${d.currentStepNo}/${d.stepCount || 1}` : `${d.stepCount || 1} cấp – đã kết thúc`],
        ['Hạn xử lý', d.dueAt ? esc(U.dt(d.dueAt)) + (d.isOverdue ? ' <span class="tag red">Quá hạn</span>' : '') : ''],
        b.label && ['Chứng từ', esc(b.label)]])
        + `<h3 class="mt">Các cấp duyệt</h3><div class="timeline">${steps.map(s => `<div class="tl ${['Rejected', 'Cancelled'].includes(s.decisionStatus) ? 'red' : s.decisionStatus === 'Approved' ? '' : 'orange'}"><b>${s.stepNo}. ${esc(s.stepName || '')} – ${esc(s.decisionStatusName || s.decisionStatus || '')}${s.isCurrent ? ' (đang chờ)' : ''}</b><small>${[s.approverName && esc(s.approverName), s.decidedAt && esc(U.dt(s.decidedAt)), !s.decidedAt && s.dueAt && 'hạn ' + esc(U.dt(s.dueAt)) + (s.isOverdue ? ' – quá hạn' : ''), s.decisionNote && esc(s.decisionNote)].filter(Boolean).join(' · ')}</small></div>`).join('') || '<div class="empty">Chưa có cấp duyệt</div>'}</div>`
        + `<h3 class="mt">Quyết định</h3><div class="timeline">${decs.map(it => { const dc = it.decision || {}; return `<div class="tl ${dc.decisionCode === 'REJECT' ? 'red' : ''}"><b>${esc(DECISION_VI[dc.decisionCode] || dc.decisionCode || '')} · ${esc(it.decidedByName || '')}</b><small>${esc(U.dt(dc.decidedAt))}${dc.comment ? ' · ' + esc(dc.comment) : ''}</small></div>`; }).join('') || '<div class="empty">Chưa có quyết định</div>'}</div>`
        + (d.canDecide || d.canCancel ? `<div class="row-flex mt">${d.canCancel ? '<button class="btn" data-x="cx">Hủy yêu cầu</button>' : ''}<span class="grow"></span>${d.canDecide ? `<button class="btn danger" data-x="rj">Từ chối</button><button class="btn primary" data-x="ap">${I('approve')} Duyệt</button>` : ''}</div>` : '');
      p.querySelectorAll('[data-x]').forEach(bt => bt.onclick = () => { const a = bt.dataset.x; closeDrawerOf(p); if (a === 'cx') cancelReq(id); else decide(id, a === 'ap'); });
    });
  }
  function cancelReq(id) {
    const r = Store.get('ApprovalRequest', id); if (!r) return;
    UI.confirm(`Hủy yêu cầu duyệt <b>${esc(r.RequestNo)}</b>?<div class="small muted mt">Chứng từ liên quan sẽ không còn chờ duyệt. Thao tác này không hoàn tác được.</div>`,
      note => Store.act('ApprovalRequest/Cancel', { form: { Id: id, Reason: note }, tables: approvalTables(r) }).then(x => { if (!x.ok) return; ws.mineIds = null; UI.toast('Đã hủy yêu cầu duyệt', 'ok'); APP.refresh(); }),
      { input: 'Lý do hủy', required: true, danger: true, ok: 'Hủy yêu cầu' });
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
  const HR_TABLES = t => [t, 'ApprovalRequest', 'ApprovalStep', 'ApprovalDecision', 'ApprovalItem', 'ShiftAssignment'];
  const hrChanged = () => { ws.mineIds = null; bust('ShiftAssignment/GetGrid'); bust('AttendanceEvent/GetTimesheet'); };
  const asgText = a => a ? `${esc(a.employeeName || '')} · <span class="scode ${esc(a.shiftCode)}">${esc(a.shiftCode || '')}</span> ${U.date(a.workDate)}${a.plannedStart ? ' ' + U.time(a.plannedStart) + '–' + U.time(a.plannedEnd) : ''}${a.statusName ? ' · ' + esc(a.statusName) : ''}` : '';
  // Chế độ API: hủy đơn còn chờ duyệt (LeaveRequest/Cancel · ShiftSwapRequest/Cancel) – hủy luôn yêu cầu duyệt, backend kiểm tra người tạo.
  function cancelHr(leave, t, id, no) {
    UI.confirm('Hủy đơn <b>' + esc(no || '') + '</b>?<div class="small muted mt">Đơn đã hủy không khôi phục được.</div>', note => (leave
      ? Store.act('LeaveRequest/Cancel', { form: { Id: id, Reason: note }, tables: HR_TABLES(t) })
      : Store.act('ShiftSwapRequest/Cancel', { form: { Id: id, Reason: note }, tables: HR_TABLES(t) })).then(x => { if (x.ok) { hrChanged(); UI.toast('Đã hủy đơn', 'ok'); APP.refresh(); } }), { input: 'Lý do', required: true, danger: true, ok: 'Hủy đơn' });
  }
  // Duyệt / từ chối ngay trên đơn (cùng engine với Hộp phê duyệt) – chỉ hiện khi GetDetail báo tài khoản có quyền.
  function decideHr(leave, t, id, no, ok) {
    const form = note => ({ Id: id, Note: note }), opt = note => ({ form: form(note), tables: HR_TABLES(t) });
    UI.confirm(`${ok ? 'Duyệt' : 'Từ chối'} đơn <b>${esc(no || '')}</b>?`, note => (leave
      ? (ok ? Store.act('LeaveRequest/Approve', opt(note)) : Store.act('LeaveRequest/Reject', opt(note)))
      : (ok ? Store.act('ShiftSwapRequest/Approve', opt(note)) : Store.act('ShiftSwapRequest/Reject', opt(note)))).then(x => {
      if (!x.ok) return; hrChanged();
      const w = list(x.data && x.data.listWarning);
      UI.toast((ok ? 'Đã duyệt đơn' : 'Đã từ chối đơn') + (w.length ? '<br><span class="small">' + w.map(esc).join('<br>') + '</span>' : ''), ok && !w.length ? 'ok' : 'warn'); APP.refresh();
    }), { input: 'Ý kiến phê duyệt', required: !ok, ok: ok ? 'Duyệt' : 'Từ chối', danger: !ok });
  }
  // Tab "Xử lý đơn" trong ngăn chi tiết: LeaveRequest/GetDetail · ShiftSwapRequest/GetDetail (ca bị ảnh hưởng, bước duyệt đang chờ, cảnh báo, thao tác được phép).
  function loadHrDetail(leave, t, id, p) {
    (leave ? Store.query('LeaveRequest/GetDetail', { id }) : Store.query('ShiftSwapRequest/GetDetail', { id })).then(x => {
      if (!p.isConnected) return;
      if (!x.ok || !x.data) { p.innerHTML = `<div class="empty">${esc(x.message || 'Không tải được chi tiết đơn')}</div>`; return; }
      const d = x.data, rq = d.request || {}, pa = d.pendingApproval, acts = list(d.allowedActions), aff = list(d.listAffectedAssignment);
      const approval = [['Yêu cầu duyệt', esc(d.approvalRequestNo || (pa && pa.requestNo) || '')], pa && ['Bước đang chờ', `${esc(pa.stepName || '')} (${pa.stepNo}/${pa.stepCount})${pa.dueAt ? ' · hạn ' + esc(U.dt(pa.dueAt)) : ''}${pa.isOverdue ? ' <span class="tag red">Quá hạn</span>' : ''}`]];
      const info = leave
        ? [['Số đơn', esc(rq.requestNo || '')], ['Trạng thái', esc(d.statusName || '')], ['Nhân viên', esc(d.employeeName || '') + (d.employeeCode ? ' · ' + esc(d.employeeCode) : '')], ['Loại nghỉ', esc(d.leaveTypeName || rq.leaveTypeCode || '')], ['Thời gian nghỉ', `${U.date(rq.fromDate)} → ${U.date(rq.toDate)}${d.days ? ' (' + d.days + ' ngày)' : ''}`], ['Lý do', esc(rq.reason || '')]]
        : [['Số đơn', esc(rq.requestNo || '')], ['Trạng thái', esc(d.statusName || '')], ['Hình thức', d.swapType === 'GIVE' ? 'Nhường ca' : d.swapType === 'SWAP' ? 'Hoán đổi 2 ca' : esc(d.swapType || '')], ['Lý do', esc(rq.reason || '')], ['Người xin đổi', esc(d.requesterName || '')], ['Người nhận', esc(d.targetName || '')], ['Ca của người xin', asgText(d.currentAssignment)], ['Ca đề xuất đổi', asgText(d.proposedAssignment)]];
      p.innerHTML = warnBox(d.listWarning) + kv(info.concat(approval))
        + (leave && aff.length ? `<h3 class="mt">Ca trong khoảng nghỉ</h3><table class="table dc-table dt"><thead><tr><th>Ngày</th><th>Ca</th><th>Trạng thái</th></tr></thead><tbody>${aff.map(a => `<tr><td>${U.date(a.workDate)}</td><td><span class="scode ${esc(a.shiftCode)}">${esc(a.shiftCode || '')}</span></td><td>${a.isDeleted ? '<span class="tag">Đã bỏ theo đơn</span>' : esc(a.statusName || '')}</td></tr>`).join('')}</tbody></table>` : '')
        + (acts.length ? `<div class="row-flex mt">${acts.includes('cancel') ? '<button class="btn danger" data-x="cancel">Hủy đơn</button>' : ''}<span class="grow"></span>${acts.includes('reject') ? '<button class="btn danger" data-x="reject">Từ chối</button>' : ''}${acts.includes('approve') ? `<button class="btn primary" data-x="approve">${I('approve')} Duyệt</button>` : ''}</div>` : '');
      p.querySelectorAll('[data-x]').forEach(bt => bt.onclick = () => { const a = bt.dataset.x; closeDrawerOf(p); if (a === 'cancel') cancelHr(leave, t, id, rq.requestNo); else decideHr(leave, t, id, rq.requestNo, a === 'approve'); });
    });
  }
  ['hr-leave', 'hr-swap'].forEach(id => APP.register(id, (el, f) => {
    const t = f.table, leave = id === 'hr-leave';
    APP.genericList(el, f, { noTransit: true, noAdd: true, top: `<div class="card panel mb spread"><div><div class="panel-title">${leave ? 'Tạo đơn xin nghỉ' : 'Tạo đơn đổi ca'}</div><div class="panel-desc">${Store.isLive ? 'Đơn được gửi duyệt ngay sau khi tạo – người duyệt xử lý tại Hộp phê duyệt' : `Đơn sinh kèm ApprovalRequest (BusinessType = ${leave ? 'LEAVE_REQUEST' : 'SHIFT_SWAP'}) → duyệt tại Hộp phê duyệt`}</div></div><button class="btn primary" id="nReq">${I('plus')} Tạo đơn</button></div>` });
    // Chế độ API: LeaveRequest/Create · ShiftSwapRequest/Create (backend sinh số đơn + yêu cầu duyệt), hủy đơn chờ duyệt qua .../Cancel, chi tiết qua .../GetDetail.
    if (Store.isLive) {
      const open = rid => {
        APP.openDetail(t, rid, f, { noEdit: true, tabs: [{ label: 'Xử lý đơn', html: p => { loadHrDetail(leave, t, rid, p); return LOADING; } }] });
        const w = [...document.querySelectorAll('.drawer-wrap')].pop(), tb = w && w.querySelector('[data-tab="1"]'); if (tb) tb.click();   // mở thẳng tab xử lý đơn
      };
      APP.rowExtra(el, tr => {
        const r = Store.get(t, tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td) return;
        // Đơn chỉ lập qua Create (không sửa); chỉ xóa được đơn đã Từ chối / Hủy.
        td.querySelectorAll('[data-edit]').forEach(b => b.remove());
        if (!['Rejected', 'Cancelled'].includes(r.RequestStatus)) td.querySelectorAll('[data-del]').forEach(b => b.remove());
        tr.onclick = e => { if (e.target.closest('button,a,input,select')) return; open(r.Id); };
        const v = td.querySelector('[data-view]'); if (v) v.onclick = () => open(r.Id);
        if (r.RequestStatus !== 'Pending') return;
        td.insertAdjacentHTML('afterbegin', '<button class="btn xs danger" data-cx>Hủy đơn</button> '); td.querySelector('[data-cx]').onclick = () => cancelHr(leave, t, r.Id, r.RequestNo);
      });
      el.querySelector('#nReq').onclick = () => UI.form(t, null, { title: f.name, fields: leave ? ['EmployeeId', 'FromDate', 'ToDate', 'LeaveTypeCode', 'Reason'] : ['TargetEmployeeId', 'CurrentShiftAssignmentId', 'ProposedShiftAssignmentId', 'Reason'],
        validate: o => leave && o.FromDate && o.ToDate && o.ToDate < o.FromDate ? 'Đến ngày phải ≥ Từ ngày' : null,
        submit: (o, close) => (leave ? Store.act('LeaveRequest/Create', { form: o, tables: HR_TABLES(t) }) : Store.act('ShiftSwapRequest/Create', { form: o, tables: HR_TABLES(t) })).then(x => { if (!x.ok) return; close(); hrChanged(); UI.toast('Đã tạo đơn và gửi duyệt', 'ok'); APP.refresh(); }) });
      return;
    }
    el.querySelector('#nReq').onclick = () => UI.form(t, null, { title: f.name, defaults: { RequestNo: (leave ? 'NP' : 'DC') + '-2611-' + String(Store.rows(t).length + 1).padStart(3, '0'), RequestStatus: 'Pending', SubmittedAt: U.iso(new Date()) }, fields: leave ? ['RequestNo', 'EmployeeId', 'FromDate', 'ToDate', 'LeaveTypeCode', 'Reason'] : ['RequestNo', 'RequesterEmployeeId', 'TargetEmployeeId', 'CurrentShiftAssignmentId', 'ProposedShiftAssignmentId', 'Reason'],
      validate: o => leave && o.FromDate && o.ToDate && o.ToDate < o.FromDate ? 'Đến ngày phải ≥ Từ ngày' : null,
      onSave: row => { const ar = APP.submitApproval(leave ? 'LEAVE_REQUEST' : 'SHIFT_SWAP', t, row.Id, [[leave ? 'Quản lý bếp' : 'Bếp trưởng']], row.RequestNo); Store.update(t, row.Id, { ApprovalRequestId: ar.Id }); APP.refresh(); } });
  }));
})();
