/* UI-6.38 (D-119): "Chấm công" (hr-attendance) – 3 tab:
 * - Hôm nay: chọn ca (đang diễn ra / sắp / đã xong), 4 chỉ số, 4 cột Chưa vào ca (quá giờ, Chấm hộ có giờ thực tế, Báo vắng) · Đã vào ca · Nghỉ phép ·
 *   Cần xử lý (thiếu ra ca → Bổ sung giờ; ngoài bán kính → Xác nhận hợp lệ) – AttendanceEvent/GetDayBoard, Supplement, CheckIn (PROXY), ReportAbsent, ConfirmValid.
 * - Bảng công: tháng ◀ ▶, người × ngày nhóm theo tổ, ký hiệu ✓ M V P T ! – …, cột Công / Giờ / Muộn / Vắng / Phép, dải "Cần xử lý trước khi xuất lương",
 *   bấm ô → chi tiết + Bổ sung giờ / Xác nhận – GetMonthGrid; Xuất bảng công (.xlsx) – ExportTimesheet.
 * - Sổ chấm công: danh sách lần chấm, chỉ xem.
 * Bỏ khối "Ca hôm nay của tôi" (nhân viên tự chấm ở Việc của tôi / bot). Chế độ DEMO giữ màn cũ. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const st = { tab: 'day', unit: '', shift: null, month: null, flag: '' };
  const hm = v => String(v || '').slice(11, 16);
  const ref = n => (Store.refList ? Store.refList(n) : []) || [];
  const AT = ['AttendanceEvent', 'ShiftAssignment'];
  const SYM = { '✓': ['#eef6e8', '#3F5A17'], M: ['#fdf0e6', '#8a3b0b'], V: ['#fdecec', '#b42318'], P: ['#e6f1fb', '#1f4c80'], T: ['#fff4d6', '#8a5a00'], '!': ['#fdecec', '#b42318'], '…': ['#f2f4f1', '#6b756e'] };
  const base = APP.SCREENS['hr-attendance'];

  // ---------- Hộp dùng chung ----------
  APP.attSupplement = (o, done) => {
    const d = o.date || U.day(o.plannedStart || Store.ctx.date), dflt = o.type === 'CHECK_IN' ? hm(o.plannedStart) : hm(o.plannedEnd);
    UI.modal({ title: 'Bổ sung giờ – ' + esc(o.name || ''), sub: `${esc(o.shiftCode || '')} ${U.date(d)} · ca ${hm(o.plannedStart)}–${hm(o.plannedEnd)}`, size: 'sm',
      body: `<div class="field"><label>Loại *</label><select class="select" id="suT"><option value="CHECK_OUT" ${o.type !== 'CHECK_IN' ? 'selected' : ''}>Ra ca</option><option value="CHECK_IN" ${o.type === 'CHECK_IN' ? 'selected' : ''}>Vào ca</option></select></div>
        <div class="field mt"><label>Giờ thực tế *</label><input class="input" type="datetime-local" id="suA" value="${d}T${dflt || '00:00'}"></div>
        <div class="field mt"><label>Lý do *</label><select class="select" id="suR"><option value="">— Chọn lý do —</option>${ref('AttendanceSupplementReason').map(x => `<option value="${esc(x.code)}">${esc(x.name)}</option>`).join('')}</select></div>
        <div class="field mt"><label>Ghi chú</label><input class="input" id="suN" maxlength="500"></div>
        <div class="small muted mt">Chỉ bổ sung trong khung ca (tới 3 giờ sau giờ kết thúc) và cho ca trong 3 ngày gần nhất. Lần bổ sung ghi thêm vào sổ, không sửa lần chấm cũ.</div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="suGo">Ghi bổ sung</button>',
      onMount: (m, close) => m.querySelector('#suGo').onclick = () => {
        const v = id => m.querySelector('#' + id).value; if (!v('suA')) return UI.toast('Nhập giờ thực tế', 'err'); if (!v('suR')) return UI.toast('Chọn lý do', 'err');
        Store.act('AttendanceEvent/Supplement', { json: { employeeId: o.employeeId, shiftAssignmentId: o.assignmentId, eventType: v('suT'), eventAt: v('suA'), reasonCode: v('suR'), note: v('suN') }, tables: AT }).then(r => { if (!r.ok) return; close(); UI.toast(esc(r.res && r.res.responseMessage || 'Đã bổ sung'), 'ok'); done && done(); });
      } });
  };
  const confirmValid = (eventId, name, done) => UI.confirm(`Xác nhận lần chấm ngoài bán kính của <b>${esc(name || '')}</b> là hợp lệ?`, note =>
    Store.act('AttendanceEvent/ConfirmValid', { json: { id: eventId, note }, tables: AT }).then(r => { if (r.ok) { UI.toast('Đã xác nhận hợp lệ', 'ok'); done && done(); } }), { ok: 'Xác nhận hợp lệ', input: 'Lý do (vd. nhận xe ở bãi)', required: true });
  const proxyIn = (p, done) => UI.modal({ title: 'Chấm hộ vào ca – ' + esc(p.fullName || ''), sub: `${esc(p.shiftCode || '')} ${hm(p.plannedStart)}–${hm(p.plannedEnd)}`, size: 'sm',
    body: `<div class="field"><label>Giờ vào thực tế (trống = bây giờ)</label><input class="input" type="time" id="pxT"></div><div class="field mt"><label>Lý do *</label><select class="select" id="pxR"><option value="">— Chọn lý do —</option>${ref('AttendanceSupplementReason').map(x => `<option value="${esc(x.code)}">${esc(x.name)}</option>`).join('')}</select></div><div class="field mt"><label>Ghi chú</label><input class="input" id="pxN" maxlength="500"></div>`,
    foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="pxGo">Ghi vào ca</button>',
    onMount: (m, close) => m.querySelector('#pxGo').onclick = () => {
      const t = m.querySelector('#pxT').value, r = m.querySelector('#pxR'), reason = r.value, label = r.options[r.selectedIndex].text, note = m.querySelector('#pxN').value.trim();
      if (!reason) return UI.toast('Chọn lý do', 'err');
      const fin = x => { if (!x.ok) return; close(); UI.toast(esc(x.res && x.res.responseMessage || 'Đã ghi'), 'ok'); done && done(); };
      if (!t) return Store.act('AttendanceEvent/CheckIn', { form: { EmployeeId: p.employeeId, SourceChannel: 'PROXY', Note: label + (note ? ' – ' + note : '') }, tables: AT }).then(fin);
      Store.act('AttendanceEvent/Supplement', { json: { employeeId: p.employeeId, shiftAssignmentId: p.assignmentId, eventType: 'CHECK_IN', eventAt: U.day(p.workDate || p.plannedStart) + 'T' + t, reasonCode: reason, note }, tables: AT }).then(fin);
    } });

  APP.register('hr-attendance', (el, f, p) => {
    if (!Store.isLive) return base(el, f, p);
    if (p && ['day', 'grid', 'log'].includes(p.tab)) st.tab = p.tab;
    const re = () => APP.SCREENS['hr-attendance'](el, f, {});
    const perm = Store.perm('hr-attendance'), canAdd = perm.add !== false, canEdit = perm.edit !== false;
    Store.ensure('OrganizationUnit').then(() => {
      if (!el.isConnected) return;
      if (st.tab === 'log') {
        APP.genericList(el, f, { noAdd: true, noTransit: true });
        const h = el.querySelector('.page-heading'); if (h) h.insertAdjacentHTML('afterend', `<div class="mb">${tabs()}</div><div class="small muted mb">Sổ chỉ ghi thêm – không sửa, không xóa. Quên chấm công: dùng Bổ sung giờ ở tab Hôm nay / Bảng công.</div>`);
        bindTabs(); APP.rowExtra(el, tr => tr.querySelectorAll('[data-edit],[data-del]').forEach(b => b.remove()));
        return;
      }
      el.innerHTML = APP.header(f, `<span id="atUnit"></span><button class="btn" id="atExp">${I('down')} Xuất bảng công</button>`, { noNote: true }) + `<div class="mb">${tabs()}</div><div id="atBody"><div class="card panel small muted">Đang tải…</div></div>`;
      bindTabs();
      APP.unitPicker(el.querySelector('#atUnit'), { value: st.unit, placeholder: 'Mọi tổ / đơn vị', onChange: v => { st.unit = v; draw(); } });
      el.querySelector('#atExp').onclick = () => Store.download('AttendanceEvent/ExportTimesheet', Object.assign({ month: st.month || U.day(Store.ctx.date).slice(0, 7) }, st.unit ? { organizationUnitId: st.unit } : {})).then(r => { if (r.ok) UI.toast('Đã tải bảng công', 'ok'); });
      const draw = () => st.tab === 'grid' ? grid() : day();
      function day() {
        const body = el.querySelector('#atBody');
        Store.query('AttendanceEvent/GetDayBoard', Object.assign({}, st.shift ? { shiftId: st.shift } : {}, st.unit ? { organizationUnitId: st.unit } : {})).then(x => {
          if (!body.isConnected) return;
          if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được')}</div>`; return; }
          const b = x.data || {}, now = new Date();
          const shiftCard = s => { const lbl = s.state === 'RUNNING' ? 'đang diễn ra' : s.state === 'DONE' ? 'đã xong' : 'bắt đầu sau ' + Math.max(0, Math.round((new Date(s.start) - now) / 36e5)) + ' giờ';
            return `<button class="chip ${s.id === b.shiftId ? 'active' : ''}" data-sh="${s.id}"><b>${esc(s.code || '')}</b> ${hm(s.start)}–${hm(s.end)} · ${lbl} · ${U.num(s.present)}/${U.num(s.assigned)}</button>`; };
          const item = (p, extra) => `<div style="border:1px solid #e5e7eb;border-radius:10px;padding:8px 10px;margin-bottom:6px;background:#fff"><div class="spread"><b>${esc(p.fullName || '')}</b>${extra || ''}</div><div class="small muted">${esc([p.code, p.unitName].filter(Boolean).join(' · '))}${p.phone ? ` · <a href="tel:${esc(p.phone)}">${esc(p.phone)}</a>` : ''}</div>`;
          const col = (title, color, n, html) => `<div class="card panel" style="background:#fafbf8"><div class="spread mb"><b>${title}</b>${UI.tag(U.num(n), color)}</div>${html || '<div class="small muted">Không có</div>'}</div>`;
          body.innerHTML = `<div class="row-flex mb" style="gap:6px;flex-wrap:wrap">${(b.listShift || []).map(shiftCard).join('') || '<span class="small muted">Hôm nay không có ca được phân.</span>'}</div>
            <div class="kpis">${UI.kpi('Có mặt', `${U.num(b.present || 0)} / ${U.num(b.assigned || 0)}`, 'được phân ca đang chọn', 'users', 'green')}${UI.kpi('Chưa vào ca (quá giờ)', U.num(b.notCheckedInOverdue || 0), '', 'alert', b.notCheckedInOverdue ? 'red' : 'green')}${UI.kpi('Đi muộn', U.num(b.lateMinutesTotal || 0) + ' phút', '', 'clock', 'orange')}${UI.kpi('Cần xử lý', U.num(b.needActionCount || 0), '3 ngày gần nhất', 'check', 'purple')}</div>
            <style>.at-cols{display:grid;grid-template-columns:repeat(4,minmax(240px,1fr));gap:12px}@media (max-width:1100px){.at-cols{grid-template-columns:repeat(2,1fr)}}@media (max-width:700px){.at-cols{grid-template-columns:1fr}.at-cols .btn{min-height:40px}}</style><div class="at-cols">
            ${col('Chưa vào ca', 'red', (b.listNotCheckedIn || []).length, (b.listNotCheckedIn || []).map(p => item(p, p.overdueMinutes ? `<span class="small bad">quá giờ ${U.num(p.overdueMinutes)}′</span>` : '<span class="small muted">chưa tới giờ</span>') + (canAdd ? `<div class="row-flex mt" style="gap:6px"><button class="btn xs primary" data-px="${p.assignmentId}">Chấm hộ</button>${canEdit && p.overdueMinutes ? `<button class="btn xs" data-ab="${p.assignmentId}">Báo vắng</button>` : ''}</div>` : '') + '</div>').join(''))}
            ${col('Đã vào ca', 'green', (b.listCheckedIn || []).length, (b.listCheckedIn || []).map(p => item(p, `<span class="small">${hm(p.checkInAt)}</span>`) + `<div class="small">${p.lateMinutes ? `<span class="warn">muộn ${U.num(p.lateMinutes)}′</span> ` : ''}${p.outside ? `<span class="bad">ngoài bán kính ${U.num(p.distanceMeters || 0)} m</span>` : ''}</div></div>`).join(''))}
            ${col('Nghỉ phép', 'blue', (b.listOnLeave || []).length, (b.listOnLeave || []).map(p => item(p, `<span class="small muted">${esc(p.leaveRequestNo || '')}</span>`) + '</div>').join(''))}
            ${col('Cần xử lý', 'orange', (b.listNeedAction || []).length, (b.listNeedAction || []).map(p => item(p, `<span class="small muted">${U.date(p.workDate).slice(0, 5)} ${esc(p.shiftCode || '')}</span>`) + `<div class="small">${p.kind === 'MISSING_OUT' ? `Thiếu ra ca (vào ${hm(p.checkInAt)})` : `Ngoài bán kính ${U.num(p.distanceMeters || 0)} m`}</div>${p.kind === 'MISSING_OUT' && canAdd ? `<button class="btn xs primary mt" data-su="${p.assignmentId}">Bổ sung giờ</button>` : ''}${p.kind === 'OUTSIDE' && canEdit ? `<button class="btn xs mt" data-cv="${p.eventId}">Xác nhận hợp lệ</button>` : ''}</div>`).join(''))}
            </div><div class="small muted mt">Ca hết giờ mà không vào ca tự chuyển Vắng lúc 23:30. Nhân viên tự chấm ở Việc của tôi hoặc bot; quá 10–15 phút còn người chưa vào ca, bot báo trưởng đơn vị.</div>`;
          body.querySelectorAll('[data-sh]').forEach(c => c.onclick = () => { st.shift = c.dataset.sh; day(); });
          const find = (list, id) => (b[list] || []).find(z => z.assignmentId === id || z.eventId === id);
          body.querySelectorAll('[data-px]').forEach(bt => bt.onclick = () => proxyIn(find('listNotCheckedIn', bt.dataset.px), day));
          body.querySelectorAll('[data-ab]').forEach(bt => bt.onclick = () => { const p2 = find('listNotCheckedIn', bt.dataset.ab); UI.confirm(`Ghi vắng ca ${esc(p2.shiftCode || '')} của <b>${esc(p2.fullName || '')}</b>?`, note => Store.act('AttendanceEvent/ReportAbsent', { json: { shiftAssignmentId: p2.assignmentId, note }, tables: AT }).then(r => { if (r.ok) { UI.toast('Đã ghi vắng', 'ok'); day(); } }), { ok: 'Báo vắng', danger: true, input: 'Ghi chú (không bắt buộc)' }); });
          body.querySelectorAll('[data-su]').forEach(bt => bt.onclick = () => { const p2 = find('listNeedAction', bt.dataset.su); APP.attSupplement({ employeeId: p2.employeeId, assignmentId: p2.assignmentId, name: p2.fullName, type: 'CHECK_OUT', date: U.day(p2.workDate), plannedStart: p2.plannedStart, plannedEnd: p2.plannedEnd, shiftCode: p2.shiftCode }, day); });
          body.querySelectorAll('[data-cv]').forEach(bt => bt.onclick = () => { const p2 = find('listNeedAction', bt.dataset.cv); confirmValid(p2.eventId, p2.fullName, day); });
        });
      }
      function grid() {
        const body = el.querySelector('#atBody'), month = st.month || U.day(Store.ctx.date).slice(0, 7);
        const shift = n => { const [y, mo] = month.split('-').map(Number); const d = new Date(y, mo - 1 + n, 1); return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0'); };
        Store.query('AttendanceEvent/GetMonthGrid', Object.assign({ month }, st.unit ? { organizationUnitId: st.unit } : {})).then(x => {
          if (!body.isConnected) return;
          if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được')}</div>`; return; }
          const g = x.data || {}, days = g.days || [];
          let rows = g.listRow || [];
          if (st.flag) rows = rows.filter(r => Object.values(r.cells || {}).some(c => st.flag === 'T' ? c.symbol === 'T' : st.flag === '!' ? c.symbol === '!' : c.symbol === 'V'));
          const groups = []; rows.forEach(r => { let gg = groups.find(z => z.n === (r.unitName || 'Chưa có tổ')); if (!gg) groups.push(gg = { n: r.unitName || 'Chưa có tổ', list: [] }); gg.list.push(r); });
          const flag = (k, label, n) => `<button class="chip ${st.flag === k ? 'active' : ''}" data-fl="${k}">${label} · ${U.num(n || 0)}</button>`;
          body.innerHTML = `<div class="row-flex mb" style="gap:8px;flex-wrap:wrap;align-items:center"><button class="btn sm" data-mo="-1">◀</button><b>Tháng ${month.slice(5)}/${month.slice(0, 4)}</b><button class="btn sm" data-mo="1">▶</button></div>
            ${(g.missingTime || g.outsideUnconfirmed || g.absentNoLeave) ? `<div class="notice warn mb">${I('alert')}<div><b style="display:inline">Cần xử lý trước khi xuất lương:</b> ${flag('T', 'Thiếu giờ ra / vào', g.missingTime)} ${flag('!', 'Ngoài bán kính chưa xác nhận', g.outsideUnconfirmed)} ${flag('V', 'Vắng không có đơn nghỉ', g.absentNoLeave)}${st.flag ? ' <button class="chip" data-fl="">Bỏ lọc</button>' : ''}</div></div>` : ''}
            <div class="card panel"><div class="small muted mb">✓ đủ · M muộn / về sớm · V vắng · P phép · T thiếu ra / vào ca · ! ngoài bán kính chưa xác nhận · – không có ca · … ca hôm nay chưa vào. Bấm ô để xem chi tiết.</div>
            <div class="table-wrap" style="max-height:620px"><table class="table dc-table" style="font-size:12px"><thead><tr><th style="min-width:160px">Nhân viên</th>${days.map(d => { const w = new Date(d + 'T00:00').getDay(); return `<th style="text-align:center;padding:4px 2px;${w === 0 ? 'background:#f2f4f1' : ''}">${+d.slice(8)}</th>`; }).join('')}<th>Công</th><th>Giờ</th><th>Muộn</th><th>Vắng</th><th>Phép</th></tr></thead><tbody>
            ${groups.map(gg => `<tr><td colspan="${days.length + 6}" style="background:#f7f8f4"><b>${esc(gg.n)}</b> <span class="small muted">${U.num(gg.list.length)} người</span></td></tr>${gg.list.map(r => `<tr><td><b>${esc(r.fullName || '')}</b><div class="small muted">${esc(r.code || '')}</div></td>${days.map(d => { const c = (r.cells || {})[d] || {}, s = c.symbol || '', col = SYM[s]; const w = new Date(d + 'T00:00').getDay();
              return `<td style="text-align:center;padding:2px;${w === 0 ? 'background:#f7f8f6' : ''}">${s && s !== '–' ? `<span data-cell="${r.employeeId}|${d}" style="display:inline-block;min-width:20px;border-radius:5px;padding:2px 3px;font-weight:700;cursor:pointer;${col ? `background:${col[0]};color:${col[1]}` : ''}">${esc(s)}${c.supplemented ? '*' : ''}</span>` : `<span class="muted">${esc(s)}</span>`}</td>`; }).join('')}<td style="text-align:center"><b>${U.num(r.work)}</b></td><td style="text-align:center">${U.num(r.hours, 1)}</td><td style="text-align:center">${U.num(r.late)}</td><td style="text-align:center" class="${r.absent ? 'bad' : ''}">${U.num(r.absent)}</td><td style="text-align:center">${U.num(r.leave)}</td></tr>`).join('')}`).join('') || `<tr><td colspan="${days.length + 6}" class="empty">Không có dữ liệu</td></tr>`}
            </tbody></table></div><div class="small muted mt">* có lần bổ sung giờ.</div></div>`;
          body.querySelectorAll('[data-mo]').forEach(bt => bt.onclick = () => { st.month = shift(+bt.dataset.mo); grid(); });
          body.querySelectorAll('[data-fl]').forEach(bt => bt.onclick = () => { st.flag = bt.dataset.fl; grid(); });
          body.querySelectorAll('[data-cell]').forEach(sp => sp.onclick = () => {
            const [eid, d] = sp.dataset.cell.split('|'), r = rows.find(z => z.employeeId === eid), c = r.cells[d];
            const canSup = canAdd && ['T', 'V'].includes(c.symbol);
            UI.modal({ title: esc(r.fullName || '') + ' – ' + U.date(d), sub: esc(c.shiftCode || ''), size: 'sm',
              body: `<div class="kv"><div><label>Vào ca</label><span>${c.checkInAt ? hm(c.checkInAt) : '–'}</span></div><div><label>Ra ca</label><span>${c.checkOutAt ? hm(c.checkOutAt) : '–'}</span></div><div><label>Muộn / về sớm</label><span>${U.num(c.lateMinutes || 0)}′ / ${U.num(c.earlyLeaveMinutes || 0)}′</span></div><div><label>Bổ sung</label><span>${c.supplemented ? 'Có' : 'Không'}</span></div></div>`,
              foot: `<button class="btn" data-close>Đóng</button>${canSup ? '<button class="btn primary" id="cdSu">Bổ sung giờ</button>' : ''}${c.symbol === '!' && canEdit ? '<button class="btn" id="cdCv">Xác nhận hợp lệ</button>' : ''}`,
              onMount: (m, close) => {
                const su = m.querySelector('#cdSu'); if (su) su.onclick = () => { close(); APP.attSupplement({ employeeId: eid, assignmentId: c.assignmentId, name: r.fullName, type: c.checkInAt ? 'CHECK_OUT' : 'CHECK_IN', date: d, shiftCode: c.shiftCode }, grid); };
                const cv = m.querySelector('#cdCv'); if (cv) cv.onclick = () => { close(); confirmValid(c.outsideEventId, r.fullName, grid); };
              } });
          });
        });
      }
      draw();
    });
    function tabs() { return `<div class="seg" style="display:inline-flex">${[['day', 'Hôm nay'], ['grid', 'Bảng công'], ['log', 'Sổ chấm công']].map(([k, l]) => `<button class="seg-b ${st.tab === k ? 'on' : ''}" data-at="${k}">${l}</button>`).join('')}</div>`; }
    function bindTabs() { el.querySelectorAll('[data-at]').forEach(b => b.onclick = () => { st.tab = b.dataset.at; re(); }); }
  });
})();
