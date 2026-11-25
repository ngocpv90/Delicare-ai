/* UI-6.37 (D-118): "Phân ca" (hr-shift-assign) theo tuần Thứ 2 – CN.
 * - Đầu màn: ◀ tuần ▶ (Tuần này / Tuần sau), nhãn trạng thái tuần, Sao chép tuần trước · Phân ca hàng loạt · Chốt & gửi lịch / Gửi N thay đổi.
 * - 4 chỉ số tuần (Ca đã phân · Ca thiếu người · Người chưa có ca · Ca chưa chốt); lọc đơn vị cây (APP.unitPicker), chức vụ, tìm.
 * - Lưới nhóm theo tổ: ô nháp viền đứt / đã chốt tô đặc / đã vào ca ✓ / vắng đỏ / phép / đơn nghỉ chờ … / đổi ca ⇄ / đổi sau chốt viền cam;
 *   cột cuối số ca / giờ tuần + cảnh báo; dòng tổng tổ theo ngày × ca "đã phân / cần" (thiếu đỏ).
 * - "Tô ca": chọn nút ca (hoặc Bỏ ca) rồi bấm / kéo qua ô – gom thay đổi gửi ShiftAssignment/AssignMany (≤ 1 giây sau thao tác cuối / khi thả chuột);
 *   ô bị từ chối trả lại như cũ + lý do. Không chọn nút tô → bấm ô mở hộp chọn ca. Ngày đã qua khóa.
 * - Điện thoại: xem theo ngày (◀ ngày ▶), mỗi ca một khối + danh sách người. Chế độ DEMO giữ màn cũ. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const st = { week: null, unit: '', pos: '', q: '', emp: '', paint: null, day: null };
  const WD = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
  const monday = d => { const x = new Date(d + 'T00:00'); const k = (x.getDay() + 6) % 7; return U.addDays(d, -k); };
  const today = () => U.day(Store.ctx.date || new Date());
  const hm = v => String(v || '').slice(11, 16);
  const tm = t => String(t || '').slice(0, 5);
  const base = APP.SCREENS['hr-shift-assign'];

  APP.register('hr-shift-assign', (el, f, p) => {
    if (!Store.isLive) return base(el, f, p);
    if (p && p.employee) { st.emp = String(p.employee).toLowerCase(); st.week = null; }
    if (p && p.date) st.week = monday(p.date);
    const wk = st.week || monday(today()), end = U.addDays(wk, 6), t0 = today();
    const re = () => APP.SCREENS['hr-shift-assign'](el, f, {});
    const canEdit = Store.perm('hr-shift-assign').edit !== false, canAdd = Store.perm('hr-shift-assign').add !== false;
    Promise.all([Store.ensure('OrganizationUnit'), Store.ensure('Position')]).then(() => {
      if (!el.isConnected) return;
      el.innerHTML = APP.header(f, `<span id="sfAct"></span>`, { noNote: true })
        + `<div class="row-flex mb" style="gap:8px;flex-wrap:wrap;align-items:center"><button class="btn sm" data-wk="-7" aria-label="Tuần trước">◀</button><b>Tuần ${U.date(wk).slice(0, 5)} – ${U.date(end)}</b><button class="btn sm" data-wk="7" aria-label="Tuần sau">▶</button>${wk !== monday(t0) ? '<button class="btn sm" data-wk="0">Tuần này</button>' : ''}${wk !== U.addDays(monday(t0), 7) ? '<button class="btn sm" data-wk="next">Tuần sau</button>' : ''}<span id="sfStatus"></span></div>
        <div id="sfKpi" class="kpis"></div>
        <div class="card panel"><div class="row-flex mb" style="gap:8px;flex-wrap:wrap" id="sfFilter"><span id="sfUnit"></span><select class="select" id="sfPos" style="width:auto"><option value="">Mọi chức vụ</option>${Store.rows('Position').filter(x => !x.IsDeleted).map(x => `<option value="${x.Id}" ${x.Id === st.pos ? 'selected' : ''}>${esc(x.Name || x.Code || '')}</option>`).join('')}</select><input class="input" id="sfQ" placeholder="Tìm tên, mã" value="${esc(st.q)}" style="width:200px">${st.emp ? '<button class="chip active" id="sfEmpX">Chỉ một nhân viên ✕</button>' : ''}</div>
        <div id="sfPaint" class="mb"></div><div id="sfBody"><div class="small muted">Đang tải…</div></div></div>`;
      el.querySelectorAll('[data-wk]').forEach(b => b.onclick = () => { const v = b.dataset.wk; st.week = v === '0' ? null : v === 'next' ? U.addDays(monday(t0), 7) : U.addDays(wk, +v); st.day = null; re(); });
      APP.unitPicker(el.querySelector('#sfUnit'), { value: st.unit, placeholder: 'Mọi tổ / đơn vị', onChange: v => { st.unit = v; load(); } });
      el.querySelector('#sfPos').onchange = e => { st.pos = e.target.value; load(); };
      let tq; el.querySelector('#sfQ').oninput = e => { clearTimeout(tq); tq = setTimeout(() => { st.q = e.target.value; load(); }, 300); };
      const ex = el.querySelector('#sfEmpX'); if (ex) ex.onclick = () => { st.emp = ''; re(); };
      let m = null, pending = new Map(), timer = null, down = false;
      const cellOf = (eid, d) => { const e = m && m.employees.find(x => x.id === eid); return e ? e.cells[d] || {} : {}; };
      const locked = (c, d) => d < t0 || c.hasAttendance || ['CheckedIn', 'Completed', 'Absent'].includes(c.status) || c.pendingSwap;
      const cellHtml = (e, d) => {
        const c = e.cells[d] || {}, past = d < t0, pend = pending.get(e.id + '|' + d);
        let txt = '', sty = 'border:1px dashed #c9cfc6;background:#fff', tip = [];
        if (pend !== undefined) { const s = m.shifts.find(x => x.id === pend); txt = s ? s.code : '—'; sty = 'border:1px dashed #7DA33C;background:#f6faef'; }
        else if (c.shiftCode) {
          txt = c.shiftCode + (c.hasAttendance || c.status === 'CheckedIn' || c.status === 'Completed' ? ' ✓' : '');
          sty = c.status === 'Absent' ? 'border:1px solid #e3a3a3;background:#fdecec;color:#b42318' : c.status === 'Planned' ? 'border:1px dashed #7DA33C;background:#fff' : 'border:1px solid #7DA33C;background:#eef6e8';
          tip.push(esc(c.statusName || ''), hm(c.plannedStart) + '–' + hm(c.plannedEnd));
        }
        if (c.onLeave) { txt = txt || 'Phép'; sty = 'border:1px solid #f2b98a;background:#fdf0e6;color:#8a3b0b'; tip.push('Nghỉ phép ' + esc(c.leaveRequestNo || '')); }
        if (c.leavePending) { txt += ' …'; tip.push('Đơn nghỉ chờ duyệt ' + esc(c.leaveRequestNo || '')); }
        if (c.pendingSwap) { txt += ' ⇄'; tip.push('Đang có đơn đổi ca'); }
        if (c.changedAfterConfirm) { sty += ';box-shadow:inset 0 0 0 2px #E8892B'; tip.push('Đổi sau khi chốt: ' + esc(c.changedFrom || '') + ' – chưa gửi'); }
        if (past) sty += ';opacity:.55';
        return `<td style="text-align:center;padding:3px"><div data-c="${e.id}|${d}" title="${tip.filter(Boolean).join(' · ')}" style="min-height:30px;border-radius:8px;padding:5px 2px;font-weight:700;font-size:12px;cursor:${past || !canEdit ? 'default' : 'pointer'};user-select:none;${sty}">${txt || (past ? '' : '<span class="muted" style="font-weight:400">+</span>')}</div></td>`;
      };
      const draw = () => {
        const body = el.querySelector('#sfBody'); if (!body || !m) return;
        const days = m.days, emps = m.employees.filter(e => !st.emp || e.id === st.emp);
        const groups = []; emps.forEach(e => { let g = groups.find(x => x.id === (e.organizationUnitId || '')); if (!g) groups.push(g = { id: e.organizationUnitId || '', name: e.organizationUnitName || 'Chưa có tổ', list: [] }); g.list.push(e); });
        const tot = (uid, d) => (m.unitTotals || []).filter(t => (t.unitId || '') === uid && t.day === d);
        const wide = `<div class="table-wrap" style="max-height:640px"><table class="table dc-table" style="min-width:980px"><thead><tr><th style="min-width:180px">Nhân viên</th>${days.map((d, i) => `<th style="text-align:center;${d === t0 ? 'color:#3F5A17' : ''}">${WD[i]} ${U.date(d).slice(0, 5)}${d < t0 ? '<div class="small muted">đã qua</div>' : ''}</th>`).join('')}<th style="text-align:center">Tuần</th></tr></thead><tbody>
          ${groups.map(g => `<tr><td colspan="${days.length + 2}" style="background:#f7f8f4"><b>${esc(g.name)}</b> <span class="small muted">${U.num(g.list.length)} người</span></td></tr>
            ${g.list.map(e => `<tr><td><a href="${APP.href('adm-employee-360?id=' + e.id)}"><b>${esc(e.fullName || '')}</b></a><div class="small muted">${esc([e.code, e.positionName].filter(Boolean).join(' · '))}</div></td>${days.map(d => cellHtml(e, d)).join('')}<td style="text-align:center"><b>${U.num(e.weekShiftCount || 0)}</b> ca<div class="small muted">${U.num(e.weekHours || 0, 1)} giờ</div>${(e.warnings || []).map(w => `<div class="small warn">${esc(w)}</div>`).join('')}</td></tr>`).join('')}
            <tr data-tot="${g.id}"><td class="small muted">Đã phân / cần</td>${days.map(d => `<td style="text-align:center">${tot(g.id, d).map(t => `<div class="small" style="font-weight:600;color:${t.required == null ? '#6b756e' : t.short ? '#b42318' : '#3F5A17'}" ${t.short ? 'data-short' : ''}>${esc(t.shiftCode || '')} ${U.num(t.assigned)}${t.required == null ? '' : '/' + U.num(t.required)}</div>`).join('') || '<span class="small muted">–</span>'}</td>`).join('')}<td></td></tr>`).join('')}
          </tbody></table></div>`;
        const day = st.day && days.includes(st.day) ? st.day : days.includes(t0) ? t0 : days[0], di = days.indexOf(day);
        const narrow = `<div class="row-flex mb" style="gap:6px"><button class="btn sm" data-dd="-1" ${di <= 0 ? 'disabled' : ''}>◀</button><b>${WD[di]} ${U.date(day)}</b><button class="btn sm" data-dd="1" ${di >= 6 ? 'disabled' : ''}>▶</button></div>
          ${m.shifts.map(s => { const ppl = emps.filter(e => (e.cells[day] || {}).shiftId === s.id), req = (m.unitTotals || []).filter(t => t.day === day && t.shiftId === s.id && t.required != null).reduce((a, t) => a + t.required, 0);
            return `<div class="card" style="padding:10px;margin-bottom:8px"><div class="spread"><b>${esc(s.code)} ${tm(s.startTime)}–${tm(s.endTime)}</b><span class="${req && ppl.length < req ? 'bad' : 'muted'} small">${U.num(ppl.length)}${req ? '/' + U.num(req) : ''} người</span></div>${ppl.map(e => `<div class="small">${esc(e.fullName || '')} <span class="muted">${esc(e.organizationUnitName || '')}</span>${(e.cells[day] || {}).status === 'Planned' ? ' <span class="muted">(nháp)</span>' : ''}</div>`).join('') || '<div class="small muted">Chưa có người</div>'}</div>`; }).join('')}`;
        body.innerHTML = '<style>.sf-n{display:none}@media (max-width:700px){.sf-w{display:none}.sf-n{display:block}}</style>' + `<div class="sf-w">${emps.length ? wide : '<div class="empty">Không có nhân viên khớp bộ lọc.</div>'}</div><div class="sf-n">${narrow}</div>`;
        body.querySelectorAll('[data-dd]').forEach(b => b.onclick = () => { st.day = days[di + +b.dataset.dd]; draw(); });
        if (!canEdit) return;
        body.querySelectorAll('[data-c]').forEach(c => {
          const [eid, d] = c.dataset.c.split('|');
          c.onmousedown = ev => { if (st.paint === null) return; ev.preventDefault(); down = true; paintCell(eid, d); };
          c.onmouseenter = () => { if (down && st.paint !== null) paintCell(eid, d); };
          c.onclick = () => { if (st.paint !== null) return; const cell = cellOf(eid, d); if (d < t0) return UI.toast('Ngày đã qua – không phân ca', 'warn'); if (locked(cell, d)) return UI.toast('Ca đã chấm công / đang đổi ca – dùng Đơn đổi ca', 'warn'); detail(eid, d); };
        });
      };
      const paintCell = (eid, d) => {
        const c = cellOf(eid, d); if (d < t0 || locked(c, d) || c.onLeave) return;
        const v = st.paint || ''; if ((c.shiftId || '') === v && !pending.has(eid + '|' + d)) return;
        pending.set(eid + '|' + d, v); draw(); clearTimeout(timer); timer = setTimeout(flush, 900);
      };
      const flush = () => {
        clearTimeout(timer); if (!pending.size) return;
        const cells = [...pending.entries()].map(([k, v]) => { const [employeeId, workDate] = k.split('|'); return { employeeId, workDate, shiftId: v || null }; });
        pending = new Map();
        Store.act('ShiftAssignment/AssignMany', { json: { listCell: cells }, method: 'PUT', tables: ['ShiftAssignment'], quiet: true }).then(r => {
          if (!r.ok) { UI.toast(esc(r.message || 'Không lưu được'), 'err'); return load(); }
          const bad = ((r.data && r.data.listResult) || []).filter(x => !x.ok);
          if (bad.length) UI.toast(`Lưu ${U.num(r.data.done)} ô · ${U.num(bad.length)} ô không đổi được:<br><span class="small">${bad.slice(0, 4).map(x => esc(x.message || '')).join('<br>')}</span>`, 'warn');
          load();
        });
      };
      document.onmouseup = () => { if (down) { down = false; flush(); } };
      const detail = (eid, d) => {
        const e = m.employees.find(x => x.id === eid), c = cellOf(eid, d);
        UI.modal({ title: 'Phân ca – ' + esc(e.fullName || ''), sub: WD[m.days.indexOf(d)] + ' ' + U.date(d), size: 'sm',
          body: `<div class="grid">${m.shifts.map(s => `<button class="btn ${c.shiftId === s.id ? 'primary' : ''}" data-s="${s.id}"><b>${esc(s.code)}</b> ${esc(s.name || '')} <span class="small muted">${tm(s.startTime)}–${tm(s.endTime)}</span></button>`).join('')}${c.shiftId ? '<button class="btn danger" data-s="">Bỏ ca</button>' : ''}</div>${c.shiftId ? `<div class="small muted mt">Trạng thái: ${esc(c.statusName || '')}${c.status === 'Confirmed' ? ' – đổi / bỏ sẽ cần bấm "Gửi thay đổi" để báo nhân viên' : ''}</div>` : ''}${c.leavePending ? `<div class="small warn mt">Có đơn nghỉ chờ duyệt ${esc(c.leaveRequestNo || '')}</div>` : ''}`,
          onMount: (md, close) => md.querySelectorAll('[data-s]').forEach(b => b.onclick = () => { close(); pending.set(eid + '|' + d, b.dataset.s); flush(); }) });
      };
      const paintBar = () => {
        const bar = el.querySelector('#sfPaint'); if (!canEdit || !m) { bar.innerHTML = ''; return; }
        bar.innerHTML = `<div class="row-flex" style="gap:6px;flex-wrap:wrap;align-items:center"><span class="small muted">Tô ca:</span>${m.shifts.map(s => `<button class="chip ${st.paint === s.id ? 'active' : ''}" data-p="${s.id}"><b>${esc(s.code)}</b> ${tm(s.startTime)}–${tm(s.endTime)}</button>`).join('')}<button class="chip ${st.paint === '' ? 'active' : ''}" data-p="">Bỏ ca</button>${st.paint !== null ? '<button class="chip" data-p="off">Thôi tô</button><span class="small muted">Bấm hoặc kéo qua các ô</span>' : '<span class="small muted">Chọn một ca rồi bấm / kéo qua nhiều ô; không chọn thì bấm ô để mở hộp chọn ca</span>'}</div>`;
        bar.querySelectorAll('[data-p]').forEach(b => b.onclick = () => { st.paint = b.dataset.p === 'off' ? null : b.dataset.p; paintBar(); });
      };
      const load = () => Store.query('ShiftAssignment/GetGrid', { WeekStart: wk, OrganizationUnitId: st.unit || null, PositionId: st.pos || null, SearchText: st.q || null }).then(x => {
        if (!el.isConnected) return;
        if (!x.ok) { el.querySelector('#sfBody').innerHTML = `<div class="empty">${esc(x.message || 'Không tải được lưới ca')}</div>`; return; }
        m = x.data || {}; m.employees = m.employees || []; m.shifts = m.shifts || [];
        const ws = m.weekStatus || {}, k = m.kpi || {};
        el.querySelector('#sfStatus').innerHTML = !ws.assignedCount ? UI.tag('Chưa phân ca', '') : ws.changedAfterConfirmCount ? UI.tag(`Có ${U.num(ws.changedAfterConfirmCount)} thay đổi chưa gửi`, 'orange') : ws.draftCount ? UI.tag(`Nháp · ${U.num(ws.draftCount)} ca chưa chốt`, '') : UI.tag('Đã chốt & gửi', 'green');
        el.querySelector('#sfAct').innerHTML = (canAdd ? `<button class="btn" id="sfCopy">${I('copy')} Sao chép tuần trước</button><button class="btn" id="sfBulk">${I('plus')} Phân ca hàng loạt</button>` : '')
          + (canEdit ? `<button class="btn primary" id="sfSend">${I('ok')} ${ws.changedAfterConfirmCount && !ws.draftCount ? 'Gửi ' + U.num(ws.changedAfterConfirmCount) + ' thay đổi' : 'Chốt & gửi lịch'}</button>` : '');
        const kp = (l, v, s, i, c, a) => UI.kpi(l, v, s, i, c).replace('<article ', a ? `<article data-k="${a}" style="cursor:pointer" ` : '<article ');
        el.querySelector('#sfKpi').innerHTML = kp('Ca đã phân (tuần)', U.num(k.assigned || 0), '', 'calendar', 'blue') + kp('Ca thiếu người', U.num(k.shortSlots || 0), k.shortSlots ? 'bấm để xem' : '', 'alert', k.shortSlots ? 'red' : 'green', 'short')
          + kp('Người chưa có ca', U.num(k.peopleWithoutShift || 0), 'từ hôm nay', 'users', 'orange') + kp('Ca chưa chốt', U.num(k.draft || 0), '', 'clock', 'purple');
        const ks = el.querySelector('[data-k="short"]'); if (ks) ks.onclick = () => { const r = el.querySelector('[data-short]'); if (r) r.scrollIntoView({ behavior: 'smooth', block: 'center' }); };
        const cp = el.querySelector('#sfCopy'); if (cp) cp.onclick = () => UI.confirm(`Chép lịch tuần ${U.date(U.addDays(wk, -7)).slice(0, 5)} – ${U.date(U.addDays(wk, -1)).slice(0, 5)} sang tuần này${st.unit ? ' (tổ đang lọc)' : ''}? Chỉ điền ô trống; bỏ qua ngày đã qua, ngày nghỉ phép, người đã nghỉ việc.`,
          () => Store.act('ShiftAssignment/CopyWeek', { json: { fromWeekStart: U.addDays(wk, -7), toWeekStart: wk, organizationUnitId: st.unit || null }, tables: ['ShiftAssignment'] }).then(r => { if (!r.ok) return; const d = r.data || {}; UI.toast(esc(r.res && r.res.responseMessage || ''), d.skipped ? 'warn' : 'ok'); skippedDlg(d.listSkipped); load(); }), { ok: 'Sao chép' });
        const bk = el.querySelector('#sfBulk'); if (bk) bk.onclick = () => bulk(m, wk, load);
        const sd = el.querySelector('#sfSend'); if (sd) sd.onclick = () => UI.confirm(`Chốt lịch tuần ${U.date(wk).slice(0, 5)} – ${U.date(end)}${st.unit ? ' của tổ đang lọc' : ''}: ca nháp → đã chốt, gửi lịch qua bot cho từng người${ws.changedAfterConfirmCount ? `; ${U.num(ws.changedAfterConfirmCount)} ô đổi sau chốt được báo riêng (ca cũ → ca mới / đã hủy)` : ''}.`,
          () => Store.act('ShiftAssignment/Confirm', { form: Object.assign({ WorkDate: wk, ToDate: end }, st.unit ? { OrganizationUnitId: st.unit } : {}), tables: ['ShiftAssignment'] }).then(r => { if (!r.ok) return; const d = r.data || {}; UI.toast(`Đã chốt ${U.num(d.confirmed || 0)} ca${d.changedNotified ? ` · báo thay đổi cho ${U.num(d.changedNotified)} người` : ''}`, 'ok'); load(); }), { ok: 'Chốt & gửi' });
        paintBar(); draw();
      });
      load();
    });
  });
  function skippedDlg(sk) {
    sk = sk || []; if (!sk.length) return;
    UI.modal({ title: 'Các ô không được phân', sub: `${sk.length} ô giữ nguyên`, size: 'lg', body: `<div class="table-wrap" style="max-height:420px"><table class="table dc-table"><thead><tr><th>Nhân viên</th><th>Ngày</th><th>Lý do</th></tr></thead><tbody>${sk.map(x => `<tr><td>${esc(x.employeeName || '')}</td><td>${U.date(x.workDate)}</td><td>${esc(x.reason || '')}</td></tr>`).join('')}</tbody></table></div>` });
  }
  function bulk(m, wk, done) {
    const t0 = today(), from = wk < t0 ? t0 : wk, to = U.addDays(wk, 6) < from ? from : U.addDays(wk, 6), emps = m.employees;
    const units = [...new Map(emps.filter(e => e.organizationUnitId).map(e => [e.organizationUnitId, e.organizationUnitName])).entries()];
    const poss = [...new Map(emps.filter(e => e.positionId).map(e => [e.positionId, e.positionName])).entries()];
    UI.modal({ title: 'Phân ca hàng loạt', sub: 'Ô đã có ca hoặc ngày nghỉ phép được giữ nguyên', size: 'lg',
      body: `<div class="form-grid"><div class="field"><label>Ca *</label><select class="select" id="bS"><option value="">— Chọn ca —</option>${m.shifts.map(s => `<option value="${s.id}">${esc(s.code)} · ${esc(s.name || '')} (${tm(s.startTime)}–${tm(s.endTime)})</option>`).join('')}</select></div>
        <div class="field"><label>Không phân vào các thứ</label><div class="row-flex">${WD.map((l, i) => `<label class="small"><input type="checkbox" data-wd="${i + 1}"> ${l}</label>`).join('')}</div></div>
        <div class="field"><label>Từ ngày *</label><input type="date" class="input" id="bF" value="${from}" min="${t0}"></div><div class="field"><label>Đến ngày *</label><input type="date" class="input" id="bT" value="${to}" min="${t0}"></div>
        <div class="field full"><label>Chọn nhanh</label><div class="row-flex" style="gap:6px;flex-wrap:wrap">${units.map(([id, n]) => `<button class="chip" data-gu="${id}">Cả tổ ${esc(n || '')}</button>`).join('')}${poss.map(([id, n]) => `<button class="chip" data-gp="${id}">Tất cả ${esc(String(n || '').toLowerCase())}</button>`).join('')}<button class="chip" data-gx>Bỏ chọn</button></div></div>
        <div class="field full"><label>Nhân viên * · <span id="bCnt">0</span>/${emps.length} đã chọn</label><input class="input mb" id="bQ" placeholder="Tìm tên"><div class="table-wrap" style="max-height:240px;padding:8px 12px">${emps.map(e => `<label style="display:block;margin:4px 0" data-bn="${esc((e.fullName || '').toLowerCase())}"><input type="checkbox" data-be="${e.id}" data-u="${e.organizationUnitId || ''}" data-p="${e.positionId || ''}"> ${esc(e.fullName || '')} <span class="small muted">${esc([e.positionName, e.organizationUnitName].filter(Boolean).join(' · '))}</span></label>`).join('')}</div></div></div>`,
      foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="bOk">${I('ok')} Phân ca</button>`,
      onMount: (md, close) => {
        const boxes = () => [...md.querySelectorAll('[data-be]')], count = () => { md.querySelector('#bCnt').textContent = boxes().filter(x => x.checked).length; };
        boxes().forEach(x => x.onchange = count);
        md.querySelectorAll('[data-gu]').forEach(b => b.onclick = () => { boxes().forEach(x => { if (x.dataset.u === b.dataset.gu) x.checked = true; }); count(); });
        md.querySelectorAll('[data-gp]').forEach(b => b.onclick = () => { boxes().forEach(x => { if (x.dataset.p === b.dataset.gp) x.checked = true; }); count(); });
        md.querySelector('[data-gx]').onclick = () => { boxes().forEach(x => { x.checked = false; }); count(); };
        md.querySelector('#bQ').oninput = e => { const k = e.target.value.trim().toLowerCase(); md.querySelectorAll('[data-bn]').forEach(l => { l.style.display = !k || l.dataset.bn.includes(k) ? 'block' : 'none'; }); };
        md.querySelector('#bOk').onclick = () => {
          const sid = md.querySelector('#bS').value, f = md.querySelector('#bF').value, t = md.querySelector('#bT').value, ids = boxes().filter(x => x.checked).map(x => x.dataset.be), skip = [...md.querySelectorAll('[data-wd]:checked')].map(x => x.dataset.wd);
          if (!sid) return UI.toast('Chọn ca', 'err'); if (!f || !t || t < f) return UI.toast('Đến ngày phải từ Từ ngày', 'err'); if (!ids.length) return UI.toast('Chọn nhân viên', 'err');
          const fd = new FormData(); ids.forEach(v => fd.append('EmployeeIds', v)); fd.append('ShiftId', sid); fd.append('FromDate', f); fd.append('ToDate', t); skip.forEach(v => fd.append('SkipWeekdays', v));
          Store.act('ShiftAssignment/BulkAssign', { json: fd, tables: ['ShiftAssignment'] }).then(r => { if (!r.ok) return; close(); const d = r.data || {}; UI.toast(`Đã phân ${U.num(d.created || 0)} ca` + (d.skipped ? `, bỏ qua ${U.num(d.skipped)} ô` : ''), d.skipped ? 'warn' : 'ok'); skippedDlg(d.listSkipped); done(); });
        };
      } });
  }
})();
