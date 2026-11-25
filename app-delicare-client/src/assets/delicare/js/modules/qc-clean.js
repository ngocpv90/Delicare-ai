/* UI-6.32 (D-113): "Vệ sinh khay hộp" (qc-cleaning) theo mô phỏng cleaning-board-mockup.html – 2 tab:
 * - Đầu màn: ngày ◀ ▶, thanh "Khay sạch cho ngày mai" (PackagingLedger/GetCleanStock – theo loại bao bì), 4 chỉ số.
 * - Hôm nay: 3 cột Chờ kiểm tra → Đang vệ sinh → Hoàn tất hôm nay (CleaningBatch/GetDayBoard). Kiểm tra (ContainerReturn/Inspect – kết quả một chạm,
 *   người kiểm = người đăng nhập ở máy chủ) · Ghi bước (GetDetail → RecordResults; mặc định Đạt, hóa chất điền sẵn) · "Đạt tất cả & kết luận" (PassAll) ·
 *   Vệ sinh lại (CleaningBatch/Create).
 * - Sổ vệ sinh (chỉ xem): CleaningBatch/GetBook – gom theo ngày, lọc, bấm mẻ xem đủ 6 bước, In sổ / Xuất Excel.
 * Thay "Kiểm tra vệ sinh" (qc-cleaning-result) + "Kiểm tra khay hộp thu hồi" (log-return-inspection) đã ẩn. Chế độ DEMO giữ màn cũ. Tải SAU qc.js. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const st = { tab: 'today', day: null, book: { period: '7d', from: null, to: null, q: '', result: '' } };
  const today = () => U.day(Store.ctx.date || new Date());
  const hhmm = v => String(v || '').slice(11, 16);
  const RESULTS = [['OK', 'Đủ'], ['SHORTAGE', 'Thiếu'], ['DAMAGED', 'Hỏng'], ['DIRTY', 'Bẩn'], ['REJECTED', 'Không đạt']];
  const TABLES = ['CleaningBatch', 'CleaningChecklistResult', 'ContainerReturn', 'ReturnInspection', 'PackagingLedger'];
  const bar = (passed, total, fail) => `<div class="row-flex mt" style="gap:4px">${Array.from({ length: total || 6 }, (_, i) => `<span style="flex:1;height:6px;border-radius:3px;background:${i < passed ? '#7DA33C' : fail && i === passed ? '#C0392B' : '#E6E8E3'}"></span>`).join('')}</div>`;

  const base = APP.SCREENS['qc-cleaning'];
  APP.register('qc-cleaning', (el, f, p) => {
    if (!Store.isLive) return base(el, f, p);
    if (p && ['today', 'book'].includes(p.tab)) st.tab = p.tab;
    const re = () => APP.SCREENS['qc-cleaning'](el, f, {});
    const tabs = `<div class="tabs mb">${[['today', 'Hôm nay'], ['book', 'Sổ vệ sinh']].map(([k, l]) => `<button class="tab ${st.tab === k ? 'active' : ''}" data-qct="${k}">${l}</button>`).join('')}</div>`;
    (st.tab === 'book' ? drawBook : drawToday)(el, f, tabs, re);
    el.querySelectorAll('[data-qct]').forEach(b => b.onclick = () => { st.tab = b.dataset.qct; re(); });
  });

  // ================= Hôm nay =================
  function drawToday(el, f, tabs, re) {
    const day = st.day || today();
    el.innerHTML = APP.header(f, '') + `<div id="qcHead"><div class="card panel small muted">Đang tải…</div></div>` + tabs + '<div id="qcBody"></div>';
    Promise.all([Store.query('CleaningBatch/GetDayBoard', { date: day }), Store.query('PackagingLedger/GetCleanStock', { date: day })]).then(([x, y]) => {
      const head = el.querySelector('#qcHead'), body = el.querySelector('#qcBody'); if (!head || !body) return;
      if (!x.ok) { head.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được bảng vệ sinh')}</div>`; return; }
      const b = x.data || {}, types = (y.ok && y.data && y.data.listType) || [];
      const p = Store.perm('qc-cleaning'), canInspect = Store.perm('log-return-inspection').add !== false, canStep = Store.perm('qc-cleaning-result').add !== false, canEnd = p.edit !== false;
      const stockLine = t => { const pct = t.needed ? Math.min(100, 100 * t.ready / t.needed) : 100; return `<div class="row-flex" style="gap:10px;flex-wrap:wrap"><b style="min-width:170px">Khay sạch cho ngày mai${types.length > 1 ? ' – ' + esc(t.label || t.packagingType) : ''}</b><div style="flex:1;min-width:160px;height:10px;background:#eee;border-radius:5px;overflow:hidden"><div style="width:${pct}%;height:100%;background:${t.shortage ? '#E8892B' : '#7DA33C'}"></div></div><span class="small"><b>${U.num(t.ready)}</b> sẵn sàng / cần <b>${U.num(t.needed)}</b>${t.shortage ? ` – <b class="bad">thiếu ${U.num(t.shortage)}</b>` : ' – đủ'} · đang vệ sinh ${U.num(t.cleaning)} · chờ kiểm tra ${U.num(t.waitingInspection)}</span></div>`; };
      head.innerHTML = `<div class="card panel mb"><div class="spread"><div class="small muted">Khay / hộp thu hồi → kiểm tra → vệ sinh 6 bước → sẵn sàng dùng lại. Mẻ vệ sinh tự lập khi kiểm tra xong; mỗi lần ghi là một dòng mới (hồ sơ ATTP).</div>
          <div class="row-flex"><button class="btn sm" data-dd="-1" aria-label="Ngày trước">◀</button><input type="date" class="input" id="qcDay" value="${day}" style="width:150px"><button class="btn sm" data-dd="1" aria-label="Ngày sau">▶</button>${day === today() ? '' : '<button class="btn sm" data-dd="0">Hôm nay</button>'}</div></div>
          <div class="mt" style="border:1px solid #f3d2c4;background:#fff8f4;border-radius:10px;padding:10px 12px">${types.length ? types.map(stockLine).join('<div class="mt"></div>') : '<span class="small muted">Chưa có số liệu khay sạch / nhu cầu ngày mai (tồn khay sạch đầu kỳ ghi ở Sổ bao bì – loại "Sẵn sàng dùng lại").</span>'}</div></div>
        <div class="kpis">${UI.kpi('Chờ kiểm tra', U.num(b.waitingReturns || 0), `${U.num(b.waitingContainers || 0)} khay / hộp đã thu`, 'clipboard', 'blue')}${UI.kpi('Đang vệ sinh', U.num(b.inProgress || 0), `${U.num(b.inProgressContainers || 0)} khay / hộp`, 'recycle', 'orange')}${UI.kpi('Hoàn tất hôm nay', `${U.num(b.donePass || 0)} / ${U.num(b.doneTotal || 0)}`, b.doneReject ? `<span class="bad">${U.num(b.doneReject)} mẻ không đạt</span>` : 'Không có mẻ không đạt', 'check', 'green')}${UI.kpi('Tỷ lệ đạt 7 ngày', b.passRate7d === null || b.passRate7d === undefined ? '–' : U.pct(b.passRate7d), '', 'shield', 'purple')}</div>`;
      head.querySelectorAll('[data-dd]').forEach(bt => bt.onclick = () => { const n = +bt.dataset.dd; st.day = n === 0 ? null : U.addDays(day, n); re(); });
      head.querySelector('#qcDay').onchange = e => { st.day = e.target.value || null; re(); };
      const col = (title, n, cls, inner) => `<div class="card panel" style="background:#fafbf8"><div class="spread mb"><b>${title}</b>${UI.tag(U.num(n), cls)}</div>${inner || '<div class="small muted">Không có</div>'}</div>`;
      const card = (color, inner) => `<div style="border:1px solid #e5e7eb;border-left:4px solid ${color};border-radius:10px;background:#fff;padding:10px 12px;margin-bottom:10px">${inner}</div>`;
      body.innerHTML = `<style>.qc-cols{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}@media(max-width:900px){.qc-cols{grid-template-columns:1fr}}</style><div class="qc-cols">
        ${col('Chờ kiểm tra', (b.listWaiting || []).length, 'blue', (b.listWaiting || []).map((w, i) => card('#4A7FC1', `<b>${esc(w.customerName || '')}</b><div class="small muted">${esc(w.packagingType || '')} · ${esc(w.deliveryNumber || '')} · thu lúc ${hhmm(w.returnDate)}</div>
          <div class="small mt">Phải thu ${U.num(w.expectedQuantity)} · thu về <b>${U.num(w.returnedQuantity)}</b>${w.damagedQuantity ? ` · hỏng ${U.num(w.damagedQuantity)}` : ''}${w.missingQuantity ? ` · <span class="bad">thiếu ${U.num(w.missingQuantity)}</span>` : ''}</div>
          ${canInspect ? `<div style="text-align:right"><button class="btn sm primary mt" data-insp="${i}">Kiểm tra</button></div>` : ''}`)).join(''))}
        ${col('Đang vệ sinh', (b.listInProgress || []).length, 'orange', (b.listInProgress || []).map(m => card('#E8892B', `<b>${esc(m.number || '')}</b><div class="small muted">${esc(m.customerName || '')} · ${esc(m.packagingType || '')} · ${U.num(m.containerQuantity)} cái</div>${bar(m.passedStepCount, m.stepCount, m.hasFailedStep)}
          <div class="spread mt"><span class="small">Bước ${U.num(m.passedStepCount)}/${U.num(m.stepCount)}${m.responsibleName ? ' · ' + esc(m.responsibleName) : ''}${m.hasFailedStep ? ' · <span class="bad">có bước Không đạt</span>' : ''}</span>${canStep || canEnd ? `<button class="btn sm orange" data-step="${m.id}">Ghi bước</button>` : ''}</div>`)).join(''))}
        ${col('Hoàn tất hôm nay', (b.listDone || []).length, 'green', (b.listDone || []).map(m => card(m.qcResult === 'PASS' ? '#7DA33C' : '#C0392B', `<div class="spread"><b>${esc(m.number || '')}</b>${m.qcResult === 'PASS' ? UI.tag('Đạt – sẵn sàng', 'green') : UI.tag('Không đạt', 'red')}</div><div class="small muted">${esc(m.customerName || '')} · ${esc(m.packagingType || '')} · ${U.num(m.containerQuantity)} cái${m.responsibleName ? ' · ' + esc(m.responsibleName) : ''}</div>${bar(m.qcResult === 'PASS' ? m.stepCount : m.passedStepCount, m.stepCount, m.qcResult !== 'PASS')}
          ${m.qcResult !== 'PASS' ? `<div class="small bad mt">${esc(m.rejectReason || 'Không đạt')}</div>${!m.redone && canEnd ? `<div style="text-align:right"><button class="btn sm danger mt" data-redo="${m.id}" data-ri="${m.returnInspectionId}" data-q="${m.containerQuantity}">Vệ sinh lại</button></div>` : m.redone ? '<div class="small muted mt">Đã lập mẻ vệ sinh lại</div>' : ''}` : ''}`)).join(''))}
      </div>`;
      body.querySelectorAll('[data-insp]').forEach(bt => bt.onclick = () => inspectForm(b.listWaiting[+bt.dataset.insp], re));
      body.querySelectorAll('[data-step]').forEach(bt => bt.onclick = () => stepForm(bt.dataset.step, canEnd, re));
      body.querySelectorAll('[data-redo]').forEach(bt => bt.onclick = () => UI.confirm(`Lập mẻ vệ sinh mới cho <b>${U.num(+bt.dataset.q)}</b> khay / hộp của mẻ không đạt?`, () =>
        Store.act('CleaningBatch/Create', { form: { ReturnInspectionId: bt.dataset.ri, ContainerQuantity: bt.dataset.q }, tables: TABLES }).then(r => { if (r.ok) { UI.toast('Đã lập mẻ vệ sinh lại ' + esc(((r.data || {}).batch || {}).cleaningBatchNumber || ''), 'ok'); re(); } }), { ok: 'Vệ sinh lại' }));
    });
  }

  function inspectForm(w, done) {
    let res = w.suggestedResult || 'OK';
    UI.modal({
      title: 'Kiểm tra khay / hộp thu hồi – ' + esc(w.customerName || ''), sub: `${esc(w.packagingType || '')} · ${esc(w.deliveryNumber || '')} · phải thu ${U.num(w.expectedQuantity)}, thu về ${U.num(w.returnedQuantity)}`, size: 'md',
      body: `<div class="field"><label>Kết quả</label><div class="row-flex" style="flex-wrap:wrap;gap:6px">${RESULTS.map(([k, l]) => `<button type="button" class="chip ${k === res ? 'active' : ''}" data-r="${k}" style="font-size:15px;padding:8px 14px">${l}</button>`).join('')}</div></div>
        <div class="form-grid mt"><div class="field"><label>Số hỏng</label><input class="input" type="number" min="0" id="ikD" value="${w.damagedQuantity || 0}"></div><div class="field"><label>Số thiếu</label><input class="input" type="number" min="0" id="ikM" value="${w.missingQuantity || 0}"></div></div>
        <div class="field mt"><label>Người kiểm</label><input class="input" value="${esc((window.DC_HOST && DC_HOST.user && (DC_HOST.user.fullName || DC_HOST.user.userName)) || 'Tài khoản đang đăng nhập')}" readonly></div>
        <div class="field mt"><label>Ghi chú <span class="req" id="ikReq" style="display:${res === 'OK' ? 'none' : 'inline'}">*</span></label><textarea class="input" rows="2" id="ikN" placeholder="Bắt buộc khi khác Đủ"></textarea></div>
        <div class="small muted mt">Số khay dùng được (thu về − hỏng) tự vào mẻ vệ sinh mới.</div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="ikGo">Ghi kiểm tra & lập mẻ</button>',
      onMount: (m, close) => {
        m.querySelectorAll('[data-r]').forEach(b => b.onclick = () => { res = b.dataset.r; m.querySelectorAll('[data-r]').forEach(x => x.classList.toggle('active', x === b)); m.querySelector('#ikReq').style.display = res === 'OK' ? 'none' : 'inline'; });
        m.querySelector('#ikGo').onclick = () => {
          const note = m.querySelector('#ikN').value.trim();
          if (res !== 'OK' && note.length < 2) return UI.toast('Ghi chú bắt buộc khi kết quả khác Đủ', 'err');
          Store.act('ContainerReturn/Inspect', { form: { Id: w.containerReturnId, InspectionResult: res, DamagedQty: m.querySelector('#ikD').value, MissingQty: m.querySelector('#ikM').value, Note: note }, tables: TABLES }).then(r => {
            if (!r.ok) return; const d = r.data || {}, mb = (d.listCleaningBatch || [])[0];
            UI.toast('Đã ghi kiểm tra' + (mb ? ` · lập mẻ <b>${esc(mb.cleaningBatchNumber || '')}</b> ${U.num(mb.containerQuantity)} khay` : ''), 'ok'); close(); done();
          });
        };
      }
    });
  }

  function stepForm(id, canEnd, done) {
    Store.query('CleaningBatch/GetDetail', { id }).then(x => {
      if (!x.ok) return UI.toast(esc(x.message || 'Không tải được mẻ'), 'err');
      const d = x.data || {}, b = d.batch || {}, steps = d.listStep || [];
      UI.modal({
        title: 'Ghi bước vệ sinh – ' + esc(b.cleaningBatchNumber || b.cleaningNo || ''), sub: `${esc(d.customerName || '')} · ${esc(d.packagingType || '')} · ${U.num(b.containerQuantity)} cái`, size: 'lg',
        body: `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Bước</th><th>Kết quả</th><th>Hóa chất / khắc phục</th></tr></thead><tbody>${steps.map((s, i) => { const r = s.result || {}, done2 = r.resultCode === 'PASS'; return `<tr data-i="${i}"><td><b>${esc((s.item || {}).itemName || '')}</b>${done2 ? `<br><span class="small muted">Đạt · ${esc(s.checkedByName || '')} ${hhmm(r.checkedAt)}</span>` : ''}</td>
          <td>${done2 ? UI.tag('Đạt', 'green') : `<div class="row-flex" style="gap:4px"><button type="button" class="chip ${r.resultCode === 'FAIL' ? '' : 'active'}" data-v="PASS">Đạt</button><button type="button" class="chip ${r.resultCode === 'FAIL' ? 'active' : ''}" data-v="FAIL">Không đạt</button></div>`}</td>
          <td>${done2 ? esc(r.chemicalUsed || '') : `${s.requiresChemical ? `<input class="input" data-chem placeholder="Hóa chất khử khuẩn" value="${esc(d.suggestedChemical || '')}" style="width:220px">` : ''}<input class="input mt" data-cor placeholder="Hành động khắc phục (bắt buộc khi Không đạt)" value="${esc(r.resultCode === 'FAIL' ? r.correctiveAction || '' : '')}" style="width:260px;display:${r.resultCode === 'FAIL' ? 'block' : 'none'}">`}</td></tr>`; }).join('')}</tbody></table></div>`,
        foot: `<button class="btn" data-close>Đóng</button><button class="btn" id="sfSave">Lưu bước đã làm</button>${canEnd ? '<button class="btn primary" id="sfAll">Đạt tất cả & kết luận</button>' : ''}`,
        onMount: (m, close) => {
          const rows = [...m.querySelectorAll('tr[data-i]')].filter(tr => tr.querySelector('[data-v]'));
          const val = tr => (tr.querySelector('[data-v].active') || {}).dataset ? tr.querySelector('[data-v].active').dataset.v : 'PASS';
          const sync = () => { const anyFail = rows.some(tr => val(tr) === 'FAIL'); rows.forEach(tr => { const c = tr.querySelector('[data-cor]'); if (c) c.style.display = val(tr) === 'FAIL' ? 'block' : 'none'; }); const all = m.querySelector('#sfAll'); if (all) { all.disabled = anyFail; all.title = anyFail ? 'Có bước Không đạt – làm lại bước đó trước' : ''; } };
          rows.forEach(tr => tr.querySelectorAll('[data-v]').forEach(bt => bt.onclick = () => { tr.querySelectorAll('[data-v]').forEach(x => x.classList.toggle('active', x === bt)); sync(); }));
          sync();
          const chem = () => { const e = m.querySelector('[data-chem]'); return e ? e.value.trim() : ''; };
          m.querySelector('#sfSave').onclick = () => {
            // Ghi lần lượt tới bước Không đạt đầu tiên (bao gồm) – các bước sau giữ chưa làm.
            const list = [];
            for (const tr of rows) {
              const s = steps[+tr.dataset.i], v = val(tr), cor = (tr.querySelector('[data-cor]') || {}).value || '';
              if (v === 'FAIL' && cor.trim().length < 3) return UI.toast(esc((s.item || {}).itemName || '') + ': ghi hành động khắc phục', 'err');
              if (v === 'PASS' && s.requiresChemical && !chem()) return UI.toast(esc((s.item || {}).itemName || '') + ': ghi hóa chất đã dùng', 'err');
              list.push({ checklistItemId: (s.item || {}).id, resultCode: v, chemicalUsed: s.requiresChemical ? chem() || null : null, correctiveAction: v === 'FAIL' ? cor.trim() : null });
              if (v === 'FAIL') break;
            }
            if (!list.length) return UI.toast('Mọi bước đã Đạt – bấm "Đạt tất cả & kết luận"', 'warn');
            Store.act('CleaningBatch/RecordResults', { json: { id, listItem: list }, tables: TABLES }).then(r => { if (!r.ok) return; UI.toast(`Đã ghi ${list.length} bước`, (r.data || {}).listWarning && r.data.listWarning.length ? 'warn' : 'ok'); close(); done(); });
          };
          const all = m.querySelector('#sfAll'); if (all) all.onclick = () => {
            if (steps.some(s => s.requiresChemical && (s.result || {}).resultCode !== 'PASS') && !chem()) return UI.toast('Ghi hóa chất khử khuẩn đã dùng', 'err');
            Store.act('CleaningBatch/PassAll', { form: { Id: id, ChemicalUsed: chem() }, tables: TABLES }).then(r => { if (!r.ok) return; UI.toast(`Mẻ ${esc(b.cleaningBatchNumber || '')} đạt – ${U.num(b.containerQuantity)} khay sạch sẵn sàng dùng lại`, 'ok'); close(); done(); });
          };
        }
      });
    });
  }

  // ================= Sổ vệ sinh =================
  const bk = st.book;
  const range = () => { const t = today(); if (bk.period === 'today') return [t, t]; if (bk.period === 'month') return [t.slice(0, 8) + '01', t]; if (bk.period === 'custom' && bk.from && bk.to) return [bk.from, bk.to]; return [U.addDays(t, -6), t]; };
  function drawBook(el, f, tabs, re) {
    const [a, b] = range(), canExp = Store.perm('qc-cleaning').exp !== false;
    el.innerHTML = APP.header(f, `<button class="btn" id="cbPrint">${I('print')} In sổ vệ sinh</button>${canExp ? `<button class="btn" id="cbXls">${I('download')} Xuất Excel</button>` : ''}`) + tabs + `
      <div class="card panel mb"><div class="row-flex" style="flex-wrap:wrap;gap:8px"><input class="input" type="search" id="cbQ" placeholder="Tìm mẻ, khách, hóa chất, người làm" value="${esc(bk.q)}" style="width:250px">
        ${[['today', 'Hôm nay'], ['7d', '7 ngày'], ['month', 'Tháng này'], ['custom', 'Chọn khoảng']].map(([k, l]) => `<button class="chip ${bk.period === k ? 'active' : ''}" data-cbp="${k}">${l}</button>`).join('')}
        <span style="display:${bk.period === 'custom' ? 'inline-flex' : 'none'};gap:4px"><input type="date" class="input" id="cbFrom" value="${a}" style="width:145px"><input type="date" class="input" id="cbTo" value="${b}" style="width:145px"></span>
        <select class="select" id="cbRes" style="width:auto"><option value="">Mọi kết quả</option><option value="PASS" ${bk.result === 'PASS' ? 'selected' : ''}>Đạt</option><option value="REJECT" ${bk.result === 'REJECT' ? 'selected' : ''}>Không đạt</option></select></div></div><div id="cbBody"><div class="card panel small muted">Đang tải…</div></div>`;
    el.querySelectorAll('[data-cbp]').forEach(x => x.onclick = () => { bk.period = x.dataset.cbp; if (bk.period === 'custom') { bk.from = bk.from || a; bk.to = bk.to || b; } re(); });
    const on = (s, ev, fn) => { const e = el.querySelector(s); if (e) e.addEventListener(ev, fn); };
    let tq; on('#cbQ', 'input', e => { clearTimeout(tq); tq = setTimeout(() => { bk.q = e.target.value.trim(); loadBook(el); }, 350); });
    on('#cbFrom', 'change', e => { bk.from = e.target.value; loadBook(el); }); on('#cbTo', 'change', e => { bk.to = e.target.value; loadBook(el); }); on('#cbRes', 'change', e => { bk.result = e.target.value; loadBook(el); });
    loadBook(el);
  }
  function loadBook(el) {
    const body = el.querySelector('#cbBody'); if (!body) return;
    const [a, b] = range();
    Store.query('CleaningBatch/GetBook', { From: a, To: b, Keyword: bk.q || null, Result: bk.result || null }).then(x => {
      if (!body.isConnected) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được sổ')}</div>`; return; }
      const d = x.data || {}, days = d.listDay || [];
      const res = r => (r.qcResult === 'PASS' ? UI.tag('Đạt', 'green') : UI.tag('Không đạt', 'red'));
      body.innerHTML = `<style>.qb-cards{display:none}.qb-card{border:1px solid var(--line,#e5e7eb);border-radius:10px;padding:10px;margin-bottom:8px}@media(max-width:640px){.qb-table{display:none}.qb-cards{display:block}}</style>
        <div class="kpis">${UI.kpi('Mẻ đã kết luận', U.num(d.batches || 0), `${U.num(d.containers || 0)} khay / hộp`, 'recycle', 'blue')}${UI.kpi('Đạt', U.num(d.pass || 0), '', 'check', 'green')}${UI.kpi('Không đạt', U.num(d.reject || 0), '', 'alert', 'red')}${UI.kpi('Tỷ lệ đạt', d.batches ? U.pct(100 * d.pass / d.batches) : '–', '', 'shield', 'purple')}</div>
        ${days.length ? days.map(g => `<details class="card panel mb" open><summary class="spread" style="cursor:pointer"><span><b>${U.date(g.date)}</b> <span class="small muted">· ${U.num(g.batches)} mẻ${g.reject ? ` · <span class="bad">${U.num(g.reject)} không đạt</span>` : ''}</span></span><button class="btn xs" data-pday="${U.day(g.date)}">${I('print')} In ngày</button></summary>
          <div class="table-wrap qb-table mt"><table class="table dc-table"><thead><tr><th>Giờ kết luận</th><th>Mẻ</th><th>Khách</th><th>Loại</th><th class="num">Số lượng</th><th>Kết quả</th><th>Hóa chất khử khuẩn</th><th>Người làm</th><th>Khắc phục</th></tr></thead><tbody>${g.listRow.map(r => `<tr><td>${hhmm(r.endDate)}</td><td><a href="javascript:void 0" data-bd="${r.id}">${esc(r.number || '')}</a></td><td>${esc(r.customerName || '')}</td><td>${esc(r.packagingType || '')}</td><td class="num">${U.num(r.containerQuantity)}</td><td>${res(r)}</td><td>${esc(r.chemical || '–')}</td><td class="small">${esc(r.people || '')}</td><td class="small">${esc(r.correctiveAction || '–')}</td></tr>`).join('')}</tbody></table></div>
          <div class="qb-cards mt">${g.listRow.map(r => `<div class="qb-card"><div class="spread"><a href="javascript:void 0" data-bd="${r.id}"><b>${esc(r.number || '')}</b></a>${res(r)}</div><div class="small muted">${hhmm(r.endDate)} · ${esc(r.customerName || '')} · ${esc(r.packagingType || '')} · ${U.num(r.containerQuantity)} cái</div>${r.chemical ? `<div class="small">Hóa chất: ${esc(r.chemical)}</div>` : ''}</div>`).join('')}</div></details>`).join('') : '<div class="card panel empty">Không có mẻ vệ sinh nào khớp bộ lọc.</div>'}`;
      body.querySelectorAll('[data-bd]').forEach(a2 => a2.onclick = () => batchDetail(a2.dataset.bd));
      body.querySelectorAll('[data-pday]').forEach(btn => btn.onclick = e => { e.preventDefault(); e.stopPropagation(); printBook(days.filter(g => U.day(g.date) === btn.dataset.pday)); });
      const pr = el.querySelector('#cbPrint'); if (pr) pr.onclick = () => printBook(days);
      const xl = el.querySelector('#cbXls'); if (xl) xl.onclick = () => exportCsv(days.flatMap(g => g.listRow));
    });
  }
  function batchDetail(id) {
    Store.query('CleaningBatch/GetDetail', { id }).then(x => {
      if (!x.ok) return UI.toast(esc(x.message || 'Không tải được mẻ'), 'err');
      const d = x.data || {}, b = d.batch || {};
      UI.modal({ title: 'Mẻ vệ sinh ' + esc(b.cleaningBatchNumber || ''), sub: `${esc(d.customerName || '')} · ${esc(d.packagingType || '')} · ${U.num(b.containerQuantity)} cái · ${esc(d.qcResultName || d.statusName || '')}`, size: 'lg',
        body: `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Bước</th><th>Kết quả</th><th>Người · giờ</th><th>Hóa chất</th><th>Khắc phục</th><th class="num">Số lần ghi</th></tr></thead><tbody>${(d.listStep || []).map(s => { const r = s.result || {}; return `<tr><td>${esc((s.item || {}).itemName || '')}</td><td>${esc(s.resultName || '–')}</td><td class="small">${esc(s.checkedByName || '')} ${hhmm(r.checkedAt)}</td><td class="small">${esc(r.chemicalUsed || '')}</td><td class="small">${esc(r.correctiveAction || '')}</td><td class="num">${U.num(s.historyCount || 0)}</td></tr>`; }).join('')}</tbody></table></div>` });
    });
  }
  // In sổ vệ sinh (bản in trình duyệt) – mẫu chính thức chờ biểu mẫu của anh (PROGRESS).
  function printBook(days) {
    const [a, b] = range(), rows = days.flatMap(g => g.listRow);
    if (!rows.length) return UI.toast('Không có dòng nào để in', 'warn');
    const w = window.open('', '_blank'); if (!w) return UI.toast('Trình duyệt chặn cửa sổ in – cho phép cửa sổ bật lên rồi thử lại', 'err');
    const t = v => esc(v == null ? '' : String(v));
    w.document.write(`<!doctype html><html lang="vi"><head><meta charset="utf-8"><title>Sổ vệ sinh khay hộp</title><style>body{font:12px Arial,sans-serif;margin:18px}h1{font-size:16px;margin:0 0 4px}table{border-collapse:collapse;width:100%;margin-top:10px}th,td{border:1px solid #999;padding:4px 5px;vertical-align:top}th{background:#eee}</style></head><body>
      <h1>SỔ VỆ SINH KHAY / HỘP</h1><div>Từ ngày ${U.date(a)} đến ngày ${U.date(b)} · ${rows.length} mẻ</div>
      <table><thead><tr><th>Ngày giờ kết luận</th><th>Mẻ</th><th>Khách</th><th>Loại</th><th>Số lượng</th><th>Kết quả</th><th>Hóa chất</th><th>Người làm</th><th>Khắc phục</th></tr></thead><tbody>
      ${rows.map(r => `<tr><td>${U.date(r.endDate)} ${hhmm(r.endDate)}</td><td>${t(r.number)}</td><td>${t(r.customerName)}</td><td>${t(r.packagingType)}</td><td>${U.num(r.containerQuantity)}</td><td>${r.qcResult === 'PASS' ? 'Đạt' : 'Không đạt'}</td><td>${t(r.chemical)}</td><td>${t(r.people)}</td><td>${t(r.correctiveAction)}</td></tr>`).join('')}
      </tbody></table><p style="margin-top:24px">Người kiểm tra ký: ........................................ &nbsp;&nbsp;&nbsp; Người phụ trách ký: ........................................</p><script>window.onload=function(){window.print()}<\/script></body></html>`);
    w.document.close();
  }
  function exportCsv(rows) {
    if (!rows.length) return UI.toast('Không có dòng nào để xuất', 'warn');
    const q = v => '"' + String(v == null ? '' : v).replace(/"/g, '""') + '"';
    const head = ['Ngày', 'Giờ kết luận', 'Mẻ', 'Khách', 'Loại', 'Số lượng', 'Kết quả', 'Hóa chất', 'Người làm', 'Khắc phục'];
    const lines = rows.map(r => [U.date(r.endDate), hhmm(r.endDate), r.number, r.customerName, r.packagingType, r.containerQuantity, r.qcResult === 'PASS' ? 'Đạt' : 'Không đạt', r.chemical, r.people, r.correctiveAction].map(q).join(','));
    const blob = new Blob(['﻿' + [head.map(q).join(',')].concat(lines).join('\r\n')], { type: 'text/csv;charset=utf-8' });
    const [x, y] = range(), a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = `so-ve-sinh-khay_${x}_${y}.csv`; document.body.appendChild(a); a.click(); setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 500);
    UI.toast('Đã xuất ' + rows.length + ' dòng', 'ok');
  }
})();
