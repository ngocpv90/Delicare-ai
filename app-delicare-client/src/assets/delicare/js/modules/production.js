/* P2/P5 – Kế hoạch sản xuất, Lệnh SX, Bảng điều độ mẻ (ngày × ca × dây chuyền), Giá thành */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const BFLOW = ['Planned', 'Cooking', 'QCPending', 'Passed', 'Packed'];
  const poOf = b => Store.get('ProductionOrder', b.ProductionOrderId) || {};
  const planOf = p => Store.get('ProductionPlan', p.ProductionPlanId) || {};
  // ---------- Tiện ích cho chế độ dữ liệu thật ----------
  const LOADING = '<div class="muted small">Đang tải…</div>';
  const failHtml = (x, what) => `<div class="empty">Không tải được ${what}${x && x.message ? ': ' + esc(x.message) : ''}</div>`;
  const addDays = (d, n) => { const t = new Date(U.day(d) + 'T00:00:00'); t.setDate(t.getDate() + n); return U.iso(t).slice(0, 10); };
  const mini = (heads, rows) => rows.length ? `<div class="table-wrap"><table><thead><tr>${heads.map(h => `<th>${h}</th>`).join('')}</tr></thead><tbody>${rows.map(r => `<tr>${r.map(c => `<td>${c === null || c === undefined ? '' : c}</td>`).join('')}</tr>`).join('')}</tbody></table></div>` : '<div class="muted small">Chưa có</div>';
  const notes = (list, kind) => (list || []).length ? `<div class="notice ${kind || 'warn'} mb">${I('alert')}<div>${list.map(esc).join('<br>')}</div></div>` : '';
  const pascal = o => { const r = {}; Object.entries(o || {}).forEach(([k, v]) => { r[k.charAt(0).toUpperCase() + k.slice(1)] = typeof v === 'boolean' ? (v ? 1 : 0) : v; }); return r; };
  const warnToast = d => { const w = d && d.listWarning; if (w && w.length) UI.toast(w.map(esc).join('<br>'), 'warn'); };
  const nil = v => v === null || v === undefined;
  // Đọc qua action riêng cho màn hình vẽ đồng bộ: trả kết quả đã có, tải lại ở nền và chỉ vẽ lại màn hình khi dữ liệu đổi.
  const LC = {};
  function liveData(key, path, params) {
    if (!Store.isLive) return null;
    const sig = path + '?' + JSON.stringify(params || {});
    let c = LC[key]; if (!c || c.sig !== sig) c = LC[key] = { sig, x: null, json: '' };
    if (c.skip) { c.skip = false; return c.x; }
    if (!c.busy) {
      c.busy = true; const scr = APP.current;
      Store.query(path, params).then(x => { c.busy = false; const json = JSON.stringify(x.ok ? x.res : [x.message || '']); if (json === c.json) return; c.json = json; c.x = x; if (LC[key] === c && APP.current === scr) { c.skip = true; APP.refresh(); } });
    }
    return c.x;
  }
  // Loại hao hụt: danh mục tham chiếu của backend (tải 1 lần); không tải được thì dùng danh sách mặc định.
  const WASTE_DEFAULT = [['OVERPRODUCTION', 'Nấu dư'], ['QC_REJECT', 'QC loại'], ['SPOILAGE', 'Hư hỏng'], ['PREP_LOSS', 'Hao hụt sơ chế'], ['RETURNED', 'Khách trả lại'], ['PACKING_DAMAGE', 'Hỏng / loại khi đóng gói']].map(([v, l]) => ({ v, l }));
  let wasteTypeP = null, wasteTypeList = WASTE_DEFAULT;
  const wasteTypes = () => wasteTypeP || (wasteTypeP = Store.query('MetaData/GetReferenceLists', { name: 'WasteType' }).then(x => { const li = x.ok && x.list[0] && (x.list[0].listItem || []); if (li && li.length) wasteTypeList = li.map(i => ({ v: i.code, l: i.label || i.code })); return wasteTypeList; }));
  const wasteName = v => (wasteTypeList.find(t => String(t.v).toUpperCase() === String(v || '').toUpperCase()) || {}).l || v || '';
  // Ghi hao hụt: theo mẻ (ProductionBatch/RecordWaste) hoặc ghi tự do theo mẻ / lệnh / món + ca (MealWasteRecord/Record).
  function wasteModal(opt, after) {
    const b = opt.batchId && Store.get('ProductionBatche', opt.batchId);
    const sel = (id, rows, label, ph) => `<select class="select" id="${id}"><option value="">${ph}</option>${rows.map(r => `<option value="${esc(r.Id)}">${esc(label(r))}</option>`).join('')}</select>`;
    wasteTypes().then(types => UI.modal({
      title: 'Ghi hao hụt' + (b ? ' – mẻ ' + esc(b.BatchNumber || '') : ''), size: 'sm',
      body: `${b ? '' : `<div class="field"><label>Mẻ sản xuất</label>${sel('wB', Store.rows('ProductionBatche').filter(r => !['Planned', 'Cancelled'].includes(r.Status)).sort((x, y) => String(y.StartDate || '').localeCompare(String(x.StartDate || ''))).slice(0, 200), r => (r.BatchNumber || '') + ' · ' + Store.label('Dishe', r.DishId), '— Không gắn mẻ —')}</div>
        <div class="field mt"><label>Lệnh sản xuất</label>${sel('wO', Store.rows('ProductionOrder').filter(r => r.Status !== 'Cancelled').sort((x, y) => String(y.PlannedStartDate || '').localeCompare(String(x.PlannedStartDate || ''))).slice(0, 200), r => (r.ProductionOrderNumber || '') + ' · ' + Store.label('Dishe', r.DishId), '— Không gắn lệnh —')}</div>
        <div class="field mt"><label>Món (khi không chọn mẻ / lệnh)</label>${sel('wD', Store.rows('Dishe'), r => Store.label('Dishe', r), '— Chọn món —')}</div>
        <div class="field mt"><label>Ca</label>${sel('wS', Store.rows('Shift'), r => Store.label('Shift', r), '— Chọn ca —')}</div>`}
        <div class="field ${b ? '' : 'mt'}"><label>Loại hao hụt <span class="req">*</span></label><select class="select" id="wT">${types.map(t => `<option value="${esc(t.v)}">${esc(t.l)}</option>`).join('')}</select></div>
        <div class="field mt"><label>Số suất</label><input type="number" min="0" step="any" class="input" id="wM"></div>
        <div class="field mt"><label>Khối lượng (kg)</label><input type="number" min="0" step="any" class="input" id="wK"></div>
        <div class="field mt"><label>Lý do</label><textarea class="input" rows="2" id="wR" maxlength="1000"></textarea></div>
        <div class="field mt"><label>Cách xử lý</label><input class="input" id="wP" maxlength="500" placeholder="Vd. hủy theo quy trình, chuyển suất nhân viên…"></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="wOk">Ghi hao hụt</button>',
      onMount: (m, c) => m.querySelector('#wOk').onclick = () => {
        const v = k => { const e = m.querySelector('#' + k); return e ? e.value.trim() : ''; };
        const meals = v('wM'), kg = v('wK');
        if (!(+meals > 0) && !(+kg > 0)) return UI.toast('Nhập số suất hoặc khối lượng hao hụt (lớn hơn 0).', 'err');
        if (!b && !v('wB') && !v('wO') && !v('wD')) return UI.toast('Chọn mẻ, lệnh sản xuất hoặc món bị hao hụt.', 'err');
        const tables = ['MealWasteRecord', 'ProductionBatche', 'ProductionOutput'];
        const done = r => { if (!r.ok) return; c(); UI.toast('Đã ghi hao hụt', 'ok'); if (after) after(); };
        if (b) return Store.act('ProductionBatch/RecordWaste', { form: { Id: b.Id, WasteType: v('wT'), QuantityMeals: meals, QuantityKg: kg, Reason: v('wR'), Disposition: v('wP') }, tables }).then(done);
        return Store.act('MealWasteRecord/Record', { form: { ProductionBatchId: v('wB'), ProductionOrderId: v('wO'), DishId: v('wD'), ShiftId: v('wS'), WasteType: v('wT'), QuantityMeals: meals, QuantityKg: kg, Reason: v('wR'), Disposition: v('wP') }, tables }).then(done);
      }
    }));
  }
  // ---------- Bảng điều độ mẻ ----------
  const bs = { date: null, area: '' };
  // Dòng mẻ của ProductionBatch/GetBoard → cùng dạng với dòng bảng ProductionBatche để dùng chung phần vẽ.
  const boardRow = b => ({ Priority: b.priorityLabel || null, Id: b.id, BatchNumber: b.batchNumber, ProductionOrderId: b.productionOrderId, DishId: b.dishId, DishName: b.dishName, ShiftId: b.shiftId, ProductionLineId: b.productionLineId, PlannedQuantity: b.plannedQuantity, ActualQuantity: b.actualQuantity, Status: b.status, QcStatus: b.qcStatus, Temp: b.lastCoreTemperatureC, TempOk: b.coreTemperatureOk, Sample: b.sampleRetained, live: true });
  const shiftIdOf = b => b.live ? b.ShiftId : poOf(b).ShiftId;
  const lineIdOf = b => b.live ? b.ProductionLineId : poOf(b).ProductionLineId;
  APP.register('prod-batch', (el, f) => {
    const date = bs.date || Store.ctx.date; bs.date = date;
    const shifts = Store.rows('Shift'), lines = Store.rows('ProductionLine');
    const local = Store.rows('ProductionBatche').filter(b => U.day(planOf(poOf(b)).ProductionDate) === date);
    // Chế độ API: ProductionBatch/GetBoard trả mẻ của ngày kèm nhiệt độ lõi gần nhất, lưu mẫu; lỗi thì dùng dữ liệu bảng.
    const bx = liveData('prod-batch', 'ProductionBatch/GetBoard', { ProductionDate: date });
    const board = bx && bx.ok && bx.data ? bx.data : null;
    const batches = board ? (board.listBatch || []).map(boardRow) : local;
    const orphan = batches.filter(b => !lines.some(l => l.Id === lineIdOf(b)));
    const cols = orphan.length ? lines.concat({ Id: null, Name: 'Chưa gán dây chuyền' }) : lines;
    const inCell = (b, s, l) => shiftIdOf(b) === s.Id && (l.Id ? lineIdOf(b) === l.Id : orphan.includes(b));
    const dates = [...new Set(Store.rows('ProductionPlan').map(p => U.day(p.ProductionDate)))].filter(d => Store.rows('ProductionOrder').some(o => U.day(planOf(o).ProductionDate) === d)).sort().slice(-5);
    const cnt = U.group(batches, 'Status');
    el.innerHTML = APP.header(f, `<a class="btn" href="#/prod-plan">${I('calendar')} Kế hoạch</a><a class="btn" href="#/prod-order">${I('clipboard')} Lệnh sản xuất</a>`, { sub: 'Điều độ mẻ sản xuất theo ngày × ca × dây chuyền. Bấm thẻ mẻ để chuyển bước: Kế hoạch → Đang nấu → Chờ QC → Đạt/Không đạt → Đã đóng gói. Mẻ không đạt QC bị chặn chuyển sang đóng gói.' }) + `
    <div class="card panel mb"><div class="row-flex"><span class="small muted">Ngày sản xuất:</span>${dates.map(d => `<button class="chip ${d === date ? 'active' : ''}" data-d="${d}">${U.date(d)}${d === Store.ctx.date ? ' · hôm nay' : ''}</button>`).join('')}${Store.isLive ? `<input type="date" class="input" data-dp value="${esc(date)}" style="width:auto" title="Chọn ngày sản xuất khác">` : ''}<span class="grow"></span>${BFLOW.concat('Failed').map(s => `<span class="badge ${s === 'Failed' ? 'red' : ''}">${UI.statusVi('ProductionBatche', 'Status', s)} · ${(cnt[s] || []).length}</span>`).join(' ')}</div></div>
    <div class="kpis">${UI.kpi('Số mẻ', batches.length, `${new Set(batches.map(b => b.ProductionOrderId)).size} lệnh`, 'factory', 'green')}${UI.kpi('Phần món kế hoạch', U.num(U.sum(batches, 'PlannedQuantity')), '', 'utensils', 'blue')}${UI.kpi('Phần món đạt QC', U.num(board ? board.passedPortions : U.sum(batches.filter(b => b.QcStatus === 'PASS'), 'ActualQuantity')), '', 'shield', 'green')}${UI.kpi('Tạm giữ / Loại', batches.filter(b => ['HOLD', 'REJECT'].includes(b.QcStatus)).length, '', 'alert', 'red')}</div>
    <div class="card panel"><div class="board" style="grid-template-columns:150px repeat(${cols.length},minmax(200px,1fr))"><div class="bh">Ca \\ Dây chuyền</div>${cols.map(l => `<div class="bh">${esc(l.Name)}${l.Id ? `<div style="font-weight:500;text-transform:none">${U.num(l.CapacityMealPerBatch)} suất/mẻ · ${U.num(l.MinutesPerBatch)}'</div>` : ''}</div>`).join('')}
      ${shifts.map(s => `<div class="bh" style="display:flex;flex-direction:column;justify-content:center">${esc(s.Name)}<span style="font-weight:500">${U.time(s.StartTime)}–${U.time(s.EndTime)}</span></div>${cols.map(l => `<div class="cell">${batches.filter(b => inCell(b, s, l)).sort((x, y) => (y.Priority ? 1 : 0) - (x.Priority ? 1 : 0)).map(b => `<div class="batch ${b.Status}" data-b="${b.Id}"${b.Priority ? ' style="border-left:4px solid var(--red,#D64545)"' : ''}>${b.Priority ? `<div class="small" style="color:var(--red,#D64545);font-weight:700">${esc(b.Priority)} – ưu tiên</div>` : ''}<b>${esc(b.DishName || Store.label('Dishe', b.DishId))}</b><small>${esc(b.BatchNumber)} · ${U.num(b.ActualQuantity || b.PlannedQuantity)} suất</small>${b.live ? `<small>${nil(b.Temp) ? 'Chưa đo nhiệt độ lõi' : 'Lõi ' + U.num(b.Temp, 1) + '°C ' + (b.TempOk ? '✓' : '✗')} · ${b.Sample ? 'đã lưu mẫu' : 'chưa lưu mẫu'}</small>` : ''}<div class="row-flex" style="margin-top:4px">${UI.badge(b.Status, 'ProductionBatche', 'Status')}${b.QcStatus ? UI.badge(b.QcStatus, 'ProductionBatche', 'QcStatus') : ''}</div></div>`).join('') || '<span class="muted small" style="margin:auto">—</span>'}</div>`).join('')}`).join('')}</div></div>`;
    el.querySelectorAll('[data-d]').forEach(b => b.onclick = () => { bs.date = b.dataset.d; APP.refresh(); });
    const dp = el.querySelector('[data-dp]'); if (dp) dp.onchange = () => { if (dp.value) { bs.date = dp.value; APP.refresh(); } };
    el.querySelectorAll('[data-b]').forEach(c => c.onclick = () => batchAct(c.dataset.b));
  });
  // UG-1 C-15: công đoạn của mẻ (Sơ chế → Nấu → Chia suất…) – ProductionBatch/GetDetail.listStage; nút Bắt đầu / Xong gọi StartStage / FinishStage.
  // Máy chủ chặn bắt đầu khi công đoạn trước chưa xong và chặn kết thúc nấu khi công đoạn bắt buộc chưa xong.
  function stagePane(p, id) {
    Store.query('ProductionBatch/GetDetail', { id }).then(x => {
      if (!p.isConnected) return;
      if (!x.ok || !x.data) { p.innerHTML = failHtml(x, 'công đoạn'); return; }
      const st = x.data.listStage || [], canEdit = Store.perm('prod-batch').edit !== false;
      if (!st.length) { p.innerHTML = '<div class="muted small">Mẻ này không theo dõi công đoạn.</div>'; return; }
      const cur = st.findIndex(s => s.status === 'InProgress');
      const next = cur >= 0 ? -1 : st.findIndex(s => s.status !== 'Done');
      p.innerHTML = `<table class="table dc-table dt"><thead><tr><th>Công đoạn</th><th>Trạng thái</th><th>Tổ / người làm</th><th>Bắt đầu</th><th>Xong</th><th></th></tr></thead><tbody>${st.map((s, i) => `<tr>
        <td><b>${esc(s.stageName || s.stageCode)}</b>${s.isRequired ? ' <span class="muted small">bắt buộc</span>' : ''}</td><td>${UI.tag(s.statusName || s.status, s.status === 'Done' ? 'green' : s.status === 'InProgress' ? 'orange' : 'gray')}</td>
        <td class="small">${esc([s.teamName || s.teamCode, (s.employeeNames || []).join(', ')].filter(Boolean).join(' · '))}</td><td class="small">${esc(U.dt(s.startedAt))}</td><td class="small">${esc(U.dt(s.finishedAt))}</td>
        <td>${canEdit && i === cur ? `<button class="btn xs primary" data-stg="FinishStage" data-code="${esc(s.stageCode)}">Xong</button>` : canEdit && i === next ? `<button class="btn xs orange" data-stg="StartStage" data-code="${esc(s.stageCode)}">Bắt đầu</button>` : ''}</td></tr>`).join('')}</tbody></table>`;
      p.querySelectorAll('[data-stg]').forEach(b => b.onclick = () => Store.act('ProductionBatch/' + b.dataset.stg, { form: { Id: id, StageCode: b.dataset.code }, tables: ['ProductionBatche'] })
        .then(res => { if (res.ok) { UI.toast(b.dataset.stg === 'StartStage' ? 'Đã bắt đầu công đoạn' : 'Đã xong công đoạn', 'ok'); stagePane(p, id); } }));
    });
  }
  function batchAct(id) {
    const b = Store.get('ProductionBatche', id); if (!b) return UI.toast('Đang tải dữ liệu mẻ – thử lại sau giây lát.', 'warn');
    const po = poOf(b);
    const i = BFLOW.indexOf(b.Status);
    const temps = Store.by('TemperatureLog', 'ProductionBatchId', id);
    const fsr = Store.by('FoodSafetyChecklistResult', 'ProductionBatchId', id);
    UI.detail('ProductionBatche', id, { screen: 'prod-batch',
      // Chế độ API: ProductionBatch/GetDetail – điều kiện còn thiếu, công đoạn, nguyên liệu theo định lượng, phiếu xuất, nhiệt độ, lưu mẫu, sản lượng, hao hụt.
      tabs: Store.isLive ? [{ label: 'Nguyên liệu & an toàn', html: p => { Store.query('ProductionBatch/GetDetail', { id }).then(x => { if (p.isConnected) p.innerHTML = x.ok && x.data ? batchDetailHtml(x.data) : failHtml(x, 'chi tiết mẻ'); }); return LOADING; } },
        { label: 'Công đoạn', html: p => { stagePane(p, id); return LOADING; } }] : [],
      top: `${UI.stepper(BFLOW.map(s => UI.statusVi('ProductionBatche', 'Status', s)), b.Status === 'Failed' ? 3 : i, b.Status === 'Failed')}
        <div class="kv mt mb"><div><label>Lệnh sản xuất</label><span>${esc(po.ProductionOrderNumber)} · ${esc(Store.label('Shift', po.ShiftId))}</span></div><div><label>Công thức (phiên bản khóa)</label><span>${esc(Store.label('Recipe', b.RecipeId))}</span></div><div><label>Nhiệt độ lõi (TemperatureLog)</label><span>${temps.map(t => `${U.num(t.TemperatureC)}°C ${t.IsWithinRange ? '✓' : '✗'}`).join(', ') || '<span class="muted">Chưa đo</span>'}</span></div><div><label>Checklist ATTP theo mẻ</label><span>${fsr.length ? fsr.filter(x => x.ResultCode === 'PASS').length + '/' + fsr.length + ' đạt' : '<span class="muted">–</span>'}</span></div></div>`,
      foot: () => ({ Planned: `<button class="btn orange" data-act="cook">${I('play')} Bắt đầu nấu</button>`, Cooking: `<button class="btn orange" data-act="finish">Kết thúc nấu → Chờ QC</button>`, QCPending: `${Store.isLive && !Store.by('ProductionOutput', 'ProductionBatchId', id).length ? `<button class="btn orange" data-act="output">Ghi sản lượng</button>` : ''}${Store.isLive ? `<button class="btn" data-act="sample">Ghi lưu mẫu</button>` : ''}<button class="btn primary" data-act="pass">QC: PASS</button><button class="btn" data-act="hold">HOLD</button><button class="btn danger" data-act="reject">REJECT</button>`, Passed: `<button class="btn primary" data-act="pack">Tạo lệnh đóng gói</button>${Store.isLive ? `<button class="btn danger" data-act="reject">Loại mẻ</button>` : ''}` }[b.Status] || '') + (Store.isLive && ['Cooking', 'QCPending', 'Passed', 'Packed', 'Failed'].includes(b.Status) ? `<button class="btn" data-act="waste">Ghi hao hụt</button>` : ''),
      onAct: (a, row, close) => {
        const fin = m => { close(); UI.toast(m, 'ok'); APP.refresh(); };
        // Chế độ API: action nghiệp vụ ProductionBatch/* (backend kiểm tra nhiệt độ lõi, lưu mẫu, sản lượng và tự ghi các bảng liên quan).
        const PT = ['ProductionBatche', 'ProductionOrder', 'ProductionPlan', 'TemperatureLog', 'ProductionOutput', 'MealWasteRecord', 'PackingOrder', 'FoodSafetyChecklistResult'];
        const call = (action, form, demo) => Store.act('ProductionBatch/' + action, { form: Object.assign({ Id: id }, form), tables: PT, demo });
        if (a === 'cook') call('Start', null, () => { Store.update('ProductionBatche', id, { Status: 'Cooking', StartDate: U.iso(new Date()) }); if (po.Status === 'Released') Store.update('ProductionOrder', po.Id, { Status: 'InProgress' }); }).then(r => { if (r.ok) fin('Mẻ bắt đầu nấu'); });
        if (a === 'waste') wasteModal({ batchId: id }, () => { close(); APP.refresh(); });
        // UG-1 C-07: mẻ Chờ QC chưa có sản lượng (vd. kết thúc từ bot bản cũ) → ghi sản lượng để cho Đạt / đóng gói được.
        if (a === 'output') UI.modal({ title: 'Ghi sản lượng mẻ', size: 'sm', body: `<div class="field"><label>Số suất thực tế <span class="req">*</span></label><input type="number" min="0" class="input" id="oq" value="${b.ActualQuantity || b.PlannedQuantity || ''}"></div><div class="field mt"><label>Số suất hỏng / loại</label><input type="number" min="0" class="input" id="orj" value="0"></div>`, foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="oOk">Ghi sản lượng</button>',
          onMount: (m, cl) => m.querySelector('#oOk').onclick = () => { const q = m.querySelector('#oq').value; if (q === '' || Number(q) <= 0) return UI.toast('Nhập số suất thực tế', 'err'); call('RecordOutput', { ProducedQuantity: q, RejectedQuantity: m.querySelector('#orj').value || 0 }).then(r => { if (r.ok) { cl(); close(); UI.toast('Đã ghi sản lượng mẻ', 'ok'); APP.refresh(); batchAct(id); } }); } });
        if (a === 'sample') call('RecordSampleRetained', { ActualValue: 'Đã lưu mẫu' }).then(r => { if (r.ok) { close(); UI.toast('Đã ghi lưu mẫu thức ăn', 'ok'); APP.refresh(); batchAct(id); } });
        // UG-1 C-09: ngưỡng nhiệt độ lõi lấy từ máy chủ (BusinessRules:CoreTempMinC – mặc định 70 °C), không điền sẵn nhiệt độ;
        // dưới ngưỡng bắt buộc nhập Hành động khắc phục (máy chủ cũng chặn – CoreTempCorrectiveRequired).
        if (a === 'finish') {
          let minC = 70;
          UI.modal({ title: 'Ghi nhận sản lượng & nhiệt độ lõi', size: 'sm',
            body: `<div class="field"><label>Sản lượng thực tế (suất) <span class="req">*</span></label><input type="number" min="0" class="input" id="aq" value="${b.PlannedQuantity || ''}"></div>
              <div class="field mt"><label>Nhiệt độ lõi °C <span class="req">*</span> <span class="muted small" id="tcMin">(đạt khi ≥ ${minC} °C)</span></label><input type="number" step="0.1" class="input" id="tc" placeholder="Đo bằng nhiệt kế que"></div>
              <div class="field mt hide" id="caBox"><label>Hành động khắc phục <span class="req">*</span></label><textarea class="input" id="ca" rows="2" placeholder="vd. Gia nhiệt lại 10 phút, đo lại"></textarea><span class="hint">Nhiệt độ dưới ngưỡng – mẻ chưa cho Đạt QC được tới khi đo lại đạt.</span></div>`,
            foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="ok">Lưu</button>',
            onMount: (m, c2) => {
              const tcIn = m.querySelector('#tc'), caBox = m.querySelector('#caBox');
              const sync = () => caBox.classList.toggle('hide', !(tcIn.value !== '' && Number(tcIn.value) < minC));
              tcIn.oninput = sync;
              if (Store.isLive) Store.query('ProductionBatch/GetDetail', { id }).then(x => { if (x.ok && x.data && x.data.coreTempMinC) { minC = Number(x.data.coreTempMinC); const l = m.querySelector('#tcMin'); if (l) l.textContent = `(đạt khi ≥ ${U.num(minC, 1)} °C)`; sync(); } });
              m.querySelector('#ok').onclick = () => {
                const aqv = m.querySelector('#aq').value, tcv = tcIn.value, ca = m.querySelector('#ca').value.trim();
                if (aqv === '' || Number(aqv) <= 0) return UI.toast('Nhập sản lượng thực tế', 'err');
                if (tcv === '') return UI.toast('Nhập nhiệt độ lõi đã đo', 'err');
                const aq = Number(aqv), tc = Number(tcv), low = tc < minC;
                if (low && !ca) return UI.toast(`Nhiệt độ ${U.num(tc, 1)} °C dưới ngưỡng ${U.num(minC, 1)} °C – nhập hành động khắc phục`, 'err');
                if (Store.isLive) {
                  // Đo nhiệt độ lõi → kết thúc nấu (Chờ QC) → ghi sản lượng. Dừng ở bước đầu tiên bị backend từ chối.
                  return call('RecordCoreTemperature', { TemperatureC: tc, DeviceCode: 'PROBE-01', CorrectiveAction: low ? ca : null })
                    .then(r => r.ok ? call('Finish', { ActualQuantity: aq }) : r)
                    .then(r => r.ok ? call('RecordOutput', { ProducedQuantity: aq }) : r)
                    .then(r => { if (r.ok) { c2(); fin(low ? 'Đã ghi – nhiệt độ lõi dưới ngưỡng, cần đo lại trước khi cho Đạt QC' : 'Đã ghi nhiệt độ lõi + sản lượng – chờ QC'); } });
                }
                Store.update('ProductionBatche', id, { Status: 'QCPending', ActualQuantity: aq, EndDate: U.iso(new Date()) });
                Store.add('TemperatureLog', { ProductionBatchId: id, MeasurementType: 'CORE_TEMP', TemperatureC: tc, MinAllowedC: minC, IsWithinRange: low ? 0 : 1, MeasuredAt: U.iso(new Date()), DeviceCode: 'PROBE-01', SourceChannel: 'WEB', RecordedBy: Store.user.id, CorrectiveAction: low ? ca : null });
                c2(); fin(low ? 'Nhiệt độ lõi dưới ngưỡng – cảnh báo QC' : 'Đã ghi sản lượng – chờ QC');
              };
            } });
        }
        if (a === 'pass' || a === 'hold' || a === 'reject') {
          const qc = { pass: 'PASS', hold: 'HOLD', reject: 'REJECT' }[a];
          const bad = Store.by('TemperatureLog', 'ProductionBatchId', id).some(t => t.IsWithinRange === 0);
          if (qc === 'PASS' && bad && !Store.isLive) return UI.toast('Không thể PASS: có lần đo nhiệt độ lõi ngoài ngưỡng. Gia nhiệt lại & đo lại.', 'err');
          UI.confirm(`Kết luận QC thành phẩm: ${UI.badge(qc)}`, note => {
            if (Store.isLive) return Store.act({ PASS: 'ProductionBatch/Release', HOLD: 'ProductionBatch/Hold', REJECT: 'ProductionBatch/Reject' }[qc], { form: { Id: id, Note: note }, tables: PT }).then(r => { if (!r.ok) return; close(); UI.toast('QC: ' + qc, qc === 'PASS' ? 'ok' : 'warn'); APP.refresh(); });
            Store.update('ProductionBatche', id, { QcStatus: qc, Status: qc === 'PASS' ? 'Passed' : qc === 'REJECT' ? 'Failed' : 'QCPending', AcceptedQty: qc === 'PASS' ? b.ActualQuantity : 0, RejectedQty: qc === 'REJECT' ? b.ActualQuantity : 0 }, note);
            Store.add('ProductionOutput', { ProductionBatchId: id, DishId: b.DishId, ProducedQuantity: b.ActualQuantity || 0, AcceptedQuantity: qc === 'PASS' ? b.ActualQuantity : 0, RejectedQuantity: qc === 'REJECT' ? b.ActualQuantity : 0, QualityStatus: qc });
            if (qc === 'REJECT') Store.add('MealWasteRecord', { ProductionBatchId: id, ProductionOrderId: po.Id, DishId: b.DishId, ShiftId: po.ShiftId, WasteType: 'QC_REJECT', QuantityMeals: b.ActualQuantity, Reason: note || 'QC không đạt', Disposition: 'Hủy theo quy trình', OccurredAt: U.iso(new Date()), RecordedBy: Store.user.id });
            close(); UI.toast('QC: ' + qc + (qc === 'REJECT' ? ' – đã ghi hao hụt MealWasteRecord' : ''), qc === 'PASS' ? 'ok' : 'warn'); APP.refresh();
          }, { input: 'Ghi chú QC', required: qc !== 'PASS', ok: 'Xác nhận' });
        }
        if (a === 'pack' && Store.isLive) return Store.act('PackingOrder/GenerateFromBatch', { form: { ProductionBatchId: id }, tables: PT }).then(r => { if (r.ok) fin('Đã sinh lệnh đóng gói (PackingOrder) từ mẻ'); });
        if (a === 'pack') { const o = Store.rows('Order').find(x => U.day(x.MealDate) === U.day(planOf(po).ProductionDate) && x.Status !== 'Cancelled'); const n = Store.rows('PackingOrder').length + 1; Store.add('PackingOrder', { PackingOrderNumber: 'DG-' + String(n).padStart(4, '0'), ProductionBatchId: id, OrderId: o && o.Id, PlannedQty: b.AcceptedQty || b.ActualQuantity, PackingDate: U.iso(new Date()), Status: 'Packing' }); Store.update('ProductionBatche', id, { Status: 'Packed' }); fin('Đã tạo lệnh đóng gói (PackingOrder) từ mẻ – BRD'); }
      } });
  }
  function batchDetailHtml(d) {
    const bt = d.batch || {}, out = d.output;
    return `${notes(d.listBlocker, 'gap')}${notes(d.listWarning)}
      <div class="kv mb"><div><label>Ngưỡng nhiệt độ lõi</label><span>≥ ${U.num(d.coreTempMinC, 1)}°C</span></div><div><label>Lưu mẫu thức ăn</label><span>${bt.sampleRetained ? 'Đã lưu mẫu' : '<span class="muted">Chưa lưu mẫu</span>'}</span></div><div><label>Xuất nguyên liệu</label><span>${bt.materialIssued ? 'Đã xuất kho' : '<span class="muted">Chưa xuất kho</span>'}</span></div><div><label>Sản lượng</label><span>${out ? `${U.num(out.producedQuantity)} phần · đạt ${U.num(out.acceptedQuantity || 0)} · loại ${U.num(out.rejectedQuantity || 0)}` : '<span class="muted">Chưa ghi</span>'}</span></div></div>
      ${(d.listStage || []).length ? `<h3>Công đoạn</h3>${mini(['Công đoạn', 'Tổ', 'Người làm', 'Bắt đầu', 'Xong', 'Trạng thái'], d.listStage.map(s => [esc(s.stageName || s.stageCode), esc(s.teamName || ''), esc((s.employeeNames || []).join(', ')), esc(U.dt(s.startedAt)), esc(U.dt(s.finishedAt)), esc(s.statusName || s.status || '')]))}` : ''}
      <h3 class="mt">Nguyên liệu theo định lượng</h3>${mini(['Nguyên liệu', 'Định lượng / suất', 'Cần', 'Đã xuất'], (d.listMaterial || []).map(m => [esc(m.ingredientName || m.ingredientCode || ''), U.num(m.quantityPerServing, 3) + ' ' + esc(m.recipeUnitName || ''), U.num(m.requiredQuantity, 3) + ' ' + esc(m.recipeUnitName || ''), U.num(m.issuedQuantity, 3) + ' ' + esc(m.issuedUnitName || '')]))}
      <h3 class="mt">Phiếu xuất nguyên liệu</h3>${mini(['Số phiếu', 'Ngày', 'Kho', 'Trạng thái'], (d.listIssue || []).map(i => [esc(i.issueNumber || ''), esc(U.date(i.issueDate)), esc(Store.label('Warehouse', i.warehouseId)), esc(i.statusName || i.status || '')]))}
      <h3 class="mt">Nhiệt độ lõi</h3>${mini(['Thời điểm', 'Nhiệt độ', 'Kết quả', 'Khắc phục'], (d.listTemperature || []).map(t => [esc(U.dt(t.measuredAt)), U.num(t.temperatureC, 1) + '°C', t.isWithinRange ? 'Đạt' : '<b style="color:var(--red)">Không đạt</b>', esc(t.correctiveAction || '')]))}
      <h3 class="mt">Lưu mẫu</h3>${mini(['Thời điểm', 'Mô tả mẫu'], (d.listSample || []).map(x => [esc(U.dt(x.checkedAt)), esc(x.actualValue || '')]))}
      <h3 class="mt">Hao hụt</h3>${mini(['Thời điểm', 'Loại', 'Số suất', 'Kg', 'Lý do'], (d.listWaste || []).map(w => [esc(U.dt(w.occurredAt)), esc(wasteName(w.wasteType)), U.num(w.quantityMeals), U.num(w.quantityKg), esc(w.reason || '')]))}`;
  }
  // ---------- Kế hoạch sản xuất ----------
  APP.register('prod-plan', (el, f) => {
    APP.genericList(el, f, { noTransit: true, cols: [{ k: 'PlanNumber', label: 'Số kế hoạch' }, { k: 'ProductionDate', label: 'Ngày SX' }, { k: 'ShiftId', label: 'Ca' }, { k: 'OrganizationUnitId', label: 'Đơn vị' }, { k: 'n', label: 'Số món', col: false, num: true, render: r => Store.by('ProductionPlanLine', 'ProductionPlanId', r.Id).length }, { k: 'q', label: 'Tổng suất', col: false, num: true, render: r => U.num(U.sum(Store.by('ProductionPlanLine', 'ProductionPlanId', r.Id), 'PlannedQuantity')) }, { k: 'o', label: 'Lệnh SX', col: false, num: true, render: r => Store.by('ProductionOrder', 'ProductionPlanId', r.Id).length }, { k: 'Status', label: 'Trạng thái' }],
      top: `<div class="notice warn">${I('alert')}<div><b>GAP-C-04:</b> BRD lập kế hoạch theo ngày, ca, line, menu – ProductionPlan chưa có ProductionLineId/MenuId ở header. Dây chuyền được gán ở cấp Lệnh sản xuất.</div></div>` });
    // Chế độ API: ProductionPlan/Generate lập (hoặc tính lại) kế hoạch Nháp của ngày từ đơn Đã xác nhận / Đã chốt.
    const ha = Store.isLive && el.querySelector('.heading-actions');
    if (ha) { ha.insertAdjacentHTML('afterbegin', `<button class="btn orange" data-gen>${I('flow')} Lập từ đơn hàng</button>`); ha.querySelector('[data-gen]').onclick = generatePlan; }
    APP.rowExtra(el, tr => { const r = Store.get('ProductionPlan', tr.dataset.row); const td = tr.querySelector('.actions');
      if (r.Status === 'Draft' && Store.canApprove('prod-plan')) { td.insertAdjacentHTML('afterbegin', `<button class="btn xs orange" data-a>Duyệt</button> `); td.querySelector('[data-a]').onclick = () => UI.confirm('Duyệt kế hoạch ' + esc(r.PlanNumber) + '?', () => Store.act('ProductionPlan/Approve', { form: { Id: r.Id }, tables: ['ProductionPlan', 'ApprovalRequest'], demo: () => Store.update('ProductionPlan', r.Id, { Status: 'Approved' }) }).then(x => { if (x.ok) { UI.toast('Đã duyệt kế hoạch', 'ok'); APP.refresh(); } })); }
      if (Store.isLive) { td.insertAdjacentHTML('afterbegin', `<button class="btn xs" data-xs title="Xuất phiếu sản xuất">${I('export')}</button> `); td.querySelector('[data-xs]').onclick = () => Store.download('ProductionPlan/ExportProductionSheet', { id: r.Id }); }
      if (r.Status === 'Approved' && !Store.by('ProductionOrder', 'ProductionPlanId', r.Id).length) { td.insertAdjacentHTML('afterbegin', `<button class="btn xs primary" data-g>Ra lệnh SX</button> `); td.querySelector('[data-g]').onclick = () => release(r); } });
  });
  function generatePlan() {
    UI.modal({ title: 'Lập kế hoạch sản xuất từ đơn hàng', size: 'sm',
      body: `<p style="margin-top:0" class="small muted">Tổng hợp số phần từng món từ các đơn Đã xác nhận / Đã chốt của ngày. Kế hoạch Nháp cùng ngày sẽ được tính lại; kế hoạch đã duyệt thì không thay đổi.</p>
        <div class="field"><label>Ngày sản xuất <span class="req">*</span></label><input type="date" class="input" id="gD" value="${esc(Store.ctx.date)}"></div>
        <div class="field mt"><label>Đơn vị</label><select class="select" id="gU"><option value="">— Không chọn —</option>${Store.rows('OrganizationUnit').map(o => `<option value="${esc(o.Id)}" ${o.Id === Store.ctx.branch ? 'selected' : ''}>${esc(Store.label('OrganizationUnit', o))}</option>`).join('')}</select></div>
        <div class="field mt"><label>Ghi chú</label><textarea class="input" rows="2" id="gN" maxlength="1000"></textarea></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="gOk">Lập kế hoạch</button>',
      onMount: (m, c) => m.querySelector('#gOk').onclick = () => {
        const date = m.querySelector('#gD').value; if (!date) return UI.toast('Chọn ngày sản xuất.', 'err');
        Store.act('ProductionPlan/Generate', { form: { ProductionDate: date, OrganizationUnitId: m.querySelector('#gU').value, Note: m.querySelector('#gN').value.trim() }, tables: ['ProductionPlan', 'ProductionPlanLine'] }).then(r => {
          if (!r.ok) return; const d = r.data || {}, p = d.productionPlan || {};
          c(); UI.toast(`Đã lập kế hoạch ${esc(p.planNumber || '')} · ${(d.listLine || []).length} món · ${U.num(d.totalPortions || 0)} phần`, 'ok'); warnToast(d); APP.refresh();
        });
      } });
  }
  function release(pl) {
    const lines = Store.by('ProductionPlanLine', 'ProductionPlanId', pl.Id);
    const lineFor = d => { const cat = Store.label('DishCategorie', (Store.get('Dishe', d) || {}).DishCategoryId); const L = Store.rows('ProductionLine'); return /mặn/i.test(cat) ? L[0] : /rau|canh/i.test(cat) ? L[1] : /cơm/i.test(cat) ? L[2] : L[3]; };
    UI.confirm(`Phát hành ${lines.length} lệnh sản xuất từ kế hoạch ${esc(pl.PlanNumber)}? Hệ thống chụp công thức đang hiệu lực (RecipeSnapshotJson) và giữ chỗ nguyên liệu (MaterialReservation).`, () => {
      // Chế độ API: ProductionPlan/Release sinh lệnh SX + mẻ theo quy tắc backend (lịch ngược từ giờ xuất bếp).
      if (Store.isLive) return Store.act('ProductionPlan/Release', { form: { Id: pl.Id }, tables: ['ProductionPlan', 'ProductionOrder', 'ProductionBatche', 'MaterialReservation'] }).then(x => { if (x.ok) { UI.toast('Đã phát hành lệnh sản xuất và mẻ', 'ok'); APP.go('prod-order'); } });
      let n = Store.rows('ProductionOrder').length;
      lines.forEach(pll => {
        const rc = Store.by('Recipe', 'DishId', pll.DishId).find(r => r.IsActive); if (!rc) return;
        const po = Store.add('ProductionOrder', { ProductionOrderNumber: 'LSX-' + U.day(pl.ProductionDate).slice(2).replace(/-/g, '') + '-' + String(++n).padStart(3, '0'), ProductionPlanId: pl.Id, DishId: pll.DishId, RecipeId: rc.Id, ShiftId: pl.ShiftId, ProductionLineId: lineFor(pll.DishId).Id, PlannedQty: pll.PlannedQuantity, Status: 'Released', PlannedStartDate: U.day(pl.ProductionDate) + 'T06:00', RecipeSnapshotJson: JSON.stringify({ recipe: rc.VersionCode }) });
        const cap = lineFor(pll.DishId).CapacityMealPerBatch * 3; let rem = pll.PlannedQuantity, k = 0;
        while (rem > 0) { const q = Math.min(cap, rem); rem -= q; Store.add('ProductionBatche', { BatchNumber: 'ME-' + po.ProductionOrderNumber.slice(4) + '-' + (++k), ProductionOrderId: po.Id, DishId: pll.DishId, RecipeId: rc.Id, PlannedQuantity: q, Status: 'Planned', StartDate: po.PlannedStartDate }); }
        Store.by('RecipeLine', 'RecipeId', rc.Id).forEach(l => { const g = Store.get('Ingredient', l.IngredientId); const q = +(l.Quantity * pll.PlannedQuantity / 100 / ((g.YieldPercent || 100) / 100)).toFixed(3); Store.add('MaterialReservation', { ProductionOrderId: po.Id, IngredientId: l.IngredientId, RequiredQty: q, ReservedQuantity: q, UnitId: l.UnitId, Status: 'Reserved' }); });
      });
      Store.update('ProductionPlan', pl.Id, { Status: 'Released' }); UI.toast('Đã phát hành lệnh sản xuất, mẻ và giữ chỗ nguyên liệu (DEMO)', 'ok'); APP.go('prod-order');
    }, { ok: 'Phát hành' });
  }
  // ---------- Lệnh sản xuất: chi tiết, đóng, hủy, tính giá thành ----------
  const OT = ['ProductionOrder', 'ProductionBatche', 'ProductionPlan', 'MaterialReservation', 'MaterialIssue', 'ActualCost', 'MaterialConsumptionVariance'];
  const RSV = { Reserved: 'Đã giữ', PartiallyIssued: 'Đã xuất một phần', Issued: 'Đã xuất', Released: 'Đã giải phóng' };
  const canCancel = o => ['Planned', 'Released'].includes(o.Status) && !Store.by('ProductionBatche', 'ProductionOrderId', o.Id).some(b => !['Planned', 'Cancelled'].includes(b.Status));
  const canCost = o => ['InProgress', 'Completed', 'Closed'].includes(o.Status);
  const money = v => nil(v) ? '–' : U.money(v);
  function calcCost(o, done) {
    return Store.act('ProductionOrder/CalculateCost', { form: { Id: o.Id }, tables: ['ActualCost'] }).then(r => {
      if (!r.ok) return; const bd = (r.data && r.data.breakdown) || {};
      UI.toast(`Đã tính giá thành · tổng ${U.money(bd.totalCost || 0)}${bd.costPerPortion ? ' · ' + U.money(bd.costPerPortion) + ' / phần' : ''}`, 'ok'); warnToast(bd); if (done) done();
    });
  }
  function orderAct(a, o, done) {
    const no = esc(o.ProductionOrderNumber || '');
    if (a === 'close') UI.confirm(`Đóng lệnh sản xuất <b>${no}</b>? Hệ thống tính chênh lệch tiêu hao nguyên liệu, giá thành thực tế và giải phóng nguyên liệu giữ chỗ còn dư. Lệnh đã đóng không mở lại được.`, note =>
      Store.act('ProductionOrder/Close', { form: { Id: o.Id, Note: note }, tables: OT }).then(r => { if (!r.ok) return; const d = r.data || {}, ac = d.actualCost || {}; UI.toast(`Đã đóng lệnh ${no} · giá thành ${U.money(ac.totalCost || 0)} · ${(d.listVariance || []).length} dòng chênh lệch tiêu hao`, 'ok'); warnToast(d.detail); done(); }), { input: 'Ghi chú', ok: 'Đóng lệnh' });
    if (a === 'cancel') UI.confirm(`Hủy lệnh sản xuất <b>${no}</b>? Các mẻ của lệnh và phiếu xuất nháp bị hủy, nguyên liệu giữ chỗ được giải phóng. Không hoàn tác được.`, reason => {
      if (reason.length < 3) return UI.toast('Lý do hủy cần ít nhất 3 ký tự.', 'err');
      Store.act('ProductionOrder/Cancel', { form: { Id: o.Id, Reason: reason }, tables: OT }).then(r => { if (!r.ok) return; UI.toast('Đã hủy lệnh ' + no, 'warn'); warnToast(r.data); done(); });
    }, { input: 'Lý do hủy', required: true, danger: true, ok: 'Hủy lệnh' });
    if (a === 'cost') UI.confirm(`Tính giá thành thực tế của lệnh <b>${no}</b>? Số liệu giá thành đã tính trước đó của lệnh sẽ được ghi đè.`, () => calcCost(o, done), { ok: 'Tính giá thành' });
  }
  const costKv = c => `<div class="kv mb"><div><label>Nguyên vật liệu</label><span>${U.money(c.materialCost || 0)}</span></div><div><label>Nhân công</label><span>${U.money(c.laborCost || 0)}</span></div><div><label>Chi phí chung</label><span>${U.money(c.overheadCost || 0)}</span></div><div><label>Hao hụt</label><span>${U.money(c.wasteCost || 0)}</span></div><div><label>Khác</label><span>${U.money(c.otherCost || 0)}</span></div><div><label>Tổng giá thành</label><span><b>${U.money(c.totalCost || 0)}</b></span></div><div><label>Số phần đạt</label><span>${U.num(c.acceptedPortions || 0)}</span></div><div><label>Giá thành / phần</label><span><b>${money(c.costPerPortion)}</b></span></div><div><label>Giá thành chuẩn / phần</label><span>${money(c.standardCostPerPortion)}</span></div><div><label>Tính lúc</label><span>${esc(U.dt(c.calculatedAt)) || '–'}</span></div></div>`;
  const orderWorkHtml = d => `${notes(d.listWarning)}
    <h3>Mẻ sản xuất</h3>${mini(['Số mẻ', 'Kế hoạch', 'Thực tế', 'Bắt đầu', 'Trạng thái', 'QC'], (d.listBatch || []).map(b => [esc(b.batchNumber || ''), U.num(b.plannedQuantity), U.num(b.actualQuantity), esc(U.dt(b.actualStart || b.plannedStart)), esc(b.statusName || b.status || ''), b.qcStatus ? UI.badge(b.qcStatus, 'ProductionBatche', 'QcStatus') : '']))}
    <h3 class="mt">Định lượng lúc phát hành</h3>${mini(['Nguyên liệu', 'Định lượng', 'Hao hụt'], (d.listRecipeLine || []).map(l => [esc(l.ingredientName || ''), U.num(l.quantity, 3) + ' ' + esc(l.unitName || ''), nil(l.wastePercent) ? '' : U.pct(l.wastePercent)]))}
    <h3 class="mt">Giữ chỗ nguyên liệu</h3>${mini(['Nguyên liệu', 'Cần', 'Đã giữ', 'Thiếu', 'Trạng thái'], (d.listReservation || []).map(x => [esc(x.ingredientName || x.ingredientCode || ''), U.num(x.requiredQuantity, 3) + ' ' + esc(x.unitName || ''), U.num(x.reservedQuantity, 3), x.shortQuantity > 0 ? `<b style="color:var(--red)">${U.num(x.shortQuantity, 3)}</b>` : '0', esc(RSV[x.status] || x.status || '')]))}
    <h3 class="mt">Phiếu xuất nguyên liệu</h3>${mini(['Số phiếu', 'Ngày', 'Kho', 'Trạng thái'], (d.listIssue || []).map(i => [esc(i.issueNumber || ''), esc(U.date(i.issueDate)), esc(Store.label('Warehouse', i.warehouseId)), esc(i.statusName || i.status || '')]))}`;
  const orderCostHtml = d => `<h3>Giá thành thực tế</h3>${d.actualCost ? notes(d.actualCost.listWarning) + costKv(d.actualCost) : '<div class="muted small mb">Chưa tính giá thành. Bấm “Tính giá thành” hoặc đóng lệnh để tính.</div>'}
    <h3 class="mt">Chênh lệch tiêu hao nguyên liệu</h3>${mini(['Nguyên liệu', 'Định mức', 'Thực tế', 'Chênh lệch', '%', 'Giá trị'], (d.listVariance || []).map(v => [esc(v.ingredientName || v.ingredientCode || ''), U.num(v.standardQuantity, 3) + ' ' + esc(v.unitName || ''), U.num(v.actualQuantity, 3), v.isOverThreshold ? `<b style="color:var(--red)">${U.num(v.varianceQuantity, 3)}</b>` : U.num(v.varianceQuantity, 3), nil(v.variancePercent) ? '' : U.pct(v.variancePercent), nil(v.varianceCost) ? '' : U.money(v.varianceCost)]))}`;
  function orderDetail(id) {
    const o = Store.get('ProductionOrder', id); if (!o) return UI.toast('Không tìm thấy lệnh sản xuất', 'err');
    let dq = null; const detail = () => dq || (dq = Store.query('ProductionOrder/GetDetail', { id }));
    const pane = render => p => { detail().then(x => { if (p.isConnected) p.innerHTML = x.ok && x.data ? render(x.data) : failHtml(x, 'chi tiết lệnh'); }); return LOADING; };
    const dis = Store.perm('prod-order').edit === false ? 'disabled' : '';
    UI.detail('ProductionOrder', id, { screen: 'prod-order', onChange: () => APP.refresh(),
      tabs: [{ label: 'Mẻ & nguyên liệu', html: pane(orderWorkHtml) }, { label: 'Giá thành & chênh lệch', html: pane(orderCostHtml) }],
      foot: () => (canCost(o) ? `<button class="btn" data-act="cost" ${dis}>${I('calc')} Tính giá thành</button>` : '') + (o.Status === 'Completed' ? `<button class="btn orange" data-act="close" ${dis}>Đóng lệnh</button>` : '') + (canCancel(o) ? `<button class="btn danger" data-act="cancel" ${dis}>Hủy lệnh</button>` : ''),
      onAct: (a, row, close) => orderAct(a, o, () => { close(); APP.refresh(); }) });
  }
  APP.register('prod-order', (el, f) => {
    APP.genericList(el, f);
    if (!Store.isLive) return;
    const dis = Store.perm('prod-order').edit === false ? 'disabled' : '';
    APP.rowExtra(el, tr => {
      const r = Store.get('ProductionOrder', tr.dataset.row); if (!r) return; const td = tr.querySelector('.actions');
      // Mở ngăn chi tiết theo ProductionOrder/GetDetail (mẻ, giữ chỗ, chênh lệch, giá thành) thay cho ngăn chi tiết chung.
      tr.onclick = e => { if (e.target.closest('button,a,input,select')) return; orderDetail(r.Id); };
      const vw = td && td.querySelector('[data-view]'); if (vw) vw.onclick = () => orderDetail(r.Id);
      if (!td) return;
      if (r.Status === 'Completed') { td.insertAdjacentHTML('afterbegin', `<button class="btn xs orange" data-oc ${dis}>Đóng lệnh</button> `); td.querySelector('[data-oc]').onclick = () => orderAct('close', r, () => APP.refresh()); }
      if (canCancel(r)) { td.insertAdjacentHTML('afterbegin', `<button class="btn xs danger" data-ox ${dis}>Hủy lệnh</button> `); td.querySelector('[data-ox]').onclick = () => orderAct('cancel', r, () => APP.refresh()); }
    });
  });
  // ---------- Hao hụt suất ăn ----------
  const wf = { from: '', to: '', type: '' };
  APP.register('prod-waste', (el, f) => {
    if (!Store.isLive) return APP.genericList(el, f);
    wasteTypes().then(() => { if (!wf.named && APP.current === 'prod-waste') { wf.named = true; APP.refresh(); } });
    const to = wf.to || Store.ctx.date, from = wf.from || addDays(to, -30);
    // Chế độ API: MealWasteRecord/GetList lọc theo kỳ / loại, trả kèm tên món, ca, mẻ, người ghi và tổng suất / kg.
    const x = liveData('prod-waste', 'MealWasteRecord/GetList', { From: from, To: to, WasteType: wf.type });
    const d = x && x.ok && x.data ? x.data : null, items = d ? d.listItem || [] : [];
    const info = {}; items.forEach(it => { if (it.record) info[it.record.id] = it; });
    const local = Store.rows('MealWasteRecord');
    const rows = d ? items.filter(it => it.record).map(it => Store.get('MealWasteRecord', it.record.id) || pascal(it.record)) : local;
    const of = r => info[r.Id] || {};
    const byType = Object.entries(U.group(rows, r => r.WasteType || '')).map(([k, v]) => ({ k, meals: U.sum(v, 'QuantityMeals') })).sort((a, b) => b.meals - a.meals);
    APP.genericList(el, f, { rows: () => rows,
      cols: [{ k: 'OccurredAt', label: 'Thời điểm' }, { k: 'WasteType', label: 'Loại hao hụt', render: r => UI.tag(of(r).wasteTypeName || wasteName(r.WasteType)), text: r => of(r).wasteTypeName || wasteName(r.WasteType) }, { k: 'DishId', label: 'Món', render: r => esc(of(r).dishName || Store.label('Dishe', r.DishId)), text: r => of(r).dishName || Store.label('Dishe', r.DishId) }, { k: 'ShiftId', label: 'Ca', render: r => esc(of(r).shiftCode || Store.label('Shift', r.ShiftId)), text: r => of(r).shiftCode || '' }, { k: 'bn', label: 'Mẻ / lệnh', col: false, render: r => esc(of(r).batchNumber || of(r).productionOrderNumber || ''), text: r => (of(r).batchNumber || '') + ' ' + (of(r).productionOrderNumber || '') }, { k: 'QuantityMeals', label: 'Số suất', num: true }, { k: 'QuantityKg', label: 'Kg', num: true }, { k: 'Reason', label: 'Lý do' }, { k: 'rb', label: 'Người ghi', col: false, render: r => esc(of(r).recordedByName || ''), text: r => of(r).recordedByName || '' }],
      top: `<div class="card panel mb"><div class="row-flex"><span class="small muted">Kỳ:</span><input type="date" class="input" data-wf="from" value="${esc(from)}" style="width:auto"><span class="small muted">đến</span><input type="date" class="input" data-wf="to" value="${esc(to)}" style="width:auto"><select class="select" data-wf="type" style="width:auto"><option value="">Loại hao hụt: Tất cả</option>${wasteTypeList.map(t => `<option value="${esc(t.v)}" ${t.v === wf.type ? 'selected' : ''}>${esc(t.l)}</option>`).join('')}</select><span class="grow"></span>${d ? `<span class="badge">Tổng ${U.num(d.totalMeals || 0)} suất · ${U.num(d.totalKg || 0, 1)} kg</span>${byType.slice(0, 4).map(t => `<span class="badge orange">${esc(wasteName(t.k) || 'Không ghi loại')} · ${U.num(t.meals)}</span>`).join(' ')}` : `<span class="muted small">${x ? esc(x.message || 'Không tải được danh sách theo kỳ') + ' – đang hiện toàn bộ' : 'Đang tải…'}</span>`}</div></div>` });
    el.querySelectorAll('[data-wf]').forEach(i => i.onchange = () => { wf[i.dataset.wf] = i.value; APP.refresh(); });
    // Ghi mới qua MealWasteRecord/Record (kiểm tra loại hao hụt, mẻ đã nấu, tự gán món / ca theo mẻ – lệnh) thay cho form thêm chung.
    const add = el.querySelector('[data-act="add"]'); if (add) add.onclick = () => wasteModal({}, () => APP.refresh());
    APP.rowExtra(el, tr => { if (!Store.get('MealWasteRecord', tr.dataset.row)) tr.querySelectorAll('[data-edit],[data-del]').forEach(b => b.remove()); });
  });
  // ---------- Giá thành / suất & biên lợi nhuận ----------
  const cm = { from: '', to: '' };
  function mealCalc(order, done) {
    return Store.act('MealCost/Calculate', { form: { OrderId: order.Id }, tables: ['MealCost'] }).then(r => {
      if (!r.ok) return; const d = r.data || {};
      UI.toast(`Đã tính giá vốn đơn ${esc(d.orderNumber || order.OrderNumber || '')} · ${nil(d.costPerMeal) ? '' : U.money(d.costPerMeal) + ' / suất · '}lãi gộp ${U.money(d.grossMargin || 0)}`, 'ok'); warnToast(d); if (done) done();
    });
  }
  function mealDetail(d) {
    if (!d) return;
    UI.modal({ title: 'Giá vốn suất – đơn ' + esc(d.orderNumber || ''), sub: `Ngày ăn ${esc(U.date(d.mealDate))} · ${U.num(d.acceptedMeals || 0)} suất khách nhận / ${U.num(d.orderedMeals || 0)} suất đặt`, size: 'lg',
      body: `${notes(d.listWarning)}${(d.listLine || []).map(l => `<h3>${esc(l.mealType || 'Bữa')}${l.shiftCode ? ' · ca ' + esc(l.shiftCode) : ''}</h3>
        <div class="kv mb"><div><label>Suất đặt / nhận</label><span>${U.num(l.orderedMeals || 0)} / ${U.num(l.acceptedMeals || 0)}</span></div><div><label>Giá vốn / suất</label><span><b>${money(l.costPerMeal)}</b></span></div><div><label>Tổng giá vốn</label><span>${U.money(l.totalActualCost || 0)}</span></div><div><label>Doanh thu</label><span>${U.money(l.revenue || 0)}</span></div><div><label>Lãi gộp</label><span><b>${U.money(l.grossMargin || 0)}</b>${nil(l.grossMarginPercent) ? '' : ' · ' + U.pct(l.grossMarginPercent)}</span></div></div>
        ${mini(['Món', 'Số phần', 'Giá vốn / phần', 'Tổng giá vốn', 'Nguồn giá'], (l.listDish || []).map(x => [esc(x.dishName || ''), U.num(x.portions || 0), money(x.costPerPortion), U.money(x.totalActualCost || 0), { ACTUAL: 'Giá thành thực tế', STANDARD: 'Giá thành chuẩn', NONE: 'Chưa có giá' }[x.costSource] || esc(x.costSource || '')]))}`).join('') || '<div class="empty">Đơn chưa có dòng giá vốn</div>'}` });
  }
  APP.register('cost-meal', (el, f) => {
    if (!Store.isLive) return APP.genericList(el, f);
    const to = cm.to || Store.ctx.date, from = cm.from || addDays(to, -30);
    // MealCost/GetList: giá vốn suất đã tính theo đơn trong khoảng ngày ăn (tối đa 92 ngày).
    const x = liveData('cost-meal', 'MealCost/GetList', { From: from, To: to });
    const list = x && x.ok ? x.list : [];
    // D-78 (3): đơn bù do lỗi Delicare = Chi phí lỗi – tách khỏi giá vốn / lãi gộp suất bán.
    const sale = list.filter(r => !r.isFaultCost), fault = list.filter(r => r.isFaultCost), faultCost = U.sum(fault, 'totalActualCost');
    const rev = U.sum(sale, 'revenue'), cost = U.sum(sale, 'totalActualCost'), gm = U.sum(sale, 'grossMargin'), meals = U.sum(sale, 'acceptedMeals');
    const dis = Store.perm('cost-meal').edit === false ? 'disabled' : '';
    el.innerHTML = APP.header(f, `<button class="btn primary" data-mc ${dis}>${I('calc')} Tính giá vốn cho đơn</button>`, { sub: 'Giá vốn mỗi suất và lãi gộp theo đơn hàng đã giao / đã đối soát.' }) + `
      <div class="card panel mb"><div class="row-flex"><span class="small muted">Ngày ăn:</span><input type="date" class="input" data-cm="from" value="${esc(from)}" style="width:auto"><span class="small muted">đến</span><input type="date" class="input" data-cm="to" value="${esc(to)}" style="width:auto"><span class="grow"></span><span class="muted small">${!x ? 'Đang tải…' : x.ok ? '' : esc(x.message || 'Không tải được dữ liệu')}</span></div></div>
      <div class="kpis">${UI.kpi('Đơn đã tính giá vốn', U.num(sale.length), U.num(meals) + ' suất', 'clipboard', 'green')}${fault.length ? UI.kpi('Chi phí do lỗi Delicare', U.money(faultCost), U.num(fault.length) + ' đơn bù', 'alert', 'red') : ''}${UI.kpi('Giá vốn / suất', meals ? U.money(cost / meals) : '–', 'tổng ' + U.money(cost), 'utensils', 'orange')}${UI.kpi('Doanh thu', U.money(rev), '', 'chart', 'blue')}${UI.kpi('Lãi gộp', U.money(gm), rev ? U.pct(gm / rev * 100) + ' doanh thu' : '', 'calc', gm < 0 ? 'red' : 'green')}</div>
      <div class="card panel"><div id="cmt"></div></div>`;
    const rows = list.map(r => Object.assign({ Id: r.orderId }, r));
    UI.table(el.querySelector('#cmt'), { rows, pageSize: 10, onRow: id => mealDetail(rows.find(r => r.Id === id)),
      cols: [{ k: 'orderNumber', label: 'Số đơn', render: r => esc(r.orderNumber || '') + (r.isFaultCost ? ' <span class="tag red">Chi phí lỗi</span>' : '') }, { k: 'mealDate', label: 'Ngày ăn', render: r => esc(U.date(r.mealDate)) }, { k: 'orderStatus', label: 'Trạng thái đơn', render: r => UI.badge(r.orderStatus, 'Order', 'Status') }, { k: 'acceptedMeals', label: 'Suất nhận', num: true, render: r => U.num(r.acceptedMeals || 0) }, { k: 'totalActualCost', label: 'Tổng giá vốn', num: true, render: r => U.money(r.totalActualCost || 0) }, { k: 'costPerMeal', label: 'Giá vốn / suất', num: true, render: r => money(r.costPerMeal) }, { k: 'revenue', label: 'Doanh thu', num: true, render: r => U.money(r.revenue || 0) }, { k: 'grossMargin', label: 'Lãi gộp', num: true, render: r => `<span ${r.grossMargin < 0 ? 'style="color:var(--red)"' : ''}>${U.money(r.grossMargin || 0)}</span>` }, { k: 'grossMarginPercent', label: '% lãi gộp', num: true, render: r => nil(r.grossMarginPercent) ? '–' : U.pct(r.grossMarginPercent) }],
      actions: r => `<button class="btn xs" data-md="${esc(r.Id)}" title="Xem chi tiết">${I('search')}</button> <button class="btn xs" data-mr="${esc(r.Id)}" ${dis}>Tính lại</button>`,
      after: tb => {
        tb.querySelectorAll('[data-md]').forEach(b => b.onclick = () => mealDetail(rows.find(r => r.Id === b.dataset.md)));
        tb.querySelectorAll('[data-mr]').forEach(b => b.onclick = () => { const r = rows.find(z => z.Id === b.dataset.mr); UI.confirm(`Tính lại giá vốn suất của đơn <b>${esc(r.orderNumber || '')}</b>? Số liệu giá vốn đã tính của đơn sẽ được ghi đè.`, () => mealCalc({ Id: r.Id, OrderNumber: r.orderNumber }, () => APP.refresh()), { ok: 'Tính lại' }); });
      } });
    el.querySelectorAll('[data-cm]').forEach(i => i.onchange = () => { cm[i.dataset.cm] = i.value; APP.refresh(); });
    el.querySelector('[data-mc]').onclick = () => {
      const orders = Store.rows('Order').filter(o => ['Delivered', 'Reconciled'].includes(o.Status)).sort((a, b) => String(b.MealDate || '').localeCompare(String(a.MealDate || ''))).slice(0, 300);
      UI.modal({ title: 'Tính giá vốn suất cho đơn hàng', size: 'sm',
        body: `<p style="margin-top:0" class="small muted">Chỉ tính cho đơn Đã giao / Đã đối soát. Nếu đơn đã có giá vốn, số liệu cũ sẽ được ghi đè.</p><div class="field"><label>Đơn hàng <span class="req">*</span></label><select class="select" id="mO"><option value="">— Chọn đơn —</option>${orders.map(o => `<option value="${esc(o.Id)}">${esc((o.OrderNumber || '') + ' · ' + U.date(o.MealDate) + ' · ' + Store.label('Customer', o.CustomerId))}</option>`).join('')}</select></div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="mOk">Tính giá vốn</button>',
        onMount: (m, c) => m.querySelector('#mOk').onclick = () => { const o = Store.get('Order', m.querySelector('#mO').value); if (!o) return UI.toast('Chọn đơn hàng.', 'err'); mealCalc(o, () => { c(); APP.refresh(); }); } });
    };
  });
  // ---------- Giá thành thực tế ----------
  const ca = { from: '', to: '', dish: '' };
  const COST_PARTS = [['materialCost', 'Nguyên vật liệu', '#91AF42'], ['laborCost', 'Nhân công', '#79A8CC'], ['overheadCost', 'Chi phí chung', '#F5B17E'], ['wasteCost', 'Hao hụt', '#D95C4F'], ['otherCost', 'Khác', '#B5BCB4']];
  function costActualLive(el, f) {
    const to = ca.to || Store.ctx.date, from = ca.from || addDays(to, -30);
    // ActualCost/GetList: giá thành theo lệnh đã tính (CalculateCost / đóng lệnh) + cơ cấu tổng của kỳ.
    const x = liveData('cost-actual', 'ActualCost/GetList', { From: from, To: to, DishId: ca.dish });
    const d = x && x.ok && x.data ? x.data : {}, list = d.listData || [], bd = d.breakdown || {};
    const dis = Store.perm('cost-actual').edit === false ? 'disabled' : '';
    el.innerHTML = APP.header(f, `<button class="btn primary" data-cc ${dis}>${I('calc')} Tính giá thành lệnh</button>`, { sub: 'Giá thành thực tế theo lệnh sản xuất đã tính giá, so với giá thành chuẩn.' }) + `
      <div class="card panel mb"><div class="row-flex"><span class="small muted">Ngày sản xuất:</span><input type="date" class="input" data-ca="from" value="${esc(from)}" style="width:auto"><span class="small muted">đến</span><input type="date" class="input" data-ca="to" value="${esc(to)}" style="width:auto"><select class="select" data-ca="dish" style="width:auto"><option value="">Món: Tất cả</option>${Store.rows('Dishe').map(r => `<option value="${esc(r.Id)}" ${r.Id === ca.dish ? 'selected' : ''}>${esc(Store.label('Dishe', r))}</option>`).join('')}</select><span class="grow"></span><span class="muted small">${!x ? 'Đang tải…' : x.ok ? '' : esc(x.message || 'Không tải được dữ liệu')}</span></div></div>
      <div class="grid g2 mb"><div class="card panel"><div class="panel-title mb">Cơ cấu giá thành thực tế</div>${list.length ? UI.donut(COST_PARTS.map(([k, l, c]) => ({ l, v: Math.round((bd[k] || 0) / 1e3), c })), { label: 'nghìn đồng' }) : '<div class="empty">Chưa có lệnh nào được tính giá thành trong kỳ</div>'}</div>
        <div class="card panel"><div class="panel-title mb">So sánh với giá thành chuẩn</div><div class="kv"><div><label>Tổng chi phí thực tế</label><span><b>${U.money(bd.totalCost || 0)}</b></span></div><div><label>Số phần đạt</label><span>${U.num(bd.acceptedPortions || 0)}</span></div><div><label>Giá thành thực tế / phần</label><span><b>${money(bd.costPerPortion)}</b></span></div><div><label>Giá thành chuẩn / phần</label><span>${money(bd.standardCostPerPortion)}</span></div></div></div></div>
      <div class="card panel"><div id="cat"></div></div>`;
    const rows = list.map(r => Object.assign({ Id: r.productionOrderId }, r));
    const orderOf = id => Store.get('ProductionOrder', id) || { Id: id, ProductionOrderNumber: (rows.find(r => r.Id === id) || {}).productionOrderNumber };
    UI.table(el.querySelector('#cat'), { rows, pageSize: 10,
      cols: [{ k: 'productionOrderNumber', label: 'Lệnh sản xuất', render: r => esc(r.productionOrderNumber || '') }, { k: 'productionDate', label: 'Ngày SX', render: r => esc(U.date(r.productionDate)) }, { k: 'dishName', label: 'Món', render: r => esc(r.dishName || '') }, { k: 'shiftCode', label: 'Ca', render: r => esc(r.shiftCode || '') }].concat(COST_PARTS.map(([k, l]) => ({ k, label: l, num: true, render: r => U.money(r[k] || 0) }))).concat([{ k: 'totalCost', label: 'Tổng', num: true, render: r => `<b>${U.money(r.totalCost || 0)}</b>` }, { k: 'acceptedPortions', label: 'Phần đạt', num: true, render: r => U.num(r.acceptedPortions || 0) }, { k: 'costPerPortion', label: 'Thực tế / phần', num: true, render: r => `<span ${r.standardCostPerPortion && r.costPerPortion > r.standardCostPerPortion ? 'style="color:var(--red)"' : ''}>${money(r.costPerPortion)}</span>` }, { k: 'standardCostPerPortion', label: 'Chuẩn / phần', num: true, render: r => money(r.standardCostPerPortion) }]),
      actions: r => `<button class="btn xs" data-rc="${esc(r.Id)}" ${dis}>Tính lại</button>`,
      after: tb => tb.querySelectorAll('[data-rc]').forEach(b => b.onclick = () => orderAct('cost', orderOf(b.dataset.rc), () => APP.refresh())) });
    el.querySelectorAll('[data-ca]').forEach(i => i.onchange = () => { ca[i.dataset.ca] = i.value; APP.refresh(); });
    el.querySelector('[data-cc]').onclick = () => {
      const orders = Store.rows('ProductionOrder').filter(canCost).sort((a, b) => String(b.PlannedStartDate || '').localeCompare(String(a.PlannedStartDate || ''))).slice(0, 300);
      UI.modal({ title: 'Tính giá thành thực tế cho lệnh sản xuất', size: 'sm',
        body: `<p style="margin-top:0" class="small muted">Áp dụng cho lệnh Đang sản xuất / Hoàn thành / Đóng. Nếu lệnh đã có giá thành, số liệu cũ sẽ được ghi đè.</p><div class="field"><label>Lệnh sản xuất <span class="req">*</span></label><select class="select" id="cO"><option value="">— Chọn lệnh —</option>${orders.map(o => `<option value="${esc(o.Id)}">${esc((o.ProductionOrderNumber || '') + ' · ' + Store.label('Dishe', o.DishId) + ' · ' + UI.statusVi('ProductionOrder', 'Status', o.Status))}</option>`).join('')}</select></div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="cOk">Tính giá thành</button>',
        onMount: (m, c) => m.querySelector('#cOk').onclick = () => { const o = Store.get('ProductionOrder', m.querySelector('#cO').value); if (!o) return UI.toast('Chọn lệnh sản xuất.', 'err'); calcCost(o, () => { c(); APP.refresh(); }); } });
    };
  }
  APP.register('cost-actual', (el, f) => {
    if (Store.isLive) return costActualLive(el, f);
    const rows = Store.rows('ActualCost');
    const tot = k => U.sum(rows, k);
    const parts = [{ l: 'NVL (MaterialCost)', v: tot('MaterialCost'), c: '#91AF42' }, { l: 'Nhân công (LaborCost)', v: tot('LaborCost'), c: '#79A8CC' }, { l: 'Chi phí chung (OverheadCost)', v: tot('OverheadCost'), c: '#F5B17E' }, { l: 'Hao hụt (WasteCost)', v: tot('WasteCost'), c: '#D95C4F' }, { l: 'Khác (OtherCost)', v: tot('OtherCost'), c: '#B5BCB4' }];
    const meals = U.sum(rows.map(r => Store.get('ProductionOrder', r.ProductionOrderId)).filter(Boolean), 'ActualQty');
    APP.genericList(el, f, { noAdd: true, top: `<div class="grid g2 mb"><div class="card panel"><div class="panel-title mb">Cơ cấu giá thành thực tế (Σ ActualCost)</div>${UI.donut(parts.map(p => ({ l: p.l, v: Math.round(p.v / 1e6), c: p.c })), { label: 'triệu đồng' })}</div><div class="card panel"><div class="panel-title mb">So sánh với giá thành chuẩn</div><div class="kv"><div><label>Tổng chi phí thực tế</label><span><b>${U.money(tot('TotalCost'))}</b></span></div><div><label>Số suất thực tế (ProductionOrder.ActualQty)</label><span>${U.num(meals)}</span></div><div><label>Giá thành thực tế / suất</label><span><b>${U.money(meals ? tot('TotalCost') / meals : 0)}</b></span></div><div><label>Giá thành chuẩn bình quân (StandardCost.CostPerServing)</label><span>${U.money(U.sum(Store.rows('StandardCost').filter(s => s.Status === 'Active'), 'CostPerServing') / Math.max(1, Store.rows('StandardCost').filter(s => s.Status === 'Active').length))}</span></div></div><div class="notice gap small mt">${I('alert')}<div>GAP-C-01: BRD tính cả Packaging, Logistics, Utility – SQL chưa có cột riêng (tạm gộp OtherCost/OverheadCost).</div></div></div></div>` });
  });
})();
