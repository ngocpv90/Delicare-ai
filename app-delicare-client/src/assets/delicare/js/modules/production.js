/* P2/P5 – Kế hoạch sản xuất, Lệnh SX, Bảng điều độ mẻ (ngày × ca × dây chuyền), Giá thành */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const BFLOW = ['Planned', 'Cooking', 'QCPending', 'Passed', 'Packed'];
  const poOf = b => Store.get('ProductionOrder', b.ProductionOrderId) || {};
  const planOf = p => Store.get('ProductionPlan', p.ProductionPlanId) || {};
  // ---------- Bảng điều độ mẻ ----------
  const bs = { date: null, area: '' };
  APP.register('prod-batch', (el, f) => {
    const date = bs.date || Store.ctx.date; bs.date = date;
    const shifts = Store.rows('Shift'), lines = Store.rows('ProductionLine');
    const batches = Store.rows('ProductionBatche').filter(b => U.day(planOf(poOf(b)).ProductionDate) === date);
    const dates = [...new Set(Store.rows('ProductionPlan').map(p => U.day(p.ProductionDate)))].filter(d => Store.rows('ProductionOrder').some(o => U.day(planOf(o).ProductionDate) === d)).sort().slice(-5);
    const cnt = U.group(batches, 'Status');
    el.innerHTML = APP.header(f, `<a class="btn" href="#/prod-plan">${I('calendar')} Kế hoạch</a><a class="btn" href="#/prod-order">${I('clipboard')} Lệnh sản xuất</a>`, { sub: 'Điều độ mẻ sản xuất theo ngày × ca × dây chuyền. Bấm thẻ mẻ để chuyển bước: Kế hoạch → Đang nấu → Chờ QC → Đạt/Không đạt → Đã đóng gói. Mẻ không đạt QC bị chặn chuyển sang đóng gói.' }) + `
    <div class="card panel mb"><div class="row-flex"><span class="small muted">Ngày sản xuất:</span>${dates.map(d => `<button class="chip ${d === date ? 'active' : ''}" data-d="${d}">${U.date(d)}${d === Store.ctx.date ? ' · hôm nay' : ''}</button>`).join('')}<span class="grow"></span>${BFLOW.concat('Failed').map(s => `<span class="badge ${s === 'Failed' ? 'red' : ''}">${UI.statusVi('ProductionBatche', 'Status', s)} · ${(cnt[s] || []).length}</span>`).join(' ')}</div></div>
    <div class="kpis">${UI.kpi('Số mẻ', batches.length, `${new Set(batches.map(b => b.ProductionOrderId)).size} lệnh sản xuất`, 'factory', 'green')}${UI.kpi('Phần món kế hoạch', U.num(U.sum(batches, 'PlannedQuantity')), 'Σ PlannedQuantity (suất × món)', 'utensils', 'blue')}${UI.kpi('Phần món đạt QC', U.num(U.sum(batches.filter(b => b.QcStatus === 'PASS'), 'ActualQuantity')), 'QcStatus = PASS', 'shield', 'green')}${UI.kpi('Tạm giữ / Loại', batches.filter(b => ['HOLD', 'REJECT'].includes(b.QcStatus)).length, 'QcStatus HOLD / REJECT', 'alert', 'red')}</div>
    <div class="card panel"><div class="board" style="grid-template-columns:150px repeat(${lines.length},minmax(200px,1fr))"><div class="bh">Ca \\ Dây chuyền</div>${lines.map(l => `<div class="bh">${esc(l.Name)}<div style="font-weight:500;text-transform:none">${U.num(l.CapacityMealPerBatch)} suất/mẻ · ${U.num(l.MinutesPerBatch)}'</div></div>`).join('')}
      ${shifts.map(s => `<div class="bh" style="display:flex;flex-direction:column;justify-content:center">${esc(s.Name)}<span style="font-weight:500">${U.time(s.StartTime)}–${U.time(s.EndTime)}</span></div>${lines.map(l => `<div class="cell">${batches.filter(b => poOf(b).ShiftId === s.Id && poOf(b).ProductionLineId === l.Id).map(b => `<div class="batch ${b.Status}" data-b="${b.Id}"><b>${esc(Store.label('Dishe', b.DishId))}</b><small>${esc(b.BatchNumber)} · ${U.num(b.ActualQuantity || b.PlannedQuantity)} suất</small><div class="row-flex" style="margin-top:4px">${UI.badge(b.Status, 'ProductionBatche', 'Status')}${b.QcStatus ? UI.badge(b.QcStatus, 'ProductionBatche', 'QcStatus') : ''}</div></div>`).join('') || '<span class="muted small" style="margin:auto">—</span>'}</div>`).join('')}`).join('')}</div></div>`;
    el.querySelectorAll('[data-d]').forEach(b => b.onclick = () => { bs.date = b.dataset.d; APP.refresh(); });
    el.querySelectorAll('[data-b]').forEach(c => c.onclick = () => batchAct(c.dataset.b));
  });
  function batchAct(id) {
    const b = Store.get('ProductionBatche', id), po = poOf(b);
    const i = BFLOW.indexOf(b.Status);
    const temps = Store.by('TemperatureLog', 'ProductionBatchId', id);
    const fsr = Store.by('FoodSafetyChecklistResult', 'ProductionBatchId', id);
    UI.detail('ProductionBatche', id, { screen: 'prod-batch',
      top: `${UI.stepper(BFLOW.map(s => UI.statusVi('ProductionBatche', 'Status', s)), b.Status === 'Failed' ? 3 : i, b.Status === 'Failed')}
        <div class="kv mt mb"><div><label>Lệnh sản xuất</label><span>${esc(po.ProductionOrderNumber)} · ${esc(Store.label('Shift', po.ShiftId))}</span></div><div><label>Công thức (phiên bản khóa)</label><span>${esc(Store.label('Recipe', b.RecipeId))}</span></div><div><label>Nhiệt độ lõi (TemperatureLog)</label><span>${temps.map(t => `${U.num(t.TemperatureC)}°C ${t.IsWithinRange ? '✓' : '✗'}`).join(', ') || '<span class="muted">Chưa đo</span>'}</span></div><div><label>Checklist ATTP theo mẻ</label><span>${fsr.length ? fsr.filter(x => x.ResultCode === 'PASS').length + '/' + fsr.length + ' đạt' : '<span class="muted">–</span>'}</span></div></div>`,
      foot: () => ({ Planned: `<button class="btn orange" data-act="cook">${I('play')} Bắt đầu nấu</button>`, Cooking: `<button class="btn orange" data-act="finish">Kết thúc nấu → Chờ QC</button>`, QCPending: `${Store.isLive ? `<button class="btn" data-act="sample">Ghi lưu mẫu</button>` : ''}<button class="btn primary" data-act="pass">QC: PASS</button><button class="btn" data-act="hold">HOLD</button><button class="btn danger" data-act="reject">REJECT</button>`, Passed: `<button class="btn primary" data-act="pack">Tạo lệnh đóng gói</button>` }[b.Status] || ''),
      onAct: (a, row, close) => {
        const fin = m => { close(); UI.toast(m, 'ok'); APP.refresh(); };
        // Chế độ API: action nghiệp vụ ProductionBatch/* (backend kiểm tra nhiệt độ lõi, lưu mẫu, sản lượng và tự ghi các bảng liên quan).
        const PT = ['ProductionBatche', 'ProductionOrder', 'ProductionPlan', 'TemperatureLog', 'ProductionOutput', 'MealWasteRecord', 'PackingOrder', 'FoodSafetyChecklistResult'];
        const call = (action, form, demo) => Store.act('ProductionBatch/' + action, { form: Object.assign({ Id: id }, form), tables: PT, demo });
        if (a === 'cook') call('Start', null, () => { Store.update('ProductionBatche', id, { Status: 'Cooking', StartDate: U.iso(new Date()) }); if (po.Status === 'Released') Store.update('ProductionOrder', po.Id, { Status: 'InProgress' }); }).then(r => { if (r.ok) fin('Mẻ bắt đầu nấu'); });
        if (a === 'sample') call('RecordSampleRetained', { ActualValue: 'Đã lưu mẫu' }).then(r => { if (r.ok) { close(); UI.toast('Đã ghi lưu mẫu thức ăn', 'ok'); APP.refresh(); batchAct(id); } });
        if (a === 'finish') UI.modal({ title: 'Ghi nhận sản lượng & nhiệt độ lõi', size: 'sm', body: `<div class="field"><label>Sản lượng thực tế <code>ActualQuantity</code></label><input type="number" class="input" id="aq" value="${b.PlannedQuantity}"></div><div class="field mt"><label>Nhiệt độ lõi °C <code>TemperatureLog.TemperatureC</code> (≥ 75°C)</label><input type="number" step="0.1" class="input" id="tc" value="82.5"></div>`, foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="ok">Lưu</button>', onMount: (m, c2) => m.querySelector('#ok').onclick = () => { const aq = +m.querySelector('#aq').value, tc = +m.querySelector('#tc').value;
          if (Store.isLive) {
            // Đo nhiệt độ lõi → kết thúc nấu (Chờ QC) → ghi sản lượng. Dừng ở bước đầu tiên bị backend từ chối.
            return call('RecordCoreTemperature', { TemperatureC: tc, DeviceCode: 'PROBE-01' })
              .then(r => r.ok ? call('Finish', { ActualQuantity: aq }) : r)
              .then(r => r.ok ? call('RecordOutput', { ProducedQuantity: aq }) : r)
              .then(r => { if (r.ok) { c2(); fin('Đã ghi nhiệt độ lõi + sản lượng – chờ QC'); } });
          }
          Store.update('ProductionBatche', id, { Status: 'QCPending', ActualQuantity: aq, EndDate: U.iso(new Date()) }); Store.add('TemperatureLog', { ProductionBatchId: id, MeasurementType: 'CORE_TEMP', TemperatureC: tc, MinAllowedC: 75, IsWithinRange: tc >= 75 ? 1 : 0, MeasuredAt: U.iso(new Date()), DeviceCode: 'PROBE-01', SourceChannel: 'WEB', RecordedBy: Store.user.id, CorrectiveAction: tc < 75 ? 'Cần gia nhiệt lại' : null }); c2(); fin(tc < 75 ? 'Nhiệt độ lõi < 75°C – cảnh báo QC' : 'Đã ghi sản lượng – chờ QC'); } });
        if (a === 'pass' || a === 'hold' || a === 'reject') {
          const qc = { pass: 'PASS', hold: 'HOLD', reject: 'REJECT' }[a];
          const bad = Store.by('TemperatureLog', 'ProductionBatchId', id).some(t => t.IsWithinRange === 0);
          if (qc === 'PASS' && bad && !Store.isLive) return UI.toast('Không thể PASS: có lần đo nhiệt độ lõi ngoài ngưỡng. Gia nhiệt lại & đo lại.', 'err');
          UI.confirm(`Kết luận QC thành phẩm: ${UI.badge(qc)}`, note => {
            if (Store.isLive) return call({ PASS: 'Release', HOLD: 'Hold', REJECT: 'Reject' }[qc], { Note: note }).then(r => { if (!r.ok) return; close(); UI.toast('QC: ' + qc, qc === 'PASS' ? 'ok' : 'warn'); APP.refresh(); });
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
  // ---------- Kế hoạch sản xuất ----------
  APP.register('prod-plan', (el, f) => {
    APP.genericList(el, f, { noTransit: true, cols: [{ k: 'PlanNumber', label: 'Số kế hoạch' }, { k: 'ProductionDate', label: 'Ngày SX' }, { k: 'ShiftId', label: 'Ca' }, { k: 'OrganizationUnitId', label: 'Đơn vị' }, { k: 'n', label: 'Số món', col: false, num: true, render: r => Store.by('ProductionPlanLine', 'ProductionPlanId', r.Id).length }, { k: 'q', label: 'Tổng suất', col: false, num: true, render: r => U.num(U.sum(Store.by('ProductionPlanLine', 'ProductionPlanId', r.Id), 'PlannedQuantity')) }, { k: 'o', label: 'Lệnh SX', col: false, num: true, render: r => Store.by('ProductionOrder', 'ProductionPlanId', r.Id).length }, { k: 'Status', label: 'Trạng thái' }],
      top: `<div class="notice warn">${I('alert')}<div><b>GAP-C-04:</b> BRD lập kế hoạch theo ngày, ca, line, menu – ProductionPlan chưa có ProductionLineId/MenuId ở header. Dây chuyền được gán ở cấp Lệnh sản xuất.</div></div>` });
    APP.rowExtra(el, tr => { const r = Store.get('ProductionPlan', tr.dataset.row); const td = tr.querySelector('.actions');
      if (r.Status === 'Draft') { td.insertAdjacentHTML('afterbegin', `<button class="btn xs orange" data-a>Duyệt</button> `); td.querySelector('[data-a]').onclick = () => UI.confirm('Duyệt kế hoạch ' + esc(r.PlanNumber) + '?', () => Store.act('ProductionPlan/Approve', { form: { Id: r.Id }, tables: ['ProductionPlan', 'ApprovalRequest'], demo: () => Store.update('ProductionPlan', r.Id, { Status: 'Approved' }) }).then(x => { if (x.ok) { UI.toast('Đã duyệt kế hoạch', 'ok'); APP.refresh(); } })); }
      if (Store.isLive) { td.insertAdjacentHTML('afterbegin', `<button class="btn xs" data-xs title="Xuất phiếu sản xuất">${I('export')}</button> `); td.querySelector('[data-xs]').onclick = () => Store.download('ProductionPlan/ExportProductionSheet', { id: r.Id }); }
      if (r.Status === 'Approved' && !Store.by('ProductionOrder', 'ProductionPlanId', r.Id).length) { td.insertAdjacentHTML('afterbegin', `<button class="btn xs primary" data-g>Ra lệnh SX</button> `); td.querySelector('[data-g]').onclick = () => release(r); } });
  });
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
  // ---------- Giá thành thực tế ----------
  APP.register('cost-actual', (el, f) => {
    const rows = Store.rows('ActualCost');
    const tot = k => U.sum(rows, k);
    const parts = [{ l: 'NVL (MaterialCost)', v: tot('MaterialCost'), c: '#91AF42' }, { l: 'Nhân công (LaborCost)', v: tot('LaborCost'), c: '#79A8CC' }, { l: 'Chi phí chung (OverheadCost)', v: tot('OverheadCost'), c: '#F5B17E' }, { l: 'Hao hụt (WasteCost)', v: tot('WasteCost'), c: '#D95C4F' }, { l: 'Khác (OtherCost)', v: tot('OtherCost'), c: '#B5BCB4' }];
    const meals = U.sum(rows.map(r => Store.get('ProductionOrder', r.ProductionOrderId)).filter(Boolean), 'ActualQty');
    APP.genericList(el, f, { noAdd: true, top: `<div class="grid g2 mb"><div class="card panel"><div class="panel-title mb">Cơ cấu giá thành thực tế (Σ ActualCost)</div>${UI.donut(parts.map(p => ({ l: p.l, v: Math.round(p.v / 1e6), c: p.c })), { label: 'triệu đồng' })}</div><div class="card panel"><div class="panel-title mb">So sánh với giá thành chuẩn</div><div class="kv"><div><label>Tổng chi phí thực tế</label><span><b>${U.money(tot('TotalCost'))}</b></span></div><div><label>Số suất thực tế (ProductionOrder.ActualQty)</label><span>${U.num(meals)}</span></div><div><label>Giá thành thực tế / suất</label><span><b>${U.money(meals ? tot('TotalCost') / meals : 0)}</b></span></div><div><label>Giá thành chuẩn bình quân (StandardCost.CostPerServing)</label><span>${U.money(U.sum(Store.rows('StandardCost').filter(s => s.Status === 'Active'), 'CostPerServing') / Math.max(1, Store.rows('StandardCost').filter(s => s.Status === 'Active').length))}</span></div></div><div class="notice gap small mt">${I('alert')}<div>GAP-C-01: BRD tính cả Packaging, Logistics, Utility – SQL chưa có cột riêng (tạm gộp OtherCost/OverheadCost).</div></div></div></div>` });
  });
})();
