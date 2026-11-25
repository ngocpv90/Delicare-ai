/* UI-6.30 (D-111): "Kiểm tra theo ca / mẻ" (qc-checklist-result) – 3 tab:
 * - Hôm nay: ngày ◀ ▶, 4 chỉ số, ma trận Bộ kiểm tra × Ca (FoodSafetyChecklistResult/GetDayBoard) – bấm ô → form kiểm tra (GetRun → Record):
 *   kết quả mặc định Đạt, "Đạt tất cả", Không đạt bắt buộc khắc phục, tiêu chí trọng yếu Không đạt bắt buộc chọn mẻ bị ảnh hưởng (hoặc lý do không ảnh hưởng mẻ nào).
 *   Không gửi trạm bếp (trạm lấy theo mẻ ở máy chủ).
 * - Sổ kiểm tra (kiểm thực bước 2, chỉ xem): GetBook – gom theo ngày, lọc, In sổ / In ngày, Xuất Excel.
 * - Tỷ lệ đạt: GetPassRate – biểu đồ 30 ngày theo bộ + tiêu chí hay Không đạt nhất.
 * Chế độ DEMO giữ màn cũ (qc.js). Tải SAU qc.js. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const has = v => v !== null && v !== undefined && v !== '';
  const st = { tab: 'today', day: null, board: null, book: { period: '7d', from: null, to: null, q: '', ck: '', shift: '', result: '', critical: false } };
  const today = () => U.day(Store.ctx.date || new Date());
  const hhmm = v => String(v || '').slice(11, 16);
  const CELL = { NOT_DUE: ['', 'Chưa đến hạn'], DUE: ['blue', 'Đến hạn'], OVERDUE: ['red', 'Quá hạn'], PASS: ['green', 'Đạt hết'], FAIL: ['orange', 'Có lỗi (không trọng yếu)'], CRITICAL: ['red', 'Có lỗi trọng yếu'] };
  const RES = { PASS: ['green', 'Đạt'], FAIL: ['red', 'Không đạt'] };
  const resTag = (r, name) => (RES[r] ? UI.tag(RES[r][1], RES[r][0]) : name ? UI.tag(name, 'blue') : '<span class="muted">–</span>');
  const listOf = x => (x.list && x.list.length ? x.list : Array.isArray(x.data) ? x.data : []);

  const base = APP.SCREENS['qc-checklist-result'];
  APP.register('qc-checklist-result', (el, f, p) => {
    if (!Store.isLive) return base(el, f, p);
    if (p && ['today', 'book', 'rate', 'sets'].includes(p.tab)) st.tab = p.tab;
    // UI-6.33 (D-114): tab Bộ kiểm tra (mục Bộ kiểm tra ATTP bỏ khỏi sidebar) – chỉ người có quyền Xem qc-checklist.
    const canSets = Store.perm('qc-checklist').access !== false;
    if (st.tab === 'sets' && !canSets) st.tab = 'today';
    if (p && p.date) st.day = p.date;
    const tabs = `<div class="tabs mb">${[['today', 'Hôm nay'], ['book', 'Sổ kiểm tra'], ['rate', 'Tỷ lệ đạt']].concat(canSets ? [['sets', 'Bộ kiểm tra']] : []).map(([k, l]) => `<button class="tab ${st.tab === k ? 'active' : ''}" data-qst="${k}">${l}</button>`).join('')}</div>`;
    const rerender = () => APP.SCREENS['qc-checklist-result'](el, f, {});
    if (st.tab === 'sets') {
      const meta = Object.assign({}, APP.FN['qc-checklist'], { name: f.name });
      const draw = () => {
        APP.SCREENS['qc-checklist'](el, meta, {});
        const h = el.querySelector('.page-heading'); if (h && !el.querySelector('[data-qst]')) h.insertAdjacentHTML('afterend', tabs);
        if (APP.qcChecklistSets) APP.qcChecklistSets(el);
        el.querySelectorAll('[data-qst]').forEach(b => b.onclick = () => { st.tab = b.dataset.qst; rerender(); });
      };
      return Store.ensure('FoodSafetyChecklist').then(draw, draw);
    }
    if (st.tab === 'book') drawBook(el, f, tabs);
    else if (st.tab === 'rate') drawRate(el, f, tabs);
    else drawToday(el, f, tabs);
    el.querySelectorAll('[data-qst]').forEach(b => b.onclick = () => { st.tab = b.dataset.qst; rerender(); });
  });

  // ================= Hôm nay =================
  function drawToday(el, f, tabs) {
    const day = st.day || today();
    el.innerHTML = APP.header(f, '') + tabs + `
      <div class="card panel mb"><div class="spread"><div class="small muted">Kiểm thực bước 2 – kiểm tra trong chế biến theo ca. Hạn mỗi bộ tính từ giờ xuất bếp của ca. Bấm một ô để ghi kết quả.</div>
        <div class="row-flex"><button class="btn sm" data-dd="-1" aria-label="Ngày trước">◀</button><input type="date" class="input" id="qsDay" value="${day}" style="width:150px"><button class="btn sm" data-dd="1" aria-label="Ngày sau">▶</button>${day === today() ? '' : '<button class="btn sm" data-dd="0">Hôm nay</button>'}</div></div></div>
      <div id="qsBody"><div class="card panel small muted">Đang tải…</div></div>`;
    const re = () => APP.SCREENS['qc-checklist-result'](el, f, {}), go = d => { st.day = d; re(); };
    el.querySelectorAll('[data-dd]').forEach(b => b.onclick = () => { const n = +b.dataset.dd; go(n === 0 ? null : U.addDays(day, n)); });
    el.querySelector('#qsDay').onchange = e => go(e.target.value || null);
    Store.query('FoodSafetyChecklistResult/GetDayBoard', { date: day }).then(x => {
      const body = el.querySelector('#qsBody'); if (!body) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được bảng kiểm tra')}</div>`; return; }
      const b = st.board = x.data || {}, shifts = b.listShift || [], rows = b.listChecklist || [];
      const canRecord = Store.perm('qc-checklist-result').add !== false;
      body.innerHTML = `<div class="kpis">${UI.kpi('Lượt đến hạn', U.num(b.dueCount || 0), `${rows.length} bộ × ${shifts.length} ca`, 'clipboard', 'blue')}${UI.kpi('Đã làm', U.num(b.doneCount || 0), '', 'check', 'green')}${UI.kpi('Quá hạn', U.num(b.overdueCount || 0), b.overdueCount ? '<span class="bad">cần làm ngay</span>' : 'Không có', 'alert', 'red')}${UI.kpi('Lỗi trọng yếu đang mở', U.num(b.openCriticalCount || 0), b.openCriticalCount ? '<span class="bad">kiểm lại sau khắc phục</span>' : 'Không có', 'shield', 'orange')}</div>
        <style>.qs-m th.cur,.qs-m td.cur{background:rgba(94,155,58,.08)}.qs-c{display:block;width:100%;min-width:110px;border:1px solid var(--line,#e5e7eb);border-left-width:4px;border-radius:8px;padding:6px 8px;background:#fff;text-align:left;cursor:pointer}.qs-c b{font-size:15px}.qs-c.green{border-left-color:#5E9B3A}.qs-c.red{border-left-color:#D64545;background:#fff5f5}.qs-c.orange{border-left-color:#E8892B}.qs-c.blue{border-left-color:#2F6FC4}.qs-m td:first-child,.qs-m th:first-child{position:sticky;left:0;background:#fff;z-index:1}</style>
        <div class="card panel">${rows.length && shifts.length ? `<div class="table-wrap"><table class="table dc-table qs-m"><thead><tr><th>Bộ kiểm tra</th>${shifts.map(s => `<th class="${s.isCurrent ? 'cur' : ''}">${esc(s.name || s.code || '')}<br><span class="small muted">${esc(s.startTime || '')}–${esc(s.endTime || '')}${s.isCurrent ? ' · đang chạy' : ''}</span></th>`).join('')}</tr></thead><tbody>
          ${rows.map((r, ri) => `<tr><td><b>${esc(r.name || r.code || '')}</b><br><span class="small muted">${U.num(r.itemCount)} tiêu chí</span></td>${shifts.map((s, si) => { const c = (r.listCell || []).find(z => z.shiftId === s.id) || {}; const k = CELL[c.status] || CELL.DUE; return `<td class="${s.isCurrent ? 'cur' : ''}"><button class="qs-c ${k[0]}" data-cell="${ri}:${si}" ${canRecord ? '' : 'disabled'} title="${esc(k[1] + (c.dueSource ? ' · ' + c.dueSource : ''))}"><b>${U.num(c.done || 0)}/${U.num(c.total || 0)}</b> <span class="small">${esc(k[1])}</span><br><span class="small muted">${c.dueAt ? 'hạn ' + hhmm(c.dueAt) : 'không có hạn'}</span>${(c.listHeldBatch || []).length ? `<br><span class="small bad">Giữ mẻ: ${c.listHeldBatch.map(esc).join(', ')}</span>` : ''}</button></td>`; }).join('')}</tr>`).join('')}
        </tbody></table></div>
        <div class="row-flex mt small" style="flex-wrap:wrap;gap:10px">${Object.values(CELL).map(([c, l]) => `<span><span class="qs-c ${c}" style="display:inline-block;width:14px;min-width:0;height:14px;padding:0;vertical-align:middle"></span> ${l}</span>`).join('')}<span class="muted">Bộ công đoạn, lưu mẫu ghi ở màn Mẻ nấu; vệ sinh khay ở màn Vệ sinh khay hộp.</span></div>`
        : '<div class="empty">Chưa có bộ kiểm tra hoặc ca làm việc đang áp dụng cho ngày này.</div>'}</div>`;
      body.querySelectorAll('[data-cell]').forEach(btn => btn.onclick = () => { const [ri, si] = btn.dataset.cell.split(':').map(Number); checkForm(rows[ri], shifts[si], day, re); });
    });
  }

  function checkForm(row, shift, day, done) {
    Store.query('FoodSafetyChecklistResult/GetRun', { ChecklistId: row.checklistId, CheckDate: day, ShiftId: shift.id }).then(x => {
      if (!x.ok) return UI.toast(esc(x.message || 'Không tải được bộ kiểm tra'), 'err');
      const run = x.data || {}, items = run.listItem || [];
      if (!items.length) return UI.toast('Bộ kiểm tra chưa có tiêu chí', 'warn');
      const batches = shift.listBatch || [];
      const expected = it => [it.expectedValue, it.unitCode].filter(Boolean).join(' ');
      UI.modal({
        title: esc(row.name || '') + ' – ' + esc(shift.name || ''), sub: U.date(day) + (run.dueAt ? ' · hạn ' + hhmm(run.dueAt) : '') + (run.dueSource ? ' · ' + esc(run.dueSource) : ''), size: 'lg',
        body: `<div class="row-flex mb"><button class="btn sm" id="qfAll">${I('check')} Đạt tất cả</button><span class="small muted">Mặc định Đạt – chỉ đổi tiêu chí có vấn đề.</span></div>
          <div class="table-wrap"><table class="table dc-table"><thead><tr><th>Tiêu chí</th><th>Giá trị đo</th><th>Kết quả</th><th>Hành động khắc phục</th></tr></thead><tbody>${items.map((r, i) => { const it = r.item || {}, cur = r.result || {}; return `<tr data-i="${i}">
            <td><b>${esc(it.itemName || it.itemCode || '')}</b>${it.isCritical ? ' ' + UI.tag('Trọng yếu', 'red') : ''}${it.isRequired ? '' : ' <span class="small muted">(không bắt buộc)</span>'}${cur.resultCode ? `<br><span class="small muted">Lần trước: ${RES[cur.resultCode] ? RES[cur.resultCode][1] : ''} ${hhmm(cur.checkedAt)}</span>` : ''}</td>
            <td>${expected(it) ? `<input class="input" data-f="v" value="${esc(cur.actualValue || '')}" placeholder="chuẩn ${esc(expected(it))}" style="width:130px">` : '<span class="muted small">–</span>'}</td>
            <td><select class="select" data-f="r" style="width:120px"><option value="PASS" ${cur.resultCode === 'FAIL' ? '' : 'selected'}>Đạt</option><option value="FAIL" ${cur.resultCode === 'FAIL' ? 'selected' : ''}>Không đạt</option></select></td>
            <td><input class="input" data-f="c" value="${esc(cur.resultCode === 'FAIL' ? cur.correctiveAction || '' : '')}" placeholder="Bắt buộc khi Không đạt" style="width:220px;display:${cur.resultCode === 'FAIL' ? 'block' : 'none'}"></td></tr>`; }).join('')}</tbody></table></div>
          <div id="qfBatch" style="display:none" class="card panel mt"><div class="panel-title">Mẻ bị ảnh hưởng bởi lỗi trọng yếu</div><div class="panel-desc">Mẻ được chọn sẽ bị giữ (không đạt QC / đóng gói / xuất chuyến được tới khi kiểm lại Đạt).</div>
            <div class="mt">${batches.length ? batches.map(b => `<label class="row-flex small" style="gap:6px;font-weight:400"><input type="checkbox" data-b="${b.id}" ${b.suggested ? 'checked' : ''}> <b>${esc(b.batchNumber || '')}</b> ${esc(b.dishName || '')} · ${esc(b.statusName || '')}</label>`).join('') : '<div class="small muted">Ca này chưa có mẻ nào.</div>'}
            <label class="row-flex small mt" style="gap:6px;font-weight:400"><input type="checkbox" id="qfNone"> Không ảnh hưởng mẻ nào</label><input class="input mt" id="qfReason" placeholder="Lý do không ảnh hưởng mẻ nào (bắt buộc khi tick)" style="display:none"></div></div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="qfGo">Ghi kết quả</button>',
        onMount: (m, close) => {
          const trs = [...m.querySelectorAll('tr[data-i]')];
          const sync = () => {
            trs.forEach(tr => { const c = tr.querySelector('[data-f="c"]'); c.style.display = tr.querySelector('[data-f="r"]').value === 'FAIL' ? 'block' : 'none'; });
            const crit = trs.some(tr => (items[+tr.dataset.i].item || {}).isCritical && tr.querySelector('[data-f="r"]').value === 'FAIL');
            m.querySelector('#qfBatch').style.display = crit ? 'block' : 'none';
          };
          trs.forEach(tr => tr.querySelector('[data-f="r"]').onchange = sync);
          m.querySelector('#qfAll').onclick = () => { trs.forEach(tr => { tr.querySelector('[data-f="r"]').value = 'PASS'; }); sync(); };
          m.querySelector('#qfNone').onchange = e => { m.querySelector('#qfReason').style.display = e.target.checked ? 'block' : 'none'; if (e.target.checked) m.querySelectorAll('[data-b]').forEach(b => { b.checked = false; }); };
          sync();
          m.querySelector('#qfGo').onclick = () => {
            const list = [];
            for (const tr of trs) {
              const r = items[+tr.dataset.i], it = r.item || {}, cur = r.result || {};
              const code = tr.querySelector('[data-f="r"]').value, vEl = tr.querySelector('[data-f="v"]'), v = vEl ? vEl.value.trim() : '', c = tr.querySelector('[data-f="c"]').value.trim();
              if (code === 'FAIL' && c.length < 3) return UI.toast(esc(it.itemName || '') + ': nhập hành động khắc phục', 'err');
              // Chỉ ghi tiêu chí mới / thay đổi (kiểm lại = ghi lần mới); lần đầu ghi đủ.
              if (cur.resultCode && cur.resultCode === code && (cur.actualValue || '') === v && (code === 'PASS' || (cur.correctiveAction || '') === c)) continue;
              list.push({ checklistItemId: it.id, resultCode: code, actualValue: v || null, correctiveAction: code === 'FAIL' ? c : null });
            }
            if (!list.length) return UI.toast('Không có thay đổi nào để ghi', 'warn');
            const crit = list.some(z => z.resultCode === 'FAIL' && (items.find(r => (r.item || {}).id === z.checklistItemId).item || {}).isCritical);
            const ids = [...m.querySelectorAll('[data-b]:checked')].map(b => b.dataset.b), none = m.querySelector('#qfNone').checked, reason = m.querySelector('#qfReason').value.trim();
            if (crit && !ids.length && !(none && reason.length >= 3)) return UI.toast('Có tiêu chí trọng yếu Không đạt – chọn mẻ bị ảnh hưởng hoặc tick "Không ảnh hưởng mẻ nào" và ghi lý do', 'err');
            Store.act('FoodSafetyChecklistResult/Record', { json: { checklistId: row.checklistId, checkDate: day, shiftId: shift.id, listItem: list, listProductionBatchId: crit ? ids : null, noBatchReason: crit && !ids.length ? reason : null }, tables: ['FoodSafetyChecklistResult', 'ProductionBatche'] }).then(r => {
              if (!r.ok) return;
              const d = r.data || {}, held = d.listHeldBatch || [];
              UI.toast(`Đã ghi ${U.num(d.saved || list.length)} tiêu chí` + (held.length ? '<br><b>Mẻ bị giữ:</b> ' + held.map(esc).join('; ') : ''), held.length ? 'warn' : 'ok');
              close(); if (done) done();
            });
          };
        }
      });
    });
  }

  // ================= Sổ kiểm tra (kiểm thực bước 2) =================
  const bk = st.book;
  const range = () => {
    const t = today();
    if (bk.period === 'today') return [t, t];
    if (bk.period === 'month') return [t.slice(0, 8) + '01', t];
    if (bk.period === 'custom' && bk.from && bk.to) return [bk.from, bk.to];
    return [U.addDays(t, -6), t];
  };
  function drawBook(el, f, tabs) {
    const [a, b] = range(), canExp = Store.perm('qc-checklist-result').exp !== false;
    el.innerHTML = APP.header(f, `<button class="btn" id="qbPrint">${I('print')} In sổ kiểm thực bước 2</button>${canExp ? `<button class="btn" id="qbXls">${I('download')} Xuất Excel</button>` : ''}`) + tabs + `
      <div class="card panel mb"><div class="row-flex" style="flex-wrap:wrap;gap:8px">
        <input class="input" type="search" id="qbQ" placeholder="Tìm tiêu chí, bộ, mẻ, khắc phục, người kiểm" value="${esc(bk.q)}" style="width:260px">
        ${[['today', 'Hôm nay'], ['7d', '7 ngày'], ['month', 'Tháng này'], ['custom', 'Chọn khoảng']].map(([k, l]) => `<button class="chip ${bk.period === k ? 'active' : ''}" data-qbp="${k}">${l}</button>`).join('')}
        <span style="display:${bk.period === 'custom' ? 'inline-flex' : 'none'};gap:4px"><input type="date" class="input" id="qbFrom" value="${a}" style="width:145px"><input type="date" class="input" id="qbTo" value="${b}" style="width:145px"></span>
        <select class="select" id="qbCk" style="width:auto"><option value="">Mọi bộ kiểm tra</option>${Store.rows('FoodSafetyChecklist').filter(c => c.Status !== 'Inactive').map(c => `<option value="${c.Id}" ${bk.ck === c.Id ? 'selected' : ''}>${esc(c.Name || c.Code || '')}</option>`).join('')}</select>
        <select class="select" id="qbSh" style="width:auto"><option value="">Mọi ca</option>${Store.rows('Shift').map(s => `<option value="${s.Id}" ${bk.shift === s.Id ? 'selected' : ''}>${esc(s.Name || s.Code || '')}</option>`).join('')}</select>
        <select class="select" id="qbRes" style="width:auto"><option value="">Mọi kết quả</option><option value="PASS" ${bk.result === 'PASS' ? 'selected' : ''}>Đạt</option><option value="FAIL" ${bk.result === 'FAIL' ? 'selected' : ''}>Không đạt</option></select>
        <label class="small"><input type="checkbox" id="qbCrit" ${bk.critical ? 'checked' : ''}> Chỉ lỗi trọng yếu</label>
      </div></div><div id="qbBody"><div class="card panel small muted">Đang tải…</div></div>`;
    const redraw = () => APP.SCREENS['qc-checklist-result'](el, f, {});
    el.querySelectorAll('[data-qbp]').forEach(x => x.onclick = () => { bk.period = x.dataset.qbp; if (bk.period === 'custom') { bk.from = bk.from || a; bk.to = bk.to || b; } redraw(); });
    const on = (id, ev, fn) => { const e = el.querySelector(id); if (e) e.addEventListener(ev, fn); };
    let tq; on('#qbQ', 'input', e => { clearTimeout(tq); tq = setTimeout(() => { bk.q = e.target.value.trim(); loadBook(el); }, 350); });
    on('#qbFrom', 'change', e => { bk.from = e.target.value; loadBook(el); }); on('#qbTo', 'change', e => { bk.to = e.target.value; loadBook(el); });
    on('#qbCk', 'change', e => { bk.ck = e.target.value; loadBook(el); }); on('#qbSh', 'change', e => { bk.shift = e.target.value; loadBook(el); });
    on('#qbRes', 'change', e => { bk.result = e.target.value; loadBook(el); }); on('#qbCrit', 'change', e => { bk.critical = e.target.checked; loadBook(el); });
    if (!Store.rows('FoodSafetyChecklist').length || !Store.rows('Shift').length) Promise.all([Store.ensure('FoodSafetyChecklist'), Store.ensure('Shift')]).then(() => { if (Store.rows('Shift').length && el.querySelector('#qbSh') && el.querySelector('#qbSh').options.length < 2) redraw(); }, () => {});
    loadBook(el);
  }
  function loadBook(el) {
    const body = el.querySelector('#qbBody'); if (!body) return;
    const [a, b] = range();
    Store.query('FoodSafetyChecklistResult/GetBook', { From: a, To: b, Keyword: bk.q || null, ChecklistId: bk.ck || null, ShiftId: bk.shift || null, Result: bk.result || null, OnlyCritical: bk.critical ? true : null }).then(x => {
      if (!body.isConnected) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được sổ')}</div>`; return; }
      const d = x.data || {}, days = d.listDay || [];
      const batchCell = r => (r.listBatch || []).length ? (r.resultCode === 'FAIL' ? '<span class="bad">' : '<span class="muted">') + r.listBatch.map(esc).join(', ') + '</span>' : '<span class="muted">–</span>';
      const card = r => `<div class="qb-card"><div class="spread"><b>${esc(r.itemName || '')}</b>${resTag(r.resultCode, r.resultName)}</div><div class="small muted">${hhmm(r.checkedAt)} · ${esc(r.shiftName || '')} · ${esc(r.checklistName || '')}${r.isCritical ? ' · trọng yếu' : ''}${r.isRecheck ? ' · kiểm lại' : ''}</div>${r.actualValue ? `<div class="small">Đo: ${esc(r.actualValue)}</div>` : ''}${r.correctiveAction ? `<div class="small">Khắc phục: ${esc(r.correctiveAction)}</div>` : ''}${(r.listBatch || []).length ? `<div class="small">Mẻ: ${batchCell(r)}</div>` : ''}</div>`;
      body.innerHTML = `<style>.qb-cards{display:none}.qb-card{border:1px solid var(--line,#e5e7eb);border-radius:10px;padding:10px;margin-bottom:8px}@media(max-width:640px){.qb-table{display:none}.qb-cards{display:block}}</style>
        <div class="kpis">${UI.kpi('Lượt kiểm', U.num(d.runs || 0), `${U.num(d.lines || 0)} tiêu chí đã ghi`, 'clipboard', 'blue')}${UI.kpi('Không đạt', U.num(d.fail || 0), '', 'alert', 'orange')}${UI.kpi('Lỗi trọng yếu', U.num(d.criticalFail || 0), d.criticalFail ? '<span class="bad">mẻ liên quan bị giữ</span>' : '', 'shield', 'red')}${UI.kpi('Tỷ lệ đạt', d.lines ? U.pct(100 * (d.lines - d.fail) / d.lines) : '–', 'trên tiêu chí đã ghi', 'check', 'green')}</div>
        ${days.length ? days.map(g => `<details class="card panel mb" open><summary class="spread" style="cursor:pointer"><span><b>${U.date(g.date)}</b> <span class="small muted">· ${U.num(g.runs)} lượt · ${U.num(g.lines)} tiêu chí${g.fail ? ` · <span class="bad">${U.num(g.fail)} không đạt</span>` : ''}</span></span><button class="btn xs" data-pday="${U.day(g.date)}">${I('print')} In ngày</button></summary>
          <div class="table-wrap qb-table mt"><table class="table dc-table"><thead><tr><th>Giờ ghi</th><th>Ca</th><th>Bộ kiểm tra</th><th>Tiêu chí</th><th>Giá trị đo</th><th>Kết quả</th><th>Khắc phục</th><th>Mẻ bị giữ</th><th>Người kiểm</th></tr></thead><tbody>${g.listRow.map(r => `<tr><td>${hhmm(r.checkedAt)}</td><td>${esc(r.shiftName || '–')}</td><td>${esc(r.checklistName || '')}</td><td>${esc(r.itemName || '')}${r.isCritical ? ' ' + UI.tag('Trọng yếu', 'red') : ''}${r.isRecheck ? ' ' + UI.tag('Kiểm lại', 'blue') : ''}</td><td>${esc(r.actualValue || '–')}${r.expectedValue ? `<br><span class="small muted">chuẩn ${esc(r.expectedValue)}</span>` : ''}</td><td>${resTag(r.resultCode, r.resultName)}</td><td>${esc(r.correctiveAction || '–')}</td><td>${batchCell(r)}</td><td>${esc(r.checkedByName || '')}</td></tr>`).join('')}</tbody></table></div>
          <div class="qb-cards mt">${g.listRow.map(card).join('')}</div></details>`).join('') : '<div class="card panel empty">Không có kết quả kiểm tra nào khớp bộ lọc.</div>'}`;
      body.querySelectorAll('[data-pday]').forEach(btn => btn.onclick = e => { e.preventDefault(); e.stopPropagation(); printBook(days.filter(g => U.day(g.date) === btn.dataset.pday)); });
      const pr = el.querySelector('#qbPrint'); if (pr) pr.onclick = () => printBook(days);
      const xl = el.querySelector('#qbXls'); if (xl) xl.onclick = () => exportCsv(days.flatMap(g => g.listRow));
    });
  }
  // In sổ kiểm thực bước 2 (bản in trình duyệt theo khoảng ngày đang xem) – mẫu PDF chính thức chờ biểu mẫu của anh (PROGRESS).
  function printBook(days) {
    const [a, b] = range(), rows = days.flatMap(g => g.listRow);
    if (!rows.length) return UI.toast('Không có dòng nào để in', 'warn');
    const w = window.open('', '_blank'); if (!w) return UI.toast('Trình duyệt chặn cửa sổ in – cho phép cửa sổ bật lên rồi thử lại', 'err');
    const t = v => esc(v == null ? '' : String(v));
    w.document.write(`<!doctype html><html lang="vi"><head><meta charset="utf-8"><title>Sổ kiểm thực bước 2</title><style>body{font:12px Arial,sans-serif;margin:18px}h1{font-size:16px;margin:0 0 4px}table{border-collapse:collapse;width:100%;margin-top:10px}th,td{border:1px solid #999;padding:4px 5px;vertical-align:top}th{background:#eee}</style></head><body>
      <h1>SỔ KIỂM THỰC BA BƯỚC – BƯỚC 2: KIỂM TRA TRONG QUÁ TRÌNH CHẾ BIẾN</h1><div>Từ ngày ${U.date(a)} đến ngày ${U.date(b)} · ${rows.length} tiêu chí đã ghi</div>
      <table><thead><tr><th>Ngày giờ</th><th>Ca</th><th>Bộ kiểm tra</th><th>Tiêu chí</th><th>Giá trị đo / chuẩn</th><th>Kết quả</th><th>Khắc phục</th><th>Mẻ</th><th>Người kiểm</th></tr></thead><tbody>
      ${rows.map(r => `<tr><td>${U.date(r.checkDate)} ${hhmm(r.checkedAt)}</td><td>${t(r.shiftName)}</td><td>${t(r.checklistName)}</td><td>${t(r.itemName)}${r.isCritical ? ' (trọng yếu)' : ''}${r.isRecheck ? ' – kiểm lại' : ''}</td><td>${t(r.actualValue)}${r.expectedValue ? ' / ' + t(r.expectedValue) : ''}</td><td>${RES[r.resultCode] ? RES[r.resultCode][1] : ''}</td><td>${t(r.correctiveAction)}</td><td>${t((r.listBatch || []).join(', '))}</td><td>${t(r.checkedByName)}</td></tr>`).join('')}
      </tbody></table><p style="margin-top:24px">Người kiểm tra ký: ........................................ &nbsp;&nbsp;&nbsp; Người phụ trách ký: ........................................</p><script>window.onload=function(){window.print()}<\/script></body></html>`);
    w.document.close();
  }
  function exportCsv(rows) {
    if (!rows.length) return UI.toast('Không có dòng nào để xuất', 'warn');
    const q = v => '"' + String(v == null ? '' : v).replace(/"/g, '""') + '"';
    const head = ['Ngày', 'Giờ ghi', 'Ca', 'Bộ kiểm tra', 'Tiêu chí', 'Trọng yếu', 'Giá trị đo', 'Chuẩn', 'Kết quả', 'Khắc phục', 'Mẻ', 'Người kiểm', 'Kiểm lại'];
    const lines = rows.map(r => [U.date(r.checkDate), hhmm(r.checkedAt), r.shiftName, r.checklistName, r.itemName, r.isCritical ? 'Có' : '', r.actualValue, r.expectedValue, RES[r.resultCode] ? RES[r.resultCode][1] : '', r.correctiveAction, (r.listBatch || []).join(', '), r.checkedByName, r.isRecheck ? 'Có' : ''].map(q).join(','));
    const blob = new Blob(['﻿' + [head.map(q).join(',')].concat(lines).join('\r\n')], { type: 'text/csv;charset=utf-8' });
    const [x, y] = range(), a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = `so-kiem-tra-che-bien_${x}_${y}.csv`; document.body.appendChild(a); a.click(); setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 500);
    UI.toast('Đã xuất ' + rows.length + ' dòng', 'ok');
  }

  // ================= Tỷ lệ đạt =================
  function drawRate(el, f, tabs) {
    const to = today(), from = U.addDays(to, -29);
    el.innerHTML = APP.header(f, '') + tabs + '<div id="qrBody"><div class="card panel small muted">Đang tải…</div></div>';
    Store.query('FoodSafetyChecklistResult/GetPassRate', { from, to }).then(x => {
      const body = el.querySelector('#qrBody'); if (!body) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được tỷ lệ đạt')}</div>`; return; }
      const d = x.data || {}, series = d.listSeries || [], top = d.listTopFail || [];
      const labels = (d.listDate || []).map(v => U.date(v).slice(0, 5));
      body.innerHTML = `<div class="card panel mb"><div class="panel-title">Tỷ lệ đạt 30 ngày theo bộ kiểm tra</div><div class="panel-desc">Kết quả mới nhất của mỗi tiêu chí trong ngày / ca (kiểm lại thay lần trước). ${series.map(s => `${esc(s.name || '')}: <b>${has(s.ratePct) ? U.pct(s.ratePct) : '–'}</b>`).join(' · ')}</div>
          ${series.length ? UI.chart({ type: 'line', title: 'Tỷ lệ đạt theo bộ kiểm tra', labels, unit: '%', series: series.map(s => ({ name: s.name || '', values: s.listRate || [] })) }) : '<div class="empty">Chưa có kết quả kiểm tra trong 30 ngày.</div>'}</div>
        <div class="card panel"><div class="panel-title">Tiêu chí hay Không đạt nhất</div>${top.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Tiêu chí</th><th>Bộ kiểm tra</th><th class="num">Số lần Không đạt</th><th>Lần gần nhất</th></tr></thead><tbody>${top.map(t => `<tr><td>${esc(t.itemName || '')}${t.isCritical ? ' ' + UI.tag('Trọng yếu', 'red') : ''}</td><td>${esc(t.checklistName || '')}</td><td class="num"><b>${U.num(t.failCount)}</b></td><td>${U.date(t.lastDate)}</td></tr>`).join('')}</tbody></table></div>` : '<div class="empty">Không có tiêu chí Không đạt trong 30 ngày.</div>'}</div>`;
    });
  }
})();
