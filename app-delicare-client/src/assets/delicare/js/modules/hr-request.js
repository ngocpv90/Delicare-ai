/* UI-6.39 (D-120): "Nghỉ & đổi ca" (hr-leave; hr-swap ẩn – link cũ mở màn này lọc Đổi ca).
 * - 4 chỉ số (HrRequest/GetSummary): Chờ duyệt (quá hạn · nghỉ · đổi) · Nghỉ hôm nay · Nghỉ 7 ngày tới · Ca trống do nghỉ (bấm → Phân ca).
 * - Tab Đơn (HrRequest/GetList): chip trạng thái + loại + tìm; cột Ảnh hưởng; Xem / Duyệt (hộp duyệt có phép năm, người nghỉ trùng, tổ còn x / y, người có thể thay,
 *   "Duyệt xong mở Phân ca"), Hủy, Đồng ý / Từ chối (người nhận ca).
 * - Tab Lịch nghỉ (HrRequest/GetCalendar): 2 tuần, nghỉ đã duyệt / chờ / đổi ca, ngày tổ thiếu người.
 * - "Tạo đơn" 3 loại: Xin nghỉ (xem trước PreviewLeave) · Đổi ca (thẻ ca của tôi → thẻ ca đồng nghiệp – GetSwapCandidates) · Nhường ca (→ người cùng tổ đang trống).
 * Chế độ DEMO giữ màn cũ. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const st = { tab: 'list', status: 'PENDING', type: '', q: '', from: null };
  const ref = n => (Store.refList ? Store.refList(n) : []) || [];
  const hm = v => String(v || '').slice(11, 16);
  const dmy = v => (v ? U.date(v) : '');
  const T = ['LeaveRequest', 'ShiftSwapRequest', 'ShiftAssignment', 'ApprovalRequest'];
  const base = APP.SCREENS['hr-leave'];

  APP.register('hr-leave', (el, f, p) => {
    if (!Store.isLive) return base(el, f, p);
    if (p && ['LEAVE', 'SWAP', 'GIVE'].includes(p.type)) st.type = p.type;
    if (p && p.tab === 'cal') st.tab = 'cal';
    const re = () => APP.SCREENS['hr-leave'](el, f, {});
    el.innerHTML = APP.header(f, `<button class="btn primary" id="rqNew">${I('plus')} Tạo đơn</button>`, { noNote: true })
      + `<div id="rqKpi" class="kpis"></div><div class="mb"><div class="seg" style="display:inline-flex">${[['list', 'Đơn'], ['cal', 'Lịch nghỉ']].map(([k, l]) => `<button class="seg-b ${st.tab === k ? 'on' : ''}" data-rt="${k}">${l}</button>`).join('')}</div></div><div id="rqBody"><div class="card panel small muted">Đang tải…</div></div>`;
    el.querySelector('#rqNew').onclick = () => createDlg(re);
    el.querySelectorAll('[data-rt]').forEach(b => b.onclick = () => { st.tab = b.dataset.rt; re(); });
    Store.query('HrRequest/GetSummary').then(x => {
      const k = el.querySelector('#rqKpi'); if (!k || !x.ok) { if (k) k.innerHTML = ''; return; }
      const d = x.data || {}, off = d.listOffToday || [];
      k.innerHTML = UI.kpi('Chờ duyệt', U.num(d.pending || 0), `${d.pendingOverdue ? U.num(d.pendingOverdue) + ' quá hạn · ' : ''}${U.num(d.pendingLeave || 0)} nghỉ · ${U.num(d.pendingSwap || 0)} đổi ca`, 'clock', d.pendingOverdue ? 'red' : 'orange')
        + UI.kpi('Nghỉ hôm nay', U.num(off.length), off.slice(0, 3).map(esc).join(', '), 'users', 'blue')
        + UI.kpi('Nghỉ 7 ngày tới', `${U.num(d.offNext7People || 0)} người`, `${U.num(d.offNext7Days || 0)} ngày công`, 'calendar', 'purple')
        + UI.kpi('Ca trống do nghỉ', U.num(d.emptyShifts || 0), d.emptyShifts ? 'bấm để phân người thay' : '', 'alert', d.emptyShifts ? 'red' : 'green').replace('<article ', d.emptyShifts ? `<article data-empty="${U.day((d.listEmptyShiftDay || [])[0])}" style="cursor:pointer" ` : '<article ');
      const em = k.querySelector('[data-empty]'); if (em) em.onclick = () => APP.go('hr-shift-assign?date=' + em.dataset.empty);
    });
    if (st.tab === 'cal') return calendar(el.querySelector('#rqBody'), re);
    list(el.querySelector('#rqBody'), re);
  });

  function list(body, re) {
    const chips = (arr, cur, attr) => arr.map(([k, l]) => `<button class="chip ${cur === k ? 'active' : ''}" ${attr}="${k}">${l}</button>`).join(' ');
    body.innerHTML = `<div class="card panel"><div class="row-flex mb" style="gap:8px;flex-wrap:wrap">${chips([['PENDING', 'Chờ duyệt'], ['APPROVED', 'Đã duyệt'], ['CLOSED', 'Từ chối / hủy'], ['ALL', 'Tất cả']], st.status, 'data-rs')} <span class="muted">·</span> ${chips([['', 'Mọi loại'], ['LEAVE', 'Xin nghỉ'], ['SWAP', 'Đổi ca'], ['GIVE', 'Nhường ca']], st.type, 'data-ty')}<input class="input" id="rqQ" placeholder="Tìm người, số đơn" value="${esc(st.q)}" style="width:200px"></div><div id="rqList"><div class="small muted">Đang tải…</div></div></div>`;
    body.querySelectorAll('[data-rs]').forEach(b => b.onclick = () => { st.status = b.dataset.rs; list(body, re); });
    body.querySelectorAll('[data-ty]').forEach(b => b.onclick = () => { st.type = b.dataset.ty; list(body, re); });
    let tq; body.querySelector('#rqQ').oninput = e => { clearTimeout(tq); tq = setTimeout(() => { st.q = e.target.value; load(); }, 300); };
    const load = () => Store.query('HrRequest/GetList', { status: st.status, type: st.type || null, keyword: st.q || null }).then(x => {
      const box = body.querySelector('#rqList'); if (!box) return;
      if (!x.ok) { box.innerHTML = `<div class="empty">${esc(x.message || 'Không tải được')}</div>`; return; }
      const rows = x.list || [];
      const act = r => [r.allowedActions.includes('consent') ? `<button class="btn xs primary" data-cs="${r.id}">Đồng ý</button><button class="btn xs" data-cn="${r.id}">Từ chối</button>` : '',
        `<button class="btn xs" data-op="${r.id}|${r.type}">${['Pending'].includes(r.status) ? 'Xem / duyệt' : 'Xem'}</button>`, r.allowedActions.includes('cancel') ? `<button class="btn xs" data-cx="${r.id}|${r.type}">Hủy đơn</button>` : ''].join(' ');
      box.innerHTML = rows.length ? `<style>.rq-n{display:none}@media (max-width:700px){.rq-w{display:none}.rq-n{display:block}}</style><div class="rq-w table-wrap"><table class="table dc-table"><thead><tr><th>Đơn</th><th>Loại</th><th>Người</th><th>Nội dung</th><th>Ảnh hưởng</th><th>Gửi lúc</th><th>Trạng thái</th><th></th></tr></thead><tbody>${rows.map(r => `<tr><td>${esc(r.requestNo || '')}</td><td>${UI.tag(r.typeName || '', r.type === 'LEAVE' ? 'blue' : 'purple')}</td><td><b>${esc(r.employeeName || '')}</b><div class="small muted">${esc(r.unitName || '')}</div></td><td style="white-space:normal;max-width:320px">${esc(r.content || '')}</td><td class="small" style="white-space:normal;max-width:240px">${(r.listImpact || []).map(i => `<div class="${/thiếu|Vượt|chưa/.test(i) ? 'warn' : ''}">${esc(i)}</div>`).join('') || '–'}</td><td class="${r.overdue ? 'bad' : ''}">${r.submittedAt ? U.dt(r.submittedAt) : ''}</td><td>${UI.tag(r.statusName || '', r.status === 'Approved' ? 'green' : r.status === 'Pending' || r.status === 'AwaitingConsent' ? 'orange' : '')}</td><td class="actions" style="white-space:nowrap">${act(r)}</td></tr>`).join('')}</tbody></table></div>
        <div class="rq-n">${rows.map(r => `<div class="card" style="padding:10px;margin-bottom:8px"><div class="spread"><b>${esc(r.employeeName || '')}</b>${UI.tag(r.statusName || '', r.status === 'Approved' ? 'green' : 'orange')}</div><div class="small">${esc(r.typeName || '')} · ${esc(r.content || '')}</div>${(r.listImpact || []).map(i => `<div class="small warn">${esc(i)}</div>`).join('')}<div class="row-flex mt" style="gap:6px">${act(r)}</div></div>`).join('')}</div>` : '<div class="empty">Không có đơn.</div>';
      box.querySelectorAll('[data-op]').forEach(b => b.onclick = () => { const [id, type] = b.dataset.op.split('|'); type === 'LEAVE' ? leaveDlg(id, re) : swapDlg(id, re); });
      box.querySelectorAll('[data-cx]').forEach(b => b.onclick = () => { const [id, type] = b.dataset.cx.split('|'); UI.confirm('Hủy đơn này?', note => Store.act(type === 'LEAVE' ? 'LeaveRequest/Cancel' : 'ShiftSwapRequest/Cancel', { form: { Id: id, Reason: note }, tables: T }).then(r => { if (r.ok) { UI.toast('Đã hủy đơn', 'ok'); re(); } }), { ok: 'Hủy đơn', danger: true, input: 'Lý do hủy' }); });
      box.querySelectorAll('[data-cs],[data-cn]').forEach(b => b.onclick = () => APP.hrConsent(b.dataset.cs || b.dataset.cn, !!b.dataset.cs, re));
    });
    load();
  }
  function impactHtml(m, withRepl) {
    if (!m) return '';
    const annual = m.isAnnual ? `<div class="kv"><div><label>Phép năm còn</label><span>${U.num(m.annualRemaining)} / ${U.num(m.annualTotal)} ngày</span></div><div><label>Sau đơn</label><span class="${m.annualOver ? 'bad' : ''}">${U.num(m.annualAfter)} ngày</span></div></div>` : `<div class="small muted">Phép năm còn ${U.num(m.annualRemaining)} / ${U.num(m.annualTotal)} ngày (loại nghỉ này không trừ phép năm).</div>`;
    const drop = (m.listShiftDrop || []).length ? `<div class="notice ${(m.listStaffing || []).some(s => s.short) ? 'warn' : 'info'} mt">${I('alert')}<div>Duyệt đơn sẽ bỏ ${U.num(m.listShiftDrop.length)} ca: ${(m.listStaffing || []).map(s => `${esc(s.shiftCode || '')} ${dmy(s.day).slice(0, 5)} – tổ còn ${U.num(s.remaining)}${s.required != null ? ' / cần ' + U.num(s.required) : ''}${s.short ? ' (thiếu)' : ''}`).join('; ')}</div></div>` : '<div class="small muted mt">Không có ca nào bị bỏ.</div>';
    const ov = (m.listOverlap || []).length ? `<div class="mt small"><b style="display:inline">Người cùng tổ nghỉ trùng:</b> ${m.listOverlap.map(o => `${esc(o.fullName || '')} (${dmy(o.fromDate).slice(0, 5)}–${dmy(o.toDate).slice(0, 5)}, ${esc(o.statusName || '')})`).join('; ')}</div>` : '';
    const rp = withRepl && (m.listReplacement || []).length ? `<div class="mt small"><b style="display:inline">Người có thể thay:</b> ${m.listReplacement.map(r => `${esc(r.shiftCode || '')} ${dmy(r.day).slice(0, 5)}: ${(r.listPerson || []).map(x => esc(x.fullName || '')).join(', ') || 'không có'}`).join(' · ')}</div>` : '';
    return annual + drop + ov + rp + (m.listWarning || []).filter(w => !/tổ còn/.test(w)).map(w => `<div class="small warn mt">${esc(w)}</div>`).join('');
  }
  function leaveDlg(id, re) {
    Store.query('LeaveRequest/GetDetail', { id }).then(x => {
      if (!x.ok) return UI.toast(esc(x.message || 'Không tải được đơn'), 'err');
      const d = x.data || {}, r = d.request || {}, can = (d.allowedActions || []).includes('approve'), drops = ((d.impact || {}).listShiftDrop || []).length;
      UI.modal({ title: 'Đơn nghỉ ' + esc(r.requestNo || ''), sub: `${esc(d.employeeName || '')} · ${esc(d.leaveTypeName || '')} · ${dmy(r.fromDate)} – ${dmy(r.toDate)} (${U.num(d.days)} ngày) · ${esc(d.statusName || '')}`, size: 'md',
        body: `<div class="small mb">Lý do: ${esc(r.reason || '')}</div>${impactHtml(d.impact, true)}${can ? `<div class="field mt"><label>Ý kiến (bắt buộc khi từ chối)</label><input class="input" id="lvN"></div>${drops ? '<label class="row-flex mt" style="gap:6px"><input type="checkbox" id="lvGo" checked> Duyệt xong mở Phân ca</label>' : ''}` : ''}`,
        foot: `<button class="btn" data-close>Đóng</button>${can ? '<button class="btn danger" id="lvRj">Từ chối</button><button class="btn primary" id="lvOk">Duyệt</button>' : ''}`,
        onMount: (m, close) => {
          if (!can) return;
          const dec = ok => { const note = m.querySelector('#lvN').value.trim(); if (!ok && note.length < 2) return UI.toast('Ghi ý kiến khi từ chối', 'err');
            Store.act(ok ? 'LeaveRequest/Approve' : 'LeaveRequest/Reject', { form: { Id: id, Note: note }, tables: T }).then(z => { if (!z.ok) return; close(); UI.toast(ok ? 'Đã duyệt đơn nghỉ' : 'Đã từ chối', 'ok'); const g = m.querySelector('#lvGo'); if (ok && g && g.checked) APP.go('hr-shift-assign?date=' + U.day(r.fromDate)); else re(); }); };
          m.querySelector('#lvOk').onclick = () => dec(true); m.querySelector('#lvRj').onclick = () => dec(false);
        } });
    });
  }
  function swapDlg(id, re) {
    Store.query('ShiftSwapRequest/GetDetail', { id }).then(x => {
      if (!x.ok) return UI.toast(esc(x.message || 'Không tải được đơn'), 'err');
      const d = x.data || {}, r = d.request || {}, can = (d.allowedActions || []).includes('approve'), c = d.currentAssignment || {}, pr = d.proposedAssignment;
      UI.modal({ title: (d.swapType === 'GIVE' ? 'Nhường ca ' : 'Đổi ca ') + esc(r.requestNo || ''), sub: esc(d.statusName || ''), size: 'md',
        body: `<div class="kv"><div><label>Người xin</label><span>${esc(d.requesterName || '')}: ca ${esc(c.shiftCode || '')} ${dmy(c.workDate)} ${hm(c.plannedStart)}–${hm(c.plannedEnd)}</span></div><div><label>Người nhận</label><span>${esc(d.targetName || '')}${pr ? `: ca ${esc(pr.shiftCode || '')} ${dmy(pr.workDate)} ${hm(pr.plannedStart)}–${hm(pr.plannedEnd)}` : ''}</span></div></div><div class="small mt">Lý do: ${esc(r.reason || '')}</div>${r.requestStatus === 'AwaitingConsent' ? '<div class="notice info mt">Đang chờ người nhận đồng ý (bot / Việc của tôi) – chưa chuyển người duyệt.</div>' : ''}${(d.listWarning || []).map(w => `<div class="small warn mt">${esc(w)}</div>`).join('')}${can ? '<div class="field mt"><label>Ý kiến (bắt buộc khi từ chối)</label><input class="input" id="swN"></div>' : ''}`,
        foot: `<button class="btn" data-close>Đóng</button>${can ? '<button class="btn danger" id="swRj">Từ chối</button><button class="btn primary" id="swOk">Duyệt</button>' : ''}`,
        onMount: (m, close) => { if (!can) return; const dec = ok => { const note = m.querySelector('#swN').value.trim(); if (!ok && note.length < 2) return UI.toast('Ghi ý kiến khi từ chối', 'err'); Store.act(ok ? 'ShiftSwapRequest/Approve' : 'ShiftSwapRequest/Reject', { form: { Id: id, Note: note }, tables: T }).then(z => { if (!z.ok) return; close(); UI.toast(ok ? 'Đã duyệt' : 'Đã từ chối', 'ok'); re(); }); };
          m.querySelector('#swOk').onclick = () => dec(true); m.querySelector('#swRj').onclick = () => dec(false); } });
    });
  }
  function calendar(body, re) {
    const from = st.from || (() => { const d = new Date(U.day(Store.ctx.date) + 'T00:00'); const k = (d.getDay() + 6) % 7; return U.addDays(U.day(Store.ctx.date), -k); })();
    Store.query('HrRequest/GetCalendar', { from, to: U.addDays(from, 13) }).then(x => {
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được')}</div>`; return; }
      const days = (x.data && x.data.listDay) || [], WD = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
      const ent = e => { const sty = e.kind === 'LEAVE_APPROVED' ? 'background:#eef6e8' : e.kind === 'LEAVE_PENDING' ? 'border:1px dashed #E8892B;background:#fff' : 'background:#f1ecfb'; return `<div class="small" style="border-radius:6px;padding:2px 5px;margin-top:3px;${sty}">${esc(e.fullName || '')}<span class="muted"> · ${esc(e.detail || '')}</span></div>`; };
      body.innerHTML = `<div class="card panel"><div class="row-flex mb" style="gap:8px"><button class="btn sm" data-cw="-14">◀</button><b>${dmy(from).slice(0, 5)} – ${dmy(U.addDays(from, 13))}</b><button class="btn sm" data-cw="14">▶</button><span class="small muted">Xanh = đã duyệt · viền cam = chờ duyệt · tím = đổi / nhường ca · viền đỏ = tổ sẽ thiếu người nếu duyệt hết</span></div>
        <style>.rq-cal{display:grid;grid-template-columns:repeat(7,minmax(0,1fr));gap:6px}@media (max-width:700px){.rq-cal{grid-template-columns:1fr}}</style><div class="rq-cal">${days.map((d, i) => `<div style="border:1px solid ${(d.listShort || []).length ? '#e3a3a3' : '#e5e7eb'};border-radius:10px;padding:6px;min-height:90px;background:#fff"><div class="spread"><b class="small">${WD[i % 7]} ${dmy(d.day).slice(0, 5)}</b>${(d.listShort || []).length ? `<span class="small bad">${esc(d.listShort.join('; '))}</span>` : ''}</div>${(d.listEntry || []).map(ent).join('')}</div>`).join('')}</div></div>`;
      body.querySelectorAll('[data-cw]').forEach(b => b.onclick = () => { st.from = U.addDays(from, +b.dataset.cw); calendar(body, re); });
    });
  }

  // ---------- Tạo đơn: 3 loại ----------
  function createDlg(re) {
    let kind = 'LEAVE';
    const canOther = Store.perm('hr-leave').add !== false;
    UI.modal({ title: 'Tạo đơn', size: 'lg', body: `<div class="seg mb" style="display:inline-flex">${[['LEAVE', 'Xin nghỉ'], ['SWAP', 'Đổi ca'], ['GIVE', 'Nhường ca']].map(([k, l]) => `<button class="seg-b ${k === kind ? 'on' : ''}" data-k="${k}">${l}</button>`).join('')}</div><div id="cdBody"></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="cdGo">Gửi đơn</button>',
      onMount: (m, close) => {
        let state = {};
        const body = m.querySelector('#cdBody');
        const draw = () => { m.querySelectorAll('[data-k]').forEach(b => b.classList.toggle('on', b.dataset.k === kind)); state = {}; kind === 'LEAVE' ? leaveForm() : swapForm(); };
        m.querySelectorAll('[data-k]').forEach(b => b.onclick = () => { kind = b.dataset.k; draw(); });
        const today = U.day(Store.ctx.date);
        function leaveForm() {
          body.innerHTML = `<div class="form-grid">${canOther ? `<div class="field"><label>Nhân viên (trống = chính tôi)</label><input class="input" id="lfE" list="lfEl" placeholder="Gõ tên để tìm"><datalist id="lfEl">${Store.rows('Employee').filter(e => !e.IsDeleted && (e.Status || 'Active') === 'Active').map(e => `<option value="${esc((e.FullName || '') + ' · ' + (e.Code || ''))}" data-id="${e.Id}">`).join('')}</datalist></div>` : ''}
            <div class="field"><label>Loại nghỉ *</label><select class="select" id="lfT">${ref('LeaveType').map(t => `<option value="${esc(t.code)}">${esc(t.name)}</option>`).join('')}</select></div>
            <div class="field"><label>Từ ngày *</label><input class="input" type="date" id="lfF" value="${today}"></div><div class="field"><label>Đến ngày *</label><input class="input" type="date" id="lfTo" value="${today}"></div>
            <div class="field full"><label>Lý do *</label><input class="input" id="lfR" maxlength="1000"></div></div><div id="lfP" class="mt"></div>`;
          const empId = () => { const v = (body.querySelector('#lfE') || {}).value || ''; const o = [...body.querySelectorAll('#lfEl option')].find(x => x.value === v); return o ? o.dataset.id : ''; };
          const pv = () => Store.query('HrRequest/PreviewLeave', Object.assign({ FromDate: body.querySelector('#lfF').value, ToDate: body.querySelector('#lfTo').value, LeaveTypeCode: body.querySelector('#lfT').value }, empId() ? { EmployeeId: empId() } : {})).then(x => { const b = body.querySelector('#lfP'); if (b) b.innerHTML = x.ok ? impactHtml(x.data, false) : !empId() && canOther ? '<div class="small muted">Chọn nhân viên để xem trước phép năm và các ca bị ảnh hưởng.</div>' : `<div class="small bad">${esc(x.message || '')}</div>`; });
          body.querySelectorAll('#lfE,#lfT,#lfF,#lfTo').forEach(i => i.onchange = pv); pv();
          state.submit = () => { const v = id => body.querySelector('#' + id).value; if (!v('lfF') || !v('lfTo') || v('lfTo') < v('lfF')) return UI.toast('Đến ngày phải từ Từ ngày', 'err'); if (v('lfR').trim().length < 3) return UI.toast('Nhập lý do', 'err');
            const form = { FromDate: v('lfF'), ToDate: v('lfTo'), LeaveTypeCode: v('lfT'), Reason: v('lfR').trim() }; if (empId()) form.EmployeeId = empId();
            Store.act('LeaveRequest/Create', { form, tables: T }).then(r => { if (r.ok) { close(); UI.toast(esc(r.res && r.res.responseMessage || 'Đã gửi đơn nghỉ'), 'ok'); re(); } }); };
        }
        function swapForm() {
          body.innerHTML = '<div class="small muted">Đang tải ca của bạn…</div>';
          Store.query('ShiftAssignment/GetMine', { days: 14 }).then(x => {
            const mine = (x.list || (x.data && x.data.listData) || []).filter(s => ['Planned', 'Confirmed'].includes(s.assignmentStatus) && U.day(s.workDate) >= today);
            if (!x.ok) { body.innerHTML = `<div class="empty">${esc(x.message || 'Không tải được ca của bạn')}</div>`; return; }
            const card = (s, attr, on) => `<button class="btn" style="display:block;text-align:left;margin:4px 0;width:100%;${on ? 'border-color:#7DA33C;background:#f6faef' : ''}" ${attr}><b>${esc(s.shiftCode || s.shiftName || '')}</b> ${dmy(s.workDate)} <span class="small muted">${hm(s.plannedStart)}–${hm(s.plannedEnd)}</span>${s.employeeName ? ` · ${esc(s.employeeName)}` : ''}</button>`;
            body.innerHTML = `<div class="form-grid"><div class="field"><label>1. Ca của tôi (14 ngày tới)</label><div id="swMine">${mine.map(s => card(s, `data-my="${s.assignmentId}"`, false)).join('') || '<div class="small muted">Bạn không có ca còn mở trong 14 ngày tới.</div>'}</div></div><div class="field"><label>2. ${kind === 'SWAP' ? 'Ca của đồng nghiệp' : 'Người nhận (cùng tổ, đang trống ngày đó)'}</label><div id="swOther" class="small muted">Chọn ca của bạn trước.</div></div><div class="field full"><label>Lý do *</label><input class="input" id="swR" maxlength="1000"></div></div><div class="small muted mt">Người nhận đồng ý qua bot hoặc Việc của tôi rồi đơn mới chuyển người duyệt.</div>`;
            body.querySelectorAll('[data-my]').forEach(b => b.onclick = () => {
              state.cur = b.dataset.my; state.pick = null;
              body.querySelectorAll('[data-my]').forEach(z => { z.style.borderColor = z === b ? '#7DA33C' : ''; z.style.background = z === b ? '#f6faef' : ''; });
              Store.query('ShiftAssignment/GetSwapCandidates', { assignmentId: state.cur }).then(c => {
                const o = body.querySelector('#swOther'); if (!o) return;
                if (!c.ok) { o.innerHTML = `<span class="bad">${esc(c.message || '')}</span>`; return; }
                const d = c.data || {}, items = kind === 'SWAP' ? (d.listShift || []) : (d.listColleague || []);
                o.innerHTML = items.length ? items.map(s => kind === 'SWAP' ? card({ shiftCode: s.shiftName, workDate: s.workDate, plannedStart: s.plannedStart, plannedEnd: s.plannedEnd, employeeName: s.employeeName }, `data-pk="${s.assignmentId}|${s.employeeId}"`, false)
                  : `<button class="btn" style="display:block;text-align:left;margin:4px 0;width:100%" data-pk="|${s.employeeId}">${esc(s.employeeName || '')}</button>`).join('') : `<span class="small muted">${kind === 'SWAP' ? 'Không có ca đồng nghiệp phù hợp (±7 ngày, cùng tổ).' : 'Không có người cùng tổ đang trống ngày đó.'}</span>`;
                o.querySelectorAll('[data-pk]').forEach(z => z.onclick = () => { state.pick = z.dataset.pk; o.querySelectorAll('[data-pk]').forEach(y => { y.style.borderColor = y === z ? '#7DA33C' : ''; y.style.background = y === z ? '#f6faef' : ''; }); });
              });
            });
            state.submit = () => {
              if (!state.cur) return UI.toast('Chọn ca của bạn', 'err'); if (!state.pick) return UI.toast(kind === 'SWAP' ? 'Chọn ca của đồng nghiệp' : 'Chọn người nhận', 'err');
              const r = body.querySelector('#swR').value.trim(); if (r.length < 3) return UI.toast('Nhập lý do', 'err');
              const [aid, eid] = state.pick.split('|'), form = { CurrentShiftAssignmentId: state.cur, Reason: r };
              if (kind === 'SWAP') form.ProposedShiftAssignmentId = aid; else form.TargetEmployeeId = eid;
              Store.act('ShiftSwapRequest/Create', { form, tables: T }).then(z => { if (z.ok) { close(); UI.toast(esc(z.res && z.res.responseMessage || 'Đã gửi đơn'), 'ok'); re(); } });
            };
          });
        }
        m.querySelector('#cdGo').onclick = () => state.submit && state.submit();
        draw();
      } });
  }
  // Người nhận đồng ý / từ chối (tab Đơn và Việc của tôi).
  function consentRun(id, accept, note, done) { Store.act('ShiftSwapRequest/Consent', { json: { id, accept, note: note || '' }, tables: T }).then(r => { if (r.ok) { UI.toast(esc(r.res && r.res.responseMessage || 'Đã ghi'), 'ok'); done && done(); } }); }
  APP.hrConsent = (id, accept, done) => accept ? UI.confirm('Đồng ý nhận ca theo đơn này? Đơn sẽ chuyển người duyệt.', () => consentRun(id, true, '', done), { ok: 'Đồng ý' })
    : UI.confirm('Từ chối nhận ca?', note => consentRun(id, false, note, done), { ok: 'Từ chối', danger: true, input: 'Lý do (không bắt buộc)' });
})();
